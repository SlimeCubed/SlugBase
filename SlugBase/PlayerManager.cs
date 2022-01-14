using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Menu;
using RWCustom;
using UnityEngine;

namespace SlugBase
{
    /// <summary>
    /// Contains utilities related to SlugBase characters.
    /// </summary>
    public static class PlayerManager
    {
        internal static readonly List<SlugBaseCharacter> customPlayers = new List<SlugBaseCharacter>();
        private static readonly Dictionary<string, SlugBaseCharacter> customPlayersByName = new Dictionary<string, SlugBaseCharacter>();
        private static readonly Dictionary<RainWorldGame, GameSetup> gameSetups = new Dictionary<RainWorldGame, GameSetup>();
        internal static bool useOriginalColor;
        private static SlugBaseCharacter currentPlayer;

        /// <summary>
        /// Returns a path to the folder containing resources for SlugBase characters.
        /// </summary>
        public static string ResourceDirectory => Path.Combine(Custom.RootFolderDirectory(), Path.Combine("Mods", "SlugBase"));

        /// <summary>
        /// The custom character that is being played in the current game.
        /// This will be null if there is not an ongoing game, or if the current character was not added through SlugBase.
        /// </summary>
        [Obsolete("Use " + nameof(GetCustomPlayer) + " instead.")]
        public static SlugBaseCharacter CurrentCharacter
        {
            get => currentPlayer;
            internal set => currentPlayer = value;
        }

        /// <summary>
        /// True if the current game session uses a player added by SlugBase.
        /// </summary>
        [Obsolete("Check " + nameof(GetCustomPlayer) + " instead.")]
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

        /// <summary>
        /// Gets the <see cref="SlugBaseCharacter"/> that this game's world is using, affecting things such as spawns and placed object filters.
        /// </summary>
        /// <param name="game">The game to check.</param>
        /// <returns>The <see cref="SlugBaseCharacter"/> used for the world or <c>null</c> if a custom character is not being used.</returns>
        public static SlugBaseCharacter GetCustomPlayer(RainWorldGame game)
        {
            if (game == null)
                return null;

            gameSetups.TryGetValue(game, out GameSetup setup);
            return setup?.worldCharacter;
        }

        /// <summary>
        /// Gets the <see cref="SlugBaseCharacter"/> that the given <see cref="Player"/> is an instance of.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <returns>The <see cref="SlugBaseCharacter"/> used for this player or <c>null</c> if a custom character is not being used.</returns>
        public static SlugBaseCharacter GetCustomPlayer(Player player)
        {
            if (player == null)
                return null;

            var game = player.abstractCreature.world.game;

            var worldCha = GetCustomPlayer(game);
            var playerCha = GetCustomPlayer((int)player.slugcatStats.name);

            if(worldCha != null && !worldCha.MultiInstance)
            {
                return worldCha;
            }
            else
            {
                if (playerCha != null && !playerCha.MultiInstance && gameWithError?.Target != game)
                {
                    gameWithError = new WeakReference(game);
                    Debug.LogException(new Exception($"Single-instance character \"{playerCha}\" is in a mismatched world, \"{worldCha?.Name ?? "VANILLA"}\"!"));
                }

                return playerCha;
            }
        }
        private static WeakReference gameWithError;

        /// <summary>
        /// Checks if the given string can be used as <see cref="SlugBaseCharacter.Name"/>.
        /// </summary>
        /// <param name="name">The string to check.</param>
        /// <returns>True if the string can be a name, false otherwise.</returns>
        public static bool IsValidCharacterName(string name)
        {
            return !string.IsNullOrEmpty(name) && Regex.IsMatch(name, "^[\\w ]+$");
        }

        /// <summary>
        /// Gets the color associated with the given player with the given slugcat character.
        /// </summary>
        /// <remarks>
        /// This functions exactly like <see cref="PlayerGraphics.SlugcatColor(int)"/>, but will
        /// not misbehave when using multi-instance <see cref="SlugBaseCharacter"/>s.
        /// </remarks>
        /// <param name="player">The player to get the color of.</param>
        /// <param name="slugcatCharacter">
        /// The character to check. This is normally -1 or the <paramref name="player"/>'s
        /// <see cref="SlugBaseCharacter.SlugcatIndex"/>, but may differ in arena more or when using other mods.
        /// </param>
        public static Color GetSlugcatColor(Player player, int slugcatCharacter)
        {
            return PlayerColors.DrawingPlayer(player, () => PlayerGraphics.SlugcatColor(slugcatCharacter));
        }

