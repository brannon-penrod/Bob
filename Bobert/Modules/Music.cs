using Bobert.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace Bobert.Modules
{
    [Summary("Commands to play music in a voice channel.")]
    [RequireContext(ContextType.Guild)]
    public class Music : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly AudioService _audio;
        private readonly DiscordSocketClient _client;

        public Music(LavaNode node, AudioService audio, DiscordSocketClient client)
        {
            _lavaNode = node;
            _audio = audio;
            _client = client;
        }

        [Command("join")]
        [Summary("Joins your current voice channel.")]
        public async Task JoinAsync()
        {
            if (Context.IsPrivate)
                return;

            if (_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed($"I'm already connected to **{_lavaNode.GetPlayer(Context.Guild).VoiceChannel.Name}**."));
                return;
            }

            var voiceState = Context.User as IVoiceState;
            if (voiceState?.VoiceChannel == null)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("You must be connected to a voice channel."));
                return;
            }

            // TODO: check whether bot can connect to voice channel
            //var connections = await _client.GetConnectionsAsync();??
            //if (connections.Contains())...

            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, Context.Channel as ITextChannel);
            }
            catch (Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("leave")]
        [Alias("gtfo")]
        [Summary("Leaves the current voice channel.")]
        public async Task LeaveAsync()
        {
            if (Context.IsPrivate)
                return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to any voice channels."));
                return;
            }

            var voiceChannel = (Context.User as IVoiceState)?.VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not sure which voice channel to disconnect from."));
                return;
            }

            try
            {
                await _lavaNode.LeaveAsync(voiceChannel);
            }
            catch (Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("play")]
        [Summary("Attempts to play the music using the given query.")]
        public async Task PlayAsync([Remainder] string searchQuery)
        {
            if (Context.IsPrivate)
                return;

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Please provide search terms."));
                return;
            }

            var searchResponse = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);

            if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
            {
                // check YouTube search
                searchResponse = await _lavaNode.SearchYouTubeAsync(searchQuery);

                // search still returns nothing
                if (searchResponse.Status is SearchStatus.LoadFailed or SearchStatus.NoMatches)
                {
                    await ReplyAsync(embed: Bot.ErrorEmbed($"I wasn't able to find anything for `{searchQuery}`."));
                    return;
                }
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
                await JoinAsync();

            var player = _lavaNode.GetPlayer(Context.Guild);

            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                var tracks = searchResponse.Tracks;
                player.Queue.Enqueue(tracks);

                TimeSpan totalTime = TimeSpan.Zero;

                foreach (var track in tracks)
                {
                    totalTime += track.Duration;
                }

                await ReplyAsync(embed: new EmbedBuilder()
                {
                    Color = ModuleColor.Music,
                    Title = $"Added {tracks.Count} tracks to queue.",
                    Footer = new EmbedFooterBuilder()
                    {
                        IconUrl = Context.User.GetAvatarUrl(),
                        Text = (Context.User != null ? $"Queued by {Context.User.Username} • " : null) + $"Total duration: {Bot.FormatTimeSpan(totalTime)}"
                    },
                    ThumbnailUrl = await tracks.First().FetchArtworkAsync()
                }.Build());
            }

            else
            {
                var track = searchResponse.Tracks.FirstOrDefault();
                player.Queue.Enqueue(track);

                if (player.Queue.Count > 0)
                    await ReplyAsync(embed: await Bot.GetTrackEmbedAsync("Added to queue.", track, Context.User));
            }

            if (player.PlayerState is PlayerState.Playing or PlayerState.Paused)
                return;

            player.Queue.TryDequeue(out var lavaTrack);

            await player.PlayAsync(x =>
            {
                x.Track = lavaTrack;
                x.ShouldPause = false;
            });
        }

        [Command("pause")]
        [Summary("Pauses the player if it is currently playing.")]
        public async Task PauseAsync()
        {
            if (Context.IsPrivate)
                return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("I'm not playing anything."));
                return;
            }

            try
            {
                await player.PauseAsync();
                await ReplyAsync(embed: Bot.MusicEmbed($"Paused: {player.Track.Title}"));
            }
            catch (Exception exception)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(exception.Message));
            }
        }

        [Command("resume")]
        [Summary("Resumes the player if it is currently paused.")]
        public async Task ResumeAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Paused)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not playing anything."));
                return;
            }

            try
            {
                await player.ResumeAsync();
                await ReplyAsync(embed: Bot.MusicEmbed($"Resumed: {player.Track.Title}"));
            }
            catch (Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("stop")]
        [Alias("stfu")]
        [Summary("Stops the player, removing all tracks from the queue.")]
        public async Task StopAsync()
        {
            if (Context.IsPrivate)
                return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not playing anything."));
                return;
            }

            try
            {
                await player.StopAsync();
                await ReplyAsync(embed: Bot.MusicEmbed($"Skipped the current song and removed all tracks from queue."));
            }
            catch (Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("skip")]
        [Alias("i hate this")]
        [Summary("Skips the current track in the player.")]
        public async Task SkipAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Nothing is playing."));
                return;
            }

            var voiceChannelUsers = (player.VoiceChannel as SocketVoiceChannel)?.Users
                .Where(x => !x.IsBot)
                .ToArray();

            if (!voiceChannelUsers.Contains(Context.User))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("You're not in the channel."));
                return;
            }

            if (_audio.VoteQueue.Contains(Context.User.Id))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("You can't vote again."));
                return;
            }

            _audio.VoteQueue.Add(Context.User.Id);

            // 50% vote required to skip -- rounded up
            int votesNeeded = (int)Math.Ceiling(voiceChannelUsers.Count() * 0.5);

            await ReplyAsync(embed: Bot.ErrorEmbed($"{_audio.VoteQueue.Count} / {votesNeeded} votes to skip."));

            if (_audio.VoteQueue.Count >= votesNeeded)
            {
                try
                {
                    await ReplyAsync(embed: Bot.MusicEmbed($"Skipping {player.Track.Title}."));

                    if (player.Queue.Count > 0)
                    {
                        (var skipped, var current) = await player.SkipAsync();
                        await ReplyAsync(embed: await Bot.GetTrackEmbedAsync("Now playing: ", current, Context.User));
                    }

                    else await player.StopAsync();
                }
                catch (Exception exception)
                {
                    await ReplyAsync(embed: Bot.ErrorEmbed(exception.Message));
                }

                _audio.VoteQueue.Clear();
            }
        }

        [Command("volume")]
        [Alias("vol", "v")]
        [Summary("Changes the volume to the supplied value.")]
        public async Task SetVolumeAsync(short volume)
        {
            if(!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not in a voice channel."));
                return;
            }

            if(volume < 0)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Minimum volume is 0."));
                return;
            }

            if(volume > 150)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Maximum volume is 150."));
                return;
            }

            try
            {
                await player.UpdateVolumeAsync((ushort)volume);
                await ReplyAsync(embed: Bot.MusicEmbed($"Changed the volume to {volume}."));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("queue")]
        [Alias("q")]
        [Summary("Views the queue of the current player if one exists.")]
        public async Task ViewQueueAsync()
        {
            if(!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if(player.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("No tracks are in queue."));
                return;
            }

            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = ModuleColor.Music,
                Title = $"Queue for {player.VoiceChannel.Name}",
                ThumbnailUrl = await player.Track.FetchArtworkAsync()
            };

            builder.AddField(f =>
            {
                f.Name = $"Now Playing: {player.Track.Title}";
                f.Value = $"{Bot.FormatTimeSpan(player.Track.Position)} / {Bot.FormatTimeSpan(player.Track.Duration)} - [Link]({player.Track.Url})";
                f.IsInline = false;
            });

            int queueIndex = 1;

            var totalQueueTime = player.Track.Duration - player.Track.Position;

            const int tracksToDisplay = 9;

            foreach (var track in player.Queue)
            {
                if (queueIndex <= tracksToDisplay)
                {
                    builder.AddField(f =>
                    {
                        f.Name = $"{queueIndex}. {track.Title}";
                        f.Value = $"Duration: {Bot.FormatTimeSpan(track.Duration)} - [Link]({track.Url})";
                        f.IsInline = false;
                    });

                    queueIndex++;
                }

                totalQueueTime += track.Duration;
            }

            var extraTracks = player.Queue.Count > tracksToDisplay ? $"Plus {player.Queue.Count - tracksToDisplay} more tracks • " : null;

            builder.Footer = new EmbedFooterBuilder()
            {
                Text = $"{extraTracks}Total duration: {Bot.FormatTimeSpan(totalQueueTime)}"
            };

            await ReplyAsync(embed: builder.Build());
        }

        [Command("clear")]
        [Summary("Clears the queue of all upcoming tracks.")]
        public async Task ClearQueueAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if(player.Queue.Count == 0)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("No tracks are in queue."));
                return;
            }

            try
            {
                var count = player.Queue.Count;
                player.Queue.Clear();
                await ReplyAsync(embed: Bot.MusicEmbed($"Cleared {count} track{(count > 1 ? 's' : null)} from queue."));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }
        
        [Command("remove")]
        [Summary("Removes the tracks from the given start index to the given end index, or just the given track if no end index is provided.")]
        public async Task RemoveTracksAsync(int start, int end = -1)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.Queue.Count == 0)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("No tracks are in queue."));
                return;
            }

            if (end == -1)
                end = start;

            if (start < 1)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Indexes must be greater than 1."));
                return;
            }

            if(start > player.Queue.Count || end > player.Queue.Count)
            {
                await ReplyAsync("Indexes must be less than the total queue length.");
                return;
            }

            else if (end < start || start > end)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Start index must be greater than end index."));
                return;
            }

            try
            {
                var count = end - start + 1;

                if (count == 1)
                {
                    var track = player.Queue.ElementAt(start);
                    player.Queue.RemoveAt(start);

                    await ReplyAsync(embed: Bot.MusicEmbed($"Removed **{start}. [{track.Title}]({track.Url})** from queue."));
                }
                else
                {
                    for (int i = start; i < end; i++)
                    {
                        player.Queue.RemoveAt(i - 1);
                    }

                    await ReplyAsync(embed: Bot.MusicEmbed($"Removed {count} tracks from queue."));
                }
            }
            catch (Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("shuffle")]
        [Summary("Shuffles the current playlist.")]
        public async Task ShuffleQueueAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.Queue.Count < 2)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not enough tracks in queue to shuffle."));
                return;
            }

            try
            {
                player.Queue.Shuffle();
                var nextUp = player.Queue.ElementAt(0);
                await ReplyAsync(embed: Bot.MusicEmbed($"Shuffled the queue. Up next: [{nextUp.Title}]({nextUp.Url})"));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("song")]
        [Alias("track", "nowplaying", "np")]
        [Summary("Gives the currently playing track.")]
        public async Task NowPlayingAsync()
        {
            if(!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if(player.PlayerState != PlayerState.Playing && player.PlayerState != PlayerState.Paused)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not currently playing anything."));
                return;
            }

            await ReplyAsync(embed: await Bot.GetTrackEmbedAsync("Now Playing:", player.Track));
        }

        [Command("seek")]
        [Alias("goto")]
        [Summary("Goes to the given time stamp in the currently playing track.")]
        public async Task SeekAsync(TimeSpan position)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing && player.PlayerState != PlayerState.Paused)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not currently playing anything."));
                return;
            }

            if(position < TimeSpan.Zero)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Position should not be negative."));
                return;
            }

            if(position > player.Track.Duration)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Position is greater than the length of the track."));
                return;
            }

            try
            {
                await player.SeekAsync(position);
                await ReplyAsync(embed: Bot.MusicEmbed("Current track is now at " + Bot.FormatTimeSpan(position)));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("forward")]
        [Alias(">")]
        [Summary("Moves the track forward by the given time span, or 5 seconds if none is supplied.")]
        public async Task ForwardAsync(TimeSpan? amount = null)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not currently playing anything."));
                return;
            }

            if (!amount.HasValue)
                amount = TimeSpan.FromSeconds(5d);

            if(player.Track.Position + amount >= player.Track.Duration)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Can't move forward; not enough time left in track."));
                return;
            }

            try
            {
                await player.SeekAsync(player.Track.Position + amount);
                await ReplyAsync(embed: Bot.MusicEmbed("Forwarded."));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }

        [Command("reverse")]
        [Alias("<")]
        [Summary("Moves the track back by the given time span, or by 5 seconds if none is supplied.")]
        public async Task ReverseAsync(TimeSpan? amount = null)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out LavaPlayer player))
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Not currently playing anything."));
                return;
            }

            if (!amount.HasValue)
                amount = TimeSpan.FromSeconds(5d);

            if(player.Track.Position - amount <= TimeSpan.Zero)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed("Can't reverse; not enough time passed in track."));
                return;
            }

            try
            {
                await player.SeekAsync(player.Track.Position - amount);
                await ReplyAsync(embed: Bot.MusicEmbed("Reversed."));
            }
            catch(Exception ex)
            {
                await ReplyAsync(embed: Bot.ErrorEmbed(ex.Message));
            }
        }
    }
}
