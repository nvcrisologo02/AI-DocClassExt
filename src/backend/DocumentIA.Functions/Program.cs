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
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services;
using Microsoft.Extensions.Options;
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

        services.Configure<ExtractionRoutingSettings>(context.Configuration.GetSection("Extraction"));
        services.Configure<AzureContentUnderstandingSettings>(context.Configuration.GetSection("Extraction:AzureContentUnderstanding"));
        services.Configure<GptFallbackExtraerSettings>(context.Configuration.GetSection("Extraction:GptFallback"));
        services.Configure<ClassificationRoutingSettings>(context.Configuration.GetSection("Classification"));
        services.Configure<AzureDocumentIntelligenceClassificationSettings>(context.Configuration.GetSection("Classification:AzureDocumentIntelligence"));
        services.Configure<GptClasificarSettings>(context.Configuration.GetSection("Classification:GptFallback"));

        services.AddSingleton<MockExtraerDataProvider>();
        services.AddSingleton<AzureContentUnderstandingProvider>();
        services.AddSingleton<GptFallbackExtraerDataProvider>();
        services.AddSingleton<ContentUnderstandingResultMapper>();
        services.AddSingleton<IExtraerDataProvider, ConfigurableExtraerDataProvider>();

        services.AddSingleton<MockClasificarDataProvider>();
        services.AddSingleton<AzureDocumentIntelligenceClasificarProvider>();
        services.AddSingleton<GptClasificarDataProvider>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<GptClasificarSettings>>();
            var logger = provider.GetRequiredService<ILogger<GptClasificarDataProvider>>();
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "tipologias");
            return new GptClasificarDataProvider(settings, configPath, logger);
        });
        services.AddSingleton<IClasificarDataProvider, ConfigurableClasificarDataProvider>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        // Configurar HttpClientFactory para plugins
        services.AddHttpClient();

        // Bind GDC settings
        services.Configure<GdcSettings>(context.Configuration.GetSection("GDC"));

        // Named HttpClient for GDC (used by GdcService)
        services.AddHttpClient("GDC", client =>
        {
            var endpoint = context.Configuration["GDC:Endpoint"];
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                try
                {
                    client.BaseAddress = new Uri(endpoint);
                }
                catch { }
            }

            if (int.TryParse(context.Configuration["GDC:TimeoutSeconds"], out var t))
            {
                client.Timeout = TimeSpan.FromSeconds(t);
            }

            var basicUser = context.Configuration["GDC:HttpBasicUsername"];
            var basicPass = context.Configuration["GDC:HttpBasicPassword"];
            if (!string.IsNullOrEmpty(basicUser))
            {
                var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{basicUser}:{basicPass}"));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            if (context.HostingEnvironment.IsDevelopment())
            {
                // Bypass SSL validation for internal DEV endpoints with self-signed certificates
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            return handler;
        });

        // Register GDC services: concrete implementation + resilient decorator
        services.AddScoped<GdcService>();
        services.AddScoped<IGdcService>(sp =>
            new ResilientGdcService(
                sp.GetRequiredService<GdcService>(),
                sp.GetRequiredService<IOptions<GdcSettings>>(),
                sp.GetRequiredService<ILogger<ResilientGdcService>>()));

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

        // Registrar TipologiaConfigLoader (usado por SubirGDCActivity)
        services.AddSingleton<TipologiaConfigLoader>(provider =>
        {
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "tipologias");
            return new TipologiaConfigLoader(configPath);
        });

        services.AddSingleton<ITipologiaVersionResolver>(provider =>
        {
            string configPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "tipologias");
            return new TipologiaVersionResolver(configPath);
        });

        services.AddSingleton<ExtractionModelRegistryLoader>(provider =>
        {
            string registryPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "extraction", "models.json");
            return new ExtractionModelRegistryLoader(registryPath);
        });

        services.AddSingleton<ClassificationModelRegistryLoader>(provider =>
        {
            string registryPath = Path.Combine(Directory.GetCurrentDirectory(), "config", "classification", "models.json");
            return new ClassificationModelRegistryLoader(registryPath);
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
