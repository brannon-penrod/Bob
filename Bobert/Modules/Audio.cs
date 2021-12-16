using Bobert.Services;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace Bobert.Modules
{
    public class Audio : ModuleBase<SocketCommandContext>
    {
        private readonly AudioService _service;

        public Audio(AudioService service)
        {
            _service = service;
        }

        [Command("join", RunMode = RunMode.Async)]
        [Summary("Joins your current voice channel.")]
        public async Task JoinCmd()
        {
            await _service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        }

        [Command("leave", RunMode = RunMode.Async)]
        [Summary("Leaves the current voice channel.")]
        public async Task LeaveCmd()
        {
            await _service.LeaveAudio(Context.Guild);
        }
    }
}
