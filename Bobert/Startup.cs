using Bobert.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Victoria;

namespace Bobert
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("_config.json");
            Configuration = builder.Build();
        }

        public static async Task RunAsync(string[] args)
        {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        public async Task RunAsync()
        {
            var services = new ServiceCollection();
            await ConfigureServicesAsync(services);

            var provider = services.BuildServiceProvider();
            provider.GetRequiredService<LoggingService>();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<AudioService>();

            await provider.GetRequiredService<StartupService>().StartAsync();

            await Task.Delay(-1);
        }

        private async Task ConfigureServicesAsync(IServiceCollection services)
        {
            var dsc = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
                MessageCacheSize = 1000
            });

            services.AddSingleton(dsc)
            .AddSingleton(new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false,
                IgnoreExtraArgs = true
            }))
            .AddSingleton<StartupService>()
            .AddSingleton<LoggingService>()
            .AddSingleton<CommandHandler>()
            .AddSingleton<AudioService>()
            .AddLavaNode(x =>
            {
                x.SelfDeaf = true;
            })
            .AddSingleton(Configuration);

            await Task.CompletedTask;
        }
    }
}
