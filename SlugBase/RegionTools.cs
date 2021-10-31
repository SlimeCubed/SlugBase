using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using DevInterface;
using System.IO;
using RWCustom;

namespace SlugBase
{
    // Allow modded regions to change their behavior by character name
    internal static class RegionTools
    {
        private static readonly AttachedField<RoomSettings, SupplementaryRoomSettings> supplementarySettings = new AttachedField<RoomSettings, SupplementaryRoomSettings>();
        private static readonly Dictionary<WorldLoader, List<WorldCoordinate>> forbiddenDens = new Dictionary<WorldLoader, List<WorldCoordinate>>();
        private static readonly Dictionary<WorldLoader, List<World.CreatureSpawner>> replacementSpawners = new Dictionary<WorldLoader, List<World.CreatureSpawner>>();

        public static void ApplyHooks()
        {
            On.WorldLoader.NextActivity += WorldLoader_NextActivity;
            On.WorldLoader.FindingCreatures += WorldLoader_FindingCreatures;
            On.RoomSettings.Load += RoomSettings_Load;
            On.RoomSettings.Save += RoomSettings_Save;
            On.DevInterface.FilterRepresentation.FilterControlPanel.ctor += FilterControlPanel_ctor;
            On.DevInterface.FilterRepresentation.FilterControlPanel.UpdateButtonText += FilterControlPanel_UpdateButtonText;
            On.DevInterface.FilterRepresentation.FilterControlPanel.Signal += FilterControlPanel_Signal;
            On.RoomSettings.LoadPlacedObjects += RoomSettings_LoadPlacedObjects;
        }

