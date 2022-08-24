﻿// Copyright 2019 Serilog Contributors
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using Serilog.Extensions;
using Serilog.Models;

namespace Serilog.AspNetCore
{
    // ReSharper disable once ClassNeverInstantiated.Global
    class RequestLoggingMiddleware
    {
        readonly RequestDelegate _next;
        readonly DiagnosticContext _diagnosticContext;
        private readonly RequestLoggingOptions _options;
        readonly Action<IDiagnosticContext, HttpContext> _enrichDiagnosticContext;
        readonly ILogger _logger;
        readonly bool _includeQueryInRequestPath;
        static readonly LogEventProperty[] NoProperties = new LogEventProperty[0];

        public RequestLoggingMiddleware(RequestDelegate next, DiagnosticContext diagnosticContext,
            RequestLoggingOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _diagnosticContext = diagnosticContext ?? throw new ArgumentNullException(nameof(diagnosticContext));
            _options = options;

            _enrichDiagnosticContext = options.EnrichDiagnosticContext;
            _logger = options.Logger?.ForContext<RequestLoggingMiddleware>();
            _includeQueryInRequestPath = options.IncludeQueryInRequestPath;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

            var start = Stopwatch.GetTimestamp();

            FetchOrGenerateCorrelationId(httpContext);

            var collector = _diagnosticContext.BeginCollection();

            httpContext.Request.EnableBuffering();
            var memoryStream = new MemoryStream();
            var originalResponseBodyStream = httpContext.Response.Body;
            httpContext.Response.Body = memoryStream;
            try
            {
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
                await RevertResponseBodyStreamAsync(memoryStream, originalResponseBodyStream);
                collector.Dispose();
                memoryStream.Dispose();
            }
        }


        private void FetchOrGenerateCorrelationId(HttpContext context)
        {
            var header = context.Request.Headers["X-Correlation-ID"];
            var correlationId = header.Count > 0 ? header[0] : Guid.NewGuid().ToString();
            context.Items["CorrelationId"] = correlationId;
        }

        static double GetElapsedMilliseconds(long start, long stop)
        {
            return (stop - start) * 1000 / (double)Stopwatch.Frequency;
        }

