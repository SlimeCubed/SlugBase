using Menu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RWCustom;
using MonoMod.RuntimeDetour;
using System.Reflection;
using UnityEngine;
using System.Text.RegularExpressions;

namespace SlugBase
{
    // Whenever save data would be read from the progression file, check if it would be for a SlugBase character
    // If it is, then search for save data elsewhere

    // The data saved for each character is the same as would be stored for the "SAVE STATE" key
    // "SAV STATE NUMBER" should be omitted, since it may change for slugbase characters

    /// <summary>
    /// Manages save data for SlugBase characters.
    /// </summary>
    /// <remarks>
    /// Save data for different SlugBase characters is saved to separate files.
    /// This ensures that one character's save game will never be played by another character
    /// and that, after uninstalling SlugBase, the vanilla game will not try to load modded saves.
    /// </remarks>
    public static class SaveManager
    {
        private static Dictionary<string, string> globalData;
        private static readonly Dictionary<string, Dictionary<string, string>> characterData = new Dictionary<string, Dictionary<string, string>>();
        private static readonly Dictionary<SaveFileIndex, SlugBaseSaveSummary> summaryCache = new Dictionary<SaveFileIndex, SlugBaseSaveSummary>();

        internal static void ApplyHooks()
        {
            On.PlayerProgression.WipeAll += PlayerProgression_WipeAll;
            On.PlayerProgression.WipeSaveState += PlayerProgression_WipeSaveState;
            On.PlayerProgression.ShelterOfSaveGame += PlayerProgression_ShelterOfSaveGame;
            On.PlayerProgression.SaveToDisk += PlayerProgression_SaveToDisk;
            On.PlayerProgression.SaveDeathPersistentDataOfCurrentState += PlayerProgression_SaveDeathPersistentDataOfCurrentState;
            On.PlayerProgression.GetOrInitiateSaveState += PlayerProgression_GetOrInitiateSaveState;
            On.PlayerProgression.IsThereASavedGame += PlayerProgression_IsThereASavedGame;
            On.Futile.OnApplicationQuit += Futile_OnApplicationQuit;

            Directory.CreateDirectory(GetSaveFileDirectory());
        }

        #region Hooks

        // Wipes everything associated with a save slot
        private static void PlayerProgression_WipeAll(On.PlayerProgression.orig_WipeAll orig, PlayerProgression self)
        {
            globalData = null;
            characterData.Clear();

            orig(self);
            string[] files = Directory.GetFiles(GetSaveFileDirectory(), $"*-{self.rainWorld.options.saveSlot}.txt");
            for (int i = 0; i < files.Length; i++)
                File.Delete(files[i]);
        }

        private static void PlayerProgression_WipeSaveState(On.PlayerProgression.orig_WipeSaveState orig, PlayerProgression self, int saveStateNumber)
        {
            orig(self, saveStateNumber);
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(saveStateNumber);
            if (ply != null)
            {
                File.Delete(GetSaveFilePath(ply.Name, self.rainWorld.options.saveSlot));
            }
        }

        // Read the shelter name from a separate file
        private static string PlayerProgression_ShelterOfSaveGame(On.PlayerProgression.orig_ShelterOfSaveGame orig, PlayerProgression self, int saveStateNumber)
        {
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(saveStateNumber);
            if (ply == null)
                return orig(self, saveStateNumber);

            if (self.currentSaveState != null && self.currentSaveState.saveStateNumber == saveStateNumber)
                return self.currentSaveState.denPosition;

            string startRoom = ply.StartRoom;

            int slot = self.rainWorld.options.saveSlot;
            if (!HasCustomSaveData(ply.Name, slot) && startRoom != null)
                return startRoom;

            string saveText = File.ReadAllText(GetSaveFilePath(ply.Name, slot));

            List<SaveStateMiner.Target> targets = new List<SaveStateMiner.Target>();
            targets.Add(new SaveStateMiner.Target(">DENPOS", "<svB>", "<svA>", 20));

            List<SaveStateMiner.Result> results = SaveStateMiner.Mine(self.rainWorld, saveText, targets);
            if (results.Count > 0 && results[0].data != null)
                return results[0].data;

            return startRoom ?? "SU_S01";
        }

