using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bobert.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _provider = provider;

            _discord.MessageReceived += OnMessageRecievedAsync;
        }

        private async Task OnMessageRecievedAsync(SocketMessage sm)
        {
            var msg = (SocketUserMessage)sm;

            if (msg == null || msg.Author.IsBot)
                return;

            var context = new SocketCommandContext(_discord, msg);

            int argPos = 0;

            if (msg.HasStringPrefix(_config["prefix"], ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    await context.Channel.SendMessageAsync(result.ErrorReason);

                    Console.WriteLine(
                        $"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss")}] " +
                        $"{context.Guild.Name} in {context.Channel.Name} " +
                        $"from {context.User.Username}: {result.ErrorReason}");
                }
            }
        }
    }
}
