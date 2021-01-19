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
            On.SlugcatStats.SlugcatFoodMeter += SlugcatStats_SlugcatFoodMeter;
            On.SlugcatStats.ctor += SlugcatStats_ctor;
            On.PlayerGraphics.SlugcatColor += PlayerGraphics_SlugcatColor;
            On.RainWorldGame.ctor += RainWorldGame_ctor;
            On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
        }

        private static IntVector2 SlugcatStats_SlugcatFoodMeter(On.SlugcatStats.orig_SlugcatFoodMeter orig, int slugcatNum)
        {
            CustomPlayer ply = GetCustomPlayer(slugcatNum);
            if(ply == null) return orig(slugcatNum);

            IntVector2 o = new IntVector2();
            ply.GetFoodMeter(out o.x, out o.y);
            return o;
        }

        private static void SlugcatStats_ctor(On.SlugcatStats.orig_ctor orig, SlugcatStats self, int slugcatNumber, bool malnourished)
        {
            orig(self, slugcatNumber, malnourished);
            CustomPlayer ply = GetCustomPlayer(slugcatNumber);
            ply?.GetStatsInternal(self);
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
