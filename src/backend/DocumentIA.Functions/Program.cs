using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocumentIA.Data.Context;
using DocumentIA.Data.Repositories;
using DocumentIA.Core.Services;

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
            var connectionString = context.Configuration["SqlConnectionString"];
            options.UseSqlServer(connectionString);
        });

        // Repositories
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddScoped<ITipologiaRepository, TipologiaRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();

        // Services
        services.AddSingleton<IBlobStorageService, BlobStorageService>();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

host.Run();
