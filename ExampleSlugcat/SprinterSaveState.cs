using System;
using SlugBase;
using System.Collections.Generic;
using UnityEngine;

namespace ExampleSlugcat
{
    // Store extra information with the Sprinter's save file
    class SprinterSaveState : CustomSaveState
    {
        public bool isTurbo = false;

        public SprinterSaveState(PlayerProgression progression, SlugBaseCharacter character) : base(progression, character)
        {
        }

        public override void Load(Dictionary<string, string> data)
        {
            isTurbo = data.TryGetValue("turbo", out string temp) ? bool.Parse(temp) : false;
        }

        public override void Save(Dictionary<string, string> data)
        {
            data["turbo"] = isTurbo.ToString();
        }
    }
}
