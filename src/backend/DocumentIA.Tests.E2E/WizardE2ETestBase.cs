using Microsoft.Playwright;
using Xunit;

namespace DocumentIA.Tests.E2E;

/// <summary>
/// Base class for all wizard E2E tests.
/// Requires the Admin frontend running at the URL defined in DOCUMENTIA_ADMIN_URL
/// (defaults to http://localhost:5288).
/// 
/// To run:
///   1. Start the frontend: dotnet run --project src/frontend/DocumentIA.Admin --launch-profile http
///   2. Install browsers (only once): pwsh playwright.ps1 install
///   3. Run tests: dotnet test src/backend/DocumentIA.Tests.E2E
///
/// Tests are skipped automatically if the frontend URL is not reachable.
/// </summary>
public abstract class WizardE2ETestBase : IAsyncLifetime
{
    protected const string SkipReason = "E2E tests require the Admin frontend running. Set DOCUMENTIA_ADMIN_URL or start the frontend.";

    protected static readonly string AdminBaseUrl =
        Environment.GetEnvironmentVariable("DOCUMENTIA_ADMIN_URL") ?? "http://localhost:5288";

    protected IPlaywright Playwright { get; private set; } = null!;
    protected IBrowser Browser { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected static bool IsFrontendAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        IsFrontendAvailable = await CheckFrontendAvailableAsync();
        if (!IsFrontendAvailable) return;

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        Page = await Browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (!IsFrontendAvailable) return;
        await Page.CloseAsync();
        await Browser.CloseAsync();
        Playwright.Dispose();
    }

    protected static async Task<bool> CheckFrontendAvailableAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            var response = await http.GetAsync(AdminBaseUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    protected void SkipIfFrontendNotAvailable()
    {
        Skip.If(!IsFrontendAvailable, SkipReason);
    }
}
