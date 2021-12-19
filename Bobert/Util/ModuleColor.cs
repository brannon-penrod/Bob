using Discord;
using System;

namespace Bobert
{
    public struct ModuleColor
    {
        public static readonly Color Help = Color.Teal;
        public static readonly Color Music = Color.Orange;

        public static Color GetColorFromModuleName(string moduleName)
        {
            if (string.Equals(moduleName, "Help", StringComparison.InvariantCultureIgnoreCase)) 
                return Help;

            if (string.Equals(moduleName, "Music", StringComparison.InvariantCultureIgnoreCase)) 
                return Music;

            return Color.Default;
        }
    }
}
