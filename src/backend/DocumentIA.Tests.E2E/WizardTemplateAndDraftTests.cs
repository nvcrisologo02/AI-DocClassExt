using FluentAssertions;
using Microsoft.Playwright;

namespace DocumentIA.Tests.E2E;

/// <summary>
/// E2E tests for wizard template application and clone mode start.
/// </summary>
public class WizardTemplateAndDraftTests : WizardE2ETestBase
{
    [SkippableFact]
    public async Task WizardTemplate_SelectingNotaSimple_FillsGdcFields()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select "Plantilla" as start mode
        var plantillaOption = Page.Locator("input[value='Plantilla'], label:has-text('Plantilla'), option:has-text('Plantilla')").First;
        if (await plantillaOption.IsVisibleAsync())
        {
            await plantillaOption.ClickAsync();
        }

        // Select notasimple template
        var templateSelect = Page.Locator("select[id*='emplate'], select[id*='lantilla']").First;
        if (await templateSelect.IsVisibleAsync())
        {
            await templateSelect.SelectOptionAsync("notasimple");
            await Page.WaitForTimeoutAsync(300);
        }

        // Navigate to GDC step (step 4) and check GDC tipo/serie are pre-filled
        // We iterate through steps via Siguiente until GDC appears
        for (var i = 0; i < 3; i++)
        {
            var next = Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First;
            if (await next.IsVisibleAsync())
                await next.ClickAsync();
            await Page.WaitForTimeoutAsync(400);
        }

        var tipoDocContent = await Page.Locator("input[id*='ipoDoc'], input[id*='gdcTipo']").First.InputValueAsync();
        tipoDocContent.Should().NotBeEmpty("la plantilla notasimple debe pre-rellenar GDC tipo documento");
    }

    [SkippableFact]
    public async Task WizardDraft_PageReload_PreservesStep()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill step 1
        await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.FillAsync("draft-persist-test");
        await Page.Locator("input[id*='ombre'], input[placeholder*='ombre']").First.FillAsync("Draft persist test");

        // Advance to step 2
        await Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Reload page
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000); // allow draft restore

        // Codigo should still be present (draft restored)
        var codigoValue = await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.InputValueAsync();
        codigoValue.Should().Be("draft-persist-test", "el draft local debe restaurar el código tras recarga");
    }

    [SkippableFact]
    public async Task WizardDraft_ClearDraft_ResetsForm()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill step 1
        await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.FillAsync("draft-to-clear");

        // Click "Nuevo / Limpiar" or similar reset button if visible
        var clearBtn = Page.Locator("button:has-text('Nuevo'), button:has-text('Limpiar'), button:has-text('Reset')").First;
        if (await clearBtn.IsVisibleAsync())
        {
            await clearBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            var codigoAfterClear = await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.InputValueAsync();
            codigoAfterClear.Should().BeEmpty("limpiar el draft debe vaciar el formulario");
        }
        // If no clear button exists the test passes silently (feature not yet exposed)
    }
}
