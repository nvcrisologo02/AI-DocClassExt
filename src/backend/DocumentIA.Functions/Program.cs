using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocumentIA.Data.Context;
using DocumentIA.Data.Repositories;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Mocks;
using DocumentIA.Plugins.Integration;
using System.IO;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Database Context
        services.AddDbContext<DocumentIADbContext>(options =>
        {
            var connectionString = context.Configuration["ConnectionStrings:DocumentIA"] 
                ?? context.Configuration["SqlConnectionString"];
            options.UseSqlServer(connectionString);
        });

        // Repositories
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddScoped<ITipologiaRepository, TipologiaRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IDocumentoEjecucionRepository, DocumentoEjecucionRepository>();

        // Services
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        // Data Providers (actualmente usando mock, reemplazar por implementación real en el futuro)
        services.AddSingleton<IExtraerDataProvider, MockExtraerDataProvider>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
  // Configurar HttpClientFactory para plugins
        services.AddHttpClient();

        // Registrar PluginManager como Singleton
        services.AddSingleton<PluginManager>();

        // Registrar PluginFactory
        services.AddSingleton<PluginFactory>();

        // Registrar PluginConfigLoader con path a configuraciones
        services.AddSingleton<PluginConfigLoader>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<PluginConfigLoader>>();
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "tipologias");
            return new PluginConfigLoader(configPath, logger);
        });


    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup.Database");
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    var runMigrationsSetting = configuration["RunDatabaseMigrationsOnStartup"];
    var runMigrations = string.IsNullOrWhiteSpace(runMigrationsSetting)
        || runMigrationsSetting.Equals("true", StringComparison.OrdinalIgnoreCase)
        || runMigrationsSetting.Equals("1", StringComparison.OrdinalIgnoreCase);

    if (runMigrations)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();
        logger.LogInformation("Applying pending EF Core migrations at startup.");
        dbContext.Database.Migrate();
        logger.LogInformation("EF Core migrations applied successfully.");
    }
    else
    {
        logger.LogInformation("Skipping EF Core migrations at startup (RunDatabaseMigrationsOnStartup={Value}).", runMigrationsSetting);
    }
}

host.Run();
