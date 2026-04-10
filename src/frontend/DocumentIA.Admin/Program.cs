using DocumentIA.Admin.Components;
using DocumentIA.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<TipologiaAdminService>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["FunctionsAdminApi:BaseUrl"] ?? "http://localhost:7071/api/";

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);

    var functionKey = configuration["FunctionsAdminApi:FunctionKey"];
    if (!string.IsNullOrWhiteSpace(functionKey))
    {
        client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
    }
});

builder.Services.AddHttpClient(nameof(SystemConfigService), (serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["FunctionsAdminApi:BaseUrl"] ?? "http://localhost:7071/api/";
    
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    
    var functionKey = configuration["FunctionsAdminApi:FunctionKey"];
    if (!string.IsNullOrWhiteSpace(functionKey))
    {
        client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
    }
});

builder.Services.AddScoped<SystemConfigService>();

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
