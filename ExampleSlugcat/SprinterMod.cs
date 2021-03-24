using UnityEngine;
using SlugBase;

/*
 * This is a basic example of a SlugBase character.
 * 
 * It adds The Sprinter, a slugcat with faster movement and higher jumps.
 * Some scenes have been overridden so then Sprinter appears instead of Survivor.
 * To install, copy ExampleSlugcat.dll and SlugBase into the Mods folder.
 */

namespace ExampleSlugcat
{
    // Your mod class
    // This does not have to be a PartialityMod
    internal class ExampleSlugcatMod : Partiality.Modloader.PartialityMod
    {
        public ExampleSlugcatMod()
        {
            ModID = "Example Slugcat";
            Version = "1.1";
            author = "Slime_Cubed";
        }

        public override void OnEnable()
        {
            PlayerManager.RegisterCharacter(new SprinterSlugcat());
        }
    }
}
