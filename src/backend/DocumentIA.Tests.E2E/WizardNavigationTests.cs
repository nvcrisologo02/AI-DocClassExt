using FluentAssertions;
using Microsoft.Playwright;

namespace DocumentIA.Tests.E2E;

/// <summary>
/// E2E tests for wizard step navigation, validation blocking, and draft persistence.
/// Requires the Admin frontend running (see WizardE2ETestBase).
/// </summary>
public class WizardNavigationTests : WizardE2ETestBase
{
    [SkippableFact]
    public async Task WizardPage_LoadsStep1_WithExpectedHeading()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var heading = await Page.TextContentAsync("h4, h3, h2");
        heading.Should().NotBeNullOrEmpty();

        // Wizard step indicator should show step 1 of 5
        var stepText = await Page.Locator("text=/paso 1|step 1|básico/i").IsVisibleAsync();
        stepText.Should().BeTrue("el wizard debe mostrar el paso 1 al cargar");
    }

    [SkippableFact]
    public async Task WizardStep1_WithEmptyCodigo_BlocksAdvanceToStep2()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Clear Codigo if pre-filled
        var codigoInput = Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First;
        await codigoInput.FillAsync(string.Empty);

        // Try to advance
        var nextBtn = Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First;
        await nextBtn.ClickAsync();

        // Should still be on step 1
        var stillOnStep1 = await Page.Locator("text=/paso 1|step 1|básico/i").IsVisibleAsync();
        stillOnStep1.Should().BeTrue("sin Codigo no debe poder avanzar al paso 2");
    }

    [SkippableFact]
    public async Task WizardStep1_WithInvalidVersion_ShowsValidationMessage()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill Codigo and Nombre but invalid version
        var codigoInput = Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First;
        await codigoInput.FillAsync("test-tipo");

        var nombreInput = Page.Locator("input[id*='ombre'], input[placeholder*='ombre']").First;
        await nombreInput.FillAsync("Test tipo");

        var versionInput = Page.Locator("input[id*='ersion'], input[placeholder*='ersion']").First;
        await versionInput.FillAsync("abc");
        await versionInput.BlurAsync();

        // A validation message should appear
        var errorVisible = await Page.Locator("text=/versión|version|formato/i").IsVisibleAsync();
        errorVisible.Should().BeTrue("version inválida debe mostrar mensaje de error");
    }

    [SkippableFact]
    public async Task WizardNavigation_CanGoBackFromStep2ToStep1()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill minimum valid step-1 data
        await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.FillAsync("tipo-test");
        await Page.Locator("input[id*='ombre'], input[placeholder*='ombre']").First.FillAsync("Tipo test");

        var nextBtn = Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First;
        await nextBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var backBtn = Page.Locator("button:has-text('Anterior'), button:has-text('Back'), button:has-text('Atrás')").First;
        await backBtn.ClickAsync();

        var backOnStep1 = await Page.Locator("text=/paso 1|step 1|básico/i").IsVisibleAsync();
        backOnStep1.Should().BeTrue("debe poder volver al paso 1 desde el paso 2");
    }
}