        /// <summary>
        /// Gets the color associated with the given player.
        /// </summary>
        /// <remarks>
        /// This functions exactly like <see cref="PlayerGraphics.SlugcatColor(int)"/>, but will
        /// not misbehave when using multi-instance <see cref="SlugBaseCharacter"/>s.
        /// </remarks>
        /// <param name="player">The player to get the color of.</param>
        public static Color GetSlugcatColor(Player player)
        {
            return PlayerColors.DrawingPlayer(player, () => PlayerGraphics.SlugcatColor(player.playerState.slugcatCharacter));
        }

        internal static void ApplyHooks()
        {
            On.AbstractCreature.Realize += AbstractCreature_Realize;
            On.Room.Loaded += Room_Loaded;
            On.ProcessManager.RequestMainProcessSwitch_1 += ProcessManager_RequestMainProcessSwitch_1;
            On.ProcessManager.SwitchMainProcess += ProcessManager_SwitchMainProcess;
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

        // Hide passage tokens
        private static void SleepAndDeathScreen_GetDataFromGame(On.Menu.SleepAndDeathScreen.orig_GetDataFromGame orig, SleepAndDeathScreen self, KarmaLadderScreen.SleepDeathScreenDataPackage package)
        {
            orig(self, package);

            if (!GetCustomPlayer(self.manager.rainWorld.progression.PlayingAsSlugcat)?.CanUsePassages(self.saveState) ?? false)
            {
                self.endgameTokens.pos.x -= 10000;
            }
        }

        // Disallow usage of passages
        private static void SleepAndDeathScreen_AddPassageButton(On.Menu.SleepAndDeathScreen.orig_AddPassageButton orig, SleepAndDeathScreen self, bool buttonBlack)
        {
            if (GetCustomPlayer(self.manager.rainWorld.progression.PlayingAsSlugcat)?.CanUsePassages(self.saveState) ?? true)
            {
                orig(self, buttonBlack);
            }
        }

        // Enable SlugBaseCharacters when their players are realized
        private static void AbstractCreature_Realize(On.AbstractCreature.orig_Realize orig, AbstractCreature self)
        {
            orig(self);

            if (self.realizedObject is Player ply)
            {
                Debug.Log($"Player realized! Num {ply.playerState.playerNumber}, name {ply.slugcatStats.name}, char {ply.playerState.slugcatCharacter}");
                UpdateCustomPlayer(self.world.game, self, GetCustomPlayer(ply));
            }
        }

        // Call StartNewGame once the first room loads
        private static void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            var cha = GetCustomPlayer(self.game);
            if (cha != null && self.abstractRoom?.name is string roomName && self.abstractRoom.firstTimeRealized)
            {
                orig(self);

                if (self.game?.GetStorySession?.saveState is SaveState save && save.cycleNumber == 0 && save.denPosition == roomName) {
                    cha.StartNewGame(self);
                }
            }
            else orig(self);
        }

