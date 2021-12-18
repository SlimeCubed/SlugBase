using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using JollyCoop;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace SlugBase.Compatibility
{
    internal static class JollyCoop
    {
        private static int[] savedPlayerCharacters;

        public static void Apply()
        {
            try
            {
                ApplyInternal();
            }
            catch
            {
                // This will throw if Jolly is not installed
            }
        }

        private static void ApplyInternal()
        {
            var jolly = (JollyMod)Partiality.PartialityManager.Instance.modManager.loadedMods.First(mod => mod is JollyMod);
            if (jolly.Version == "1.6.6" || jolly.Version == "1.6.7")
            {
                Debug.Log("Detected Jolly version 1.6.6 or 1.6.7! Applying hotfixes...");
                if (jolly.Version == "1.6.6")
                {
                    new Hook(
                        typeof(JollyOption).GetMethod(nameof(JollyOption.ConfigOnChange), BindingFlags.Public | BindingFlags.Instance),
                        new Action<Action<JollyOption>, JollyOption>(JollyOption_ConfigOnChange)
                    );
                }
                new Hook(
                    typeof(Player).GetProperty(nameof(Player.slugcatStats), BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                    new Func<PlayerHK.orig_slugcatStats, Player, SlugcatStats>(get_SlugcatStatsHK)
                );
                On.Player.ctor += Player_ctor;
                On.RainWorldGame.ctor += RainWorldGame_ctor;
                On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
            }
        }

        // Player 2 never properly inherits stats from player 0 since it isn't spawned by Jolly
        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            var chars = JollyMod.config.playerCharacters;
            savedPlayerCharacters = (int[])chars.Clone();
            for (int i = 1; i < chars.Length; i++)
            {
                if (chars[i] == -1)
                    chars[i] = chars[0];
            }
            orig(self, manager);
        }

        // Ensure that modifications to the player character array (e.g., changing -1s to player 1's character) are reverted when leaving the game
        private static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);

            if (savedPlayerCharacters != null)
            {
                JollyMod.config.playerCharacters = savedPlayerCharacters;
                savedPlayerCharacters = null;
            }
        }


        // A player character of 2 is impossible under normal circumstances, so make it possible
        // Jolly has some checks for character == 3 that re-implement Hunter's abilities. Is this a bug or a feature?
        // Obsolete with version 1.6.7
        private static void JollyOption_ConfigOnChange(Action<JollyOption> orig, JollyOption self)
        {
            orig(self);
            for (int i = 0; i < JollyMod.config.playerCharacters.Length; i++)
            {
                if (JollyMod.config.playerCharacters[i] == 3)
                    JollyMod.config.playerCharacters[i]--;
            }
        }

        // Player stats are never cleared and used cached values when possible, leading to stats carrying over between games
        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            var playerState = (PlayerState)abstractCreature.state;
            PlayerHK.stats[playerState.playerNumber] = null;
            orig(self, abstractCreature, world);
        }

        // Copied from PlayerHK.get_SlugcatStatsHK
        // The original hook is removed immediately since it is in a using statement
        // foodToHibernate is only written to when it won't be read from and only read from when it hasn't been written to
        // Player character 3 (Nightcat, or Hunter without the index fix) always inherits from the save's stats
        public static SlugcatStats get_SlugcatStatsHK(PlayerHK.orig_slugcatStats orig_slugcatStats, Player self)
        {
            SlugcatStats result;
            try
            {
                var globalStats = self.abstractPhysicalObject.world.game.session.characterStats;

                int playerNumber = self.playerState.playerNumber;
                int playerChar = JollyMod.config.playerCharacters[playerNumber];
                if (playerNumber == 0)
                {
                    result = orig_slugcatStats(self);
                }
                else
                {
                    if (PlayerHK.stats[playerNumber] == null)
                    {
                        Debug.Log($"Made new stats: {playerChar}");
                        PlayerHK.stats[playerNumber] = new SlugcatStats(playerChar, self.Malnourished);
                        PlayerHK.stats[playerNumber].foodToHibernate = globalStats.foodToHibernate;
                        PlayerHK.stats[playerNumber].maxFood = globalStats.maxFood;
                    }
                    result = PlayerHK.stats[playerNumber];
                }
            }
            catch (Exception e)
            {
                Debug.Log("slugcatStats hook failed!");
                Debug.Log(e);
                result = orig_slugcatStats(self);
            }
            return result;
        }
    }
}
