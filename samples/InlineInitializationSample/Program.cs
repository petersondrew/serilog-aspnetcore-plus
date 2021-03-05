using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace InlineInitializationSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
                .UseSerilogPlus((configuration =>
                {
                    configuration
                        .WriteTo.Debug()
                        .WriteTo.Console(
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} {NewLine}{Properties} {NewLine}{Exception}{NewLine}",
                            theme: SystemConsoleTheme.Literate);
                }));
    }
}