        // Call Prepare as early as possible
        private static void ProcessManager_RequestMainProcessSwitch_1(On.ProcessManager.orig_RequestMainProcessSwitch_1 orig, ProcessManager self, ProcessManager.ProcessID ID, float fadeOutSeconds)
        {
            // Don't disable the world character if a game is in progress
            if (ID == ProcessManager.ProcessID.Game && self.currentMainLoop?.ID != ProcessManager.ProcessID.Game)
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

        // Make sure Prepare is consistently called before RainWorldGame's constructor
        private static void ProcessManager_SwitchMainProcess(On.ProcessManager.orig_SwitchMainProcess orig, ProcessManager self, ProcessManager.ProcessID ID)
        {
            if(ID == ProcessManager.ProcessID.Game)
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
            orig(self, ID);
        }

        // Stop Iggy from spawning in on request
        private static void WorldLoader_GeneratePopulation(On.WorldLoader.orig_GeneratePopulation orig, WorldLoader self, bool fresh)
        {
            var cha = GetCustomPlayer(self.game);
            if (cha == null || cha.HasGuideOverseer)
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

            var cha = GetCustomPlayer(saveStateNumber);
            if (cha != null && !cha.HasDreams)
                self.dreamsState = null;
        }

        // Disallow the guardian skip on request
        private static void TempleGuardAI_Update(On.TempleGuardAI.orig_Update orig, TempleGuardAI self)
        {
            var cha = GetCustomPlayer(self.creature.world.game);
            if(cha != null && !cha.CanSkipTempleGuards)
            {
                self.tracker.SeeCreature(self.guard.room.game.Players[0]);
            }
            orig(self);
        }

        // Change cycle length on request
        private static void RainCycle_ctor(On.RainCycle.orig_ctor orig, RainCycle self, World world, float minutes)
        {
            orig(self, world, GetCustomPlayer(world.game)?.GetCycleLength() ?? minutes);
        }

        // Unlock gates permanently on request
        private static void OverWorld_GateRequestsSwitchInitiation(On.OverWorld.orig_GateRequestsSwitchInitiation orig, OverWorld self, RegionGate reportBackToGate)
        {
            orig(self, reportBackToGate);

            var cha = GetCustomPlayer(self.game);
            if (reportBackToGate != null && cha != null && cha.GatesPermanentlyUnlock)
                reportBackToGate.Unlock();
        }

        // Add a quarter pip shower if the player uses quarter pips
        // It's technically possible to change QuarterFood while the game is running
        private static void FoodMeter_Update(On.HUD.FoodMeter.orig_Update orig, HUD.FoodMeter self)
        {
            if (self.quarterPipShower == null && self.hud.owner is Player ply && GetCustomPlayer(ply) != null && ply.playerState.quarterFoodPoints > 0)
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
                giveQuarterFood = (GetCustomPlayer(self)?.QuarterFood ?? false) && !IsMeat(edible);
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
            var cha = GetCustomPlayer(self);
            if (lock_CanEatMeat || cha == null) return orig(self, crit);

            if ((crit is IPlayerEdible edible && edible.Edible) || !crit.dead) return false;
            lock_CanEatMeat = true;
            try
            {
                return cha.CanEatMeat(self, crit);
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
            GetCustomPlayer(slugcatNumber)?.GetStatsInternal(self);
            lock_SlugcatStatsCtor = false;
        }

        // Change eye color on request
        // Override Nightcat's colors when in arena mode
        private static void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);

            var cha = GetCustomPlayer(self.player);
            Color? bodyColor = cha?.SlugcatColorInternal(self.player.playerState.slugcatCharacter);
            Color? eyeColor = cha?.SlugcatEyeColorInternal(self.player.playerState.slugcatCharacter);

            if(sLeaser.sprites.Length >= 12)
            {
                // Fix Nightcat's colors
                if (bodyColor != null && self.player.playerState.slugcatCharacter == 3)
                {
                    if (self.malnourished > 0f)
                        bodyColor = Color.Lerp(bodyColor.Value, Color.gray, 0.4f * self.malnourished);

                    if (eyeColor == null)
                        eyeColor = palette.blackColor;

                    for (int i = 0; i < 9; i++)
                    {
                        sLeaser.sprites[i].color = bodyColor.Value;
                    }

                    sLeaser.sprites[11].color = Color.Lerp(PlayerGraphics.SlugcatColor(self.player.playerState.slugcatCharacter), Color.white, 0.3f);
                    sLeaser.sprites[10].color = PlayerGraphics.SlugcatColor(self.player.playerState.slugcatCharacter);
                }

                // Set eye color
                if (eyeColor.HasValue && sLeaser.sprites.Length > 9)
                {
                    sLeaser.sprites[9].color = eyeColor.Value;
                }
            }
        }

        // Change player color on request
        private static Color PlayerGraphics_SlugcatColor(On.PlayerGraphics.orig_SlugcatColor orig, int i)
        {
            if (useOriginalColor) return orig(i);

            bool lastOrigColor = useOriginalColor;
            try
            {
                useOriginalColor = true;
                if (PlayerColors.drawingCharacter != null)
                    return PlayerColors.drawingCharacter.SlugcatColorInternal(i) ?? orig(i);
                else
                    return GetCustomPlayer(i)?.SlugcatColorInternal(i) ?? orig(i);
            }
            finally
            {
                useOriginalColor = lastOrigColor;
            }
        }