        // If the current state represents a custom character and would be saved,
        // ... write to a separate file instead
        private static void PlayerProgression_SaveToDisk(On.PlayerProgression.orig_SaveToDisk orig, PlayerProgression self, bool saveCurrentState, bool saveMaps, bool saveMiscProg)
        {
            // Clear cached save summaries, since those could now be invalid
            summaryCache.Clear();

            WriteDataToDisk(self.rainWorld);

            if (!saveCurrentState || !(self.currentSaveState is CustomSaveState css))
            {
                orig(self, saveCurrentState, saveMaps, saveMiscProg);
                return;
            }

            // Could be moved to a separate thread
            File.WriteAllText(GetSaveFilePath(css.Character.Name, self.rainWorld.options.saveSlot), css.SaveToString());
            Debug.Log("successfully saved slugbase state for \"" + css.Character.Name + "\" to disc");

            if (saveMaps || saveMiscProg)
                orig(self, false, saveMaps, saveMiscProg);
        }

        // Update a file on the disk with the current save-persistent data
        private static void PlayerProgression_SaveDeathPersistentDataOfCurrentState(On.PlayerProgression.orig_SaveDeathPersistentDataOfCurrentState orig, PlayerProgression self, bool saveAsIfPlayerDied, bool saveAsIfPlayerQuit)
        {
            WriteDataToDisk(self.rainWorld);

            int slot = self.rainWorld.options.saveSlot;
            SlugBaseCharacter ply = null;
            if (self.currentSaveState != null) ply = PlayerManager.GetCustomPlayer(self.currentSaveState.saveStateNumber);
            if (ply == null || !(self.currentSaveState is CustomSaveState css))
            {
                orig(self, saveAsIfPlayerDied, saveAsIfPlayerQuit);
                return;
            }

            // Copied from PlayerProgression.SaveDeathPersistentDataOfCurrentState

            Debug.Log(string.Concat(new object[]
            {
                "save slugbase deathPersistent data ",
                self.currentSaveState.deathPersistentSaveData.karma,
                " sub karma: ",
                saveAsIfPlayerDied,
                " (quit:",
                saveAsIfPlayerQuit,
                ")"
            }));

            string savePath = GetSaveFilePath(ply.Name, slot);
            string customDPSD = css.SaveCustomPermanentToString(saveAsIfPlayerDied, saveAsIfPlayerQuit);
            string vanillaDPSD = self.currentSaveState.deathPersistentSaveData.SaveToString(saveAsIfPlayerDied, saveAsIfPlayerQuit);

            string inSave;
            try
            {
                inSave = File.ReadAllText(savePath);
            }
            catch (Exception)
            {
                // Consider changing to handle FileNotFound only
                return;
            }
            StringBuilder outSave = new StringBuilder();
            string[] array2 = Regex.Split(inSave, "<svA>");
            for (int j = 0; j < array2.Length; j++)
            {
                string[] pair = Regex.Split(array2[j], "<svB>");
                // Save vanilla DPSD
                if (pair[0] == "DEATHPERSISTENTSAVEDATA")
                    outSave.Append("DEATHPERSISTENTSAVEDATA<svB>" + vanillaDPSD + "<svA>");
                
                // Save custom DPSD
                else if (pair[0] == "SLUGBASEPERSISTENT")
                    outSave.Append("SLUGBASEPERSISTENT<svB>" + customDPSD + "<svA>");

                // Echo any other data
                else
                    outSave.Append(array2[j] + "<svA>");
            }

            File.WriteAllText(savePath, outSave.ToString());
        }

