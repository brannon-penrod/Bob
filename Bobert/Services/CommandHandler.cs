using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;
using Victoria;

namespace Bobert.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;
        private readonly LavaNode _lavaNode;

        public CommandHandler(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider provider, LavaNode node)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _provider = provider;
            _lavaNode = node;

            _discord.MessageReceived += OnMessageRecievedAsync;
            _discord.Ready += OnReadyAsync;
        }

        private async Task OnMessageRecievedAsync(SocketMessage sm)
        {
            var msg = (SocketUserMessage)sm;

            if (msg == null || msg.Author.IsBot)
                return;

            var context = new SocketCommandContext(_discord, msg);

            int argPos = 0;

            if (msg.HasStringPrefix(_config["prefix"], ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos) || context.IsPrivate)
            {
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    // TODO: Give Using statement upon valid command with invalid parameters
                    await context.Channel.SendMessageAsync(embed: Bot.ErrorEmbed(result.ErrorReason));

                    Console.Write($"[{DateTime.UtcNow.ToString("MM/dd/yyyy hh:mm:ss")}] ");

                    if (context.IsPrivate)
                        Console.Write($"{context.User.Username}'s DMs ");
                    else
                        Console.Write($"{context.Guild?.Name} in {context.Channel.Name}) from {context.User.Username}: ");
                    
                    Console.WriteLine(result.ErrorReason);
                }
            }
        }

        private async Task OnReadyAsync()
        {
            if (!_lavaNode.IsConnected)
                await _lavaNode.ConnectAsync();
        }
    }
}
