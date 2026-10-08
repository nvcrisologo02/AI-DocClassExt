#nullable enable
using System.ClientModel;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services.Resilience;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DocumentIA.Tests.Unit.Services.Resilience;

public class AzureOpenAIResilienceExecutorTests
{
    // Excepción de test: ClientResultException permite fijar Status (setter protected)
    // vía subclase. GetRawResponse() devuelve null → sin Retry-After (se usa backoff).
    private sealed class FakeClientResultException : ClientResultException
    {
        public FakeClientResultException(int status) : base("fake", response: null)
        {
            Status = status;
        }
    }

    private static AzureOpenAIResilienceExecutor CreateSut(AzureOpenAIResilienceOptions options)
        => new(
            Options.Create(options),
            Mock.Of<ILogger<AzureOpenAIResilienceExecutor>>(),
            new TelemetryClient(TelemetryConfiguration.CreateDefault()));

    [Fact]
    public void IsRetryableStatus_Covers429And5xx()
    {
        AzureOpenAIResilienceExecutor.IsRetryableStatus(429).Should().BeTrue();
        AzureOpenAIResilienceExecutor.IsRetryableStatus(503).Should().BeTrue();
        AzureOpenAIResilienceExecutor.IsRetryableStatus(400).Should().BeFalse();
        AzureOpenAIResilienceExecutor.IsRetryableStatus(200).Should().BeFalse();
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData(" 10 ", 10)]
    [InlineData("0", 0)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseRetryAfterSeconds_ParsesIntegerSecondsOnly(string? header, int? expected)
    {
        AzureOpenAIResilienceExecutor.ParseRetryAfterSeconds(header).Should().Be(expected);
    }

    [Fact]
    public void ComputeDelay_WhenMaxRetryDelaySecondsIsZero_CapsToZeroWithoutOneSecondFloor()
    {
        // Con MaxRetryDelaySeconds=0 el cap efectivo debe ser 0 (sin piso de 1s),
        // por lo que el backoff (5s) se recorta a 0.
        var sut = CreateSut(new AzureOpenAIResilienceOptions
        {
            InitialRetryDelayMs = 5000,
            MaxRetryDelaySeconds = 0
        });

        var delay = sut.ComputeDelay(attempt: 1, new FakeClientResultException(429));

        delay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ComputeDelay_WhenBackoffExceedsCap_ClampsToConfiguredCap()
    {
        // Regresión: el cap positivo sigue recortando el backoff.
        var sut = CreateSut(new AzureOpenAIResilienceOptions
        {
            InitialRetryDelayMs = 5000,
            MaxRetryDelaySeconds = 2
        });

        var delay = sut.ComputeDelay(attempt: 1, new FakeClientResultException(429));

        delay.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsResultAndDoesNotRetry()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { MaxRetries = 3, InitialRetryDelayMs = 1 });
        var calls = 0;

        var result = await sut.ExecuteAsync<string>(
            "ep|dep",
            _ => { calls++; return Task.FromResult("ok"); },
            CancellationToken.None);

        result.Should().Be("ok");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_Retryable429_RetriesThenThrowsRateLimitExhausted()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions
        {
            MaxRetries = 2,
            InitialRetryDelayMs = 1,
            EnableCircuitBreaker = false
        });
        var calls = 0;

        var act = async () => await sut.ExecuteAsync<string>(
            "ep|dep",
            _ => { calls++; throw new FakeClientResultException(429); },
            CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExhaustedException>();
        calls.Should().Be(3); // 1 intento + 2 reintentos
    }

    [Fact]
    public async Task ExecuteAsync_NonRetryable400_RethrowsOriginalWithoutRetry()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { MaxRetries = 3, InitialRetryDelayMs = 1 });
        var calls = 0;

        var act = async () => await sut.ExecuteAsync<string>(
            "ep|dep",
            _ => { calls++; throw new FakeClientResultException(400); },
            CancellationToken.None);

