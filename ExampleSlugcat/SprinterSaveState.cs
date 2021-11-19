using System;
using SlugBase;
using System.Collections.Generic;
using UnityEngine;

namespace ExampleSlugcat
{
    // Store extra information with the Sprinter's save file
    class SprinterSaveState : CustomSaveState
    {
        [SavedField]
        public bool isTurbo;

        public SprinterSaveState(PlayerProgression progression, SlugBaseCharacter character) : base(progression, character)
        {
        }
    }
}
