using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocumentIA.Data.Context;

public class DocumentIADbContextFactory : IDesignTimeDbContextFactory<DocumentIADbContext>
{
    public DocumentIADbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DocumentIADbContext>();

        var connectionString = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Missing database connection string for design-time operations. Set ConnectionStrings__DocumentIA or SqlConnectionString, or provide src/backend/DocumentIA.Functions/local.settings.json.");
        }

        optionsBuilder.UseSqlServer(connectionString);

        return new DocumentIADbContext(optionsBuilder.Options);
    }

    private static string? ResolveConnectionString()
    {
        var environmentConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DocumentIA")
            ?? Environment.GetEnvironmentVariable("SqlConnectionString");

        if (!string.IsNullOrWhiteSpace(environmentConnection))
        {
            return environmentConnection;
        }

        foreach (var path in GetConfigurationCandidates("local.settings.json"))
        {
            var connectionString = TryReadLocalSettingsConnectionString(path);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        foreach (var path in GetConfigurationCandidates("appsettings.json"))
        {
            var connectionString = TryReadAppSettingsConnectionString(path);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetConfigurationCandidates(string fileName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (currentDirectory is not null)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(currentDirectory.FullName, fileName),
                Path.Combine(currentDirectory.FullName, "src", "backend", "DocumentIA.Functions", fileName)
            })
            {
                if (seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            currentDirectory = currentDirectory.Parent;
        }
    }

    private static string? TryReadLocalSettingsConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Values", out var values))
        {
            return null;
        }

        if (values.TryGetProperty("ConnectionStrings:DocumentIA", out var nestedConnection))
        {
            return nestedConnection.GetString();
        }

        if (values.TryGetProperty("SqlConnectionString", out var sqlConnectionString))
        {
            return sqlConnectionString.GetString();
        }

        return null;
    }

    private static string? TryReadAppSettingsConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
            && connectionStrings.TryGetProperty("DocumentIA", out var documentIaConnection))
        {
            return documentIaConnection.GetString();
        }

        if (document.RootElement.TryGetProperty("SqlConnectionString", out var sqlConnectionString))
        {
            return sqlConnectionString.GetString();
        }

        return null;
    }
}