        private static SaveState PlayerProgression_GetOrInitiateSaveState(On.PlayerProgression.orig_GetOrInitiateSaveState orig, PlayerProgression self, int saveStateNumber, RainWorldGame game, ProcessManager.MenuSetup setup, bool saveAsDeathOrQuit)
        {
            int slot = self.rainWorld.options.saveSlot;
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(saveStateNumber);

            if (ply == null)
                return orig(self, saveStateNumber, game, setup, saveAsDeathOrQuit);


            // Copied from PlayerProgression.GetOrInitiateSaveState

            if (self.currentSaveState == null && self.starvedSaveState != null)
            {
                Debug.Log("LOADING STARVED STATE");
                self.currentSaveState = self.starvedSaveState;
                self.currentSaveState.deathPersistentSaveData.winState.ResetLastShownValues();
                self.starvedSaveState = null;
            }
            if (self.currentSaveState != null && self.currentSaveState.saveStateNumber == saveStateNumber)
            {
                if (saveAsDeathOrQuit)
                    self.SaveDeathPersistentDataOfCurrentState(true, true);
                return self.currentSaveState;
            }

            // Create a CustomSaveState instance instead
            self.currentSaveState = ply.CreateNewSave(self);

            if (!File.Exists(self.saveFilePath) || !setup.LoadInitCondition)
            {
                self.currentSaveState.LoadGame(string.Empty, game);
            }
            else
            {
                // Read the save state from a separate file instead of from prog lines
                CustomSaveState css = self.currentSaveState as CustomSaveState;
                if (css != null && HasCustomSaveData(ply.Name, slot))
                {
                    string inSave = File.ReadAllText(GetSaveFilePath(ply.Name, slot));
                    self.currentSaveState.LoadGame(inSave, game);
                    if (saveAsDeathOrQuit)
                        self.SaveDeathPersistentDataOfCurrentState(true, true);
                    return self.currentSaveState;
                }

                // By default, load an empty string
                self.currentSaveState.LoadGame(string.Empty, game);
            }
            if (saveAsDeathOrQuit)
            {
                self.SaveDeathPersistentDataOfCurrentState(true, true);
            }
            return self.currentSaveState;
            // End copied section
        }

        private static bool PlayerProgression_IsThereASavedGame(On.PlayerProgression.orig_IsThereASavedGame orig, PlayerProgression self, int saveStateNumber)
        {
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(saveStateNumber);
            if (ply != null)
                return HasCustomSaveData(ply.Name, self.rainWorld.options.saveSlot);
            return orig(self, saveStateNumber);
        }

        // Save global data when quitting
        // Without this, changes made when menuing will not save until a game is started
        private static void Futile_OnApplicationQuit(On.Futile.orig_OnApplicationQuit orig, Futile self)
        {
            orig(self);
            var rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
            if (rw != null)
                WriteDataToDisk(rw);
        }

        #endregion Hooks

        /// <summary>
        /// Gets the path that contains all SlugBase character save files.
        /// </summary>
        /// <returns>An absolute path to the save file directory.</returns>
        public static string GetSaveFileDirectory()
        {
            return string.Concat(new object[] {
                Custom.RootFolderDirectory(),
                "UserData",
                Path.DirectorySeparatorChar,
                "SlugBase",
                Path.DirectorySeparatorChar
            });
        }

        /// <summary>
        /// Gets the path to a specific SlugBase character's save file.
        /// </summary>
        /// <param name="name">The name of the SlugBase character.</param>
        /// <param name="slot">The save slot to get the path for.</param>
        /// <returns>An absolute path to the save file.</returns>
        public static string GetSaveFilePath(string name, int slot)
        {
            // UserData\Player Name-0.txt
            return string.Concat(new object[]
            {
                GetSaveFileDirectory(),
                name,
                "-",
                slot,
                ".txt"
            });
        }

        /// <summary>
        /// Gets the path to a specific SlugBase character's data file used by <see cref="GetCharacterData(string, int)"/>.
        /// </summary>
        /// <param name="name">The name of the SlugBase character.</param>
        /// <param name="slot">The save slot to get the path for.</param>
        /// <returns>An absolute path to the data file.</returns>
        public static string GetCharacterDataPath(string name, int slot)
        {
            if (!PlayerManager.IsValidCharacterName(name))
                throw new ArgumentException("Invalid SlugBaseCharacter name!", nameof(name));

            // UserData\data-Player Name-0.txt
            return string.Concat(new object[]
            {
                GetSaveFileDirectory(),
                "data-",
                name,
                "-",
                slot,
                ".txt"
            });
        }

