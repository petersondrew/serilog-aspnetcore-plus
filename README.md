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
                
                // Or this for more configuration
                // .UseSerilogPlus((configuration =>
                // {
                //     configuration
                //         .WriteTo.Debug()
                //         .WriteTo.Console(
                //             outputTemplate:
                //             "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} {NewLine}{Properties} {NewLine}{Exception}{NewLine}",
                //             theme: SystemConsoleTheme.Literate);
                // })
                
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
}
```

**Or** you can initialize logger directly using:
```csharp
    Log.Logger = new LoggerConfiguration()
                    .SetSerilogPlusDefaultConfiguration() // <-- Add awesome staff like EventId, UserAgent and other useful enrichers
                    .WriteTo.File(new RenderedCompactJsonFormatter(),"log.json")
                    .CreateLogger();
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

            // or for more options add this:
            // app.UseSerilogPlusRequestLogging(p =>
            // {
            //     p.LogMode = LogMode.LogAll;
            //     p.LogRequestBodyMode = LogMode.LogFailures;
            //     p.LogResponseBodyMode = LogMode.LogNone;
            //     p.RequestBodyTextLengthLogLimit = 5000;
            //     p.ResponseBodyTextLengthLogLimit = 5000;
            //     p.MaskFormat = "*****"; 
            //     p.MaskedProperties.Clear();
            //     p.MaskedProperties.Add("*password*");
            //     p.MaskedProperties.Add("*token*");
            // });
            
            // ...
            // Other app configuration
        }
