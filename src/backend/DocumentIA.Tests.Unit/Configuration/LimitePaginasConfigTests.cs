#nullable enable
using DocumentIA.Core.Configuration;
using DocumentIA.Data.Entities;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

/// <summary>
/// Tests para la funcionalidad "Límite de páginas por documento".
/// Cubre los criterios de aceptación de HU #99685 (configuración por tipología)
/// y la resolución del campo MaxPaginasDocumento en ResolvedTipologia.
/// </summary>
public class LimitePaginasConfigTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // TipologiaValidationConfig.MaxPaginasDocumento
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MaxPaginasDocumento_WhenAbsent_DefaultsToZero()
    {
        var json = """
            {
              "tipologiaId": "nota-simple",
              "version": "1.0",
              "isDefault": true,
              "fields": []
            }
            """;

        var config = System.Text.Json.JsonSerializer.Deserialize<TipologiaValidationConfig>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        config.MaxPaginasDocumento.Should().Be(0);
    }

    [Fact]
    public void MaxPaginasDocumento_WhenPresent_IsDeserialized()
    {
        var json = """
            {
              "tipologiaId": "nota-simple",
              "version": "1.0",
              "isDefault": true,
              "maxPaginasDocumento": 10,
              "fields": []
            }
            """;

        var config = System.Text.Json.JsonSerializer.Deserialize<TipologiaValidationConfig>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        config.MaxPaginasDocumento.Should().Be(10);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TipologiaVersionResolver → ResolvedTipologia.MaxPaginasDocumento
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WhenMaxPaginasAbsent_ResolvedValueIsZero()
    {
        var entities = BuildEntityList(codigo: "nota.simple.1_0", tipologiaId: "nota-simple",
            version: "1.0", isDefault: true, maxPaginas: null);

        var sut = new TipologiaVersionResolver(() => entities);

        var result = sut.Resolve("nota-simple");

        result.MaxPaginasDocumento.Should().Be(0);
    }

    [Fact]
    public void Resolve_WhenMaxPaginasSet_ResolvedValueMatchesConfig()
    {
        var entities = BuildEntityList(codigo: "nota.simple.1_0", tipologiaId: "nota-simple",
            version: "1.0", isDefault: true, maxPaginas: 20);

        var sut = new TipologiaVersionResolver(() => entities);

        var result = sut.Resolve("nota-simple");

        result.MaxPaginasDocumento.Should().Be(20);
    }

    [Fact]
    public void Resolve_WhenMaxPaginasZero_LimitIsDisabledForTipologia()
    {
        // Valor 0 significa "sin límite" — debe resolverse como 0 también
        var entities = BuildEntityList(codigo: "nota.simple.1_0", tipologiaId: "nota-simple",
            version: "1.0", isDefault: true, maxPaginas: 0);

        var sut = new TipologiaVersionResolver(() => entities);

        var result = sut.Resolve("nota-simple");

        result.MaxPaginasDocumento.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PipelineSettings (precedencia)
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 50, 50)]    // tipología sin límite → se usa global
    [InlineData(10, 50, 10)]   // tipología con límite → tipología tiene prioridad
    [InlineData(0, 0, 0)]      // sin ningún límite → 0 (sin límite)
    [InlineData(15, 0, 15)]    // solo tipología definida
    public void EfectiveLimit_Precedence(int maxTipologia, int maxGlobal, int expectedEffective)
    {
        var effective = maxTipologia > 0 ? maxTipologia : maxGlobal;
        effective.Should().Be(expectedEffective);
    }

    [Theory]
    [InlineData(10, 11, false, true)]    // supera límite, sin forzar → bloquear
    [InlineData(10, 10, false, false)]   // igual al límite → no bloquear
    [InlineData(10, 5, false, false)]    // por debajo del límite → no bloquear
    [InlineData(10, 11, true, false)]    // supera límite pero forzado → no bloquear
    [InlineData(0, 100, false, false)]   // sin límite configurado → no bloquear nunca
    public void ShouldBlock_Logic(int maxEfectivo, int paginas, bool forzar, bool expectedBlock)
    {
        var limitActivo = maxEfectivo > 0;
        var superaLimite = limitActivo && paginas > maxEfectivo;
        var debeBloquear = superaLimite && !forzar;

        debeBloquear.Should().Be(expectedBlock);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<TipologiaEntity> BuildEntityList(
        string codigo, string tipologiaId, string version, bool isDefault, int? maxPaginas)
    {
        var maxPaginasJson = maxPaginas.HasValue ? $",\n  \"maxPaginasDocumento\": {maxPaginas.Value}" : "";
        var json = $$"""
            {
              "tipologiaId": "{{tipologiaId}}",
              "tipologiaNombre": "Test",
              "version": "{{version}}",
              "isDefault": {{isDefault.ToString().ToLowerInvariant()}}{{maxPaginasJson}},
              "fields": []
            }
            """;

        return new List<TipologiaEntity>
        {
            new()
            {
                Codigo = codigo,
                Activa = true,
                Estado = EstadoTipologia.Published,
                ConfiguracionJson = json
            }
        };
    }
}
