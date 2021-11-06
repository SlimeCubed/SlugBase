﻿using System;
using System.IO;
using System.Collections.Generic;
using Partiality.Modloader;
using UnityEngine;

namespace SlugBase
{
    internal class SlugBaseMod : PartialityMod
    {
        public const string versionString = "1.2.0-beta3";

        // AutoUpdate support
        public string updateURL = "http://beestuff.pythonanywhere.com/audb/api/mods/6/0";
        public int version = 6;
        public string keyE = "AQAB";
        public string keyN = "szjz4lkR8G9JuQ4Jt2DEk7h5hRcvpX0LfHWXp203VrsSwWenj2xho0zl8m6gsSYNVaBFm3WXbqkj7snI+DuheYfvSLpfLZsHCOF2XdIO2FCyOFSUmQ7T4Jvd/ap5jFMofXu6geBf0hl0H4VJ1/D2SpDg7rkAi+hAbHBd1d7o1mfON1ZdzDKIeTeFCstw5w+ImfE83sg1OspLmrrec3UNyXlNzc5x+r5gHwgOfMMTWLfI1fUVRd3o43U+zV7PHsyOjPGzHfLVLS3IO6va3Pc7sng+bxifchP9IWS4RTps4qmGA6AcQE2qaI1oH0Ql9EzAfBeIhvNXica0nlTHBJQ8tZxewA1igdHl2deSgszpKseAPPxsg9+njoaq4rvqcEys3/KfJImxyS3W49U+GxGmoPx298GMSUlfyw3zY3Ytlbb7/7tbHfP71G4/ISwkn+WyhufE3SLYWX/6uR//0aMGNe/zoH8AOvnPtepX4Mwy3HYnETzc5WsCgetmCViEI0YdAKl3FClgtuhsYRXmEXDy7yeVpTSsAzoUdkqnzFSG5ykm1mh1ISCpBiQ9prB2inCaWMc6DALWsFUElOV6yVbmWorfX2EiNesDhoFmAxz6pt6CADVBoxewDTFUtT103jYVkROKe4oNUr2W0Sj1sEv6kURHfjE5+3OLfbrk3OLJrnU=";

        internal static int FirstCustomIndex = 4;

        public SlugBaseMod()
        {
            ModID = "SlugBase";
            Version = versionString;
            author = "Slime_Cubed";

            /* 
             * This mod aims to reduce the boilerplate needed to add custom slugcats.
             * This should cover everything that currently varies between characters, such as:
             *  - Stats
             *  - Scenes
             *  - Diets
             *  - Save slots
             *  
             * This does not cover anything that is unchanged between characters, such as:
             *  - Adding new abilities
             *  - Adding new regions
             *  
             * This mod should aim to limit the functionality of added characters as little
             * as possible - even though it doesn't implement something, it should still
             * expect the mod to implement it itself.
             * Generally, this should only change methods from their orig when the modder
             * requests it, such as with CustomPlayer.QuarterFood.
             */

            /*
             * TODO:
             * 
             * Scene editor (maybe)
             * Multi-instance support for arena
             * Track down uses of SlugcatColor that are associated with a Player instance
             * 
             */
        }

        public override void OnLoad()
        {
            // Compatibility fixes
            Compatibility.FlatmodeFix.Apply();
            Compatibility.HookGenFix.Apply();

            Testing.ApplyHooks();

            // Core changes
            ArenaAdditions.ApplyHooks();
            CustomSceneManager.ApplyHooks();
            PlayerManager.ApplyHooks();
            SaveManager.ApplyHooks();
            //SceneEditor.ApplyHooks();
            SelectMenu.ApplyHooks();
            ShelterScreens.ApplyHooks();
            RegionTools.ApplyHooks();
            WorldFixes.ApplyHooks();

            On.RainWorld.Start += RainWorld_Start;

            // Guess an appropriate index to assign to SlugBase characters
            // This should make them more resistant to skipping the select screen
            foreach (SlugcatStats.Name name in Enum.GetValues(typeof(SlugcatStats.Name)))
                FirstCustomIndex = Math.Max((int)name + 1, FirstCustomIndex);
        }

        // Apply modules that must be added late
        private void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
        {
            // Compatibility fixes (applied over other hooks)
            Compatibility.FancySlugcats.Apply();
            Compatibility.JollyCoop.Apply();
            WorldFixes.LateApply();

            orig(self);
            On.RainWorld.Start -= RainWorld_Start;
        }

        public object GetReloadState()
        {
            return FirstCustomIndex;
        }

        public void Reload(object state)
        {
            FirstCustomIndex = (int)state;
        }
    }
}
