using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Application Insights
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // TODO: Agregar servicios personalizados cuando se implementen
        // services.AddSingleton<IClasificacionService, ClasificacionService>();
        // services.AddSingleton<IExtraccionService, ExtraccionService>();
        // services.AddSingleton<IValidacionService, ValidacionService>();
    })
    .Build();

host.Run();