        // Check and filter a single line of a world file
        private static bool ShouldKeepLine(string line, out string newLine)
        {
            newLine = line;

            string charName = PlayerManager.CurrentCharacter?.Name;
            var args = Regex.Split(line, " : ");

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

        // Return the path to a supplementary room settings file
        private static string GetCustomSettingsPath(RoomSettings settings)
        {
            var path = settings.filePath;
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_SlugBase.txt");
        }

        #region Hooks

        // Finalize spawn replacement logic
        private static void WorldLoader_NextActivity(On.WorldLoader.orig_NextActivity orig, WorldLoader self)
        {
            if (self.activity == WorldLoader.Activity.FindingCreatures)
            {
                forbiddenDens.TryGetValue(self, out var selfForbiddenDens);
                replacementSpawners.TryGetValue(self, out var selfReplacementSpawners);

                // Remove all from forbidden dens except for replacement spawns
                if (selfForbiddenDens != null)
                    self.spawners.RemoveAll(spawner => selfForbiddenDens.Contains(spawner.den) && !(selfReplacementSpawners?.Contains(spawner) ?? false));

                forbiddenDens.Remove(self);
                replacementSpawners.Remove(self);
            }
            orig(self);
        }

        // Filter creature lines by SlugBase character before they are used
        // Remove previous den contents when the REPLACE tag is used
        private static void WorldLoader_FindingCreatures(On.WorldLoader.orig_FindingCreatures orig, WorldLoader self)
        {
            var line = self.lines[self.cntr];

            if (ShouldKeepLine(line, out var newLine))
            {
                string[] args = Regex.Split(newLine, " : ");
                bool replace = args.Length > 0 && args[0].EndsWith("_REPLACE");

                if(replace)
                {
                    args[0] = args[0].Replace("_REPLACE", "");
                    newLine = newLine.Replace("_REPLACE", "");
                }

                int spawnerCount = self.spawners.Count;

                self.lines[self.cntr] = newLine;
                orig(self);

                // If the REPLACE tag is used, mark all dens that the added spawners occupy as forbidden
                if(replace && args.Length >= 2)
                {
                    if(!forbiddenDens.TryGetValue(self, out var selfForbiddenDens))
                    {
                        selfForbiddenDens = new List<WorldCoordinate>();
                        forbiddenDens[self] = selfForbiddenDens;
                    }

                    if(args[0] != "LINEAGE")
                    {
                        // Find room index
                        int roomIndex = -1;
                        if (args[0] == "OFFSCREEN")
                        {
                            roomIndex = self.world.firstRoomIndex + self.roomAdder.Count;
                        }
                        else
                        {
                            int num2 = 0;
                            while (num2 < self.roomAdder.Count && roomIndex < 0)
                            {
                                if (self.roomAdder[num2][0] == args[0])
                                {
                                    roomIndex = self.world.firstRoomIndex + num2;
                                }
                                num2++;
                            }
                        }

                        // Find world coordinates of empty dens
                        foreach (string spawn in Regex.Split(args[1], ", "))
                        {
                            string[] spawnArgs = spawn.Split('-');
                            if (spawnArgs.Length >= 2 && spawnArgs[1] == "None" && int.TryParse(spawnArgs[0], out int denNumber))
                                selfForbiddenDens.Add(new WorldCoordinate(roomIndex, -1, -1, denNumber));
                        }
                    }

                    // Add to forbidden dens and mark the added spawners as safe
                    if(!replacementSpawners.TryGetValue(self, out var selfReplacementSpawners))
                    {
                        selfReplacementSpawners = new List<World.CreatureSpawner>();
                        replacementSpawners[self] = selfReplacementSpawners;
                    }
                    for (int i = spawnerCount; i < self.spawners.Count; i++)
                    {
                        selfForbiddenDens.Add(self.spawners[i].den);
                        selfReplacementSpawners.Add(self.spawners[i]);
                    }
                }
            }
        }

        // Load extra settings
        private static void RoomSettings_Load(On.RoomSettings.orig_Load orig, RoomSettings self, int playerChar)
        {
            orig(self, playerChar);
        }

        // Write extra settings
        private static void RoomSettings_Save(On.RoomSettings.orig_Save orig, RoomSettings self)
        {
            orig(self);

            if (supplementarySettings.TryGet(self, out var settings))
                settings.Save(self);
        }

        // Add SlugBase characters to the filter control panel
        private static void FilterControlPanel_ctor(On.DevInterface.FilterRepresentation.FilterControlPanel.orig_ctor orig, FilterRepresentation.FilterControlPanel self, DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos)
        {
            orig(self, owner, IDstring, parentNode, pos);

            int ind = 0;
            
            void AddButton(string name)
            {
                var y = 25f + 20f * (ind / 2);
                var btn = new SlugBaseFilterButton(owner, "Button_" + name, self, new Vector2(5f + 120f * ind, y), 110f, name);
                self.subNodes.Add(btn);
                ind++;

                self.size.y = Mathf.Max(self.size.y, y + 20f);
            }

            // Add buttons for all loaded characters
            foreach (var player in PlayerManager.GetCustomPlayers())
            {
                AddButton(player.Name);
            }

            // Add buttons for all existing filters
            var settings = supplementarySettings[self.RoomSettings];
            if(settings != null)
            {
                var filterOverride = settings.filterOverrides.FirstOrDefault(x => x.Key.Target == ((FilterRepresentation)self.parentNode).pObj);
                if (filterOverride.Value != null)
                {
                    foreach (var o in filterOverride.Value)
                    {
                        if (PlayerManager.GetCustomPlayer(o.character) == null)
                            AddButton(o.character);
                    }
                }
            }

            self.UpdateButtonText();
        }

        // Update buttons to reflect changes to the filter
        private static void FilterControlPanel_UpdateButtonText(On.DevInterface.FilterRepresentation.FilterControlPanel.orig_UpdateButtonText orig, FilterRepresentation.FilterControlPanel self)
        {
            orig(self);

            // Search for list of overrides
            List<FilterOverride> overrides = null;
            var pObj = ((FilterRepresentation)self.parentNode).pObj;
            var settings = supplementarySettings[self.RoomSettings];
            if(settings != null)
                overrides = settings.filterOverrides.FirstOrDefault(x => x.Key.Target == pObj).Value;

            // Update button text and color
            foreach (var node in self.subNodes)
            {
                if (node is SlugBaseFilterButton sbfb)
                {
                    bool? o = null;
                    if (overrides != null)
                    {
                        foreach (var filterOverride in overrides)
                        {
                            if (filterOverride.character == sbfb.name)
                            {
                                o = filterOverride.keepObjects;
                                break;
                            }
                        }
                    }

                    sbfb.UpdateText(o);
                }
            }
        }

        // Change filter settings for SlugBase characters
        private static void FilterControlPanel_Signal(On.DevInterface.FilterRepresentation.FilterControlPanel.orig_Signal orig, FilterRepresentation.FilterControlPanel self, DevUISignalType type, DevUINode sender, string message)
        {
            orig(self, type, sender, message);

            if (sender is SlugBaseFilterButton sbfb)
            {
                // Find the list of SlugBase filter overrides for this placed object
                var settings = supplementarySettings[self.RoomSettings];
                if (settings == null) return;

                var pObj = ((FilterRepresentation)self.parentNode).pObj;
                List<FilterOverride> overrides = settings.filterOverrides.FirstOrDefault(x => x.Key.Target == pObj).Value;

                if (overrides == null)
                {
                    overrides = new List<FilterOverride>();
                    settings.filterOverrides.Add(new KeyValuePair<WeakReference, List<FilterOverride>>(new WeakReference(pObj), overrides));
                }

                // Find this character in that list
                int ind = -1;
                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i].character == sbfb.name)
                    {
                        ind = i;
                        break;
                    }
                }

