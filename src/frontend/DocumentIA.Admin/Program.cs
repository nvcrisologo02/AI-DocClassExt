using DocumentIA.Admin.Components;
using DocumentIA.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Azure App Service expone variables de entorno con "_" simple (ej: FunctionsAdminApi_BaseUrl).
// .NET solo convierte "__" (doble guión) a ":" en la jerarquía de configuración.
// Este helper lee primero la clave jerárquica (:) y, como fallback, la plana con _ simple.
static string? GetConfig(IConfiguration cfg, string section, string key)
    => cfg[$"{section}:{key}"] ?? cfg[$"{section}_{key}"];

void ConfigureFunctionsHttpClient(IServiceProvider serviceProvider, HttpClient client)
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = GetConfig(configuration, "FunctionsAdminApi", "BaseUrl") ?? "http://localhost:7071/api/";

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

    var functionKey = GetConfig(configuration, "FunctionsAdminApi", "FunctionKey");
    if (!string.IsNullOrWhiteSpace(functionKey))
    {
        client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
    }
}

builder.Services.AddHttpClient<TipologiaAdminService>(ConfigureFunctionsHttpClient);

builder.Services.AddHttpClient(nameof(SystemConfigService), ConfigureFunctionsHttpClient);

builder.Services.AddScoped<SystemConfigService>();

builder.Services.AddHttpClient<MonitorService>(ConfigureFunctionsHttpClient);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
