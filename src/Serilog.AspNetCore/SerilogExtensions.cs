using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog.Debugging;
using Serilog.Enrichers;
using Serilog.Events;
using Serilog.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Serilog
{
    /// <summary>
    /// Serilog configuration for asp.net core middlewares
    /// </summary>
    public static class SerilogExtensions
    {
        /// <summary>
        /// Configure host to use preconfigured and practiced Serilog
        /// </summary>
        /// <param name="host"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static IHostBuilder UseSerilogPlus(this IHostBuilder host, Action<LoggerConfiguration> options = null)
        {
            host.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                config.AddJsonFile("logsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"logsettings.{env.EnvironmentName}.json",
                        optional: true, reloadOnChange: true);
            });
            host.UseSerilog((context, config) =>
            {
                var loggerConfiguration = config.Enrich.FromLogContext()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Fatal)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithEnvironmentUserName()
                    .Enrich.WithMachineName()
                    .Enrich.WithExceptionDetails()
                    .Enrich.With<UserAgentEnricher>()
                    .Enrich.With<UserClaimsEnricher>()
                    .Enrich.With<EventTypeEnricher>()
                    .Enrich.With<CorrelationIdEnricher>()
                    .ReadFrom.Configuration(context.Configuration)
                    .Destructure.With<JsonDocumentDestructuringPolicy>();
                var logPath = context.Configuration["Serilog:DefaultLogLocation"]?.ToString() ?? "App_Data/Logs";
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                options?.Invoke(loggerConfiguration);
                var file = File.CreateText($"{logPath}/internal-{DateTime.Now.Ticks}.log");
                SelfLog.Enable(TextWriter.Synchronized(file));
            });
            return host;
        }

        /// <summary>
        /// Configure host to use preconfigured and practiced Serilog
        /// </summary>
        /// <param name="host"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static IWebHostBuilder UseSerilogPlus(this IWebHostBuilder host,
            Action<LoggerConfiguration> options = null)
        {
            host.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                config.AddJsonFile("logsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"logsettings.{env.EnvironmentName}.json",
                        optional: true, reloadOnChange: true);
            });
            host.UseSerilog((context, config) =>
            {
                var loggerConfiguration = config.Enrich.FromLogContext()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Fatal)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithEnvironmentUserName()
                    .Enrich.WithMachineName()
                    .Enrich.WithExceptionDetails()
                    .Enrich.With<UserAgentEnricher>()
                    .Enrich.With<UserClaimsEnricher>()
                    .Enrich.With<EventTypeEnricher>()
                    .Enrich.With<CorrelationIdEnricher>()
                    .ReadFrom.Configuration(context.Configuration)
                    .Destructure.With<JsonDocumentDestructuringPolicy>();
                var logPath = context.Configuration["Serilog:DefaultLogLocation"]?.ToString() ?? "App_Data/Logs";
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                options?.Invoke(loggerConfiguration);
                var file = File.CreateText($"{logPath}/internal-{DateTime.Now.Ticks}.log");
                SelfLog.Enable(TextWriter.Synchronized(file));
            });
            return host;
        }

        /// <summary>
        /// Configure host to use preconfigured and practiced Serilog
        /// </summary>
        /// <param name="app"></param>
        public static void UseSerilogPlus(this IApplicationBuilder app)
        {
            //app.UseHealthAndMetricsMiddleware();
            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (httpContext, elapsed, ex) =>
                {
                    if (httpContext.Response.StatusCode >= 500)
                    {
                        return LogEventLevel.Error;
                    }

                    if (httpContext.Response.StatusCode >= 400)
                    {
                        return LogEventLevel.Warning;
                    }

                    return LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, ctx) =>
                {
                    diagnosticContext.Set("HttpRequestQuery",
                        ctx.Request.Query.ToDictionary(x => x.Key, y => y.Value.ToString()));
                    diagnosticContext.Set("HttpRequestHeaders",
                        ctx.Request.Headers.ToDictionary(x => x.Key, y => y.Value.ToString()));

                    if (ctx.Items.ContainsKey("HttpRequestBody"))
                    {
                        var data = ctx.Items["HttpRequestBody"];
                        diagnosticContext.Set("HttpRequestBody", data, false);
                    }

                    if (ctx.Items.ContainsKey("HttpRequestBodyObject"))
                    {
                        var data = ctx.Items["HttpRequestBodyObject"];
                        diagnosticContext.Set("HttpRequestBodyObject", data, true);
                    }
                };
            });
        }
    }
}