                if (ind == -1)
                {
                    // If it isn't listed, cycle to +
                    overrides.Add(new FilterOverride(sbfb.name, true));
                }
                else
                {
                    var oldOverride = overrides[ind];
                    if (oldOverride.keepObjects)
                    {
                        // If it's +, cycle to -
                        overrides[ind] = new FilterOverride(sbfb.name, false);
                    }
                    else
                    {
                        // If it's -, remove from the list
                        overrides.RemoveAt(ind);
                    }
                }

                self.UpdateButtonText();
            }
        }

        // Load custom filters
        private static void RoomSettings_LoadPlacedObjects(On.RoomSettings.orig_LoadPlacedObjects orig, RoomSettings self, string[] s, int playerChar)
        {
            orig(self, s, playerChar);

            // Read from the supplementary settings file
            var settings = SupplementaryRoomSettings.Load(self);
            if (settings != null)
                supplementarySettings[self] = settings;
            else
                settings = supplementarySettings[self] = new SupplementaryRoomSettings();

            if (!PlayerManager.UsingCustomCharacter) return;
            var charName = PlayerManager.CurrentCharacter.Name;

            var filters = new List<PlacedObject>();
            foreach(var pObj in self.placedObjects)
            {
                if(pObj.type == PlacedObject.Type.Filter)
                    filters.Add(pObj);
            }

            // Apply custom filters
            foreach(var filterOverride in settings.filterOverrides)
            {
                var filter = (PlacedObject)filterOverride.Key.Target;
                if (filter == null || !self.placedObjects.Contains(filter)) continue;

                bool? keepActive = filterOverride.Value.Select(o => (o.character == charName) ? (bool?)o.keepObjects : null).FirstOrDefault();
                if (keepActive == null) continue;

                foreach (var pObj in self.placedObjects)
                {
                    if (pObj.deactivattable && Custom.DistLess(pObj.pos, filter.pos, (filter.data as PlacedObject.FilterData).Rad))
                    {
                        pObj.active = keepActive.Value;
                        break;
                    }
                }
            }
        }

        #endregion Hooks

        private struct FilterOverride
        {
            public string character;
            public bool keepObjects;

            public FilterOverride(string character, bool keepObjects)
            {
                this.character = character;
                this.keepObjects = keepObjects;
            }
        }

        private class SupplementaryRoomSettings
        {
            public List<KeyValuePair<WeakReference, List<FilterOverride>>> filterOverrides = new List<KeyValuePair<WeakReference, List<FilterOverride>>>();

            public static SupplementaryRoomSettings Load(RoomSettings settings)
            {
                string path = GetCustomSettingsPath(settings);
                var res = new SupplementaryRoomSettings();

                string section = null;
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(path);
                }
                catch
                {
                    return null;
                }

                foreach (string line in lines)
                {
                    if (line.Length == 0) continue;

                    // Exit the current section when END is reached
                    if(line == "END" && section != null)
                    {
                        section = null;
                        continue;
                    }
                        
                    switch (section)
                    {
                        // Read a section name
                        case null:
                            section = line;
                            if (section.Length == 0) section = null;
                            break;

                        // Read a SlugBase character filter
                        case "FILTERS":
                            res.AddFilter(settings, line);
                            break;
                    }
                }

                return res;
            }

            private void AddFilter(RoomSettings settings, string line)
            {
                int split = line.IndexOf(':');
                if (split < 0) return;
                if (!int.TryParse(line.Substring(0, split), out int filterInd)) return;

                string[] names = line.Substring(split + 1).Split(',');

                var overrideList = new List<FilterOverride>();

                foreach (string name in names)
                {
                    string trimName = name.TrimStart();
                    if (trimName.Length < 1) continue;
                    overrideList.Add(new FilterOverride(trimName.Substring(1), trimName[0] == '+'));
                }

                PlacedObject filter = settings.placedObjects.Where(o => o.type == PlacedObject.Type.Filter).ElementAtOrDefault(filterInd);

                if (filter != null)
                    filterOverrides.Add(new KeyValuePair<WeakReference, List<FilterOverride>>(new WeakReference(filter), overrideList));
            }

            public void Save(RoomSettings settings)
            {
                string path = GetCustomSettingsPath(settings);
                var lines = new List<string>();

                // FILTERS
                // Index:+KeepForChar,-RemoveForChar
                if(filterOverrides.Count > 0 && filterOverrides.Any(x => x.Value.Count > 0))
                {
                    PlacedObject[] filterObjs = settings.placedObjects.Where(o => o.type == PlacedObject.Type.Filter).ToArray();

                    lines.Add("FILTERS");
                    foreach(var pair in filterOverrides)
                    {
                        if (pair.Value.Count == 0) continue;

                        var ind = Array.IndexOf(filterObjs, pair.Key.Target);
                        if (ind == -1) continue;

                        var sb = new StringBuilder();
                        sb.Append(ind);
                        sb.Append(':');
                        bool first = true;
                        foreach(var filterOverride in pair.Value)
                        {
                            if (first)
                                first = false;
                            else
                                sb.Append(',');
                            sb.Append(filterOverride.keepObjects ? '+' : '-');
                            sb.Append(filterOverride.character);
                        }
                        lines.Add(sb.ToString());
                    }
                    lines.Add("END");
                }

                if (lines.Count > 0)
                    File.WriteAllLines(path, lines.ToArray());
                else
                    File.Delete(path);
            }
        }

        private class SlugBaseFilterButton : Button
        {
            public string name;

            public SlugBaseFilterButton(DevUI owner, string IDstring, DevUINode parentNode, Vector2 pos, float width, string name) : base(owner, IDstring, parentNode, pos, width, string.Empty)
            {
                this.name = name;
            }

            public void UpdateText(bool? keepActive)
            {
                if(!keepActive.HasValue)
                {
                    Text = name;
                    colorA = Color.white;
                }
                else if(keepActive == true)
                {
                    Text = '+' + name;
                    colorA = Color.Lerp(Color.white, Color.green, 0.5f);
                }
                else
                {
                    Text = '-' + name;
                    colorA = Color.Lerp(Color.white, Color.red, 0.5f);
                }
            }
        }
    }
}
