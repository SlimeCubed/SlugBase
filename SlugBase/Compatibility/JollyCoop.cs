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
            if (jolly.Version == "1.6.6")
            {
                Debug.Log("Detected Jolly version 1.6.6! Applying hotfixes...");
                new Hook(
                    typeof(JollyOption).GetMethod(nameof(JollyOption.ConfigOnChange), BindingFlags.Public | BindingFlags.Instance),
                    new Action<Action<JollyOption>, JollyOption>(JollyOption_ConfigOnChange)
                );
                new Hook(
                    typeof(Player).GetProperty(nameof(Player.slugcatStats), BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                    new Func<PlayerHK.orig_slugcatStats, Player, SlugcatStats>(get_SlugcatStatsHK)
                );
                On.Player.ctor += Player_ctor;
            }
        }

        // Fix indices that aren't correctly decremented
        private static void JollyOption_ConfigOnChange(Action<JollyOption> orig, JollyOption self)
        {
            orig(self);
            for (int i = 0; i < JollyMod.config.playerCharacters.Length; i++)
            {
                if (JollyMod.config.playerCharacters[i] == 3)
                    JollyMod.config.playerCharacters[i]--;
            }
        }

        // Clear player stats on startup
        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            PlayerHK.stats[((PlayerState)abstractCreature.state).playerNumber] = null;
            orig(self, abstractCreature, world);
        }

        // Copied from PlayerHK.get_SlugcatStatsHK
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
