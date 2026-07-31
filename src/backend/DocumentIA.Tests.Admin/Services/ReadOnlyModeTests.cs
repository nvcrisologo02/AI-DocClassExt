using System.Net;
using DocumentIA.Admin.Models;
using DocumentIA.Admin.Services;
using DocumentIA.Data.Entities;
using DocumentIA.Tests.Admin.Helpers;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Services;

/// <summary>
/// Sin usuario autenticado el Admin no puede modificar la configuración: la comprobación
/// vive en los servicios, de modo que ninguna página pueda saltársela.
/// </summary>
public class ReadOnlyModeTests
{
    private sealed class FakeCurrentUser(bool isAuthenticated) : ICurrentUserService
    {
        public string UserName => isAuthenticated ? "usuario@sareb.es" : "no-autenticado";

        public bool IsAuthenticated => isAuthenticated;
    }

    private static HttpClient CreateClient()
        => new(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("{}")))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };

    private static TipologiaAdminService CreateTipologiaService(bool isAuthenticated)
        => new(CreateClient(), new FakeCurrentUser(isAuthenticated));

    private static PromptManagementService CreatePromptService(bool isAuthenticated)
        => new(CreateClient(), new FakeCurrentUser(isAuthenticated));

    public static TheoryData<string, Func<TipologiaAdminService, Task>> EscriturasDeTipologias() => new()
    {
        { "SaveTipologiaAsync", s => s.SaveTipologiaAsync(new TipologiaEntity { Codigo = "x", Nombre = "x", Version = "1.0" }) },
        { "PublishTipologiaAsync", s => s.PublishTipologiaAsync(1) },
        { "RetireTipologiaAsync", s => s.RetireTipologiaAsync(1) },
        { "PasarTipologiaADraftAsync", s => s.PasarTipologiaADraftAsync(1) },
        { "SaveModeloAsync", s => s.SaveModeloAsync(new ModeloConfigEntity { Key = "k", Provider = "p" }) },
        { "DeleteModeloAsync", s => s.DeleteModeloAsync(1) },
        { "SavePluginDraftAsync", s => s.SavePluginDraftAsync("tip", "{}") },
        { "PublishPluginConfigAsync", s => s.PublishPluginConfigAsync("tip") },
        { "RetirePluginConfigAsync", s => s.RetirePluginConfigAsync("tip") },
        { "SaveCatalogoTdn1Async", s => s.SaveCatalogoTdn1Async(new CatalogoTdn1Item { Codigo = "c", Nombre = "n" }) },
        { "DeleteCatalogoTdn1Async", s => s.DeleteCatalogoTdn1Async(1) },
        { "SaveCatalogoTdn2Async", s => s.SaveCatalogoTdn2Async(new CatalogoTdn2Item { Codigo = "c", Nombre = "n", CodigoTdn1 = "t" }) },
        { "DeleteCatalogoTdn2Async", s => s.DeleteCatalogoTdn2Async(1) },
    };

    [Theory]
    [MemberData(nameof(EscriturasDeTipologias))]
    public async Task SinIdentidad_LasEscriturasDeConfiguracionSeRechazan(string operacion, Func<TipologiaAdminService, Task> escritura)
    {
        var service = CreateTipologiaService(isAuthenticated: false);

        var act = async () => await escritura(service);

        (await act.Should().ThrowAsync<InvalidOperationException>($"'{operacion}' modifica configuracion y debe requerir identidad"))
            .Which.Message.Should().Contain("solo lectura");
    }

    public static TheoryData<string, Func<PromptManagementService, Task>> EscriturasDePrompts() => new()
    {
        { "CreatePromptTemplateAsync", s => s.CreatePromptTemplateAsync(new CreatePromptTemplateRequest("k", "contenido largo", null)) },
        { "UpdatePromptTemplateAsync", s => s.UpdatePromptTemplateAsync(1, new UpdatePromptTemplateRequest("contenido largo", null, "u")) },
        { "ActivatePromptVersionAsync", s => s.ActivatePromptVersionAsync(1, "u") },
        { "RollbackPromptVersionAsync", s => s.RollbackPromptVersionAsync("k", 1, "u") },
        { "DeletePromptTemplateAsync", s => s.DeletePromptTemplateAsync(1) },
    };

    [Theory]
    [MemberData(nameof(EscriturasDePrompts))]
    public async Task SinIdentidad_LasEscriturasDePromptsSeRechazan(string operacion, Func<PromptManagementService, Task> escritura)
    {
        var service = CreatePromptService(isAuthenticated: false);

        var act = async () => await escritura(service);

        (await act.Should().ThrowAsync<InvalidOperationException>($"'{operacion}' modifica prompts y debe requerir identidad"))
            .Which.Message.Should().Contain("solo lectura");
    }

    [Fact]
    public async Task SinIdentidad_LasLecturasSiguenPermitidas()
    {
        var client = new HttpClient(new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json("[]")))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        var service = new TipologiaAdminService(client, new FakeCurrentUser(isAuthenticated: false));

        var tipologias = await service.GetTipologiasAsync();

        tipologias.Should().BeEmpty("consultar la configuracion no requiere identidad");
    }

    [Fact]
    public async Task ConIdentidad_LaEscrituraLlegaAlBackend()
    {
        var llamadas = 0;
        var client = new HttpClient(new StubHttpMessageHandler(_ =>
        {
            llamadas++;
            return StubHttpMessageHandler.Json("{\"id\":1,\"codigo\":\"x\",\"nombre\":\"x\",\"version\":\"1.0\"}");
        }))
        {
            BaseAddress = new Uri("http://localhost/api/")
        };
        var service = new TipologiaAdminService(client, new FakeCurrentUser(isAuthenticated: true));

        await service.PublishTipologiaAsync(1);

        llamadas.Should().Be(1);
    }

    [Theory]
    [InlineData("usuario@sareb.es", false, true)]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("   ", false, false)]
    public void ResolveIsAuthenticated_DistingueIdentidadRealDeDesarrolloLocal(string? header, bool isDevelopment, bool esperado)
    {
        CurrentUserService.ResolveIsAuthenticated(header, isDevelopment).Should().Be(esperado);
    }

    [Fact]
    public void Banner_EnModoSoloLectura_LoIndica()
    {
        var state = EnvironmentBannerState.Resolve(
            loaded: true, backendUnreachable: false, environment: "Development", readOnlyMode: true);

        state.Label.Should().Contain("solo lectura");
    }

    [Fact]
    public void Banner_ConIdentidad_NoMencionaSoloLectura()
    {
        var state = EnvironmentBannerState.Resolve(
            loaded: true, backendUnreachable: false, environment: "Development", readOnlyMode: false);

        state.Label.Should().NotContain("solo lectura");
    }
}
