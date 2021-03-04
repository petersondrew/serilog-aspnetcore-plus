using Murmur;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Serilog.Enrichers
{
    /// <summary>
    /// Event Id enricher using murmur hash
    /// </summary>
    public class EventIdEnricher : ILogEventEnricher
    {
        /// <summary>
        /// Enriches event type id using murmur hash
        /// </summary>
        /// <param name="logEvent"></param>
        /// <param name="propertyFactory"></param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent is null)
                throw new ArgumentNullException(nameof(logEvent));

            if (propertyFactory is null)
                throw new ArgumentNullException(nameof(propertyFactory));

            Murmur32 murmur = MurmurHash.Create32();
            byte[] bytes = Encoding.UTF8.GetBytes(logEvent.MessageTemplate.Text);
            byte[] hash = murmur.ComputeHash(bytes);
            string hexadecimalHash = BitConverter.ToString(hash).Replace("-", "");
            LogEventProperty eventId = propertyFactory.CreateProperty("EventId", hexadecimalHash);
            logEvent.AddPropertyIfAbsent(eventId);
        }
    }
}
