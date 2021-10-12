using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using RWCustom;
using UnityEngine;

namespace SlugBase
{
    /// <summary>
    /// Contains utilities related to SlugBase characters.
    /// </summary>
    public static class PlayerManager
    {
        internal static List<SlugBaseCharacter> customPlayers = new List<SlugBaseCharacter>();
        internal static bool useOriginalColor;
        private static Dictionary<string, SlugBaseCharacter> customPlayersByName = new Dictionary<string, SlugBaseCharacter>();
        private static SlugBaseCharacter currentPlayer;

        /// <summary>
        /// Returns a path to the folder containing resources for SlugBase characters.
        /// </summary>
        public static string ResourceDirectory => Path.Combine(Custom.RootFolderDirectory(), Path.Combine("Mods", "SlugBase"));

        /// <summary>
        /// The custom character that is being played in the current game.
        /// This will be null if there is not an ongoing game, or if the current character was not added through SlugBase.
        /// </summary>
        public static SlugBaseCharacter CurrentCharacter
        {
            get => currentPlayer;
            internal set => currentPlayer = value;
        }

        /// <summary>
        /// True if the current game session uses a player added by SlugBase.
        /// </summary>
        public static bool UsingCustomCharacter => currentPlayer != null;

        /// <summary>
        /// Registers a new character to appear in the select menu.
        /// </summary>
        /// <param name="newCharacter">The character to register.</param>
        /// <exception cref="ArgumentException">Thrown when a SlugBase character with this name already exists.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a SlugBase character is registered after the game has started.</exception>
        public static void RegisterCharacter(SlugBaseCharacter newCharacter)
        {
            if (customPlayersByName.ContainsKey(newCharacter.Name)) throw new ArgumentException("A character with the same name already exists!");

            customPlayersByName[newCharacter.Name] = newCharacter;
            Debug.Log($"Registered SlugBase character: \"{newCharacter.Name}\"");

            // Insert the new player into the list in alphabetical order
            int i;
            for (i = 0; i < customPlayers.Count; i++)
            {
                if (string.Compare(newCharacter.Name, customPlayers[i].Name, StringComparison.InvariantCultureIgnoreCase) >= 0) break;
            }
            customPlayers.Insert(i, newCharacter);
        }

        /// <summary>
        /// Gets all registered SlugBase characters in alphabetical order.
        /// </summary>
        /// <returns>A read-only collection containing all registered SlugBase characters.</returns>
        public static ReadOnlyCollection<SlugBaseCharacter> GetCustomPlayers() => customPlayers.AsReadOnly();

        /// <summary>
        /// Gets a SlugBase character by name.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The character, or null if it was not found.</returns>
        public static SlugBaseCharacter GetCustomPlayer(string name) => customPlayersByName.TryGetValue(name, out SlugBaseCharacter ply) ? ply : null;

        /// <summary>
        /// Retrieves the SlugBase character with the given index.
        /// If no such character exists, null is returned.
        /// </summary>
        /// <remarks>
        /// <see cref="GetCustomPlayer(string)"/> should be used instead whenever possible.
        /// </remarks>
        /// <param name="index">The slugcat number of this character.</param>
        /// <returns>A <see cref="SlugBaseCharacter"/> instace with the given index or null.</returns>
        public static SlugBaseCharacter GetCustomPlayer(int index)
        {
            return customPlayers.FirstOrDefault(s => s.SlugcatIndex == index);
        }

        internal static void ApplyHooks()
        {
            On.Room.Loaded += Room_Loaded;
            On.ProcessManager.RequestMainProcessSwitch_1 += ProcessManager_RequestMainProcessSwitch_1;
            On.WorldLoader.GeneratePopulation += WorldLoader_GeneratePopulation;
            On.SaveState.ctor += SaveState_ctor;
            On.TempleGuardAI.Update += TempleGuardAI_Update;
            On.RainCycle.ctor += RainCycle_ctor;
            On.OverWorld.GateRequestsSwitchInitiation += OverWorld_GateRequestsSwitchInitiation;
            On.HUD.FoodMeter.Update += FoodMeter_Update;
            On.Player.AddFood += Player_AddFood;
            On.Player.ObjectEaten += Player_ObjectEaten;
            On.Player.CanEatMeat += Player_CanEatMeat;
            On.SlugcatStats.SlugcatFoodMeter += SlugcatStats_SlugcatFoodMeter;
            On.SlugcatStats.ctor += SlugcatStats_ctor;
            On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;
            On.PlayerGraphics.SlugcatColor += PlayerGraphics_SlugcatColor;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
        }

