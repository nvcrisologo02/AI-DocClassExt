using Bunit;
using DocumentIA.Admin.Components.Monitor;
using DocumentIA.Admin.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DocumentIA.Tests.Admin.Components;

public class EjecucionJsonModalTests : TestContext
{
    public EjecucionJsonModalTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void NoEncontrado_MuestraAviso()
    {
        var cut = RenderComponent<EjecucionJsonModal>(p => p
            .Add(c => c.Guid, "guid-inexistente")
            .Add(c => c.NoEncontrado, true));

        cut.Find(".alert-warning").TextContent.Should().Contain("guid-inexistente");
    }

    [Fact]
    public void ErrorMensaje_MuestraAlertaDeError()
    {
        var cut = RenderComponent<EjecucionJsonModal>(p => p
            .Add(c => c.Guid, "guid-1")
            .Add(c => c.ErrorMensaje, "Fallo de comunicacion"));

        cut.Find(".alert-danger").TextContent.Should().Contain("Fallo de comunicacion");
    }

    [Fact]
    public void SinContrato_MuestraMensajeYDeshabilitaBotones()
    {
        var detalle = new EjecucionDetalleDto { EjecucionGuid = "guid-2", ContratoSalidaCompletoJson = null };

        var cut = RenderComponent<EjecucionJsonModal>(p => p
            .Add(c => c.Guid, "guid-2")
            .Add(c => c.Detalle, detalle));

        cut.Markup.Should().Contain("Esta ejecución no tiene contrato almacenado");
        var botones = cut.FindAll("button.btn-outline-secondary");
        botones.Should().Contain(b => b.TextContent.Trim() == "Copiar JSON" && b.HasAttribute("disabled"));
        botones.Should().Contain(b => b.TextContent.Trim() == "Descargar JSON" && b.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Cerrar_InvocaOnCerrar()
    {
        var cerrado = false;
        var cut = RenderComponent<EjecucionJsonModal>(p => p
            .Add(c => c.Guid, "guid-3")
            .Add(c => c.NoEncontrado, true)
            .Add(c => c.OnCerrar, EventCallback.Factory.Create(this, () => cerrado = true)));

        await cut.InvokeAsync(() => cut.Find("button.btn-close").Click());

        cerrado.Should().BeTrue();
    }

    [Fact]
    public async Task Escape_InvocaOnCerrar()
    {
        var cerrado = false;
        var cut = RenderComponent<EjecucionJsonModal>(p => p
            .Add(c => c.Guid, "guid-4")
            .Add(c => c.NoEncontrado, true)
            .Add(c => c.OnCerrar, EventCallback.Factory.Create(this, () => cerrado = true)));

        await cut.InvokeAsync(() => cut.Find(".modal").KeyDown("Escape"));

        cerrado.Should().BeTrue();
    }
}
