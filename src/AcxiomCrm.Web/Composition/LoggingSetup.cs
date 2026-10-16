using System.Globalization;
using AcxiomCrm.Web.Logging;
using Serilog;
using Serilog.Formatting.Json;

namespace AcxiomCrm.Web.Composition;

public static class LoggingSetup
{
    public static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Destructure.With<SensitiveDataMaskingPolicy>()
                .WriteTo.File(
                    path: "logs/acxiomcrm-.log",
                    formatProvider: CultureInfo.InvariantCulture,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14);

            if (context.HostingEnvironment.IsDevelopment())
            {
                configuration.WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
            }
            else
            {
                configuration.WriteTo.Console(new JsonFormatter());
            }
        });
    }
}
