using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.AspNetCore;

namespace InlineInitializationSample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<RequestLoggingOptions>(o =>
            {
                o.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RemoteIpAddress", httpContext.Connection.RemoteIpAddress.MapToIPv4());
                };
            });

            services.AddControllersWithViews();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseStaticFiles();

            // Write streamlined request completion events, instead of the more verbose ones from the framework.
            // To use the default framework request logging instead, remove this line and set the "Microsoft"
            // level in appsettings.json to "Information".
            app.UseSerilogPlusRequestLogging(p =>
            {
                p.LogMode = LogMode.LogAll;
                p.LogRequestBodyMode = LogMode.LogFailures;
                p.LogResponseBodyMode = LogMode.LogNone;
                p.RequestBodyTextLengthLogLimit = 5000;
                p.ResponseBodyTextLengthLogLimit = 5000;
                p.MaskFormat = "*****"; 
                p.MaskedProperties.Clear();
                p.MaskedProperties.Add("*password*");
                p.MaskedProperties.Add("*token*");
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