```

It's important that the `UseSerilogPlusRequestLogging()` call appears _before_ handlers such as MVC. The middleware will not time or log components that appear before it in the pipeline. (This can be utilized to exclude noisy handlers from logging, such as `UseStaticFiles()`, by placing `UseSerilogPlusRequestLogging()` after them.)

### Sample Logged Item

```json
{
    "@t": "2021-03-04T21:01:36.3305267Z",
    "@m": "HTTP Request Completed { Request: { ClientIp: \"127.0.0.1\", Method: \"GET\", Scheme: \"https\", Host: \"localhost:5001\", Path: \"/home/list\", QueryString: \"\", Query: [], BodyString: null, Body: null }, Response: { StatusCode: 200, ElapsedMilliseconds: 1110, BodyString: \"[\r\n  {\r\n    \\\"date\\\": \\\"2021-03-06T00:31:35.3561032+03:30\\\",\r\n    \\\"passwordNumber\\\": \\\"*** MASKED ***\\\",\r\n    \\\"temperatureF\\\": 109,\r\n    \\\"token\\\": \\\"*** MASKED ***\\\",\r\n    \\\"summary\\\": \\\"Hot\\\"\r\n  },\r\n  {\r\n    \\\"date\\\": \\\"2021-03-07T00:31:35.3567648+03:30\\\",\r\n    \\\"passwordNumber\\\": \\\"*** MASKED ***\\\",\r\n    \\\"temperatureF\\\": 121,\r\n    \\\"token\\\": \\\"*** MASKED ***\\\",\r\n    \\\"summary\\\": \\\"Chilly\\\"\r\n  },\r\n  {\r\n    \\\"date\\\": \\\"2021-03-08T00:31:35.3567697+03:30\\\",\r\n    \\\"passwordNumber\\\": \\\"*** MASKED ***\\\",\r\n    \\\"temperatureF\\\": 29,\r\n    \\\"token\\\": \\\"*** MASKED ***\\\",\r\n    \\\"summary\\\": \\\"Hot\\\"\r\n  },\r\n  {\r\n    \\\"date\\\": \\\"2021-03-09T00:31:35.3567715+03:30\\\",\r\n    \\\"passwordNumber\\\": \\\"*** MASKED ***\\\",\r\n    \\\"temperatureF\\\": 7,\r\n    \\\"token\\\": \\\"*** MASKED ***\\\",\r\n    \\\"summary\\\": \\\"Scorching\\\"\r\n  },\r\n  {\r\n    \\\"date\\\": \\\"2021-03-10T00:31:35.3567728+03:30\\\",\r\n    \\\"passwordNumber\\\": \\\"*** MASKED ***\\\",\r\n    \\\"temperatureF\\\": 73,\r\n    \\\"token\\\": \\\"*** MASKED ***\\\",\r\n    \\\"summary\\\": \\\"Chilly\\\"\r\n  }\r\n]\", Body: [{ date: \"2021-03-06T00:31:35.3561032+03:30\", passwordNumber: \"*** MASKED ***\", temperatureF: 109, token: \"*** MASKED ***\", summary: \"Hot\" }, { date: \"2021-03-07T00:31:35.3567648+03:30\", passwordNumber: \"*** MASKED ***\", temperatureF: 121, token: \"*** MASKED ***\", summary: \"Chilly\" }, { date: \"2021-03-08T00:31:35.3567697+03:30\", passwordNumber: \"*** MASKED ***\", temperatureF: 29, token: \"*** MASKED ***\", summary: \"Hot\" }, { date: \"2021-03-09T00:31:35.3567715+03:30\", passwordNumber: \"*** MASKED ***\", temperatureF: 7, token: \"*** MASKED ***\", summary: \"Scorching\" }, { date: \"2021-03-10T00:31:35.3567728+03:30\", passwordNumber: \"*** MASKED ***\", temperatureF: 73, token: \"*** MASKED ***\", summary: \"Chilly\" }] } }",
    "@i": "42abf3a2",
    "Context": {
        "Request": {
            "ClientIp": "127.0.0.1",
            "Method": "GET",
            "Scheme": "https",
            "Host": "localhost:5001",
            "Path": "/home/list",
            "QueryString": "",
            "Query": [],
            "BodyString": null,
            "Body": null
        },
        "Response": {
            "StatusCode": 200,
            "ElapsedMilliseconds": 1110,
            "BodyString": "[\r\n  {\r\n    \"date\": \"2021-03-06T00:31:35.3561032+03:30\",\r\n    \"passwordNumber\": \"*** MASKED ***\",\r\n    \"temperatureF\": 109,\r\n    \"token\": \"*** MASKED ***\",\r\n    \"summary\": \"Hot\"\r\n  },\r\n  {\r\n    \"date\": \"2021-03-07T00:31:35.3567648+03:30\",\r\n    \"passwordNumber\": \"*** MASKED ***\",\r\n    \"temperatureF\": 121,\r\n    \"token\": \"*** MASKED ***\",\r\n    \"summary\": \"Chilly\"\r\n  },\r\n  {\r\n    \"date\": \"2021-03-08T00:31:35.3567697+03:30\",\r\n    \"passwordNumber\": \"*** MASKED ***\",\r\n    \"temperatureF\": 29,\r\n    \"token\": \"*** MASKED ***\",\r\n    \"summary\": \"Hot\"\r\n  },\r\n  {\r\n    \"date\": \"2021-03-09T00:31:35.3567715+03:30\",\r\n    \"passwordNumber\": \"*** MASKED ***\",\r\n    \"temperatureF\": 7,\r\n    \"token\": \"*** MASKED ***\",\r\n    \"summary\": \"Scorching\"\r\n  },\r\n  {\r\n    \"date\": \"2021-03-10T00:31:35.3567728+03:30\",\r\n    \"passwordNumber\": \"*** MASKED ***\",\r\n    \"temperatureF\": 73,\r\n    \"token\": \"*** MASKED ***\",\r\n    \"summary\": \"Chilly\"\r\n  }\r\n]",
            "Body": [
                {
                    "date": "2021-03-06T00:31:35.3561032+03:30",
                    "passwordNumber": "*** MASKED ***",
                    "temperatureF": 109,
                    "token": "*** MASKED ***",
                    "summary": "Hot"
                },
                {
                    "date": "2021-03-07T00:31:35.3567648+03:30",
                    "passwordNumber": "*** MASKED ***",
                    "temperatureF": 121,
                    "token": "*** MASKED ***",
                    "summary": "Chilly"
                },
                {
                    "date": "2021-03-08T00:31:35.3567697+03:30",
                    "passwordNumber": "*** MASKED ***",
                    "temperatureF": 29,
                    "token": "*** MASKED ***",
                    "summary": "Hot"
                },
                {
                    "date": "2021-03-09T00:31:35.3567715+03:30",
                    "passwordNumber": "*** MASKED ***",
                    "temperatureF": 7,
                    "token": "*** MASKED ***",
                    "summary": "Scorching"
                },
                {
                    "date": "2021-03-10T00:31:35.3567728+03:30",
                    "passwordNumber": "*** MASKED ***",
                    "temperatureF": 73,
                    "token": "*** MASKED ***",
                    "summary": "Chilly"
                }
            ]
        }
    },
    "RequestId": "0HM6VA2OSU9N1:00000001",
    "RequestPath": "/home/list",
    "ConnectionId": "0HM6VA2OSU9N1",
    "EnvironmentUserName": "DESKTOP\\Alireza",
    "MachineName": "DESKTOP",
    "EventId": "C2110DE4"
}
```