        /// <summary>
        /// Gets the path to the global data file used by <see cref="GetGlobalData(int)"/>.
        /// </summary>
        /// <param name="slot">The save slot to get data for.</param>
        /// <returns>An absolute path to the data file.</returns>
        public static string GetGlobalDataPath(int slot)
        {
            // UserData\data-global-0.txt
            return string.Concat(new object[]
            {
                GetSaveFileDirectory(),
                "data-global-",
                slot,
                ".txt"
            });
        }

        /// <summary>
        /// Gets a persistent set of key-value pairs.
        /// This persists on death, between sessions, and between resets.
        /// It is cleared only when the entire save slot is cleared.
        /// </summary>
        /// <param name="slot">The save slot to get data for.</param>
        /// <returns>A set of persistent key-value pairs.</returns>
        public static Dictionary<string, string> GetGlobalData(int slot)
        {
            if (globalData == null)
            {
                globalData = new Dictionary<string, string>();

                // Load global data
                try
                {
                    var lines = File.ReadAllLines(GetGlobalDataPath(slot));
                    for(int i = 0; i < lines.Length; i += 2)
                        globalData[Unescape(lines[i])] = Unescape(lines[i + 1]);
                }
                catch { }
            }
            return globalData;
        }

        /// <summary>
        /// Gets a persistent set of key-value pairs.
        /// This persists on death, between sessions, and between resets.
        /// It is cleared only when the entire save slot is cleared.
        /// </summary>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        /// <returns>A set of persistent key-value pairs.</returns>
        public static Dictionary<string, string> GetGlobalData(RainWorld rainWorld) => GetGlobalData(rainWorld.options.saveSlot);

        /// <summary>
        /// Gets a set of key-value pairs associated with a <see cref="SlugBaseCharacter"/>.
        /// This persists on death, between sessions, and between resets.
        /// It is cleared only when the entire save slot is cleared.
        /// </summary>
        /// <param name="name">The <see cref="SlugBaseCharacter.Name"/> of the character to get data for.</param>
        /// <param name="slot">The save slot to get data for.</param>
        /// <returns>A set of persistent key-value pairs.</returns>
        public static Dictionary<string, string> GetCharacterData(string name, int slot)
        {
            if (!characterData.TryGetValue(name, out var data))
            {
                data = new Dictionary<string, string>();
                characterData[name] = data;
                try
                {
                    var lines = File.ReadAllLines(GetCharacterDataPath(name, slot));
                    for (int i = 0; i < lines.Length; i += 2)
                        data[Unescape(lines[i])] = Unescape(lines[i + 1]);
                }
                catch { }
            }

            return data;
        }

        /// <summary>
        /// Gets a set of key-value pairs associated with a <see cref="SlugBaseCharacter"/>.
        /// This persists on death, between sessions, and between resets.
        /// It is cleared only when the entire save slot is cleared.
        /// </summary>
        /// <param name="name">The <see cref="SlugBaseCharacter.Name"/> of the character to get data for.</param>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        /// <returns>A set of persistent key-value pairs.</returns>
        public static Dictionary<string, string> GetCharacterData(string name, RainWorld rainWorld) => GetCharacterData(name, rainWorld.options.saveSlot);

        /// <summary>
        /// Writes all modified values from <see cref="GetCharacterData(string, int)"/> and <see cref="GetGlobalData(int)"/> to files.
        /// This will happen automatically if the program is closed normally, but it may be called manually to stop save scumming.
        /// </summary>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        public static void WriteDataToDisk(RainWorld rainWorld)
        {
            int slot = rainWorld.options.saveSlot;

            // Global data
            if (globalData != null)
            {
                using (var file = new StreamWriter(File.OpenWrite(GetGlobalDataPath(slot))))
                {
                    foreach (var pair in globalData)
                    {
                        file.Write(Escape(pair.Key));
                        file.Write(Environment.NewLine);
                        file.Write(Escape(pair.Value));
                        file.Write(Environment.NewLine);
                    }
                }
            }

            // Character-specific data
            foreach(var charData in characterData)
            {
                using (var file = new StreamWriter(File.OpenWrite(GetCharacterDataPath(charData.Key, slot))))
                {
                    foreach(var pair in charData.Value)
                    {
                        file.Write(Escape(pair.Key));
                        file.Write(Environment.NewLine);
                        file.Write(Escape(pair.Value));
                        file.Write(Environment.NewLine);
                    }
                }
            }
        }

