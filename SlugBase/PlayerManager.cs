using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using RWCustom;
using UnityEngine;

namespace SlugBase
{
    public static class PlayerManager
    {
        internal static List<CustomPlayer> customPlayers = new List<CustomPlayer>();
        private static Dictionary<string, CustomPlayer> customPlayersByName = new Dictionary<string, CustomPlayer>();
        private static CustomPlayer currentPlayer;

        /// <summary>
        /// Returns a path to the folder containing resources for custom slugcats.
        /// </summary>
        public static string ResourceDirectory => Path.Combine(Custom.RootFolderDirectory(), "CustomSlugcats");

        /// <summary>
        /// The custom character that is being played in the current game.
        /// This will be null if there is not an ongoing game, or if the current character was not added through SlugBase.
        /// </summary>
        public static CustomPlayer CurrentPlayer
        {
            get => currentPlayer;
            private set => currentPlayer = value;
        }

        /// <summary>
        /// True if the current game session uses a player added by SlugBase.
        /// </summary>
        public static bool UsingCustomPlayer => currentPlayer != null;

        /// <summary>
        /// Registers a new player to appear in the select menu.
        /// </summary>
        /// <param name="newPlayer">The player to register.</param>
        /// <exception cref="ArgumentException">Thrown when a custom player with this name already exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a custom player is registered after the game has started.</exception>
        public static void RegisterPlayer(CustomPlayer newPlayer)
        {
            if (customPlayersByName.ContainsKey(newPlayer.Name)) throw new ArgumentException("A custom player with the same name already exists!");

            customPlayersByName[newPlayer.Name] = newPlayer;
            Debug.Log($"Registered SlugBase slugcat: \"{newPlayer.Name}\"");

            // Insert the new player into the list in alphabetical order
            int i;
            for (i = 0; i < customPlayers.Count; i++)
            {
                if (string.Compare(newPlayer.Name, customPlayers[i].Name, StringComparison.InvariantCultureIgnoreCase) >= 0) break;
            }
            customPlayers.Insert(i, newPlayer);
        }

        /// <summary>
        /// Gets all registered custom players in alphabetical order.
        /// </summary>
        /// <returns>A read-only collection containing all registered custom players.</returns>
        public static ReadOnlyCollection<CustomPlayer> GetCustomPlayers() => customPlayers.AsReadOnly();

        /// <summary>
        /// Gets a custom player by name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The custom player, or null if it was not found.</returns>
        public static CustomPlayer GetCustomPlayer(string name) => customPlayersByName.TryGetValue(name, out CustomPlayer ply) ? ply : null;

        /// <summary>
        /// Retrieves the SlugBase character with the given index.
        /// If no such character exists, null is returned.
        /// </summary>
        /// <remarks>
        /// <see cref="GetCustomPlayer(string)"/> should be used instead whenever possible.
        /// </remarks>
        /// <param name="index">The slugcat number of this character.</param>
        /// <returns>A <see cref="CustomPlayer"/> instace with the given index or null.</returns>
        public static CustomPlayer GetCustomPlayer(int index)
        {
            for (int i = 0; i < customPlayers.Count; i++) if (customPlayers[i].slugcatIndex == index) return customPlayers[i];
            return null;
        }

        internal static void ApplyHooks()
        {
            On.TempleGuardAI.Update += TempleGuardAI_Update;
            On.RainCycle.ctor += RainCycle_ctor;
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            On.HUD.FoodMeter.Update += FoodMeter_Update;
            On.Player.AddFood += Player_AddFood;
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Player.CanEatMeat += Player_CanEatMeat;
            On.SlugcatStats.SlugcatFoodMeter += SlugcatStats_SlugcatFoodMeter;
            On.SlugcatStats.ctor += SlugcatStats_ctor;
            On.PlayerGraphics.SlugcatColor += PlayerGraphics_SlugcatColor;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
        }

        #region HOOKS

        // Disallow the guardian skip on request
        private static void TempleGuardAI_Update(On.TempleGuardAI.orig_Update orig, TempleGuardAI self)
        {
            if(UsingCustomPlayer && !CurrentPlayer.CanSkipTempleGuards)
            {
                self.tracker.SeeCreature(self.guard.room.game.Players[0]);
            }
            orig(self);
        }

        // Change cycle length on request
        private static void RainCycle_ctor(On.RainCycle.orig_ctor orig, RainCycle self, World world, float minutes)
        {
            orig(self, world, CurrentPlayer?.GetCycleLength() ?? minutes);
        }

        // Unlock gates permanently on request
        private static void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            orig(self, reportBackToGate);
            if (reportBackToGate != null && UsingCustomPlayer && CurrentPlayer.GatesPermanentlyUnlock)
                reportBackToGate.Unlock();
        }

