// Copyright 2019 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Parsing;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Serilog.AspNetCore
{
    // ReSharper disable once ClassNeverInstantiated.Global
    class RequestLoggingMiddleware
    {
        readonly RequestDelegate _next;
        readonly DiagnosticContext _diagnosticContext;
        readonly MessageTemplate _messageTemplate;
        readonly Action<IDiagnosticContext, HttpContext> _enrichDiagnosticContext;
        readonly Func<HttpContext, double, Exception, LogEventLevel> _getLevel;
        readonly ILogger _logger;
        static readonly LogEventProperty[] NoProperties = new LogEventProperty[0];

        public RequestLoggingMiddleware(RequestDelegate next, DiagnosticContext diagnosticContext, RequestLoggingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _diagnosticContext = diagnosticContext ?? throw new ArgumentNullException(nameof(diagnosticContext));

            _getLevel = options.GetLevel;
            _enrichDiagnosticContext = options.EnrichDiagnosticContext;
            _messageTemplate = new MessageTemplateParser().Parse(options.MessageTemplate);
            _logger = options.Logger?.ForContext<RequestLoggingMiddleware>();
        }

        // ReSharper disable once UnusedMember.Global
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            var start = Stopwatch.GetTimestamp();

            var collector = _diagnosticContext.BeginCollection();
            try
            {
                await CaptureRequestBody(httpContext);
                await _next(httpContext);
                var elapsedMs = GetElapsedMilliseconds(start, Stopwatch.GetTimestamp());
                var statusCode = httpContext.Response.StatusCode;
                LogCompletion(httpContext, collector, statusCode, elapsedMs, null);
            }
            catch (Exception ex)
                // Never caught, because `LogCompletion()` returns false. This ensures e.g. the developer exception page is still
                // shown, although it does also mean we see a duplicate "unhandled exception" event from ASP.NET Core.
                when (LogCompletion(httpContext, collector, 500, GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), ex))
            {
            }
            finally
            {
                collector.Dispose();
            }
        }

        private async Task CaptureRequestBody(HttpContext httpContext)
        {
            if (httpContext.Request.ContentLength.HasValue && httpContext.Request.ContentLength > 0)
            {
                httpContext.Request.EnableBuffering();

                using (StreamReader reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, true, -1, true))
                {
                    var jsonString = await reader.ReadToEndAsync();
                    try
                    {
                        httpContext.Items["RequestBody"] = jsonString;
                        var data = JsonDocument.Parse(jsonString);
                        httpContext.Items["RequestBodyObject"] = data;
                    }
                    catch (Exception ex)
                    {
                        SelfLog.WriteLine("Cannot read request body: " + ex.ToString());
                    }
                }

                httpContext.Request.Body.Position = 0;
            }

        }

        bool LogCompletion(HttpContext httpContext, DiagnosticContextCollector collector, int statusCode, double elapsedMs, Exception ex)
        {
            var logger = _logger ?? Log.ForContext<RequestLoggingMiddleware>();
            var level = _getLevel(httpContext, elapsedMs, ex);

            if (!logger.IsEnabled(level)) return false;

            // Enrich diagnostic context
            _enrichDiagnosticContext?.Invoke(_diagnosticContext, httpContext);

            if (!collector.TryComplete(out var collectedProperties))
                collectedProperties = NoProperties;

            // Last-in (correctly) wins...
            var properties = collectedProperties.Concat(new[]
            {
                new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
                new LogEventProperty("RequestPath", new ScalarValue(GetPath(httpContext))),
                new LogEventProperty("RequestRequestHost", new ScalarValue(httpContext.Request.Host.ToString())),
                new LogEventProperty("RequestPathAndQuery", new ScalarValue(httpContext.Request.Path.ToString())),
                new LogEventProperty("RequestScheme", new ScalarValue(httpContext.Request.Scheme)),
                new LogEventProperty("RequestContentType", new ScalarValue(httpContext.Request.ContentType)),
                new LogEventProperty("RequestProtocol", new ScalarValue(httpContext.Request.Protocol)),
                new LogEventProperty("RequestQueryString", new ScalarValue(httpContext.Request.QueryString.ToString())),
                new LogEventProperty("RequestQuery", new ScalarValue(statusCode)),
                new LogEventProperty("RequestHeaders", new ScalarValue(statusCode)),
                new LogEventProperty("ClientIpAddress", new ScalarValue(httpContext.Connection.RemoteIpAddress.ToString())),
                new LogEventProperty("ResponseStatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("ResponseElapsedMiliSeconds", new ScalarValue(elapsedMs)),
            });

            var evt = new LogEvent(DateTimeOffset.Now, level, ex, _messageTemplate, properties);

            if (statusCode >= 400 && httpContext.Response.Body != null)
            {
                try
                {
                    using (StreamReader reader = new StreamReader(httpContext.Response.Body, Encoding.UTF8, true, -1, true))
                    {
                        var responseText = reader.ReadToEnd();
                        evt.AddOrUpdateProperty(new LogEventProperty("ResponseBody", new ScalarValue(responseText)));
                    }

                    httpContext.Response.Body.Position = 0;
                }
                catch (Exception)
                {
                }
            }

            logger.Write(evt);

            return false;
        }

        static double GetElapsedMilliseconds(long start, long stop)
        {
            return (stop - start) * 1000 / (double)Stopwatch.Frequency;
        }

        static string GetPath(HttpContext httpContext)
        {
            /*
                In some cases, like when running integration tests with WebApplicationFactory<T>
                the RawTarget returns an empty string instead of null, in that case we can't use
                ?? as fallback.
            */
            var requestPath = httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget;
            if (string.IsNullOrEmpty(requestPath))
            {
                requestPath = httpContext.Request.Path.ToString();
            }
            
            return requestPath;
        }
    }
}
