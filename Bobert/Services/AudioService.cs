using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Bobert.Services
{
    public sealed class AudioService
    {
        private readonly DiscordSocketClient _client;
        private readonly LavaNode _lavaNode;
        private readonly IConfigurationRoot _config;

        public readonly HashSet<ulong> VoteQueue;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> DisconnectTokens;

        public AudioService(DiscordSocketClient client, LavaNode lavaNode, IConfigurationRoot config)
        {
            _client = client;
            _lavaNode = lavaNode;
            _config = config;

            DisconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            _lavaNode.OnTrackEnded += OnTrackEnded;
            _lavaNode.OnTrackStarted += OnTrackStarted;

            // Check for last user left to auto-leave.
            // _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;

            VoteQueue = new HashSet<ulong>();
        }

        private async Task OnTrackStarted(TrackStartEventArgs arg)
        {
            await _client.SetGameAsync(arg.Track.Title, type: ActivityType.Listening);

            if (!DisconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value))
            {
                return;
            }

            if (value.IsCancellationRequested)
            {
                return;
            }

            value.Cancel(true);
        }

        private async Task OnTrackEnded(TrackEndedEventArgs args)
        {
            if (args.Reason != TrackEndReason.Finished)
            {
                return;
            }

            var player = args.Player;
            if (!player.Queue.TryDequeue(out var lavaTrack))
            {
                await player.TextChannel.SendMessageAsync(embed: Bot.MusicEmbed("Queue completed."));
                await _client.SetGameAsync($"{_config["prefix"]}help", type: ActivityType.Listening);
                // Leave 5 minutes after if no track was queued.
                await InitiateDisconnectAsync(args.Player, TimeSpan.FromMinutes(5d));
                return;
            }

            if (lavaTrack is null)
            {
                await player.TextChannel.SendMessageAsync(embed: Bot.ErrorEmbed("Next item in queue is not a track."));
                return;
            }

            await args.Player.TextChannel.SendMessageAsync(embed: Bot.MusicEmbed($"Now playing: [{lavaTrack.Title}]({lavaTrack.Url})"));
            await args.Player.PlayAsync(lavaTrack);
        }

        private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan)
        {
            if (!DisconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                DisconnectTokens.TryAdd(player.VoiceChannel.Id, value);
            }
            else if (value.IsCancellationRequested)
            {
                DisconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
                value = DisconnectTokens[player.VoiceChannel.Id];
            }

            var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);

            if (isCancelled)
            {
                return;
            }

            await _lavaNode.LeaveAsync(player.VoiceChannel);
            await player.TextChannel.SendMessageAsync(embed: Bot.MusicEmbed("Left the channel because nothing was playing."));
        }
    }
}