using System.Linq;
using System.Threading.Tasks;
using Disqord;
using Disqord.Bot.Hosting;
using Disqord.Gateway;
using Disqord.Gateway.Api.Default;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoLifeBot.Data;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace NoLifeBot
{
    class Program
    {
        private const string ConfigPath = "./config.json";
        
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            using (host)
            {
                await host.RunAsync();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return new HostBuilder()
                .ConfigureLogging(x =>
                {
                    var logger = new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                        .WriteTo.Console(
                            outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                            theme: AnsiConsoleTheme.Code)
                        .CreateLogger();

                    x.AddSerilog(logger, true);

                    x.Services.Remove(x.Services.First(x => x.ServiceType == typeof(ILogger<>)));
                    x.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
                })
                .ConfigureServices((context, services) =>
                {
                    var connString = context.Configuration["postgres:connection_string"];
                    services.AddDbContext<NoLifeBotDbContext>(x => x.UseNpgsql(connString).UseSnakeCaseNamingConvention());
                })
                .ConfigureHostConfiguration(configuration => configuration.AddJsonFile(ConfigPath))
                .ConfigureDiscordBot((context, bot) =>
                {
                    bot.Token = context.Configuration["discord:token"];
                    bot.OwnerIds = context.Configuration.GetSection("bot:owner_ids").GetChildren().Select(x => Snowflake.Parse(x.Value));
                    bot.Intents = GatewayIntents.Recommended;
                    bot.Prefixes = context.Configuration.GetSection("bot:prefixes").GetChildren().Select(x => x.Value);
                });
        }
    }
}