        // Enable a character as the game starts
        private static void RainWorldGame_ctor(On.RainWorldGame.orig_ctor orig, RainWorldGame self, ProcessManager manager)
        {
            if (manager.arenaSitting == null)
                StartGame(self, manager.rainWorld.progression.PlayingAsSlugcat);
            else
            {
                // In arena, default to playing as survivor
                var ply = ArenaAdditions.GetSelectedArenaCharacter(manager.arenaSetup);
                switch (ply.type)
                {
                    case ArenaAdditions.PlayerDescriptor.Type.SlugBase:
                        StartGame(self, ply.player);
                        break;
                    case ArenaAdditions.PlayerDescriptor.Type.Vanilla:
                    default:
                        StartGame(self, 0);
                        break;
                }
            }

            Debug.Log($"Starting game!");
            Debug.Log($"Selected world character: {ArenaAdditions.GetSelectedArenaCharacter(manager.arenaSetup)}");
            for(int i = 0; i < 4; i++)
            {
                Debug.Log($"Player {i}: {ArenaAdditions.GetSelectedArenaCharacter(manager.arenaSetup, i)}");
            }

            orig(self, manager);
        }

        // Disable the active character (if one exists) as the game ends
        private static void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);
            EndGame(self);
        }

        #endregion HOOKS

        private static void SetCustomPlayer(RainWorldGame game, SlugBaseCharacter customPlayer)
        {
            if(!gameSetups.TryGetValue(game, out GameSetup setup))
                gameSetups[game] = setup = new GameSetup();
            setup.worldCharacter = customPlayer;
        }

        private static void UpdateCustomPlayer(RainWorldGame game, AbstractCreature player, SlugBaseCharacter customPlayer)
        {
            if (!gameSetups.TryGetValue(game, out GameSetup setup))
                gameSetups[game] = setup = new GameSetup();

            if (!setup.playerCharacters.TryGetValue(player, out var oldCha) || oldCha != customPlayer)
            {
                oldCha?.DisableInstance();
                customPlayer?.EnableInstance();
                setup.playerCharacters[player] = customPlayer;
                if (player.realizedObject is Player ply)
                    customPlayer?.PlayerAdded(game, ply);
            }
        }

        private static Dictionary<AbstractCreature, SlugBaseCharacter> GetAllPlayerCharacters(RainWorldGame game)
        {
            if (gameSetups.TryGetValue(game, out GameSetup setup))
                return setup.playerCharacters;
            return new Dictionary<AbstractCreature, SlugBaseCharacter>();
        }

        internal static void StartGame(RainWorldGame game, int slugcatNumber)
        {
            SlugBaseCharacter ply = GetCustomPlayer(slugcatNumber);
            if (ply != null)
                StartGame(game, ply);
        }

        internal static void StartGame(RainWorldGame game, SlugBaseCharacter customPlayer)
        {
            Debug.Log($"Started game as \"{customPlayer.Name}\"");

            SetCustomPlayer(game, customPlayer);

#pragma warning disable CS0618 // Type or member is obsolete
            CurrentCharacter = customPlayer;
#pragma warning restore CS0618 // Type or member is obsolete

            customPlayer.EnableInstance();
        }

        internal static void EndGame(RainWorldGame game)
        {
            // Disable all player characters
            foreach (var pair in GetAllPlayerCharacters(game))
                pair.Value?.DisableInstance();

            // Disable the world character
            var ply = GetCustomPlayer(game);
            if (ply != null)
            {
                Debug.Log($"Ended game as \"{ply.Name}\"");
                ply.DisableInstance();
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (CurrentCharacter == ply)
                CurrentCharacter = null;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private class GameSetup
        {
            public SlugBaseCharacter worldCharacter;
            public Dictionary<AbstractCreature, SlugBaseCharacter> playerCharacters = new Dictionary<AbstractCreature, SlugBaseCharacter>();
        }
    }
}
