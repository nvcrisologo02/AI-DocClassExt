using FluentAssertions;
using Microsoft.Playwright;

namespace DocumentIA.Tests.E2E;

/// <summary>
/// E2E tests verifying fields/rules step and the summary step of the wizard.
/// </summary>
public class WizardFieldsAndSummaryTests : WizardE2ETestBase
{
    [SkippableFact]
    public async Task WizardStep3_AddField_RendersNewFieldRow()
    {
        SkipIfFrontendNotAvailable();

        await NavigateToStep3Async();

        var initialRows = await Page.Locator("button:has-text('Añadir regla'), button:has-text('Add rule'), .field-row").CountAsync();

        var addFieldBtn = Page.Locator("button:has-text('Añadir campo'), button:has-text('Add field')").First;
        await addFieldBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var afterRows = await Page.Locator("button:has-text('Añadir regla'), button:has-text('Add rule'), .field-row").CountAsync();
        afterRows.Should().BeGreaterThan(initialRows, "añadir campo debe crear una nueva fila en el listado");
    }

    [SkippableFact]
    public async Task WizardStep3_AddRuleAndSelectRange_ShowsMinMaxInputs()
    {
        SkipIfFrontendNotAvailable();

        await NavigateToStep3Async();

        // Add a field first
        var addFieldBtn = Page.Locator("button:has-text('Añadir campo'), button:has-text('Add field')").First;
        await addFieldBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Add a rule to the first field
        var addRuleBtn = Page.Locator("button:has-text('Añadir regla'), button:has-text('Add rule')").First;
        await addRuleBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Select "range" in the rule type dropdown
        var ruleTypeSelect = Page.Locator("select[id*='uleType'], select[id*='tipo-regla'], select").Last;
        if (await ruleTypeSelect.IsVisibleAsync())
        {
            await ruleTypeSelect.SelectOptionAsync("range");
            await Page.WaitForTimeoutAsync(300);

            // Min/max inputs should now be visible
            var minInput = Page.Locator("input[placeholder*='Min'], input[id*='Min'], input[id*='min']").First;
            var minVisible = await minInput.IsVisibleAsync();
            minVisible.Should().BeTrue("seleccionar tipo 'range' debe mostrar inputs de min y max");
        }
    }

    [SkippableFact]
    public async Task WizardSummaryStep_ShowsCodigoAndNombre()
    {
        SkipIfFrontendNotAvailable();

        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.FillAsync("test-resumen");
        await Page.Locator("input[id*='ombre'], input[placeholder*='ombre']").First.FillAsync("Test resumen");

        // Navigate to the last step (summary = step 5)
        for (var i = 0; i < 4; i++)
        {
            var next = Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First;
            if (await next.IsVisibleAsync())
            {
                await next.ClickAsync();
                await Page.WaitForTimeoutAsync(400);
            }
        }

        // Summary should show the tipologia code
        var bodyText = await Page.ContentAsync();
        bodyText.Should().Contain("test-resumen", "el resumen debe mostrar el código de tipología introducido");
    }

    private async Task NavigateToStep3Async()
    {
        await Page.GotoAsync($"{AdminBaseUrl}/tipologias/wizard");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("input[id*='odigo'], input[placeholder*='odigo']").First.FillAsync("campos-test");
        await Page.Locator("input[id*='ombre'], input[placeholder*='ombre']").First.FillAsync("Campos test");

        // Step 1 → 2 → 3
        for (var i = 0; i < 2; i++)
        {
            var next = Page.Locator("button:has-text('Siguiente'), button:has-text('Next')").First;
            if (await next.IsVisibleAsync())
            {
                await next.ClickAsync();
                await Page.WaitForTimeoutAsync(400);
            }
        }
    }
}