        (await act.Should().ThrowAsync<ClientResultException>())
            .Which.Status.Should().Be(400);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCircuitOpens_FailsFastWithoutInvokingOperation()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions
        {
            MaxRetries = 1,                 // 2 intentos por llamada
            InitialRetryDelayMs = 1,
            EnableCircuitBreaker = true,
            CircuitBreakerFailureThreshold = 2, // se abre tras la 1ª llamada (2 fallos)
            CircuitBreakerOpenSeconds = 60
        });
        var calls = 0;
        Func<CancellationToken, Task<string>> failing =
            _ => { calls++; throw new FakeClientResultException(429); };

        // 1ª llamada: 2 intentos → 2 fallos → abre circuito
        await FluentActions.Awaiting(() => sut.ExecuteAsync("ep|dep", failing, CancellationToken.None))
            .Should().ThrowAsync<RateLimitExhaustedException>();
        calls.Should().Be(2);

        // 2ª llamada: circuito abierto → fail-fast, no invoca operación
        await FluentActions.Awaiting(() => sut.ExecuteAsync("ep|dep", failing, CancellationToken.None))
            .Should().ThrowAsync<RateLimitExhaustedException>();
        calls.Should().Be(2); // sin nuevas invocaciones
    }

    [Fact]
    public async Task ExecuteOnceAsync_Con429_NoReintentaYPropagaLaExcepcionDelCliente()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { MaxRetries = 3, InitialRetryDelayMs = 1 });
        var calls = 0;

        var act = async () => await sut.ExecuteOnceAsync<int>(
            "embeddings|test",
            _ => { calls++; throw new FakeClientResultException(429); },
            CancellationToken.None);

        await act.Should().ThrowAsync<ClientResultException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteOnceAsync_Con429Repetidos_AbreElCircuitoYRechazaLasSiguientes()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions
        {
            MaxRetries = 0,
            CircuitBreakerFailureThreshold = 2,
            CircuitBreakerOpenSeconds = 60
        });

        for (var i = 0; i < 2; i++)
        {
            try
            {
                await sut.ExecuteOnceAsync<int>("embeddings|test", _ => throw new FakeClientResultException(429), CancellationToken.None);
            }
            catch (ClientResultException)
            {
            }
        }

        var calls = 0;
        var act = async () => await sut.ExecuteOnceAsync("embeddings|test", _ => { calls++; return Task.FromResult(1); }, CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExhaustedException>().WithMessage("*Circuito abierto*");
        calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteOnceAsync_Exito_DevuelveElResultadoYCierraElCircuito()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { CircuitBreakerFailureThreshold = 2 });
        try
        {
            await sut.ExecuteOnceAsync<int>("embeddings|test", _ => throw new FakeClientResultException(503), CancellationToken.None);
        }
        catch (ClientResultException)
        {
        }

        var resultado = await sut.ExecuteOnceAsync("embeddings|test", _ => Task.FromResult(42), CancellationToken.None);
        try
        {
            await sut.ExecuteOnceAsync<int>("embeddings|test", _ => throw new FakeClientResultException(503), CancellationToken.None);
        }
        catch (ClientResultException)
        {
        }

        resultado.Should().Be(42);
        // Un fallo, exito, otro fallo: el contador se reinicio y el circuito sigue cerrado.
        var act = async () => await sut.ExecuteOnceAsync("embeddings|test", _ => Task.FromResult(1), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteOnceAsync_Con400_NoReintentaNiAbreElCircuito()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { CircuitBreakerFailureThreshold = 1, CircuitBreakerOpenSeconds = 60 });
        var calls = 0;

        var act = async () => await sut.ExecuteOnceAsync<int>(
            "embeddings|test",
            _ => { calls++; throw new FakeClientResultException(400); },
            CancellationToken.None);

        (await act.Should().ThrowAsync<ClientResultException>()).Which.Status.Should().Be(400);
        calls.Should().Be(1);

        // Con umbral 1, un fallo contado habria abierto el circuito.
        var siguiente = async () => await sut.ExecuteOnceAsync("embeddings|test", _ => Task.FromResult(1), CancellationToken.None);
        await siguiente.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteOnceAsync_ConHttpRequestException_CuentaComoFalloDelCircuito()
    {
        var sut = CreateSut(new AzureOpenAIResilienceOptions { CircuitBreakerFailureThreshold = 2, CircuitBreakerOpenSeconds = 60 });

        for (var i = 0; i < 2; i++)
        {
            var fallo = async () => await sut.ExecuteOnceAsync<int>(
                "embeddings|test", _ => throw new HttpRequestException("red caida"), CancellationToken.None);
            await fallo.Should().ThrowAsync<HttpRequestException>();
        }

        var calls = 0;
        var act = async () => await sut.ExecuteOnceAsync("embeddings|test", _ => { calls++; return Task.FromResult(1); }, CancellationToken.None);

        await act.Should().ThrowAsync<RateLimitExhaustedException>().WithMessage("*Circuito abierto*");
        calls.Should().Be(0);
    }
}