        /// <summary>
        /// Checks for a save file for a specific SlugBase character.
        /// </summary>
        /// <param name="name">The name of the SlugBase character.</param>
        /// <param name="slot">The game's current save slot.</param>
        /// <returns>True if the save file exists, false otherwise.</returns>
        public static bool HasCustomSaveData(string name, int slot)
        {
            return File.Exists(GetSaveFilePath(name, slot));
        }

        /// <summary>
        /// Gets a summary of the content in a SlugBase character's save file.
        /// If you need to access data saved using <see cref="CustomSaveState"/>, use <see cref="GetSaveSummary(RainWorld, string, int)"/> instead.
        /// </summary>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        /// <param name="name">The name of the SlugBase character.</param>
        /// <param name="slot">The game's current save slot.</param>
        /// <returns>A summary of the given character's save file or null if the file could not be found.</returns>
        public static SlugcatSelectMenu.SaveGameData GetCustomSaveData(RainWorld rainWorld, string name, int slot)
        {
            MineSaveData(rainWorld, name, slot, false, out var vanilla, out _, out _);
            return vanilla;
        }

        /// <summary>
        /// Gets a summary of the content in a SlugBase character's save file, including data saved using <see cref="CustomSaveState"/>.
        /// </summary>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        /// <param name="name">The name of the SlugBase character.</param>
        /// <param name="slot">The game's current save slot.</param>
        /// <returns>A summary of the given character's save file or null if the file could not be found.</returns>
        public static SlugBaseSaveSummary GetSaveSummary(RainWorld rainWorld, string name, int slot)
        {
            var key = new SaveFileIndex(name, slot);
            if (!summaryCache.TryGetValue(key, out var summary))
            {
                if (!MineSaveData(rainWorld, name, slot, true, out var vanilla, out var custom, out var customPersistent))
                {
                    summary = null;
                }
                else
                {
                    summary = new SlugBaseSaveSummary(vanilla, custom, customPersistent);
                }

                summaryCache[key] = summary;
            }

            return summary;
        }

