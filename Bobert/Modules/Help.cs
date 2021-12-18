using Bobert;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bobert.Modules
{
    [Summary("Commands to get information on the bot, or bot commands.")]
    public class Help : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private readonly IConfigurationRoot _config;

        public Help(CommandService service, IConfigurationRoot config)
        {
            _service = service;
            _config = config;
        }

        [Command("help", ignoreExtraArgs: false)]
        [Summary("Displays all modules on this bot.")]
        public async Task HelpAsync()
        {
            var builder = new EmbedBuilder
            {
                Color = ModuleColor.Help,
                Title = "Modules",
                Fields = new List<EmbedFieldBuilder>()
            };

            // add all modules' information to the help message
            foreach (ModuleInfo module in _service.Modules)
            {
                // add the module information to the help message builder
                builder.AddField(f =>
                {
                    f.Name = module.Name;
                    f.Value = module.Summary ?? "No summary given.";
                    f.IsInline = true;
                });
            }

            if(!Context.IsPrivate)
                await Context.Message.DeleteAsync();

            try
            {
                if (Context.IsPrivate)
                    await ReplyAsync(embed: builder.Build());
                else
                    await Context.User.SendMessageAsync(embed: builder.Build());
            } catch(Discord.Net.HttpException)
            {
                // DMs are not open, sending message in context channel instead.
                await ReplyAsync(embed: builder.Build());
            }
        }

        [Command("help")]
        [Summary("Shows help for the given module.")]
        public async Task HelpAsync(string moduleName)
        {
            // command list builder
            var builder = new EmbedBuilder();

            builder.Description +=
                    $"Prefix: {_config["prefix"]}\n" +
                    "[Required parameter]\n" +
                    "<Optional parameter>\n" +
                    "(Alias)\n";

            var module = _service.Modules.Where(m => string.Equals(m.Name, moduleName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

            if (module == null)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed($"Invalid module name: `{moduleName}`. Use {_config["prefix"]}help to view modules."));
                return;
            }

            builder.Color = ModuleColor.GetColorFromModuleName(moduleName);

            builder.Title = module.Name;

            // add the command's information to the help message
            foreach (CommandInfo cmd in module.Commands)
            {
                builder.AddField(f =>
                {
                    f.Name = FormatAliasesAndParameters(cmd);
                    f.Value = cmd.Summary ?? "No command summary given.";
                    f.IsInline = true;
                });
            }

            try
            {
                if (Context.IsPrivate)
                    await ReplyAsync(embed: builder.Build());
                else
                    await Context.User.SendMessageAsync(embed: builder.Build());
            }
            catch (Discord.Net.HttpException)
            {
                // DMs are not open, sending message in context channel instead.
                await ReplyAsync(embed: builder.Build());
            }
        }

        private string FormatAliasesAndParameters(CommandInfo cmd)
        {
            // add cmd's first alias to the description
            string description = cmd.Aliases.First();

            if (cmd.Aliases.Count > 1)
            {
                var list = cmd.Aliases.ToList();
                list.RemoveAt(0);

                string aliases = string.Join(", ", list);

                description += $" ({aliases})";
            }

            if (cmd.Parameters.Count > 0)
            {
                // add the parameter names and optionality
                foreach (var param in cmd.Parameters)
                {
                    if (param.IsOptional)
                        description += $" <{param.Name}>";
                    else // the parameter is required
                        description += $" [{param.Name}]";
                }
            }

            return description;
        }
    }
}
