// Copyright 2019-2020 Serilog Contributors
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
using Serilog.Events;
using System;
using System.Collections.Generic;
using Serilog.Models;

namespace Serilog.AspNetCore
{
    /// <summary>
    /// Contains options for the <see cref="RequestLoggingMiddleware"/>.
    /// </summary>
    public class RequestLoggingOptions
    {
        /// <summary>
        /// A function returning the <see cref="LogEntryParameters"/> based on the <see cref="HttpContextInfo"/> information,
        /// default behavior is logging message with template "HTTP request {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms"
        /// and attaching HTTP contextual data <see cref="HttpContextInfo"/> as property named "Context"
        /// </summary>
        public Func<HttpContextInfo, LogEntryParameters> GetLogMessageAndProperties { get; set; }

        /// <summary>
        /// A callback that can be used to set additional properties on the request completion event.
        /// </summary>
        public Action<IDiagnosticContext, HttpContext> EnrichDiagnosticContext { get; set; }

        /// <summary>
        /// A function returning the <see cref="LogEventLevel"/> based on the <see cref="HttpContext"/>, the number of
        /// elapsed milliseconds required for handling the request, and an <see cref="Exception" /> if one was thrown.
        /// The default behavior returns <see cref="LogEventLevel.Error"/> when the response status code is greater than 499 or if the
        /// <see cref="Exception"/> is not null. Also default log level for 4xx range errors set to <see cref="LogEventLevel.Warning"/>   
        /// </summary>
        /// <value>
        /// A function returning the <see cref="LogEventLevel"/>.
        /// </value>
        public Func<HttpContext, double, Exception, LogEventLevel> GetLevel { get; set; }

        /// <summary>
        /// The logger through which request completion events will be logged. The default is to use the
        /// static <see cref="Log"/> class.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Determines when logging requests information. Default is true.
        /// </summary>
        public LogMode LogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging request headers
        /// </summary>
        public LogMode RequestHeaderLogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging request body data
        /// </summary>
        public LogMode RequestBodyLogMode { get; set; } = LogMode.LogAll;
        
        /// <summary>
        /// Determines weather to log request as structured object instead of string. This is useful when you use Elastic, Splunk or any other platform to search on object properties. Default is true. Masking only works when this options is enabled.
        /// </summary>
        public bool LogRequestBodyAsStructuredObject { get; set; } = true;

        /// <summary>
        /// Determines when logging response headers
        /// </summary>
        public LogMode ResponseHeaderLogMode { get; set; } = LogMode.LogAll;

        /// <summary>
        /// Determines when logging response body data
        /// </summary>
        public LogMode ResponseBodyLogMode { get; set; } = LogMode.LogFailures;

        /// <summary>
        /// Determines whether to log response as structured object instead of string. This is useful when you use Elastic, Splunk or any other platform to search on object properties. Default is true. Masking only works when this options is enabled.
        /// </summary>
        public bool LogResponseBodyAsStructuredObject { get; set; } = true;
        
        /// <summary>
        /// Properties to mask before logging to output to prevent sensitive data leakage
        /// </summary>
        public IList<string> MaskedProperties { get; } =
            new List<string>()
                {"*password*", "*token*", "*clientsecret*", "*bearer*", "*secret*", "*authorization*", "*client-secret*", "*otp"};

        /// <summary>
        /// Mask format to replace for masked properties
        /// </summary>
        public string MaskFormat { get; set; } = "*** MASKED ***";

        /// <summary>
        /// Maximum allowed length of response body text to capture in logs. response bodies that exceeds this limit will be trimmed.
        /// </summary>
        public int ResponseBodyLogTextLengthLimit { get; set; } = 4000;

        /// <summary>
        /// Maximum allowed length of request body text to capture in logs. request bodies that exceeds this limit will be trimmed.
        /// </summary>
        public int RequestBodyLogTextLengthLimit { get; set; } = 4000;

        /// <summary>
        /// Include the full URL query string in the <c>RequestPath</c> property
        /// that is attached to request log events. The default is <c>false</c>.
        /// </summary>
        public bool IncludeQueryInRequestPath { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public RequestLoggingOptions()
        {
            GetLevel = DefaultGetLevel;
            GetLogMessageAndProperties = DefaultLogMessageAndProperties;
        }
        
        private static LogEventLevel DefaultGetLevel(HttpContext ctx, double _, Exception ex)
        {
            var level = LogEventLevel.Information;
            if (ctx.Response.StatusCode >= 500)
            {
                level = LogEventLevel.Error;
            }
            else if (ctx.Response.StatusCode >= 400)
            {
                level = LogEventLevel.Warning;
            }
            else if (ex != null)
            {
                level = LogEventLevel.Error;
            }

            return level;
        }
        
        private static LogEntryParameters DefaultLogMessageAndProperties(HttpContextInfo h)
        {
            return new LogEntryParameters()
            {
                MessageTemplate = "HTTP Request {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds:0.0000} ms",
                MessageParameters = new object[]{ h.Request.Method, h.Request.Path, h.Response.StatusCode, h.Response.ElapsedMilliseconds},
                AdditionalProperties = { ["Context"] = h }
            };
        }
    }
}
