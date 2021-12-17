using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Bobert.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _services;

        public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider services)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _services = services;
         }

        public async Task StartAsync()
        {
            string appName = _config["app:name"];
            string discordToken = _config["tokens:discord"];
            string prefix = _config["prefix"];

            if (string.IsNullOrWhiteSpace(appName))
                throw new ArgumentNullException("Please enter an app name into the `_config.json` file found in the application's root directory.");

            if (string.IsNullOrWhiteSpace(discordToken))
                throw new ArgumentNullException($"Please enter a Discord token for {appName} info into the `_config.json` file found in the application's root directory.");

            if(string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentNullException($"Please enter a Discord command prefix for {appName} in the `_config.json` file found in the application's root directory.");

            await _discord.LoginAsync(TokenType.Bot, discordToken);
            await _discord.StartAsync();

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            await _discord.SetGameAsync($"{prefix}help", null, ActivityType.Listening);
        }
    }
}
