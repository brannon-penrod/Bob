using Discord;

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
    }
}
