using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Partiality;
using Partiality.Modloader;

namespace SlugBase.Compatibility
{
    internal static class FancySlugcats
    {
        public static void Apply()
        {
            try
            {
                Type fsMain = Type.GetType("FancySlugcats.Main, FancySlugcats");
                if (fsMain == null) return;

                Debug.Log("Applying SlugBase compatibility changes for FancySlugcats...");

                // FancySlugcats.Main.ShortcutColor (from PlayerGraphics.SlugcatColor) throws with indices above 4
                On.PlayerGraphics.SlugcatColor += FSPatch_SlugcatColor;

                // FancySlugcats.FancyPlayerGraphics.ctor throws with slugcat indices above 4
                On.Player.InitiateGraphicsModule += FSPatch_InitiateGraphicsModule;
            }
            catch(Exception e)
            {
                Debug.Log("Failed to apply compatibility changes. This shouldn't be fatal, but may cause compatibility issues.");
                Debug.Log(e);
            }
        }

        private static void FSPatch_InitiateGraphicsModule(On.Player.orig_InitiateGraphicsModule orig, Player self)
        {
            try
            {
                // First, try running it as normal
                orig(self);
            }
            catch
            {
                // If that fails, try running it with a modified character index
                int oldChar = self.playerState.slugcatCharacter;
                try
                {
                    SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(self.playerState.slugcatCharacter);
                    if (ply != null)
                    {
                        // For SlugBase characters, use the character that it copies from
                        self.playerState.slugcatCharacter = ply.InheritWorldFromSlugcat;
                    }
                    else
                    {
                        // For other characters, use the player number
                        self.playerState.slugcatCharacter = self.playerState.playerNumber;
                    }
                    orig(self);
                }
                finally
                {
                    self.playerState.slugcatCharacter = oldChar;
                }
            }
        }

        private static Color FSPatch_SlugcatColor(On.PlayerGraphics.orig_SlugcatColor orig, int i)
        {
            try { return orig(i); }
            catch
            {
                SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(i);
                if(ply != null)
                {
                    try
                    {
                        // Bypass SlugBase colors when using FancySlugcats
                        PlayerManager.useOriginalColor = true;
                        return orig(ply.InheritWorldFromSlugcat);
                    }
                    catch { }
                    PlayerManager.useOriginalColor = false;
                }
                return Color.white;
            }
        }
    }
}
