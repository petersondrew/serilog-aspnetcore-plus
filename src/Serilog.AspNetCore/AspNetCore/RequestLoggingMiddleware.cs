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
        private readonly RequestLoggingOptions _options;
        readonly MessageTemplate _messageTemplate;
        readonly Action<IDiagnosticContext, HttpContext> _enrichDiagnosticContext;
        readonly ILogger _logger;
        static readonly LogEventProperty[] NoProperties = new LogEventProperty[0];

        public RequestLoggingMiddleware(RequestDelegate next, DiagnosticContext diagnosticContext,
            RequestLoggingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _diagnosticContext = diagnosticContext ?? throw new ArgumentNullException(nameof(diagnosticContext));
            _options = options;

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
                LogHttpRequest(httpContext, collector, elapsedMs, null);
            }
            catch (Exception ex)
                // Never caught, because `LogCompletion()` returns false. This ensures e.g. the developer exception page is still
                // shown, although it does also mean we see a duplicate "unhandled exception" event from ASP.NET Core.
                when (LogHttpRequest(httpContext, collector,
                    GetElapsedMilliseconds(start, Stopwatch.GetTimestamp()), ex))
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
                    httpContext.Items["RequestBody"] = jsonString;
                }

                httpContext.Request.Body.Position = 0;
            }
        }

        static double GetElapsedMilliseconds(long start, long stop)
        {
            return (stop - start) * 1000 / (double) Stopwatch.Frequency;
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

        private bool LogHttpRequest(HttpContext context, DiagnosticContextCollector collector, double elapsedMs,
            Exception ex)
        {
            var logger = _logger ?? Log.ForContext<RequestLoggingMiddleware>();
            var level = LogEventLevel.Information;
            if (context.Response.StatusCode >= 500)
            {
                level = LogEventLevel.Error;
            }
            else if (context.Response.StatusCode >= 400)
            {
                level = LogEventLevel.Warning;
            }
            else if (ex != null)
            {
                level = LogEventLevel.Error;
            }

            if (!logger.IsEnabled(level)) return false;

            // Enrich diagnostic context
            _enrichDiagnosticContext?.Invoke(_diagnosticContext, context);

            if (!collector.TryComplete(out var collectedProperties))
                collectedProperties = NoProperties;

            var requestBody = context.Items["RequestBody"]?.ToString();
            var responseBody = string.Empty;
            if (context.Response.Body != null && context.Response.ContentLength > 0)
            {
                try
                {
                    using (StreamReader reader =
                        new StreamReader(context.Response.Body, Encoding.UTF8, true, -1, true))
                    {
                        responseBody = reader.ReadToEnd();
                    }

                    context.Response.Body.Position = 0;
                }
                catch (Exception)
                {
                }
            }

            var isRequestOk = !(context.Response.StatusCode >= 400 || ex != null);
            if (_options.LogMode == LogMode.LogAll ||
                (!isRequestOk && _options.LogMode == LogMode.LogFailures))
            {
                JsonDocument requestBodyObject = null;
                if ((_options.LogRequestBodyMode == LogMode.LogAll ||
                     (!isRequestOk && _options.LogRequestBodyMode == LogMode.LogFailures)) &&
                    !string.IsNullOrWhiteSpace(requestBody))
                {
                    if (requestBody.Length > _options.RequestBodyTextLengthLogLimit)
                    {
                        requestBody = requestBody.Substring(0, _options.RequestBodyTextLengthLogLimit);
                    }

                    try
                    {
                        requestBody = requestBody.MaskFields(_options.MaskedProperties.ToArray(), _options.MaskFormat);
                        requestBodyObject = System.Text.Json.JsonDocument.Parse(requestBody);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    requestBody = null;
                }

                var requestData = new
                {
                    ClientIp = context.Connection.RemoteIpAddress.ToString(),
                    Method = context.Request.Method,
                    Scheme = context.Request.Scheme,
                    Host = context.Request.Host.Value,
                    Path = context.Request.Path.Value,
                    QueryString = context.Request.QueryString.Value,
                    context.Request.Query,
                    BodyString = requestBody,
                    Body = requestBodyObject
                };


                object responseBodyObject = null;
                if ((_options.LogResponseBodyMode == LogMode.LogAll ||
                     (!isRequestOk && _options.LogResponseBodyMode == LogMode.LogFailures)))
                {
                    if (responseBody.Length > _options.ResponseBodyTextLengthLogLimit)
                    {
                        responseBody = responseBody.Substring(0, _options.ResponseBodyTextLengthLogLimit);
                    }

                    try
                    {
                        responseBody =
                            responseBody.MaskFields(_options.MaskedProperties.ToArray(), _options.MaskFormat);
                        responseBodyObject = System.Text.Json.JsonDocument.Parse(responseBody);
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    responseBody = null;
                }

                var responseData = new
                {
                    context.Response.StatusCode,
                    IsSucceed = isRequestOk,
                    ElapsedMilliseconds = elapsedMs,
                    BodyString = responseBody,
                    Body = responseBodyObject,
                };
                
                _logger.Write(level, ex, _options.MessageTemplate, new
                {
                    Request = requestData,
                    Response = responseData,
                });
            }

            return false;
        }
    }
}