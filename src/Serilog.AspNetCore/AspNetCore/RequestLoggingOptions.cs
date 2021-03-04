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

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Serilog.AspNetCore
{
    /// <summary>
    /// Contains options for the <see cref="RequestLoggingMiddleware"/>.
    /// </summary>
    public class RequestLoggingOptions
    {
        const string DefaultRequestCompletionMessageTemplate =
            "HTTP Request Completed {@Context}";

        /// <summary>
        /// Gets or sets the message template. The default value is
        /// <c>"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms"</c>. The
        /// template can contain any of the placeholders from the default template, names of properties
        /// added by ASP.NET Core, and names of properties added to the <see cref="IDiagnosticContext"/>.
        /// </summary>
        /// <value>
        /// The message template.
        /// </value>
        public string MessageTemplate { get; set; }
        
        /// <summary>
        /// A callback that can be used to set additional properties on the request completion event.
        /// </summary>
        public Action<IDiagnosticContext, HttpContext> EnrichDiagnosticContext { get; set; }

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
        /// Determines when logging request body data
        /// </summary>
        public LogMode LogRequestBodyMode { get; set; } = LogMode.LogAll;
        /// <summary>
        /// Determines when logging response body data
        /// </summary>
        public LogMode LogResponseBodyMode { get; set; } = LogMode.LogFailures;
        /// <summary>
        /// Properties to mask before logging to output to prevent sensitive data leakage
        /// </summary>
        public IList<string> MaskedProperties { get; } =
            new List<string>() {"password", "token", "clientsecret", "otp"};
        /// <summary>
        /// Mask format to replace with masked data
        /// </summary>
        public string MaskFormat { get; set; } = "*** MASKED ***";
        /// <summary>
        /// Maximum allowed length of response body text to capture in logs
        /// </summary>
        public int ResponseBodyTextLengthLogLimit { get; set; } = 4000;
        /// <summary>
        /// Maximum allowed length of request body text to capture in logs
        /// </summary>
        public int RequestBodyTextLengthLogLimit { get; set; } = 4000;

        /// <summary>
        /// Constructor
        /// </summary>
        public RequestLoggingOptions()
        {
            MessageTemplate = DefaultRequestCompletionMessageTemplate;
        }
    }
}
