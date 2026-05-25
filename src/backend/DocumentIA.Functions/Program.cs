using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using DocumentIA.Data.Context;
using DocumentIA.Data.Repositories;
using DocumentIA.Core.Services;
using DocumentIA.Functions.Abstractions;
using DocumentIA.Functions.Mocks;
using DocumentIA.Plugins.Integration;
using DocumentIA.Core.Configuration;
using DocumentIA.Functions.Services;
using DocumentIA.Functions.Services.Classification;
using Microsoft.Extensions.Options;
using System.IO;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((context, config) =>
    {
        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
        }

        var built = config.Build();
        var secretsSource = built["SecretsSource"] ?? "Config";
        var useAzureVault = string.Equals(secretsSource, "AzureVault", StringComparison.OrdinalIgnoreCase);
        var keyVaultName = built["KeyVaultName"];
        if (useAzureVault && !string.IsNullOrWhiteSpace(keyVaultName))
        {
            var kvUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
            // DefaultAzureCredential intenta: SharedTokenCache → VisualStudio → AzurePowerShell → InteractiveBrowser
            var credentialOptions = new DefaultAzureCredentialOptions
            {
                ExcludeWorkloadIdentityCredential = true,
                ExcludeManagedIdentityCredential = !context.HostingEnvironment.IsProduction(), // permite Managed Identity en prod
                ExcludeAzureCliCredential = true,       // bloqueada por proxy corporativo
                ExcludeVisualStudioCodeCredential = true,
                TenantId = built["AZURE_TENANT_ID"] ?? "1a213c5a-2e3d-4ae4-b0ba-075c42f9700e"
            };
            config.AddAzureKeyVault(kvUri, new DefaultAzureCredential(credentialOptions));
            Console.WriteLine($"[KeyVault] Cargando secretos desde: {kvUri}");
        }
        else
        {
            Console.WriteLine($"[Config] SecretsSource={secretsSource}. Usando configuración local/appsettings.");
        }
    })
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

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
            else if (context.HostingEnvironment.IsDevelopment())
            {
                // Local development fallback: use InMemory to allow host to start without SQL configured
                options.UseInMemoryDatabase("DocumentIADev");
            }
            else
            {
                // In non-development environments fail fast so deployments don't silently run against InMemory
                throw new InvalidOperationException("Missing database connection string for DocumentIA (ConnectionStrings:DocumentIA or SqlConnectionString).");
            }
        });

        // Repositories
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddScoped<ITipologiaRepository, TipologiaRepository>();
        services.AddScoped<ICatalogoTdnRepository, CatalogoTdnRepository>();
        services.AddScoped<IModeloConfigRepository, ModeloConfigRepository>();
        services.AddScoped<IPluginTipologiaConfigRepository, PluginTipologiaConfigRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<ITipologiaConfigAuditRepository, TipologiaConfigAuditRepository>();
        services.AddScoped<IDocumentoEjecucionRepository, DocumentoEjecucionRepository>();
        services.AddMemoryCache();

        // Services
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        services.Configure<ExtractionRoutingSettings>(context.Configuration.GetSection("Extraction"));
        services.Configure<ClassificationRoutingSettings>(context.Configuration.GetSection("Classification"));
        services.Configure<ClassificationPreparationSettings>(context.Configuration.GetSection("ClassificationPreparation"));

        services.AddSingleton<MockExtraerDataProvider>();
        services.AddSingleton<AzureContentUnderstandingProvider>();
        services.AddSingleton<AzureDocumentIntelligenceExtraerDataProvider>();
        services.AddSingleton<GptFallbackExtraerDataProvider>();
        services.AddSingleton<GptDirectExtraerDataProvider>();
        services.AddSingleton<IPromptDataProvider, OpenAIPromptDataProvider>();
        services.AddSingleton<ContentUnderstandingResultMapper>();
        services.AddSingleton<IExtraerDataProvider, ConfigurableExtraerDataProvider>();

        services.AddSingleton<MockClasificarDataProvider>();
        services.AddSingleton<AzureDocumentIntelligenceClasificarProvider>();
        services.AddSingleton<ILayoutMarkdownProvider, AzureDocumentIntelligenceLayoutMarkdownProvider>();
        services.AddSingleton<PdfRecorteService>();
        services.AddSingleton<ClassificationTipologiaPromptBuilder>();
        services.AddSingleton<GptClasificarDataProvider>();
        services.AddSingleton<PdfPageLimiterService>();

        // === HybridTdn Classification Provider ===
        services.Configure<HybridTdnOptions>(context.Configuration.GetSection("HybridTdn"));
        services.AddSingleton<ITipologiaClassificationProfileProvider, DbTipologiaClassificationProfileProvider>();
        services.AddSingleton<DocumentWindowExtractor>();
        services.AddSingleton<RuleBasedTdnClassifier>();
        services.AddSingleton<FoundryTdnRescueClassifier>(sp =>
            new FoundryTdnRescueClassifier(
                sp.GetRequiredService<ILogger<FoundryTdnRescueClassifier>>(),
                sp.GetRequiredService<GptClasificarDataProvider>()));
        services.AddSingleton<HybridTdnClasificarProvider>(sp =>
            new HybridTdnClasificarProvider(
                sp.GetRequiredService<ILogger<HybridTdnClasificarProvider>>(),
                sp.GetRequiredService<AzureDocumentIntelligenceClasificarProvider>(),
                sp.GetRequiredService<ILayoutMarkdownProvider>(),
                sp.GetRequiredService<DocumentWindowExtractor>(),
                sp.GetRequiredService<RuleBasedTdnClassifier>(),
                sp.GetRequiredService<FoundryTdnRescueClassifier>(),
                sp.GetRequiredService<IOptions<HybridTdnOptions>>(),
                sp.GetRequiredService<TelemetryClient>()));

        services.AddSingleton<IClasificarDataProvider, ConfigurableClasificarDataProvider>();

        // Configure Kestrel request body size limit for local Function host
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = 200 * 1024 * 1024; // 200 MB
        });

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
            // Bypass SSL for internal endpoints with untrusted/self-signed certificates.
            // Controlled via GDC:BypassSslValidation=true (app setting / local.settings.json).
            var bypassSsl = context.Configuration["GDC:BypassSslValidation"];
            if (context.HostingEnvironment.IsDevelopment() ||
                string.Equals(bypassSsl, "true", StringComparison.OrdinalIgnoreCase))
            {
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

        // Named HttpClient for AssetResolver plugin (used by ObtenerActivoActivity)
        services.AddHttpClient("AssetResolver", client =>
        {
            var baseUrl = context.Configuration["AssetResolver:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }

            var apiKey = context.Configuration["AssetResolver:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Registrar PluginManager como Singleton
        services.AddSingleton<PluginManager>();

        // Registrar PluginFactory
        services.AddSingleton<PluginFactory>();

        // Registrar PluginConfigLoader con path a configuraciones
        services.AddSingleton<PluginConfigLoader>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<PluginConfigLoader>>();
            return new PluginConfigLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                logger);
        });

        // Registrar TipologiaConfigLoader (usado por SubirGDCActivity)
        services.AddSingleton<TipologiaConfigLoader>(provider =>
            new TipologiaConfigLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<ITipologiaVersionResolver>(provider =>
            new TipologiaVersionResolver(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<ExtractionModelRegistryLoader>(provider =>
            new ExtractionModelRegistryLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<ClassificationModelRegistryLoader>(provider =>
            new ClassificationModelRegistryLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<PromptModelRegistryLoader>(provider =>
            new PromptModelRegistryLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));

        services.AddSingleton<PromptInstruccionesValidator>();

        services.AddSingleton<LayoutModelRegistryLoader>(provider =>
            new LayoutModelRegistryLoader(
                provider.GetRequiredService<IMemoryCache>(),
                provider.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<ISystemHealthService, SystemHealthService>();


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

    var dbContext = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();

    if (dbContext.Database.IsRelational())
    {
        if (runMigrations)
        {
            logger.LogInformation("Applying pending EF Core migrations at startup.");
            dbContext.Database.Migrate();
            logger.LogInformation("EF Core migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("Skipping EF Core migrations at startup (RunDatabaseMigrationsOnStartup={Value}).", runMigrationsSetting);
        }
    }
    else
    {
        logger.LogInformation("Database provider is not relational; skipping migrations.");
    }

    await ConfigurationSeedService.SeedAsync(dbContext, logger, Path.Combine(Directory.GetCurrentDirectory(), "config"));
    logger.LogInformation("Configuration seed completed successfully.");
}

host.Run();
