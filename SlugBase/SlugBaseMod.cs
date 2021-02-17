using System;
using System.IO;
using System.Collections.Generic;
using Partiality.Modloader;
using UnityEngine;

namespace SlugBase
{
    internal class SlugBaseMod : PartialityMod
    {
        public const string version = "0.1";

        public SlugBaseMod()
        {
            ModID = "SlugBase";
            Version = version;
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
             * 
             */

            /*
             * To test:
             * 
             * Cycle time config
             * Toggle for if you can flarebomb skip
             * 
             */

        }

        public override void OnLoad()
        {
            ArenaAdditions.ApplyHooks();
            CustomSceneManager.ApplyHooks();
            PlayerManager.ApplyHooks();
            SaveManager.ApplyHooks();
            //SceneEditor.ApplyHooks();
            SelectMenu.ApplyHooks();
            ShelterScreens.ApplyHooks();
            WorldFixes.ApplyHooks();
        }
    }
}
