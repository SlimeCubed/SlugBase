﻿using SlugBase;

/*
 * This example interacts with SlugBase as little as possible.
 * 
 * The player select menu and sleep screen will display Survivor.
 * This makes the select screen ambiguous once a game is started, since the name is hidden.
 * Consider copying one of the slugcat select scenes and editing it.
 */

namespace BareMinimum
{
    public class BareMinimum : Partiality.Modloader.PartialityMod
    {
        public BareMinimum()
        {
            ModID = "Bare Minimum Example Slugcat";
            Version = "1.1";
            author = "Slime_Cubed";
        }

        public override void OnLoad()
        {
            PlayerManager.RegisterCharacter(new BareMinimumSlugcat());
        }
    }

    public class BareMinimumSlugcat : SlugBaseCharacter
    {
        public BareMinimumSlugcat() : base("Bare Minimum", FormatVersion.V1, 0, true)
        {
        }

        public override string DisplayName => "The Prototype";
        public override string Description =>
@"A new slugcat that demonstrates the bare minimum amount required.
This is an example slugcat for the SlugBase framework.";

        protected override void Disable() {}

        protected override void Enable() {}
    }
}
