using Bunit;
using DocumentIA.Admin.Components.Common;
using FluentAssertions;

namespace DocumentIA.Tests.Admin.Components;

public class ConfirmDialogTests : TestContext
{
    [Fact]
    public async Task Confirmar_DevuelveTrue()
    {
        var cut = RenderComponent<ConfirmDialog>();

        Task<bool>? confirmTask = null;
        await cut.InvokeAsync(() => { confirmTask = cut.Instance.ShowAsync("¿Seguro?"); });

        cut.WaitForAssertion(() => cut.Find(".modal").Should().NotBeNull());
        await cut.InvokeAsync(() => cut.Find("button.btn-confirm").Click());

        (await confirmTask!).Should().BeTrue();
    }

    [Fact]
    public async Task Cancelar_DevuelveFalse()
    {
        var cut = RenderComponent<ConfirmDialog>();

        Task<bool>? confirmTask = null;
        await cut.InvokeAsync(() => { confirmTask = cut.Instance.ShowAsync("¿Seguro?"); });

        cut.WaitForAssertion(() => cut.Find(".modal").Should().NotBeNull());
        await cut.InvokeAsync(() => cut.Find("button.btn-cancel").Click());

        (await confirmTask!).Should().BeFalse();
        cut.FindAll(".modal").Should().BeEmpty();
    }
}
