using DocumentIA.Admin.Services;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

public class EnvironmentInfoParserTests
{
    [Fact]
    public void GetEnvironment_JsonValido_DevuelveEntorno()
    {
        var json = """{"environment":"Production","timestampUtc":"2026-07-22T10:00:00Z"}""";

        EnvironmentInfoParser.GetEnvironment(json).Should().Be("Production");
    }

    [Fact]
    public void GetEnvironment_SinPropiedadEnvironment_DevuelveNull()
    {
        EnvironmentInfoParser.GetEnvironment("""{"otra":"cosa"}""").Should().BeNull();
    }

    [Fact]
    public void GetEnvironment_JsonInvalido_DevuelveNull()
    {
        EnvironmentInfoParser.GetEnvironment("esto no es json").Should().BeNull();
    }
}
