using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DocumentIA.Core.Caching;

public sealed class ConfigurationCacheService : IConfigurationCache
{
    private sealed class CacheEntry
    {
        public required object Value { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required DateTimeOffset ExpiresAtUtc { get; init; }

        public bool IsExpired(DateTimeOffset now) => now >= ExpiresAtUtc;
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly ILogger<ConfigurationCacheService> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _defaultTtl;
    private readonly int _maxEntries;

    private long _hitCount;
    private long _missCount;
    private DateTime? _lastAccessUtc;

    public ConfigurationCacheService(IConfiguration configuration, ILogger<ConfigurationCacheService> logger)
    {
        _logger = logger;
        _enabled = ParseBool(configuration["CacheConfiguration:Enabled"], true);
        _defaultTtl = TimeSpan.FromMinutes(ParseInt(configuration["CacheConfiguration:TTL:Minutes"], 5));
        _maxEntries = Math.Max(ParseInt(configuration["CacheConfiguration:MaxEntries"], 1000), 100);

        _logger.LogInformation(
            "ConfigurationCache initialized. Enabled={Enabled}, DefaultTtlMinutes={Ttl}, MaxEntries={MaxEntries}",
            _enabled,
            _defaultTtl.TotalMinutes,
            _maxEntries);
    }

    public Task<T> GetAsync<T>(string key) where T : class
    {
        if (!_enabled || string.IsNullOrWhiteSpace(key))
        {
            Interlocked.Increment(ref _missCount);
            return Task.FromResult<T>(default!);
        }

        var now = DateTimeOffset.UtcNow;
        _lastAccessUtc = now.UtcDateTime;

        if (!_entries.TryGetValue(key, out var entry))
        {
            Interlocked.Increment(ref _missCount);
            return Task.FromResult<T>(default!);
        }

        if (entry.IsExpired(now))
        {
            _entries.TryRemove(key, out _);
            Interlocked.Increment(ref _missCount);
            return Task.FromResult<T>(default!);
        }

        if (entry.Value is T value)
        {
            Interlocked.Increment(ref _hitCount);
            return Task.FromResult(value);
        }

        Interlocked.Increment(ref _missCount);
        return Task.FromResult<T>(default!);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null) where T : class
    {
        if (!_enabled || string.IsNullOrWhiteSpace(key) || value is null)
        {
            return Task.CompletedTask;
        }

        EvictIfNeeded();

        var now = DateTimeOffset.UtcNow;
        _entries[key] = new CacheEntry
        {
            Value = value,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(ttl ?? _defaultTtl)
        };

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _entries.Clear();
        return Task.CompletedTask;
    }

    public bool Exists(string key)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.IsExpired(DateTimeOffset.UtcNow))
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        return true;
    }

    public CacheStats GetStats()
    {
        CleanupExpiredEntries();

        var hit = Interlocked.Read(ref _hitCount);
        var miss = Interlocked.Read(ref _missCount);

        return new CacheStats
        {
            ItemCount = _entries.Count,
            ApproximateSizeBytes = _entries.Count * 256L,
            LastAccessUtc = _lastAccessUtc,
            HitCount = hit,
            MissCount = miss
        };
    }

    private void EvictIfNeeded()
    {
        CleanupExpiredEntries();

        if (_entries.Count < _maxEntries)
        {
            return;
        }

        var oldest = _entries
            .OrderBy(kvp => kvp.Value.CreatedAtUtc)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(oldest.Key))
        {
            _entries.TryRemove(oldest.Key, out _);
        }
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kvp in _entries)
        {
            if (kvp.Value.IsExpired(now))
            {
                _entries.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;

    private static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var parsed) ? parsed : defaultValue;
}
