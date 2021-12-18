using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public readonly HashSet<ulong> VoteQueue;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;

        public AudioService(DiscordSocketClient client, LavaNode lavaNode)
        {
            _client = client;
            _lavaNode = lavaNode;
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();

            _lavaNode.OnTrackEnded += OnTrackEnded;
            _lavaNode.OnTrackStarted += OnTrackStarted;

            // Check for last user left to auto-leave.
            // _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;

            VoteQueue = new HashSet<ulong>();
        }

        private async Task OnTrackStarted(TrackStartEventArgs arg)
        {
            await _client.SetGameAsync(arg.Track.Title, type: ActivityType.Listening);

            if (!_disconnectTokens.TryGetValue(arg.Player.VoiceChannel.Id, out var value))
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
                // Leave 5 minutes after if no track was queued.
                _ = InitiateDisconnectAsync(args.Player, TimeSpan.FromSeconds(300));
                return;
            }

            if (lavaTrack is null)
            {
                await player.TextChannel.SendMessageAsync(embed: Bot.ErrorEmbed("Next item in queue is not a track."));
                return;
            }

            await args.Player.TextChannel.SendMessageAsync(embed: Bot.MusicEmbed($"Now playing: {lavaTrack.Title}"));
            await args.Player.PlayAsync(lavaTrack);
        }

        private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan)
        {
            if (!_disconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                _disconnectTokens.TryAdd(player.VoiceChannel.Id, value);
            }
            else if (value.IsCancellationRequested)
            {
                _disconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
                value = _disconnectTokens[player.VoiceChannel.Id];
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