using DocumentIA.Admin.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DocumentIA.Tests.Admin.Services;

public class AdminConfigReaderTests
{
    [Fact]
    public void Get_ConClaveJerarquicaPresente_LaDevuelve()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FunctionsAdminApi:BaseUrl"] = "https://jerarquica.example.com/api/"
            })
            .Build();

        AdminConfigReader.Get(configuration, "FunctionsAdminApi", "BaseUrl")
            .Should().Be("https://jerarquica.example.com/api/");
    }

    [Fact]
    public void Get_SoloConClavePlanaPresente_LaDevuelve()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FunctionsAdminApi_BaseUrl"] = "https://plana.example.com/api/"
            })
            .Build();

        AdminConfigReader.Get(configuration, "FunctionsAdminApi", "BaseUrl")
            .Should().Be("https://plana.example.com/api/");
    }

    [Fact]
    public void Get_SinNingunaClave_DevuelveNull()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        AdminConfigReader.Get(configuration, "FunctionsAdminApi", "BaseUrl")
            .Should().BeNull();
    }
}
