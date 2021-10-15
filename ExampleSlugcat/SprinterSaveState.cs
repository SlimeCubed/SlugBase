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
            isTurbo = false;

            try
            {
                foreach (var pair in data)
                {
                    switch (pair.Key)
                    {
                        case "turbo": isTurbo = bool.Parse(pair.Value); break;
                    }
                }
            } catch(Exception e)
            {
                throw new FormatException("Failed to load Sprinter save!", e);
            }
        }

        public override void Save(Dictionary<string, string> data)
        {
            data["turbo"] = isTurbo.ToString();
        }
    }
}
