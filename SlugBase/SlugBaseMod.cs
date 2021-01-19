using System;
using System.Collections.Generic;
using Partiality.Modloader;

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
             * TODO:
             * 
             * Diets
             * Arena mode (portraits, allowing custom players to be used)
             * Sleep and death screens
             * Cutscenes
             * nifflasmode
             * Look into Scavenger.WantToLethallyAttack
             * Iterator dialogs
             * Toggle for if you can flarebomb skip
             * Cycle time config
             * 
             */
        }

        public override void OnLoad()
        {
            CustomScenes.ApplyHooks();
            WorldFixes.ApplyHooks();
            PlayerManager.ApplyHooks();
            SaveManager.ApplyHooks();
            SelectMenu.ApplyHooks();

            TestSlugcatMod.Register();
        }
    }
}