        static string GetPath(HttpContext httpContext, bool includeQueryInRequestPath)
        {
            /*
                In some cases, like when running integration tests with WebApplicationFactory<T>
                the Path returns an empty string instead of null, in that case we can't use
                ?? as fallback.
            */
            var requestPath = includeQueryInRequestPath
                ? httpContext.Features.Get<IHttpRequestFeature>()?.RawTarget
                : httpContext.Features.Get<IHttpRequestFeature>()?.Path;
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
            var level = _options.GetLevel(context, elapsedMs, ex);

            if (!logger.IsEnabled(level)) return false;

            // Enrich diagnostic context
            _enrichDiagnosticContext?.Invoke(_diagnosticContext, context);

            string requestBodyText;
            string responseBodyText;

            var isRequestOk = !(context.Response.StatusCode >= 400 || ex != null);
            if (_options.LogMode == LogMode.LogAll ||
                (!isRequestOk && _options.LogMode == LogMode.LogFailures))
            {
                object requestBody = null;
                if ((_options.RequestBodyLogMode == LogMode.LogAll ||
                     (!isRequestOk && _options.RequestBodyLogMode == LogMode.LogFailures)))
                {
                    requestBodyText = TryReadRequestBodyText(context);
                    if (_options.LogRequestBodyAsStructuredObject && !string.IsNullOrWhiteSpace(requestBodyText))
                    {
                        JToken token;
                        if (requestBodyText.TryGetJToken(out token))
                        {
                            var jToken = token.MaskFields(_options.MaskedProperties.ToArray(),
                                _options.MaskFormat);
                            requestBodyText = jToken.ToString();
                            requestBody = jToken;
                        }

                        if (requestBodyText.Length > _options.RequestBodyLogTextLengthLimit)
                            requestBodyText = requestBodyText.Substring(0, _options.RequestBodyLogTextLengthLimit);
                    }
                }
                else
                {
                    requestBodyText = "(Not Logged)";
                }

                var requestHeader = new Dictionary<string, object>();
                if (_options.RequestHeaderLogMode == LogMode.LogAll ||
                    (!isRequestOk && _options.RequestHeaderLogMode == LogMode.LogFailures))
                {
                    try
                    {
                        var valuesByKey = context.Request.Headers
                            .Mask(_options.MaskedProperties.ToArray(), _options.MaskFormat);
                        foreach (var item in valuesByKey)
                        {
                            if (item.Value.Count() > 1)
                                requestHeader.Add(item.Key, item.Value);
                            else
                                requestHeader.Add(item.Key, item.Value.First());                        }
                    }
                    catch (Exception headerParseException)
                    {
                        SelfLog.WriteLine("Cannot parse request header: " + headerParseException);
                    }
                }

                var userAgentDic = new Dictionary<string, string>();
                if (context.Request.Headers.ContainsKey("User-Agent"))
                {
                    var userAgent = context.Request.Headers["User-Agent"].ToString();
                    userAgentDic.Add("_Raw", userAgent);
                    try
                    {
                        var uaParser = UAParser.Parser.GetDefault();
                        var clientInfo = uaParser.Parse(userAgent);
                        userAgentDic.Add("Browser", clientInfo.UA.Family);
                        userAgentDic.Add("BrowserVersion", clientInfo.UA.Major + "." + clientInfo.UA.Minor);
                        userAgentDic.Add("OperatingSystem", clientInfo.OS.Family);
                        userAgentDic.Add("OperatingSystemVersion", clientInfo.OS.Major + "." + clientInfo.OS.Minor);
                        userAgentDic.Add("Device", clientInfo.Device.Family);
                        userAgentDic.Add("DeviceModel", clientInfo.Device.Model);
                        userAgentDic.Add("DeviceManufacturer", clientInfo.Device.Brand);
                    }
                    catch (Exception e)
                    {
                        SelfLog.WriteLine($"Cannot parse user agent:" + userAgent + $" --- {e}");
                    }
                }

                var requestQuery = new Dictionary<string, object>();
                try
                {
                    var valuesByKey = context.Request.Query.GroupBy(x => x.Key);
                    foreach (var item in valuesByKey)
                    {
                        if (item.Count() > 1)
                            requestQuery.Add(item.Key, item.Select(x => x.Value.ToString()).ToArray());
                        else
                            requestQuery.Add(item.Key, item.First().Value.ToString());
                    }
                }
                catch (Exception e)
                {
                    SelfLog.WriteLine($"Cannot parse query string: {e}");
                }

                var requestData = new HttpRequestInfo
                {
                    ClientIp = context.GetClientIp()?.ToString(), 
                    Method = context.Request.Method, 
                    Scheme = context.Request.Scheme,
                    Host = context.Request.Host.Value,
                    Path = context.Request.Path.Value,
                    QueryString = context.Request.QueryString.Value,
                    Query = requestQuery,
                    BodyString = requestBodyText,
                    Body = requestBody,
                    Headers = requestHeader,
                    UserAgent = userAgentDic
                };
                
                object responseBody = null;
                if ((_options.ResponseBodyLogMode == LogMode.LogAll ||
                     (!isRequestOk && _options.ResponseBodyLogMode == LogMode.LogFailures)))
                {
                    responseBodyText = TryReadResponseBodyText(context);
                    if (!string.IsNullOrWhiteSpace(responseBodyText))
                    {
                        JToken jToken;
                        if (_options.LogResponseBodyAsStructuredObject && responseBodyText.TryGetJToken(out jToken))
                        {
                            jToken = jToken.MaskFields(_options.MaskedProperties.ToArray(), _options.MaskFormat);
                            responseBodyText = jToken.ToString();
                            responseBody = jToken;
                        }

                        if (responseBodyText.Length > _options.ResponseBodyLogTextLengthLimit)
                        {
                            responseBodyText = responseBodyText.Substring(0, _options.ResponseBodyLogTextLengthLimit);
                        }
                    }
                }
                else
                {
                    responseBodyText = "(Not Logged)";
                }

                var responseHeader = new Dictionary<string, object>();
                if (_options.ResponseHeaderLogMode == LogMode.LogAll ||
                    (!isRequestOk && _options.ResponseHeaderLogMode == LogMode.LogFailures))
                {
                    try
                    {
                        var valuesByKey = context.Response.Headers
                            .Mask(_options.MaskedProperties.ToArray(), _options.MaskFormat);
                        foreach (var item in valuesByKey)
                        {
                            if (item.Value.Count() > 1)
                                responseHeader.Add(item.Key, item.Value);
                            else
                                responseHeader.Add(item.Key, item.Value.First());
                        }
                    }
                    catch (Exception headerParseException)
                    {
                        SelfLog.WriteLine("Cannot parse response header: " + headerParseException);
                    }
                }

                var responseData = new HttpResponseInfo
                {
                    StatusCode = context.Response.StatusCode, IsSucceed = isRequestOk, ElapsedMilliseconds = elapsedMs,
                    BodyString = responseBodyText,
                    Body = responseBody,
                    Headers = responseHeader
                };

                if (!collector.TryComplete(out var collectedProperties))
                    collectedProperties = NoProperties;

                var httpRequestContext = new HttpContextInfo { Request = requestData, Response = responseData, Diagnostics = collectedProperties.ToDictionary(x => x.Name, x => x.Value.ToString()) };
                var messageOptions = _options.GetLogMessageAndProperties(httpRequestContext);
                var contextLogger = logger;
                if (messageOptions.AdditionalProperties != null)
                {
                    foreach (var p in messageOptions.AdditionalProperties)
                    {
                        contextLogger = contextLogger.ForContext(p.Key, p.Value, true);
                    }
                }

                contextLogger.Write(level, ex, messageOptions.MessageTemplate, messageOptions.MessageParameters);
            }

            return false;
        }

        private static string TryReadRequestBodyText(HttpContext context)
        {
            string requestBodyText = null;
            if (context.Request.ContentLength.HasValue && context.Request.ContentLength > 0)
            {
                context.Request.Body.Position = 0;

                using (StreamReader reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, -1, true))
                {
                    requestBodyText = reader.ReadToEnd();
                }

                context.Request.Body.Position = 0;
            }

            return requestBodyText;
        }

        private static string TryReadResponseBodyText(HttpContext context)
        {
            var responseBodyText = string.Empty;
            if (context.Response.Body != null && context.Response.Body.Length > 0)
            {
                try
                {
                    context.Response.Body.Position = 0;

                    using (StreamReader reader =
                           new StreamReader(context.Response.Body, Encoding.UTF8, true, -1, true))
                    {
                        responseBodyText = reader.ReadToEnd();
                    }

                    context.Response.Body.Position = 0;
                }
                catch (Exception responseParseException)
                {
                    SelfLog.WriteLine("Failed to extract response: " + responseParseException);
                }
            }

            return responseBodyText;
        }

        async Task RevertResponseBodyStreamAsync(Stream bodyStream, Stream orginalBodyStream)
        {
            bodyStream.Seek(0, SeekOrigin.Begin);
            await bodyStream.CopyToAsync(orginalBodyStream);
        }
    }
}