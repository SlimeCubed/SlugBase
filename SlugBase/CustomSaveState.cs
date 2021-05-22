using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlugBase
{
    /// <summary>
    /// Contains the save data for any characters added through SlugBase.
    /// </summary>
    /// <remarks>
    /// If you need to save extra data for your character, you should create a class that inherits from this.
    /// </remarks>
    public class CustomSaveState : SaveState
    {
        private static bool appliedHooks = false;

        /// <summary>
        /// Creates a new representation of a SlugBase character's save state.
        /// </summary>
        /// <param name="progression">The <see cref="PlayerProgression"/> instance to attach this save state to.</param>
        /// <param name="character">The SlugBase character that owns this save state.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="character"/> is null.</exception>
        public CustomSaveState(PlayerProgression progression, SlugBaseCharacter character) : base(character.slugcatIndex, progression)
        {
            if (character == null) throw new ArgumentException("Character may not be null.", nameof(character));
            Character = character;

            if (!appliedHooks) {
                appliedHooks = true;
                ApplyHooks();
            }
        }

        /// <summary>
        /// The <see cref="SlugBaseCharacter"/> that owns this save state.
        /// </summary>
        public SlugBaseCharacter Character { get; private set; }

        /// <summary>
        /// Converts save data for this character to strings to enter into a dictionary.
        /// This data is not saved when the player dies.
        /// </summary>
        /// <remarks>
        /// <see cref="Load(Dictionary{string, string})"/> should be overridden to handle the same values.
        /// </remarks>
        /// <param name="data">The empty dictionary to add your data to.</param>
        public virtual void Save(Dictionary<string, string> data)
        {
        }

        /// <summary>
        /// Loads saved data for this character from entries in a dictionary.
        /// This data is loaded from the last time the player survived a cycle.
        /// </summary>
        /// <remarks>
        /// <see cref="Save(Dictionary{string, string})"/> should be overridden to handle the same values.
        /// </remarks>
        /// <param name="data">The dictionary to read your data from.</param>
        public virtual void Load(Dictionary<string, string> data)
        {
        }

        /// <summary>
        /// Converts death-persistant save data for this character to strings to enter into a dictionary.
        /// This data is not reset when the player dies.
        /// </summary>
        /// <remarks>
        /// Saved values that change on quit or death should be changed here.
        /// This is saved as a death once when starting the cycle, so quitting the game
        /// early counts as a death if it is not overwritten by the end of the cycle.
        /// <para><see cref="LoadPermanent(Dictionary{string, string})"/> should be overridden to handle the same values.</para>
        /// </remarks>
        /// <param name="data">The empty dictionary to add your data to.</param>
        /// <param name="asDeath">True if the player has quit or died.</param>
        /// <param name="asQuit">True if the player has quit.</param>
        public virtual void SavePermanent(Dictionary<string, string> data, bool asDeath, bool asQuit)
        {
        }

        /// <summary>
        /// Loads death-persistant saved data for this character from entries in a dictionary.
        /// This data is not reset when the player dies.
        /// </summary>
        /// <remarks>
        /// <see cref="SavePermanent(Dictionary{string, string}, bool, bool)"/> should be overridden to handle the same values.
        /// </remarks>
        /// <param name="data">The dictionary to read your data from.</param>
        public virtual void LoadPermanent(Dictionary<string, string> data)
        {
        }

        #region Hooks

        // Use hooks to simulate inheritance
        private static void ApplyHooks()
        {
            On.SaveState.SaveToString += SaveState_SaveToString;
            On.SaveState.LoadGame += SaveState_LoadGame;
        }

        private static string SaveState_SaveToString(On.SaveState.orig_SaveToString orig, SaveState self)
        {
            if(!(self is CustomSaveState css))
                return orig(self);

            StringBuilder sb = new StringBuilder(orig(self));
            string customData = css.SaveCustomToString();
            if (!string.IsNullOrEmpty(customData))
            {
                sb.Append("SLUGBASE<svB>");
                sb.Append(customData);
                sb.Append("<svA>");
            }
            customData = css.SaveCustomPermanentToString(false, false);
            if (!string.IsNullOrEmpty(customData))
            {
                sb.Append("SLUGBASEPERSISTENT<svB>");
                sb.Append(customData);
                sb.Append("<svA>");
            }

            return sb.ToString();
        }

        private static void SaveState_LoadGame(On.SaveState.orig_LoadGame orig, SaveState self, string str, RainWorldGame game)
        {
            if(!(self is CustomSaveState css))
            {
                orig(self, str, game);
                return;
            }

            // This will produce extraneous debugging logs - consider somehow suppressing them
            orig(self, str, game);

            string customStartRoom = css.Character.StartRoom;
            if (str == string.Empty && customStartRoom != null)
                self.denPosition = customStartRoom;

            var data = DataFromString(SearchForSavePair(str, "SLUGBASE", "<svB>", "<svA>"));
            var persistData = DataFromString(SearchForSavePair(str, "SLUGBASEPERSISTENT", "<svB>", "<svA>"));

            css.Load(data);
            css.LoadPermanent(persistData);
        }

        private static string SearchForSavePair(string input, string key, string separator, string cap)
        {
            int start = input.IndexOf(key + separator);
            if (start == -1) return null;
            start += key.Length + separator.Length;
            int end = input.IndexOf(cap, start);
            if (end == -1) return input.Substring(start);
            return input.Substring(start, end - start);
        }

        #endregion Hooks


        internal string SaveCustomToString()
        {
            var data = new Dictionary<string, string>();
            Save(data);
            return DataToString(data);
        }

        internal string SaveCustomPermanentToString(bool asDeath, bool asQuit)
        {
            var data = new Dictionary<string, string>();
            SavePermanent(data, asDeath, asQuit);
            return DataToString(data);
        }

        private static Dictionary<string, string> DataFromString(string dataString)
        {
            var data = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(dataString)) return data;
            string[] entries = dataString.Split(',');
            for(int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                int split = entry.IndexOf(':');
                if (split == -1) continue;

                string key = Unescape(entry.Substring(0, split));
                string value = Unescape(entry.Substring(split + 1));
                data[key] = value;
            }
            return data;
        }

        private static string DataToString(Dictionary<string, string> data)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var pair in data)
            {
                sb.Append(Escape(pair.Key));
                sb.Append(':');
                sb.Append(Escape(pair.Value));
                sb.Append(',');
            }
            return sb.ToString();
        }

        // Because of how the save file is formatted, string values such as <progDivB> will permanently corrupt a save state.
        // Although this is unlikely to happen, it shouldn't be allowed to happen in the first place.
        // Escape "<" to "\L", ">" to "\G", and "\" to "\\"
        private static string Escape(string value)
        {
            value = value.Replace("\\", "\\\\");
            value = value.Replace("<", "\\L");
            value = value.Replace(">", "\\G");
            value = value.Replace(":", "\\C");
            value = value.Replace(",", "\\c");
            return value;
        }

        private static string Unescape(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            int i = 0;
            bool escape = false;
            while(i < value.Length)
            {
                char c = value[i];
                if (escape)
                {
                    escape = false;
                    switch(c)
                    {
                        case '\\': sb.Append('\\'); break;
                        case 'L' : sb.Append('<' ); break;
                        case 'G' : sb.Append('>' ); break;
                        case 'C' : sb.Append(':' ); break;
                        case 'c' : sb.Append(',' ); break;
                        default  : sb.Append(c   ); break;
                    }
                }
                else
                {
                    if (c == '\\') escape = true;
                    else sb.Append(c);
                }
                i++;
            }
            return sb.ToString();
        }
    }
}