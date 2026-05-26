using DocumentIA.Core.Models;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Models;

public class ContratoEntradaTests
{
    [Fact]
    public void ConfiguracionIA_Defaults_NivelClasificacionIsNull()
    {
        var config = new ConfiguracionIA();

        config.NivelClasificacion.Should().BeNull();
    }
}