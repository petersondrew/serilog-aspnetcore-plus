using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;

namespace Serilog.Enrichers
{
    /// <summary>
    /// Enriches user claims in log entries
    /// </summary>
    public class UserClaimsEnricher : ILogEventEnricher
    {
        private HashSet<string> ignoredClaims = new HashSet<string>();

        /// <summary>
        /// Enriches user claims except ignoredclaims
        /// </summary>
        /// <param name="logEvent"></param>
        /// <param name="propertyFactory"></param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var httpContextAccessor = new HttpContextAccessor();
            var httpContext = httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            if (httpContext.User == null ||
                httpContext.User.Identity == null ||
                !httpContext.User.Identity.IsAuthenticated)
            {
                return;
            }

            foreach (var item in user.Claims)
            {
                if (ignoredClaims.Contains(item.Type))
                {
                    continue;
                }

                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("user_" + item.Type, item.Value));
            }
        }
    }
}
