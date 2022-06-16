using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomRegions.Mod;
using System.Runtime.CompilerServices;

namespace SlugBase
{
    internal static class CRSIntegration
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void AddCRSFilter()
        {
            API.AddRegionPreprocessor(CharacterFilterPreprocessor);
        }

        private static void CharacterFilterPreprocessor(API.RegionInfo region)
        {
            var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();

            SlugBaseCharacter ply;
            if (rw.processManager?.currentMainLoop is RainWorldGame rwg)
                ply = PlayerManager.GetCustomPlayer(rwg);
            else
                ply = PlayerManager.GetCustomPlayer(rw.progression.PlayingAsSlugcat);

            var lines = region.Lines;

            bool readingCreatures = false;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (readingCreatures)
                {
                    // Ignore creature lines
                    if (line == "CREATURES") readingCreatures = false;
                }
                else
                {
                    // Filter non-creature lines at the CRS level
                    if (line == "END CREATURES") readingCreatures = true;

                    if (RegionTools.ShouldKeepLine(ply?.Name, line, out string newLine))
                        lines[i] = newLine;
                    else
                        lines.RemoveAt(i);
                }
            }

        }
    }
}
