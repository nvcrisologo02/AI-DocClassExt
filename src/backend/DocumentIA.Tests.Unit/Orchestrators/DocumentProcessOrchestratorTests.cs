using DocumentIA.Core.Configuration;
using DocumentIA.Core.Models;
using DocumentIA.Functions.Orchestrators;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Orchestrators;

public class DocumentProcessOrchestratorTests
{
    [Fact]
    public void BuildObtenerActivoInput_WithInstructionOverrides_PrioritizesRequestValues()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones
            {
                AssetResolver = new AssetResolverInstrucciones
                {
                    CamposBusqueda = new CamposBusquedaActivo
                    {
                        Idufir = "IDUFIR-OVERRIDE",
                        ReferenciaCatastral = "REFCAT-OVERRIDE"
                    },
                    CamposSolicitados = new List<string> { "DES_SERVICER", "IMP_PT" }
                }
            },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-001" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "nota.simple.1_4" },
            DatosExtraidos = new Dictionary<string, object>
            {
                ["IDUFIR"] = "from-extract",
                ["ReferenciaCatastral"] = "from-extract-ref"
            }
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "nota.simple@1.4",
            TipologiaId: "nota.simple",
            Version: "1.4",
            TechnicalKey: "nota.simple.1_4",
            IsDefault: true,
            AssetResolverEnabled: true,
            AssetResolverCamposSolicitados: new List<string> { "DES_TIPO_AAII" },
            AssetResolverModoCombinacionCriterios: "AND",
            AssetResolverMapeoIdufir: new List<string> { "IDUFIR", "IDUFIR_CRU" },
            AssetResolverMapeoReferenciaCatastral: new List<string> { "ReferenciaCatastral" },
            AssetResolverBusquedaIdufirHabilitada: true,
            AssetResolverBusquedaReferenciaCatastralHabilitada: true,
            AssetResolverBusquedaDireccionHabilitada: true,
            AssetResolverMapeoDireccionCompleta: new List<string> { "Localizacion" },
            AssetResolverMapeoDireccionNombreVia: new List<string> { "Via" },
            AssetResolverMapeoDireccionNumero: new List<string> { "Numero" },
            AssetResolverMapeoDireccionMunicipio: new List<string> { "Municipio" },
            AssetResolverMapeoDireccionCodigoPostal: new List<string> { "CodigoPostal" },
            AssetResolverUmbralScoreDireccion: 0.8);

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.CorrelationId.Should().Be("corr-001");
        input.Tipologia.Should().Be("nota.simple.1_4");
        input.IdufirOverride.Should().Be("IDUFIR-OVERRIDE");
        input.ReferenciaCatastralOverride.Should().Be("REFCAT-OVERRIDE");
        input.CamposSolicitados.Should().BeEquivalentTo(new[] { "DES_SERVICER", "IMP_PT" });
        input.ModoCombinacionCriterios.Should().Be("AND");
        input.DatosExtraidos.Should().ContainKey("IDUFIR");
    }

    [Fact]
    public void BuildObtenerActivoInput_WithoutInstructionOverrides_UsesTipologiaValuesAndSafeDefaults()
    {
        var entrada = new ContratoEntrada
        {
            Instrucciones = new Instrucciones { AssetResolver = null },
            Trazabilidad = new Trazabilidad { CorrelationId = "corr-002" }
        };

        var salida = new ContratoSalida
        {
            Identificacion = new Identificacion { Tipologia = "nota.simple.1_3" },
            DatosExtraidos = new Dictionary<string, object>()
        };

        var tipologia = new ResolvedTipologia(
            RequestedValue: "nota.simple@1.3",
            TipologiaId: "nota.simple",
            Version: "1.3",
            TechnicalKey: "nota.simple.1_3",
            IsDefault: true,
            AssetResolverEnabled: true,
            AssetResolverCamposSolicitados: new List<string> { "DES_SERVICER", "DES_TIPO_AAII" },
            AssetResolverModoCombinacionCriterios: "OR",
            AssetResolverMapeoIdufir: new List<string> { "IDUFIR" },
            AssetResolverMapeoReferenciaCatastral: new List<string> { "ReferenciaCatastral" },
            AssetResolverBusquedaIdufirHabilitada: false,
            AssetResolverBusquedaReferenciaCatastralHabilitada: true,
            AssetResolverBusquedaDireccionHabilitada: true,
            AssetResolverMapeoDireccionCompleta: null,
            AssetResolverMapeoDireccionNombreVia: null,
            AssetResolverMapeoDireccionNumero: null,
            AssetResolverMapeoDireccionMunicipio: null,
            AssetResolverMapeoDireccionCodigoPostal: null,
            AssetResolverUmbralScoreDireccion: 0.75);

        var input = DocumentProcessOrchestrator.BuildObtenerActivoInput(entrada, salida, tipologia);

        input.CamposSolicitados.Should().BeEquivalentTo(new[] { "DES_SERVICER", "DES_TIPO_AAII" });
        input.IdufirOverride.Should().BeNull();
        input.ReferenciaCatastralOverride.Should().BeNull();
        input.MapeoIdufir.Should().BeEquivalentTo(new[] { "IDUFIR" });
        input.MapeoReferenciaCatastral.Should().BeEquivalentTo(new[] { "ReferenciaCatastral" });
        input.BusquedaIdufirHabilitada.Should().BeFalse();
        input.BusquedaReferenciaCatastralHabilitada.Should().BeTrue();
        input.BusquedaDireccionHabilitada.Should().BeTrue();
        input.MapeoDireccionCompleta.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionNombreVia.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionNumero.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionMunicipio.Should().NotBeNull().And.BeEmpty();
        input.MapeoDireccionCodigoPostal.Should().NotBeNull().And.BeEmpty();
        input.UmbralScoreDireccion.Should().Be(0.75);
    }
}
