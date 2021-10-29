using System;
using UnityEngine;

namespace SlugBase
{
    // Allow modded regions to change their behavior by character name
    internal static class RegionTools
    {
        private static readonly string[] spawnLineSplit = new string[] { " : " };

        public static void ApplyHooks()
        {
            On.WorldLoader.FindingCreatures += WorldLoader_FindingCreatures;
        }

        // Check and filter a single line of a world file
        private static bool ShouldKeepLine(string line, out string newLine)
        {
            newLine = line;

            string charName = PlayerManager.CurrentCharacter?.Name;
            var args = line.Split(spawnLineSplit, StringSplitOptions.RemoveEmptyEntries);

            if (args.Length == 0)
                return true;

            // Pre-tags
            if (args[0][0] == '[')
            {
                int close = args[0].IndexOf(']');
                if(close > 0)
                {
                    var filterRes = FilterKeepsChar(args[0].Substring(0, close + 1), charName);
                    if (filterRes.HasValue)
                    {
                        newLine = line.Substring(close + 1);
                        return filterRes.Value;
                    }
                }
            }

            // Post-tags
            for(int i = 1; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg[0] == '[' && arg[arg.Length - 1] == ']')
                {
                    var filterRes = FilterKeepsChar(args[i], charName);
                    if (filterRes.HasValue)
                    {
                        newLine = line.Remove(line.IndexOf(" : " + arg), 3 + arg.Length);
                        return filterRes.Value;
                    }
                }
            }

            return true;
        }

        // Parse a character filter
        private static bool? FilterKeepsChar(string filter, string charName)
        {
            // Remove brackets
            if (filter.Length >= 2 && filter[0] == '[' && filter[filter.Length - 1] == ']')
                filter = filter.Substring(1, filter.Length - 2);

            var names = filter.Split(',');

            bool anyInverse = false;
            bool anyNormal = false;
            bool? keepSelf = null;
            for(int i = 0; i < names.Length; i++)
            {
                var name = names[i].Trim();
                bool inv = name.Length > 0 && name[0] == '!';
                if (inv) name = name.Substring(1);

                // Exit early if an invalid name is encountered
                // This may be confusing if the region maker makes a typo, but it minimizes the risk of incompatibility with other mods that change the region file format
                if (!PlayerManager.IsValidCharacterName(name))
                    return null;

                if (inv) anyInverse = true;
                else anyNormal = true;

                if (name == charName) keepSelf = !inv;
            }
            
            if (anyInverse && anyNormal)
            {
                Debug.Log($"WARNING! Ambiguous arguments found in SlugBase spawn filter: {filter}. Please either invert all names or none.");
            }
            else if(!anyInverse && !anyNormal)
            {
                // Empty brackets are not considered a filter
                return null;
            }

            return keepSelf ?? !anyNormal;
        }

        // Remove creature lines that before they are used
        private static void WorldLoader_FindingCreatures(On.WorldLoader.orig_FindingCreatures orig, WorldLoader self)
        {
            var line = self.lines[self.cntr];
            if (ShouldKeepLine(line, out var newLine))
            {
                self.lines[self.cntr] = newLine;
                orig(self);
            }
        }
    }
}
