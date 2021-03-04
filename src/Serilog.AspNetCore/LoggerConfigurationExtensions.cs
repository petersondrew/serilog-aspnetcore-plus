using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Exceptions;

namespace Serilog
{
    /// <summary>
    /// Serilog LoggerConfiguration base configuration
    /// </summary>
    public static class LoggerConfigurationExtensions
    {
        /// <summary>
        /// Sets base practiced configuration for Logger
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static LoggerConfiguration SetSerilogPlusDefaultConfiguration(this LoggerConfiguration config)
        {
            config.Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Fatal)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentUserName()
                .Enrich.WithMachineName()
                .Enrich.WithExceptionDetails()
                .Enrich.With<UserAgentEnricher>()
                .Enrich.With<UserClaimsEnricher>()
                .Enrich.With<EventIdEnricher>()
                .Enrich.With<CorrelationIdEnricher>()
                .Destructure.With<JsonDocumentDestructuringPolicy>();
            return config;
        }
    }
}