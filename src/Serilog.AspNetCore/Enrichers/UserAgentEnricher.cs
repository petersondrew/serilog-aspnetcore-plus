using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;

namespace Serilog.Enrichers
{
    /// <summary>
    /// Enriches browser user agent data
    /// </summary>
    public class UserAgentEnricher : ILogEventEnricher
    {
        private readonly IDictionary<string, LogEventProperty> _properties;

        /// <summary>
        /// Creates User Agent Enricher
        /// </summary>
        public UserAgentEnricher()
        {
            _properties = new Dictionary<string, LogEventProperty>();
        }

        /// <summary>
        /// Enrich user agent string using UAParsers
        /// </summary>
        /// <param name="logEvent"></param>
        /// <param name="propertyFactory"></param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            try
            {
                var httpContextAccessor = new HttpContextAccessor();
                var httpContext = httpContextAccessor.HttpContext;
                if (httpContext == null)
                {
                    return;
                }

                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();
                if (userAgent == null)
                {
                    return;
                }

                var uaParser = UAParser.Parser.GetDefault();
                try
                {

                    var clientInfo = uaParser.Parse(userAgent);
                    SetProperty("UserAgent", userAgent, logEvent, propertyFactory);
                    SetProperty("ClientBrowser", clientInfo.UA.Family, logEvent, propertyFactory);
                    SetProperty("ClientBrowserVersion", clientInfo.UA.Major + "." + clientInfo.UA.Minor, logEvent, propertyFactory);
                    SetProperty("ClientOperatingSystem", clientInfo.OS.Family, logEvent, propertyFactory);
                    SetProperty("ClientOperatingSystemVersion", clientInfo.OS.Major + "." + clientInfo.OS.Minor, logEvent, propertyFactory);
                    SetProperty("ClientDevice", clientInfo.Device.Family, logEvent, propertyFactory);
                    SetProperty("ClientDeviceModel", clientInfo.Device.Model, logEvent, propertyFactory);
                    SetProperty("ClientDeviceManufacturer", clientInfo.Device.Brand, logEvent, propertyFactory);
                }
                catch (Exception)
                {
                }
            }
            catch (Exception)
            {
            }
        }

        private void SetProperty(string name, string value, LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            LogEventProperty log = null;
            if (!_properties.ContainsKey(name))
            {
                log = propertyFactory.CreateProperty(name, value);
            }
            else
            {
                log = _properties[name];
            }

            logEvent.AddPropertyIfAbsent(log);
        }
    }
}