        private static bool MineSaveData(RainWorld rainWorld, string name, int slot, bool mineCustom, out SlugcatSelectMenu.SaveGameData vanilla, out Dictionary<string, string> custom, out Dictionary<string, string> customPersistent)
        {
            vanilla = null;
            custom = null;
            customPersistent = null;

            string saveData;
            try
            {
                saveData = File.ReadAllText(GetSaveFilePath(name, slot));
            }
            catch
            {
                return false;
            }

            // Mine vanilla data
            List<SaveStateMiner.Target> targets = new List<SaveStateMiner.Target>();
            targets.Add(new SaveStateMiner.Target(">DENPOS", "<svB>", "<svA>", 20));
            targets.Add(new SaveStateMiner.Target(">CYCLENUM", "<svB>", "<svA>", 50));
            targets.Add(new SaveStateMiner.Target(">FOOD", "<svB>", "<svA>", 20));
            targets.Add(new SaveStateMiner.Target(">HASTHEGLOW", null, "<svA>", 20));
            targets.Add(new SaveStateMiner.Target(">REINFORCEDKARMA", "<dpB>", "<dpA>", 20));
            targets.Add(new SaveStateMiner.Target(">KARMA", "<dpB>", "<dpA>", 20));
            targets.Add(new SaveStateMiner.Target(">KARMACAP", "<dpB>", "<dpA>", 20));
            targets.Add(new SaveStateMiner.Target(">HASTHEMARK", null, "<dpA>", 20));
            targets.Add(new SaveStateMiner.Target(">REDEXTRACYCLES", null, "<svA>", 20));
            targets.Add(new SaveStateMiner.Target(">ASCENDED", null, "<dpA>", 20));

            if (mineCustom)
            {
                targets.Add(new SaveStateMiner.Target(">SLUGBASE", "<svB>", "<svA>", int.MaxValue / 4));
                targets.Add(new SaveStateMiner.Target(">SLUGBASEPERSISTENT", "<svB>", "<svA>", int.MaxValue / 4));
            }

            List<SaveStateMiner.Result> results = SaveStateMiner.Mine(rainWorld, saveData, targets);
            vanilla = new SlugcatSelectMenu.SaveGameData();

            for (int i = 0; i < results.Count; i++)
            {
                string targetName = results[i].name;
                try
                {
                    switch (targetName)
                    {
                        case ">DENPOS":
                            vanilla.shelterName = results[i].data;
                            break;
                        case ">CYCLENUM":
                            vanilla.cycle = int.Parse(results[i].data);
                            break;
                        case ">FOOD":
                            vanilla.food = int.Parse(results[i].data);
                            break;
                        case ">HASTHEGLOW":
                            vanilla.hasGlow = true;
                            break;
                        case ">REINFORCEDKARMA":
                            vanilla.karmaReinforced = (results[i].data == "1");
                            break;
                        case ">KARMA":
                            vanilla.karma = int.Parse(results[i].data);
                            break;
                        case ">KARMACAP":
                            vanilla.karmaCap = int.Parse(results[i].data);
                            break;
                        case ">HASTHEMARK":
                            vanilla.hasMark = true;
                            break;
                        case ">REDEXTRACYCLES":
                            vanilla.redsExtraCycles = true;
                            break;
                        case ">REDSDEATH":
                            vanilla.redsDeath = true;
                            break;
                        case ">ASCENDED":
                            vanilla.ascended = true;
                            break;
                        case ">SLUGBASE":
                            custom = CustomSaveState.DataFromString(results[i].data);
                            break;
                        case ">SLUGBASEPERSISTENT":
                            customPersistent = CustomSaveState.DataFromString(results[i].data);
                            break;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(new Exception($"Failed to parse value from slugbase save (\"{name}\") for \"{targetName}\"!", e));
                }
            }

            return true;
        }

        private static string Escape(string value)
        {
            value = value.Replace("\\", "\\\\");
            value = value.Replace("\r", "\\r");
            value = value.Replace("\n", "\\n");
            return value;
        }

        private static string Unescape(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            int i = 0;
            bool escape = false;
            while (i < value.Length)
            {
                char c = value[i];
                if (escape)
                {
                    escape = false;
                    switch (c)
                    {
                        case '\\': sb.Append('\\'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'n': sb.Append('\n'); break;
                        default: sb.Append(c); break;
                    }
                }
                else
                {
                    if (c == '\\') escape = true;
                    else sb.Append(c);
                }
                i++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// A summary of a <see cref="SlugBaseCharacter"/>'s save file including commonly used values
        /// from the vanilla game and all data attached using a <see cref="CustomSaveState"/>.
        /// </summary>
        public class SlugBaseSaveSummary
        {
            /// <summary>
            /// Commonly used values from the base game.
            /// </summary>
            public SlugcatSelectMenu.SaveGameData VanillaData { get; }

            /// <summary>
            /// Key-value pairs that would be passed to <see cref="CustomSaveState.Load(Dictionary{string, string})"/>.
            /// </summary>
            public Dictionary<string, string> CustomData { get; }

            /// <summary>
            /// Key-value pairs that would be passed to <see cref="CustomSaveState.LoadPermanent(Dictionary{string, string})"/>.
            /// </summary>
            public Dictionary<string, string> CustomPersistentData { get; }

            internal SlugBaseSaveSummary(SlugcatSelectMenu.SaveGameData vanillaData, Dictionary<string, string> customData, Dictionary<string, string> customPersistent)
            {
                VanillaData = vanillaData;
                CustomData = customData;
                CustomPersistentData = customPersistent;
            }
        }

        private struct SaveFileIndex
        {
            public readonly string name;
            public readonly int slot;

            public SaveFileIndex(string name, int slot)
            {
                this.name = name;
                this.slot = slot;
            }

            public override bool Equals(object obj)
            {
                return obj is SaveFileIndex index &&
                       name == index.name &&
                       slot == index.slot;
            }

            public override int GetHashCode()
            {
                int hashCode = -2042622137;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(name);
                hashCode = hashCode * -1521134295 + slot.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(SaveFileIndex left, SaveFileIndex right) => left.Equals(right);

            public static bool operator !=(SaveFileIndex left, SaveFileIndex right) => !(left == right);
        }
    }
}
