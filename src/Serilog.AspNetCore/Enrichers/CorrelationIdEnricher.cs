using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Serilog.Enrichers
{
    /// <summary>
    /// Enrich requests using correlation id
    /// </summary>
    public class CorrelationIdEnricher : ILogEventEnricher
    {
        /// <summary>
        /// CorrelationId HttpContext Item Property Name
        /// </summary>
        public const string CorrelationIdPropertyName = "RequestCorrelationId";
        private static readonly string CorrelationIdItemName = $"CorrelationId";
        private readonly IHttpContextAccessor _contextAccessor;

        /// <summary>
        /// Creates new enricher
        /// </summary>
        public CorrelationIdEnricher() : this(new HttpContextAccessor())
        {
        }

        internal CorrelationIdEnricher(IHttpContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
        }

        /// <summary>
        /// enrich logs with unique correlation id for each request
        /// </summary>
        /// <param name="logEvent"></param>
        /// <param name="propertyFactory"></param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_contextAccessor.HttpContext == null)
                return;

            var correlationId = GetCorrelationId();

            var correlationIdProperty = new LogEventProperty(CorrelationIdPropertyName, new ScalarValue(correlationId));

            logEvent.AddOrUpdateProperty(correlationIdProperty);
        }

        private string GetCorrelationId()
        {
            return (string)(_contextAccessor.HttpContext.Items[CorrelationIdItemName] ??
                             (_contextAccessor.HttpContext.Items[CorrelationIdItemName] = Guid.NewGuid().ToString()));
        }
    }
}
