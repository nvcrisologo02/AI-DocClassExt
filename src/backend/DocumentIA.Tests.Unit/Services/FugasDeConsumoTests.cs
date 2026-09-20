#nullable enable
using DocumentIA.Core.Models;
using DocumentIA.Core.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Services;

/// <summary>
/// Regresión de las fugas de consumo detectadas en revisión: los proveedores hoja
/// registraban su gasto, pero los que los componen devolvían el resultado de uno
/// solo y descartaban los consumos del resto. El gasto desaparecía justo en los
/// caminos que más cuestan: cadena de proveedores, fallback y restricción.
/// </summary>
public class FugasDeConsumoTests
{
    private static ConsumoIA Consumo(string modelo, decimal coste) => new()
    {
        Modelo = modelo,
        CosteEur = coste,
        TokensEntrada = 100
    };

    [Fact]
    public void Fusionar_AnadeLosConsumosDelOrigenAlDestino()
    {
        var destino = new List<ConsumoIA> { Consumo("elegido", 0.10m) };
        var origen = new List<ConsumoIA> { Consumo("descartado", 0.04m) };

        ConsumosIA.Fusionar(destino, origen, marcarDescartados: true);

        destino.Should().HaveCount(2);
        destino.Single(c => c.Modelo == "descartado").Descartado.Should().BeTrue();
        destino.Single(c => c.Modelo == "elegido").Descartado.Should().BeFalse();
    }

    [Fact]
    public void Fusionar_NoDuplicaElMismoConsumoAunqueLlegueDosVeces()
    {
        // El resultado elegido suele estar tambien en la lista de evaluados: sin
        // control de identidad su gasto se contaria dos veces.
        var compartido = Consumo("gpt-5-mini", 0.10m);
        var destino = new List<ConsumoIA> { compartido };

        ConsumosIA.Fusionar(destino, new List<ConsumoIA> { compartido }, marcarDescartados: true);

        destino.Should().ContainSingle();
        destino[0].Descartado.Should().BeFalse();
        CalculadoraCosteIA.Agregar(destino).CosteTotalEur.Should().Be(0.10m);
    }

    [Fact]
    public void Fusionar_ToleraNulos()
    {
        var destino = new List<ConsumoIA>();

        var accion = () =>
        {
            ConsumosIA.Fusionar(destino, null);
            ConsumosIA.Fusionar(null, new List<ConsumoIA> { Consumo("x", 1m) });
        };

        accion.Should().NotThrow();
        destino.Should().BeEmpty();
    }

    [Fact]
    public void FusionarEvaluados_CuentaElGastoDeTodaLaCadenaDeProveedores()
    {
        // Escenario real: reglas no convence, GPT tampoco, DI sí. Se devuelve DI,
        // pero las dos llamadas anteriores se han pagado.
        var elegido = new List<ConsumoIA> { Consumo("sareb-classifier-v1", 0.03m) };

        var evaluados = new List<IReadOnlyList<ConsumoIA>?>
        {
            new List<ConsumoIA>(),                                   // reglas, sin IA
            new List<ConsumoIA> { Consumo("gpt-5-mini", 0.12m) },    // GPT descartado
            elegido
        };

        ConsumosIA.FusionarEvaluados(elegido, evaluados);

        var agregado = CalculadoraCosteIA.Agregar(elegido);
        agregado.CosteTotalEur.Should().Be(0.15m);
        agregado.Consumos.Should().Contain(c => c.Modelo == "gpt-5-mini" && c.Descartado);
    }

    [Fact]
    public void FusionarEvaluados_ResultadoNuevoRecogeTodoElGastoPrevio()
    {
        // Escenario de restricción de tipologías: el resultado devuelto es un objeto
        // nuevo con la lista vacía. Todo lo ejecutado se descartó, pero se pagó.
        var desconocido = new List<ConsumoIA>();

        var evaluados = new List<IReadOnlyList<ConsumoIA>?>
        {
            new List<ConsumoIA> { Consumo("gpt-5-mini", 0.20m) },
            new List<ConsumoIA> { Consumo("gpt-5-mini", 0.05m) }
        };

        ConsumosIA.FusionarEvaluados(desconocido, evaluados);

        CalculadoraCosteIA.Agregar(desconocido).CosteTotalEur.Should().Be(0.25m);
        desconocido.Should().OnlyContain(c => c.Descartado);
    }

    [Fact]
    public void FusionarEvaluados_SinCandidatos_NoRompe()
    {
        var destino = new List<ConsumoIA>();

        ConsumosIA.FusionarEvaluados(destino, new List<IReadOnlyList<ConsumoIA>?> { null });

        destino.Should().BeEmpty();
    }
}