        #region HOOKS

        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            if (UsingCustomCharacter && self.abstractRoom?.name is string roomName && self.abstractRoom.firstTimeRealized)
            {
                orig(self);

                if (self.game?.GetStorySession?.saveState is SaveState save && save.cycleNumber == 0 && save.denPosition == roomName) {
                    CurrentCharacter.StartNewGame(self);
                }
            }
            else orig(self);
        }

        // Make sure Prepare is called consistently
        private static void ProcessManager_RequestMainProcessSwitch_1(On.ProcessManager.orig_RequestMainProcessSwitch_1 orig, ProcessManager self, ProcessManager.ProcessID ID, float fadeOutSeconds)
        {
            if (ID == ProcessManager.ProcessID.Game)
            {
                if (self.arenaSitting != null)
                {
                    ArenaAdditions.PlayerDescriptor ply = ArenaAdditions.GetSelectedArenaCharacter(self.arenaSetup);
                    if (ply.type == ArenaAdditions.PlayerDescriptor.Type.SlugBase)
                        ply.player.PrepareInternal();
                }
                else
                {
                    GetCustomPlayer(self.rainWorld.progression.PlayingAsSlugcat)?.PrepareInternal();
                }
            }
            orig(self, ID, fadeOutSeconds);
        }

        // Stop Iggy from spawning in on request
        private static void WorldLoader_GeneratePopulation(On.WorldLoader.orig_GeneratePopulation orig, WorldLoader self, bool fresh)
        {
            if (!UsingCustomCharacter || CurrentCharacter.HasGuideOverseer)
            {
                orig(self, fresh);
                return;
            }
            float pgosc = self.world.region.regionParams.playerGuideOverseerSpawnChance;
            self.world.region.regionParams.playerGuideOverseerSpawnChance = 0f;
            orig(self, fresh);
            self.world.region.regionParams.playerGuideOverseerSpawnChance = pgosc;
        }

        // Remove dreams on request
        private static void SaveState_ctor(On.SaveState.orig_ctor orig, SaveState self, int saveStateNumber, PlayerProgression progression)
        {
            orig(self, saveStateNumber, progression);
            if (!(CurrentCharacter?.HasDreams ?? true))
                self.dreamsState = null;
        }

        // Disallow the guardian skip on request
        private static void TempleGuardAI_Update(On.TempleGuardAI.orig_Update orig, TempleGuardAI self)
        {
            if(UsingCustomCharacter && !CurrentCharacter.CanSkipTempleGuards)
            {
                self.tracker.SeeCreature(self.guard.room.game.Players[0]);
            }
            orig(self);
        }

        // Change cycle length on request
        private static void RainCycle_ctor(On.RainCycle.orig_ctor orig, RainCycle self, World world, float minutes)
        {
            orig(self, world, CurrentCharacter?.GetCycleLength() ?? minutes);
        }

