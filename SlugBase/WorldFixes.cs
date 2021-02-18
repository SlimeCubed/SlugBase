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

            On.WorldLoader.ctor += WorldLoader_ctor;

            On.RoomSettings.ctor += RoomSettings_ctor;

            On.CollectToken.CollectTokenData.FromString += CollectTokenData_FromString;
            On.CollectToken.CollectTokenData.ctor += CollectTokenData_ctor;

            On.EventTrigger.FromString += EventTrigger_FromString;
            On.EventTrigger.ctor += EventTrigger_ctor;
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

            // Add custom characterse to the shelter list
            ResizeToFit(ref self.playerShelters, 3 + PlayerManager.customPlayers.Count, null);
            for (int i = 3; i < self.playerShelters.Length; i++)
            {
                self.playerShelters[i] = self.manager.rainWorld.progression.ShelterOfSaveGame(i);
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
            checkFastTravelForCustom = true;
            orig(self, manager, ID);
        }

        // WorldLoader
        // Copy the creatures from another character
        private static void WorldLoader_ctor(On.WorldLoader.orig_ctor orig, WorldLoader self, RainWorldGame game, int playerCharacter, bool singleRoomWorld, string worldName, Region region, RainWorldGame.SetupValues setupValues)
        {
            orig(self, game, PlayerManager.CurrentCharacter?.useSpawns ?? playerCharacter, singleRoomWorld, worldName, region, setupValues);
        }

        // RoomSettings
        private static void RoomSettings_ctor(On.RoomSettings.orig_ctor orig, RoomSettings self, string name, Region region, bool template, bool firstTemplate, int playerChar)
        {
            // Copy filters from another character
            orig(self, name, region, template, firstTemplate, PlayerManager.CurrentCharacter?.useSpawns ?? playerChar);
        }

        // CollectToken
        private static void CollectTokenData_FromString(On.CollectToken.CollectTokenData.orig_FromString orig, CollectToken.CollectTokenData self, string s)
        {
            orig(self, s);

            if(PlayerManager.UsingCustomCharacter)
                self.availableToPlayers[PlayerManager.CurrentCharacter.slugcatIndex] = self.availableToPlayers[PlayerManager.CurrentCharacter.useSpawns];
        }

        private static void CollectTokenData_ctor(On.CollectToken.CollectTokenData.orig_ctor orig, CollectToken.CollectTokenData self, PlacedObject owner, bool isBlue)
        {
            orig(self, owner, isBlue);

            ResizeToFitPlayer(ref self.availableToPlayers, true);
        }

        // EventTrigger
        private static void EventTrigger_FromString(On.EventTrigger.orig_FromString orig, EventTrigger self, string[] s)
        {
            orig(self, s);

            if(PlayerManager.UsingCustomCharacter)
                self.slugcats[PlayerManager.CurrentCharacter.slugcatIndex] = self.slugcats[PlayerManager.CurrentCharacter.useSpawns];
        }

        private static void EventTrigger_ctor(On.EventTrigger.orig_ctor orig, EventTrigger self, EventTrigger.TriggerType type)
        {
            orig(self, type);

            ResizeToFitPlayer(ref self.slugcats, true);
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

        private static void ResizeToFitPlayer<T>(ref T[] array, T initValue)
        {
            if (!PlayerManager.UsingCustomCharacter) return;

            ResizeToFit(ref array, PlayerManager.CurrentCharacter.slugcatIndex + 1, initValue);
        }
    }
}
