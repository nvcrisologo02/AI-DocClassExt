using DocumentIA.Functions.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

public class GptHierarchicalClassificationParserTests
{
    [Fact]
    public void ParsePhase1_WhenJsonIsValid_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn1": "nots",
              "propuesta": "Parece una nota simple"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().Be("NOTS");
        result.Value.Propuesta.Should().Be("Parece una nota simple");
    }

    [Fact]
    public void ParsePhase1_WhenTdn1IsNull_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn1": null,
              "propuesta": "Sugerencia libre"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().BeNull();
        result.Value.Propuesta.Should().Be("Sugerencia libre");
    }

    [Fact]
    public void ParsePhase1_WhenResumenIsProvided_ReturnsResumen()
    {
        const string response = """
            {
              "tdn1": "escr",
              "propuesta": "Parece una escritura",
              "resumen": "Resumen ejecutivo"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().Be("ESCR");
        result.Value.Propuesta.Should().Be("Parece una escritura");
        result.Value.Resumen.Should().Be("Resumen ejecutivo");
    }

    [Fact]
    public void ParsePhase1_WhenJsonIsInvalid_ReturnsControlledError()
    {
        const string response = "no es json";

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase1ParsingErrorReason);
    }

    [Fact]
    public void ParsePhase1_WhenFieldsAreMissing_ReturnsControlledError()
    {
        const string response = """
            {
              "tdn1": "NOTS"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase1ParsingErrorReason);
    }

    [Fact]
    public void ParsePhase2_WhenJsonIsValid_ReturnsParsedPayload()
    {
        const string response = """
            {
              "tdn2": "nots-01"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn2.Should().Be("NOTS-01");
    }

    [Fact]
    public void ParsePhase2_WhenTdn2IsMissing_ReturnsControlledError()
    {
        const string response = "{}";

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase2ParsingErrorReason);
    }

    [Fact]
    public void ParsePhase1_WhenConfianzaIsProvided_ReturnsConfianza()
    {
        const string response = """
            {
              "tdn1": "escr",
              "propuesta": "Parece una escritura",
              "confianza": 0.85
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn1.Should().Be("ESCR");
        result.Value.Propuesta.Should().Be("Parece una escritura");
        result.Value.Confianza.Should().Be(0.85);
    }

    [Fact]
    public void ParsePhase1_WhenConfianzaIsOutOfRange_ClampsToValidRange()
    {
        const string response = """
            {
              "tdn1": "nots",
              "propuesta": "Test",
              "confianza": 1.5
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value!.Confianza.Should().Be(1.0);
    }

    [Fact]
    public void ParsePhase1_WhenConfianzaIsMissing_ReturnsNull()
    {
        const string response = """
            {
              "tdn1": "nots",
              "propuesta": "Test"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase1(response);

        result.Success.Should().BeTrue();
        result.Value!.Confianza.Should().BeNull();
    }

    [Fact]
    public void ParsePhase2_WhenConfianzaIsProvided_ReturnsConfianza()
    {
        const string response = """
            {
              "tdn2": "nots-01",
              "confianza": 0.92
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Tdn2.Should().Be("NOTS-01");
        result.Value.Confianza.Should().Be(0.92);
    }

    [Fact]
    public void ParsePhase2_WhenConfianzaIsOutOfRange_ClampsToValidRange()
    {
        const string response = """
            {
              "tdn2": "nots-01",
              "confianza": -0.1
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeTrue();
        result.Value!.Confianza.Should().Be(0.0);
    }

    [Fact]
    public void ParsePhase2_WhenConfianzaIsMissing_ReturnsNull()
    {
        const string response = """
            {
              "tdn2": "nots-01"
            }
            """;

        var result = GptHierarchicalClassificationParser.ParsePhase2(response);

        result.Success.Should().BeTrue();
        result.Value!.Confianza.Should().BeNull();
    }

    [Fact]
    public void ParsePhase2_Tdn2NullExplicito_FallaConMotivoNingunaTipologia()
    {
        var result = GptHierarchicalClassificationParser.ParsePhase2("{\"tdn2\": null, \"confianza\": 0.9}");

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Fase2NingunaTipologiaReason);
    }

    [Fact]
    public void ParsePhase2_Tdn2StringVacio_SigueSiendoParsingError()
    {
        var result = GptHierarchicalClassificationParser.ParsePhase2("{\"tdn2\": \"\"}");

        result.Success.Should().BeFalse();
        result.ErrorReason.Should().Be(GptHierarchicalClassificationParser.Phase2ParsingErrorReason);
    }

    // ========== ParseTdn1CatalogNombresPorCodigo (AB#99984) ==========

    [Fact]
    public void ParseTdn1CatalogNombresPorCodigo_WhenCatalogHasMultipleFamilias_ReturnsCodigoNombreMap()
    {
        const string catalogo = "- TASA: Tasaciones y Valoraciones, Documentos de estimación del valor de un activo.\n" +
            "- PRES: Presupuestos, Cómputo anticipado del coste de una obra.\n" +
            "- FICH: Fichas, Folios con datos esquemáticos.";

        var mapa = GptHierarchicalClassificationParser.ParseTdn1CatalogNombresPorCodigo(catalogo);

        mapa.Should().HaveCount(3);
        mapa["TASA"].Should().Be("Tasaciones y Valoraciones");
        mapa["PRES"].Should().Be("Presupuestos");
        mapa["FICH"].Should().Be("Fichas");
    }

    [Fact]
    public void ParseTdn1CatalogNombresPorCodigo_WhenCatalogIsEmpty_ReturnsEmptyMap()
    {
        var mapa = GptHierarchicalClassificationParser.ParseTdn1CatalogNombresPorCodigo(null);

        mapa.Should().BeEmpty();
    }

    // ========== ResolverTdn1PorCatalogoDesdePropuesta (AB#99984) ==========

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaMentionsCatalogCodeMidText_ResolvesCodigo()
    {
        // Caso real observado en el baseline de evaluación: GPT identifica la familia
        // correctamente en prosa libre, pero no antepone el código de catálogo al inicio del
        // texto (ExtraerTdn1DePropuesta no puede extraerlo por el ancla '^').
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TASA"] = "Tasaciones y Valoraciones"
        };
        const string propuesta = "Informe de tasación de Sociedad de Tasación con metodología de comparación " +
            "y coste, propio de la familia TASA de valoración de inmuebles.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().Be("TASA");
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaMentionsFamiliaNombreWithoutCode_ResolvesCodigo()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TASA"] = "Tasaciones y Valoraciones"
        };
        const string propuesta = "El documento recoge un informe con metodologías de comparación y coste, " +
            "encajando en Tasaciones y Valoraciones del activo analizado.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().Be("TASA");
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaIsGenuinelyUnclassifiable_ReturnsNull()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TASA"] = "Tasaciones y Valoraciones",
            ["FICH"] = "Fichas"
        };
        const string propuesta = "null: documento ilegible o sin contenido identificable para clasificar en familias TDN1.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().BeNull();
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaOrCatalogIsEmpty_ReturnsNull()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["TASA"] = "Tasaciones y Valoraciones" };

        GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(null, catalogo).Should().BeNull();
        GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta("TASA: informe", new Dictionary<string, string>()).Should().BeNull();
    }

    // ========== ResolverTdn1PorCatalogoDesdePropuesta - vía 3: raíz de nombre (AB#99984 v2) ==========

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaNombraFamiliaEnProsaSinCodigoNiNombreCompleto_ResolvesPorRaizTasa()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TASA"] = "Tasaciones y Valoraciones"
        };
        const string propuesta = "Tasación de un inmueble realizada por una sociedad homologada.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().Be("TASA");
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenPropuestaNombraFamiliaEnProsaSinCodigoNiNombreCompleto_ResolvesPorRaizPres()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRES"] = "Presupuestos"
        };
        const string propuesta = "Presupuesto de obra para la reforma de la vivienda.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().Be("PRES");
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenRaizEsAmbiguaEntreVariasFamilias_ReturnsNull()
    {
        // El cluster CERJ/CERT/CERA comparte la misma raíz "certificado": la guarda de colisión
        // debe descartar esa raíz por completo y no resolver ninguna de las tres.
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CERJ"] = "Certificados, justificantes y recibos",
            ["CERT"] = "Certificados técnicos",
            ["CERA"] = "Certificados, autoliquidaciones, justificantes y recibos de pago / cobro"
        };
        const string propuesta = "Certificado de estar al corriente de pago emitido por el organismo competente.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().BeNull();
    }

    [Fact]
    public void ResolverTdn1PorCatalogoDesdePropuesta_WhenCodigoEnMayusculasYRaizCoinciden_PriorizaCodigoDeVia1()
    {
        var catalogo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TASA"] = "Tasaciones y Valoraciones",
            ["OTRO"] = "Tasaciones especiales"
        };
        const string propuesta = "Documento de la familia OTRO: tasación de un inmueble.";

        var resultado = GptHierarchicalClassificationParser.ResolverTdn1PorCatalogoDesdePropuesta(propuesta, catalogo);

        resultado.Should().Be("OTRO");
    }
}