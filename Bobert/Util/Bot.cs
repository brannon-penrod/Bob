using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Victoria;

namespace Bobert
{
    public static class Bot
    {
        public static readonly Color SuccessColor = Color.Green;
        public static readonly Color ErrorColor = Color.Red;

        public static Embed ErrorEmbed(string desc = null) =>
            new EmbedBuilder()
            {
                Color = ErrorColor,
                Description = desc
            }.Build();


        public static Embed SuccessEmbed(string desc = null) =>
            new EmbedBuilder()
            {
                Color = SuccessColor,
                Description = desc
            }.Build();

        public static async Task<Embed> GetTrackEmbedAsync(string message, LavaTrack track, IUser queuer = null)
        {
            if (track == null)
                throw new ArgumentNullException(nameof(track));

            string duration = string.Format(
                track.Duration.TotalHours >= 1 ? @"{0:h\:mm\:ss}" : @"{0:mm\:ss}",
                track.Duration);

            return new EmbedBuilder()
            {
                Color = ModuleColor.Music,
                Title = message,
                Description = $"[{track.Title}]({track.Url})",
                Footer = new EmbedFooterBuilder()
                {
                    IconUrl = queuer?.GetAvatarUrl() ?? null,
                    Text = (queuer != null ? $"Queued by {queuer.Username} • " : null) + $"Duration: {(track.Position > TimeSpan.Zero ? FormatTimeSpan(track.Position) + " / " : null)}{duration}"
                },
                ThumbnailUrl = await track.FetchArtworkAsync(),
            }.Build();
        }

        public static Embed MusicEmbed(string message)
        {
            return new EmbedBuilder
            {
                Color = ModuleColor.Music,
                Description = message
            }.Build();
        }

        public static string FormatTimeSpan(TimeSpan span)
        {
            return string.Format(
                span.TotalHours >= 1 ? @"{0:h\:mm\:ss}" : @"{0:mm\:ss}",
                span);
        }
    }
}
