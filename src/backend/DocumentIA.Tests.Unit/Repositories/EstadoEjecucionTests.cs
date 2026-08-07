using DocumentIA.Data.Repositories;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Repositories;

public class EstadoEjecucionTests
{
    [Theory]
    [InlineData("OK")]
    [InlineData("Completado")]
    [InlineData("Completed")]
    public void Ok_Should_ContainTodasLasVariantesDeExito(string estado)
    {
        EstadoEjecucion.Ok.Should().Contain(estado);
    }

    [Theory]
    [InlineData("REVISION")]
    [InlineData("Revision")]
    public void Revision_Should_ContainTodasLasVariantesDeRevision(string estado)
    {
        EstadoEjecucion.Revision.Should().Contain(estado);
    }

    [Theory]
    [InlineData("Error")]
    [InlineData("ERROR")]
    [InlineData("Fallido")]
    public void Error_Should_ContainTodasLasVariantesDeError(string estado)
    {
        EstadoEjecucion.Error.Should().Contain(estado);
    }

    [Fact]
    public void Categorias_Should_SerDisjuntas()
    {
        EstadoEjecucion.Ok.Should().NotIntersectWith(EstadoEjecucion.Revision);
        EstadoEjecucion.Ok.Should().NotIntersectWith(EstadoEjecucion.Error);
        EstadoEjecucion.Revision.Should().NotIntersectWith(EstadoEjecucion.Error);
    }
}