        // Unlock gates permanently on request
        private static void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            orig(self, reportBackToGate);
            if (reportBackToGate != null && UsingCustomCharacter && CurrentCharacter.GatesPermanentlyUnlock)
                reportBackToGate.Unlock();
        }

        // Add a quarter pip shower if the player uses quarter pips
        // It's technically possible to change QuarterFood while the game is running
        private static void FoodMeter_Update(On.HUD.FoodMeter.orig_Update orig, HUD.FoodMeter self)
        {
            if (self.quarterPipShower == null && UsingCustomCharacter && self.hud.owner is Player ply && ply.playerState.quarterFoodPoints > 0)
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
                giveQuarterFood = UsingCustomCharacter && CurrentCharacter.QuarterFood && !IsMeat(edible);
                orig(self, edible);
            }
            finally
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
            if (lock_CanEatMeat || !UsingCustomCharacter) return orig(self, crit);

            if ((crit is IPlayerEdible edible && edible.Edible) || !crit.dead) return false;
            lock_CanEatMeat = true;
            try
            {
                return CurrentCharacter.CanEatMeat(self, crit);
            }
            finally
            {
                lock_CanEatMeat = false;
            }
        }

        // Change food requirements on request
        private static bool lock_SlugcatFoodMeter = false;
        private static IntVector2 SlugcatStats_SlugcatFoodMeter(On.SlugcatStats.orig_SlugcatFoodMeter orig, int slugcatNum)
        {
            if (lock_SlugcatFoodMeter) return orig(slugcatNum);

            SlugBaseCharacter ply = GetCustomPlayer(slugcatNum);
            if(ply == null) return orig(slugcatNum);

            IntVector2 o = new IntVector2();
            lock_SlugcatFoodMeter = true;
            ply.GetFoodMeter(out o.x, out o.y);
            lock_SlugcatFoodMeter = false;
            return o;
        }

        // Change stats on request
        private static bool lock_SlugcatStatsCtor = false;
        private static void SlugcatStats_ctor(On.SlugcatStats.orig_ctor orig, SlugcatStats self, int slugcatNumber, bool malnourished)
        {
            orig(self, slugcatNumber, malnourished);
            if (lock_SlugcatStatsCtor) return;

            lock_SlugcatStatsCtor = true;
            SlugBaseCharacter ply = GetCustomPlayer(slugcatNumber);
            ply?.GetStatsInternal(self);
            lock_SlugcatStatsCtor = false;
        }

        // Change eye color on request
        private static void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);

            Color? eyeColor = GetCustomPlayer(self.player.playerState.slugcatCharacter)?.SlugcatEyeColor();
            if (eyeColor.HasValue && sLeaser.sprites.Length > 9)
            {
                sLeaser.sprites[9].color = eyeColor.Value;
            }
        }

        // Change player color on request
        private static Color PlayerGraphics_SlugcatColor(On.PlayerGraphics.orig_SlugcatColor orig, int i)
        {
            if (useOriginalColor) return orig(i);
            return GetCustomPlayer(i)?.SlugcatColor() ?? orig(i);
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
                if (ArenaAdditions.arenaCharacter.TryGet(manager.arenaSetup, out var ply))
                {
                    switch (ply.type)
                    {
                        case ArenaAdditions.PlayerDescriptor.Type.SlugBase:
                            StartGame(ply.player);
                            break;
                        case ArenaAdditions.PlayerDescriptor.Type.Vanilla:
                        default:
                            StartGame(0);
                            break;
                    }
                }
                else StartGame(0);
            }
            orig(self, manager);
        }

        // Disable the active character (if one exists) as the game ends
        private static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);

            // Do not end the game if the upcoming process is also the game, such as with RainWorldGame.RestartGame
            // This is to prevent the character from being disabled while it is already prepared
            if (self.manager.upcomingProcess != ProcessManager.ProcessID.Game)
                EndGame();
        }

        #endregion HOOKS

        internal static void StartGame(int slugcatNumber)
        {
            SlugBaseCharacter ply = GetCustomPlayer(slugcatNumber);
            if(ply != null)
            {
                Debug.Log($"Started game as \"{ply.Name}\"");
                ply.EnableInternal();
            }
        }

        internal static void StartGame(SlugBaseCharacter customPlayer)
        {
            Debug.Log($"Started game as \"{customPlayer.Name}\"");
            customPlayer.EnableInternal();
        }

        internal static void EndGame()
        {
            if (CurrentCharacter != null)
                Debug.Log($"Ended game as \"{CurrentCharacter.Name}\"");
            CurrentCharacter?.DisableInternal();
        }
    }
}
