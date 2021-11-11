using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SlugBase
{
    // The player index is sometimes used to do things that don't work with more than 3 characters
    // This mostly fixes them by copying the index of the character world spawns are copied from
    internal static class WorldFixes
    {
        public static void ApplyHooks()
        {
            On.Menu.FastTravelScreen.InitiateRegionSwitch += FastTravelScreen_InitiateRegionSwitch;
            On.Menu.FastTravelScreen.ctor += FastTravelScreen_ctor;

            //On.WorldLoader.ctor += WorldLoader_ctor;
            // deferred, moved to start

            On.CollectToken.CollectTokenData.ctor += CollectTokenData_ctor;
            On.CollectToken.CollectTokenData.FromString += CollectTokenData_FromString;

            On.EventTrigger.ctor += EventTrigger_ctor;
            On.EventTrigger.FromString += EventTrigger_FromString;
        }

        internal static void LateApply()
        {
            On.WorldLoader.ctor += WorldLoader_ctor;
        }

        // FastTravelScreen
        // To avoid having to replace the entire method body of FastTravelScreen.ctor
        private static bool checkFastTravelForCustom = false;
        private static void FastTravelScreen_InitiateRegionSwitch(On.Menu.FastTravelScreen.orig_InitiateRegionSwitch orig, Menu.FastTravelScreen self, int switchToRegion)
        {
            if(!checkFastTravelForCustom)
            {
                orig(self, switchToRegion);
                return;
            }

            int maxIndex = -1;
            foreach (SlugBaseCharacter ply in PlayerManager.customPlayers)
                maxIndex = Mathf.Max(maxIndex, ply.SlugcatIndex);

            // Add custom characterse to the shelter list
            int oldLen = self.playerShelters.Length;
            if (oldLen < maxIndex)
            {
                ResizeToFit(ref self.playerShelters, maxIndex + 1, null);
                for (int i = oldLen; i < self.playerShelters.Length; i++)
                {
                    self.playerShelters[i] = self.manager.rainWorld.progression.ShelterOfSaveGame(i);
                }
            }

            var prog = self.manager.rainWorld.progression;

            // Check the current slugcat for a shelter
            if (prog.PlayingAsSlugcat >= 0 && prog.PlayingAsSlugcat < self.playerShelters.Length && self.playerShelters[prog.PlayingAsSlugcat] != null)
                self.currentShelter = self.playerShelters[prog.PlayingAsSlugcat];

            // Find the region that this shelter is in
            if (self.currentShelter != null)
            {
                string regionName = self.currentShelter.Substring(0, 2);
                for (int regionInd = 0; regionInd < self.accessibleRegions.Count; regionInd++)
                {
                    if (self.allRegions[self.accessibleRegions[regionInd]].name == regionName)
                    {
                        Debug.Log(self.currentShelter);
                        Debug.Log(string.Concat(new object[]
                        {
                                "actually found start region (including SlugBase saves): ",
                                regionInd,
                                " ",
                                self.allRegions[self.accessibleRegions[regionInd]].name
                        }));
                        self.currentRegion = regionInd;
                        break;
                    }
                }
            }

            orig(self, self.currentRegion);
        }

        private static void FastTravelScreen_ctor(On.Menu.FastTravelScreen.orig_ctor orig, Menu.FastTravelScreen self, ProcessManager manager, ProcessManager.ProcessID ID)
        {
            if(PlayerManager.GetCustomPlayer(self.PlayerCharacter) != null)
                checkFastTravelForCustom = true;
            try
            {
                orig(self, manager, ID);
            }
            finally
            {
                checkFastTravelForCustom = false;
            }
        }

        // WorldLoader
        // Copy the creatures from another character
        private static void WorldLoader_ctor(On.WorldLoader.orig_ctor orig, WorldLoader self, RainWorldGame game, int playerCharacter, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig(self, game, PlayerManager.GetCustomPlayer(game)?.InheritWorldFromSlugcat ?? playerCharacter, singleRoomWorld, worldName, region, setupValues);
        }

        // CollectToken
        private static void CollectTokenData_ctor(On.CollectToken.CollectTokenData.orig_ctor orig, CollectToken.CollectTokenData self, PlacedObject owner, bool isBlue)
        {
            orig(self, owner, isBlue);

            ResizeToFitPlayers(ref self.availableToPlayers, true);
        }

        private static void CollectTokenData_FromString(On.CollectToken.CollectTokenData.orig_FromString orig, CollectToken.CollectTokenData self, string s)
        {
            orig(self, s);

            var availability = self.availableToPlayers;
            foreach (var cha in PlayerManager.GetCustomPlayers())
            {
                var from = cha.InheritWorldFromSlugcat;
                var to = cha.SlugcatIndex;
                if (from >= 0 && to >= 0 && from < availability.Length && to < availability.Length)
                    availability[to] = availability[from];
            }
        }

        // EventTrigger
        private static void EventTrigger_ctor(On.EventTrigger.orig_ctor orig, EventTrigger self, EventTrigger.TriggerType type)
        {
            orig(self, type);

            ResizeToFitPlayers(ref self.slugcats, true);
        }

        private static void EventTrigger_FromString(On.EventTrigger.orig_FromString orig, EventTrigger self, string[] s)
        {
            orig(self, s);

            var slugcats = self.slugcats;
            foreach(var cha in PlayerManager.GetCustomPlayers())
            {
                var from = cha.InheritWorldFromSlugcat;
                var to = cha.SlugcatIndex;
                if (from >= 0 && to >= 0 && from < slugcats.Length && to < slugcats.Length)
                    slugcats[to] = slugcats[from];
            }
        }

        private static void ResizeToFit<T>(ref T[] array, int length, T initValue)
        {
            int origLength = array.Length;
            if (length > origLength)
            {
                Array.Resize(ref array, length);
                for (int i = origLength; i < length; i++)
                    array[i] = initValue;
            }
        }

        private static void ResizeToFitPlayers<T>(ref T[] array, T initValue)
        {
            if (PlayerManager.GetCustomPlayers().Count == 0) return;

            var maxIndex = PlayerManager.GetCustomPlayers().Max(cha => cha.SlugcatIndex);
            ResizeToFit(ref array, maxIndex + 1, initValue);
        }
    }
}
