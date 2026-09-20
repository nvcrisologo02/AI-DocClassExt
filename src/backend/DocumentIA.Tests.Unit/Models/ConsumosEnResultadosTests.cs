#nullable enable
using System.Text.Json;
using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Models;

public class ConsumosEnResultadosTests
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void ResultadosDeActividad_ArrancanConListaDeConsumosVacia()
    {
        new ResultadoClasificacion().Consumos.Should().BeEmpty();
        new ExtraccionResultado().Consumos.Should().BeEmpty();
        new ExtraerMarkdownLayoutResultado().Consumos.Should().BeEmpty();
        new PromptResultado().Consumos.Should().BeEmpty();
    }

    [Fact]
    public void ResultadoClasificacion_SobreviveAlRoundtripDeSerializacion()
    {
        // Las actividades Durable serializan sus resultados: si la propiedad no
        // sobrevive al roundtrip, el consumo no llega nunca al orquestador.
        var origen = new ResultadoClasificacion
        {
            Consumos =
            {
                new ConsumoIA
                {
                    Actividad = "Clasificar",
                    Operacion = "classification.phase1",
                    Modelo = "gpt-5-mini",
                    TokensEntrada = 10,
                    CosteEur = 0.5m,
                    Descartado = true
                }
            }
        };

        var copia = JsonSerializer.Deserialize<ResultadoClasificacion>(
            JsonSerializer.Serialize(origen),
            Opciones);

        copia!.Consumos.Should().ContainSingle();
        copia.Consumos[0].Modelo.Should().Be("gpt-5-mini");
        copia.Consumos[0].CosteEur.Should().Be(0.5m);
        copia.Consumos[0].Descartado.Should().BeTrue();
    }

    [Fact]
    public void ExtraccionResultado_SobreviveAlRoundtripDeSerializacion()
    {
        var origen = new ExtraccionResultado
        {
            Consumos = { new ConsumoIA { Modelo = "CU_NS_1.4_2", Paginas = 12 } }
        };

        var copia = JsonSerializer.Deserialize<ExtraccionResultado>(
            JsonSerializer.Serialize(origen),
            Opciones);

        copia!.Consumos.Should().ContainSingle();
        copia.Consumos[0].Paginas.Should().Be(12);
    }

    [Fact]
    public void ResultadosAntiguosSinConsumos_DeserializanConListaVacia()
    {
        // Compatibilidad con contratos historicos persistidos antes de la
        // funcionalidad: la ausencia de la propiedad no puede dar null.
        var copia = JsonSerializer.Deserialize<ExtraerMarkdownLayoutResultado>(
            """{"Modelo":"prebuilt-layout","Paginas":3}""",
            Opciones);

        copia!.Consumos.Should().NotBeNull();
        copia.Consumos.Should().BeEmpty();
    }
}
