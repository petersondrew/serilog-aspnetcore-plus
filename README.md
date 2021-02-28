# Serilog.AspNetCore.Plus 
An improved version of Serilog.AspNetCore package based on my usage in applications with following features:

- Default log setup based on practices for faster project boostrap
- Captures request body
- Capture responses if response is not successfull (status >= 400)
- Log levels based on response status code (Warning for status >= 400, Error for status >= 500)
- Capture additional data like Event Id, User Agent Data and other environment data
- Read log configuration automatically from logsettings.json or logsettings.{Environment}.json if files exists for better log configuration management

### Instructions

**First**, install the _Serilog.AspNetCore.Plus_ [NuGet package](https://www.nuget.org/packages/Serilog.AspNetCore.Plus) into your app.

```shell
dotnet add package Serilog.AspNetCore.Plus
```

**Next**, in your application's _Program.cs_ file, configure Serilog first.  A `try`/`catch` block will ensure any configuration issues are appropriately logged:

```csharp
using Serilog;

public class Program
{
    public static int Main(string[] args)
    {
        try
        {
            Log.Information("Starting web host");
            CreateHostBuilder(args).Build().Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
```

**Then**, add `UseSerilogPlus()` to the Generic Host in `CreateHostBuilder()`.

```csharp        
    public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilogPlus() // <-- Add this line
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
}
```

### Request logging

In your application's _Startup.cs_, add the middleware with `UseSerilogPlus()`:

```csharp
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSerilogPlusRequestLogging(); // <-- Add this line

            // Other app configuration
        }
```

It's important that the `UseSerilogPlusRequestLogging()` call appears _before_ handlers such as MVC. The middleware will not time or log components that appear before it in the pipeline. (This can be utilized to exclude noisy handlers from logging, such as `UseStaticFiles()`, by placing `UseSerilogPlusRequestLogging()` after them.)

