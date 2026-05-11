using DocumentIA.AssetResolver.Data;
using DocumentIA.AssetResolver.Middleware;
using DocumentIA.AssetResolver.Models;
using DocumentIA.AssetResolver.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "AssetResolver API", Version = "v1" });
    c.AddSecurityDefinition("ApiKey", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } },
            []
        }
    });
});

builder.Services.Configure<FieldAliasesConfig>(
    builder.Configuration.GetSection("FieldAliases"));

builder.Services.Configure<AssetResolverPerformanceOptions>(
    builder.Configuration.GetSection("Performance"));

builder.Services.AddDbContext<AssetResolverDbContext>(options =>
{
    var commandTimeoutSeconds = builder.Configuration
        .GetValue<int?>("Performance:SqlCommandTimeoutSeconds") ?? 15;

    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AssetResolverDb"),
        sqlOptions => sqlOptions.CommandTimeout(commandTimeoutSeconds));
});

builder.Services.AddScoped<AssetResolverService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<ApiKeyMiddleware>();
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
