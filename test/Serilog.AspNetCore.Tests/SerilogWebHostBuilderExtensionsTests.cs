// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Filters;
using Serilog.AspNetCore.Tests.Support;
using Serilog.Events;
using Serilog.Models;

// Newer frameworks provide IHostBuilder
#pragma warning disable CS0618

namespace Serilog.AspNetCore.Tests
{
    public class SerilogWebHostBuilderExtensionsTests : IClassFixture<SerilogWebApplicationFactory>
    {
        readonly SerilogWebApplicationFactory _web;

        public SerilogWebHostBuilderExtensionsTests(SerilogWebApplicationFactory web)
        {
            _web = web;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DisposeShouldBeHandled(bool dispose)
        {
            var logger = new DisposeTrackingLogger();
            using (var web = Setup(logger, dispose))
            {
                await web.CreateClient().GetAsync("/");
            }

            Assert.Equal(dispose, logger.IsDisposed);
        }

        [Fact]
        public async Task RequestLoggingMiddlewareShouldEnrich()
        {
            var (sink, web) = Setup(options =>
            {
                options.EnrichDiagnosticContext += (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("SomeInteger", 42);
                };
            });

            var httpClient = web.CreateClient();
            await httpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://example.com/resource?query=test"),
                Headers =
                {
                    Referrer = new Uri("https://example.com/referrer"),
                }
            });

            Assert.NotEmpty(sink.Writes);

            var logEvent = sink.Writes.First(logEvent => Matching.FromSource<RequestLoggingMiddleware>()(logEvent));
            Assert.Equal("HTTP Request {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds:0.0000} ms", logEvent.MessageTemplate.Text);
            Assert.Equal(LogEventLevel.Information, logEvent.Level);
            Assert.Null(logEvent.Exception);
            var request = ((StructureValue)((StructureValue)logEvent.Properties["Context"]).Properties.First(x => x.Name == nameof(HttpContextInfo.Request)).Value).Properties.ToDictionary(x => x.Name);
            var response = ((StructureValue)((StructureValue)logEvent.Properties["Context"]).Properties.First(x => x.Name == nameof(HttpContextInfo.Response)).Value).Properties.ToDictionary(x => x.Name);
            var diagnostics = ((DictionaryValue)((StructureValue)logEvent.Properties["Context"]).Properties.First(x => x.Name == nameof(HttpContextInfo.Diagnostics)).Value).Elements.ToDictionary(x => x.Key.Value.ToString());

            Assert.Equal("GET", request[nameof(HttpRequestInfo.Method)].Value.LiteralValue());
            Assert.Equal("https", request[nameof(HttpRequestInfo.Scheme)].Value.LiteralValue());
            Assert.Equal("example.com", request[nameof(HttpRequestInfo.Host)].Value.LiteralValue());
            Assert.Equal("/resource", request[nameof(HttpRequestInfo.Path)].Value.LiteralValue());
            Assert.Equal("?query=test", request[nameof(HttpRequestInfo.QueryString)].Value.LiteralValue());
            Assert.Equal("Referer", request[nameof(HttpRequestInfo.Headers)].Value.DictionaryValue().First().Key.LiteralValue());
            Assert.Equal("https://example.com/referrer", request[nameof(HttpRequestInfo.Headers)].Value.DictionaryValue().First().Value.LiteralValue());

            Assert.Equal(200, response[nameof(HttpResponseInfo.StatusCode)].Value.LiteralValue());
            Assert.True((bool)response[nameof(HttpResponseInfo.IsSucceed)].Value.LiteralValue());
            Assert.IsType<double>(response[nameof(HttpResponseInfo.ElapsedMilliseconds)].Value.LiteralValue());

            // Assert.Equal("string", diagnostics["SomeString"].Value.ToString());
            // Assert.Equal("42", diagnostics["SomeInteger"].Value.ToString());
            
            Assert.Equal("/resource", logEvent.Properties["RequestPath"].LiteralValue());
            Assert.Equal(200, logEvent.Properties["StatusCode"].LiteralValue());
            Assert.Equal("GET", logEvent.Properties["RequestMethod"].LiteralValue());
            Assert.True(logEvent.Properties.ContainsKey("ElapsedMilliseconds"));
        }

        [Fact]
        public async Task RequestLoggingMiddlewareShouldEnrichWithCollectedExceptionIfNoUnhandledException()
        {
            var diagnosticContextException = new Exception("Exception set in diagnostic context");
            var (sink, web) = Setup(options =>
            {
                options.EnrichDiagnosticContext += (diagnosticContext, _) =>
                {
                    diagnosticContext.SetException(diagnosticContextException);
                };
            });

            await web.CreateClient().GetAsync("/resource");

            var completionEvent = sink.Writes.First(logEvent => Matching.FromSource<RequestLoggingMiddleware>()(logEvent));

            Assert.Same(diagnosticContextException, completionEvent.Exception);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task RequestLoggingMiddlewareShouldEnrichWithUnhandledExceptionEvenIfExceptionIsSetInDiagnosticContext(bool setExceptionInDiagnosticContext)
        {
            var diagnosticContextException = new Exception("Exception set in diagnostic context");
            var unhandledException = new Exception("Unhandled exception thrown in API action");
            var (sink, web) = Setup(options =>
            {
                options.EnrichDiagnosticContext += (diagnosticContext, _) =>
                {
                    if (setExceptionInDiagnosticContext)
                        diagnosticContext.SetException(diagnosticContextException);
                };
            }, actionCallback: _ => throw unhandledException);

            Func<Task> act = () => web.CreateClient().GetAsync("/resource");

            Exception thrownException = await Assert.ThrowsAsync<Exception>(act);
            var completionEvent = sink.Writes.First(logEvent => Matching.FromSource<RequestLoggingMiddleware>()(logEvent));
            Assert.Same(unhandledException, completionEvent.Exception);
            Assert.Same(unhandledException, thrownException);
        }

        WebApplicationFactory<TestStartup> Setup(ILogger logger, bool dispose, Action<RequestLoggingOptions> configureOptions = null,
            Action<HttpContext> actionCallback = null)
        {
            var web = _web.WithWebHostBuilder(
                builder => builder
                    .ConfigureServices(sc => sc.Configure<RequestLoggingOptions>(options =>
                    {
                        options.Logger = logger;
                        options.EnrichDiagnosticContext += (diagnosticContext, httpContext) =>
                        {
                            diagnosticContext.Set("SomeString", "string");
                        };
                    }))
                    .Configure(app =>
                    {
                        app.UseSerilogPlusRequestLogging(configureOptions);
                        app.Run(ctx =>
                        {
                            actionCallback?.Invoke(ctx);
                            return Task.CompletedTask;
                        }); // 200 OK
                    })
                    .UseSerilog(logger, dispose));

            return web;
        }

        (SerilogSink, WebApplicationFactory<TestStartup>) Setup(Action<RequestLoggingOptions> configureOptions = null,
            Action<HttpContext> actionCallback = null)
        {
            var sink = new SerilogSink();
            var logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .CreateLogger();

            var web = Setup(logger, true, configureOptions, actionCallback);

            return (sink, web);
        }
    }
}