        // Add a quarter pip shower if the player uses quarter pips
        // It's technically possible to change QuarterFood while the game is running
        private static void FoodMeter_Update(On.HUD.FoodMeter.orig_Update orig, HUD.FoodMeter self)
        {
            if (self.quarterPipShower == null && UsingCustomPlayer && self.hud.owner is Player ply && ply.playerState.quarterFoodPoints > 0)
                self.quarterPipShower = new HUD.FoodMeter.QuarterPipShower(self);
            orig(self);
        }

        // Turn food into quarter pips
        private static bool giveQuarterFood;
        private static void Player_AddFood(On.Player.orig_AddFood orig, Player self, int add)
        {
            if(giveQuarterFood)
            {
                giveQuarterFood = false;
                for (int i = 0; i < add; i++)
                    self.AddQuarterFood();
                giveQuarterFood = true;
            } else
                orig(self, add);
        }

        // Mark food to be turned into quarter pips if the current player uses Hunter's diet
        private static void Player_ObjectEaten(On.Player.orig_ObjectEaten orig, Player self, IPlayerEdible edible)
        {
            try
            {
                giveQuarterFood = UsingCustomPlayer && CurrentPlayer.QuarterFood && !IsMeat(edible);
                orig(self, edible);
            } finally
            {
                giveQuarterFood = false;
            }
        }

        private static bool IsMeat(IPlayerEdible edible)
        {
            return edible is Centipede       ||
                   edible is VultureGrub     ||
                   edible is Hazer           ||
                   edible is EggBugEgg       ||
                   edible is SmallNeedleWorm ||
                   edible is JellyFish;
        }

        // Allow the player to each meat
        private static bool lock_CanEatMeat = false;
        private static bool Player_CanEatMeat(On.Player.orig_CanEatMeat orig, Player self, Creature crit)
        {
            if (lock_CanEatMeat || !UsingCustomPlayer) return orig(self, crit);

            if (crit is IPlayerEdible || !crit.dead) return false;
            lock_CanEatMeat = true;
            try
            {
                return CurrentPlayer.CanEatMeat(self, crit);
            }
            finally
            {
                lock_CanEatMeat = false;
            }
        }

        private static bool lock_SlugcatFoodMeter = false;
        private static IntVector2 SlugcatStats_SlugcatFoodMeter(On.SlugcatStats.orig_SlugcatFoodMeter orig, int slugcatNum)
        {
            if (lock_SlugcatFoodMeter) return orig(slugcatNum);

            CustomPlayer ply = GetCustomPlayer(slugcatNum);
            if(ply == null) return orig(slugcatNum);

            IntVector2 o = new IntVector2();
            lock_SlugcatFoodMeter = true;
            ply.GetFoodMeter(out o.x, out o.y);
            lock_SlugcatFoodMeter = false;
            return o;
        }

        private static bool lock_SlugcatStatsCtor = false;
        private static void SlugcatStats_ctor(On.SlugcatStats.orig_ctor orig, SlugcatStats self, int slugcatNumber, bool malnourished)
        {
            orig(self, slugcatNumber, malnourished);
            if (lock_SlugcatStatsCtor) return;

            lock_SlugcatStatsCtor = true;
            CustomPlayer ply = GetCustomPlayer(slugcatNumber);
            ply?.GetStatsInternal(self);
            lock_SlugcatStatsCtor = false;
        }

        private static Color PlayerGraphics_SlugcatColor(On.PlayerGraphics.orig_SlugcatColor orig, int i)
        {
            CustomPlayer ply = GetCustomPlayer(i);
            return ply?.SlugcatColor() ?? orig(i);
        }

        // Enable a character as the game starts
        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            if (manager.arenaSitting == null)
                StartGame(manager.rainWorld.progression.PlayingAsSlugcat);
            else
            {
                // In arena, default to playing as survivor
                // If a SlugBase character is in dev mode, use that slot instead
                int arenaChar = 0;
                foreach(CustomPlayer ply in customPlayers)
                {
                    if(ply.DevMode)
                    {
                        arenaChar = ply.slugcatIndex;
                        break;
                    }
                }
                StartGame(arenaChar);
            }
            orig(self, manager);
        }

        // Disable the active character (if one exists) as the game ends
        private static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);
            EndGame();
        }

        #endregion HOOKS

        internal static void StartGame(int slugcatNumber)
        {
            CustomPlayer ply = GetCustomPlayer(slugcatNumber);
            if(ply != null)
            {
                Debug.Log($"Started game as \"{ply.Name}\"");
                CurrentPlayer = ply;
                ply.Enable();
            }
        }

        internal static void EndGame()
        {
            if (CurrentPlayer != null)
                Debug.Log($"Ended game as \"{CurrentPlayer.Name}\"");
            CurrentPlayer?.Disable();
        }
    }
}
