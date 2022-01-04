using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlayerDescriptor = SlugBase.ArenaAdditions.PlayerDescriptor;

namespace SlugBase.Config
{
    /// <summary>
    /// Represents a group of character selections.
    /// This may include any number of multi-instance or vanilla characters or one repeated single-instance character.
    /// </summary>
    public class CharacterSelectGroup
    {
        private readonly Dictionary<int, PlayerDescriptor> characters = new Dictionary<int, PlayerDescriptor>();
        private PlayerDescriptor defaultCharacter = new PlayerDescriptor(0);

        /// <summary>
        /// Creates an empty character group.
        /// </summary>
        public CharacterSelectGroup()
        {
        }

        /// <summary>
        /// Sets a player, specified by index, to use a the given <paramref name="character"/>.
        /// </summary>
        /// <remarks>
        /// This may change other selections in the same group.
        /// </remarks>
        /// <param name="playerNumber">The index of the player.</param>
        /// <param name="character">The character that player should use.</param>
        public void SetPlayer(int playerNumber, PlayerDescriptor character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));

            if (character == defaultCharacter)
            {
                characters.Remove(playerNumber);
            }
            else
            {
                if (character.MultiInstance)
                {
                    // Clear all single-instance characters when any others are selected
                    if (!defaultCharacter.MultiInstance)
                        SetAllPlayers(new PlayerDescriptor(0));

                    characters[playerNumber] = character;
                }
                else
                {
                    // Multi-instance characters will always override others
                    SetAllPlayers(character);
                }
            }
        }

        /// <summary>
        /// Sets all players to use the given <paramref name="character"/>
        /// </summary>
        /// <param name="character">The character that all players should use.</param>
        public void SetAllPlayers(PlayerDescriptor character)
        {
            defaultCharacter = character;
            characters.Clear();
        }

        /// <summary>
        /// Retrieves the characer a player, specified by index, has selected.
        /// </summary>
        /// <param name="playerNumber">The index of the player to check.</param>
        public PlayerDescriptor GetPlayer(int playerNumber)
        {
            if (characters.TryGetValue(playerNumber, out var res))
                return res;
            else
                return defaultCharacter;
        }

        /// <summary>
        /// Creates a string representation of this group of characters.
        /// </summary>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            // Header
            sb.AppendLine("V2");

            // Default
            sb.AppendLine(defaultCharacter.ToString());

            // Key/value pairs for all individual selections
            foreach (var pair in characters)
                sb.AppendLine($"{pair.Key}:{pair.Value}");

            return sb.ToString();
        }

        /// <summary>
        /// Loads a group of characters from a string created with <see cref="ToString"/>.
        /// </summary>
        /// <remarks>
        /// The output may differ from the original group if any of the constituent characters have been uninstalled or have changed <see cref="SlugBaseCharacter.MultiInstance"/> to true.
        /// It does this to ensure that it is always a valid group, meaning that it will contain either one single-instance character or many multi-instance characters.
        /// </remarks>
        /// <param name="s">The string to parse.</param>
        internal static CharacterSelectGroup FromString(string s)
        {
            var group = new CharacterSelectGroup();
            group.SetFromString(s);
            return group;
        }

        /// <inheritdoc cref="FromString(string)"/>
        public void SetFromString(string s)
        {
            SetAllPlayers(new PlayerDescriptor(0));

            string[] lines = s.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            int i;
            switch (lines[0])
            {
                // V2: Header and default character, then key/value pairs for the rest
                case "V2":
                    SetAllPlayers(PlayerDescriptor.FromString(lines[1]));
                    for (i = 2; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        int split = line.IndexOf(':');

                        int playerNum = int.Parse(line.Substring(0, split));
                        SetPlayer(playerNum, PlayerDescriptor.FromString(line.Substring(split + 1)));
                    }
                    break;

                // V1: No header, just a list of player descriptors
                default:
                    for (i = 0; i < lines.Length; i++)
                    {
                        SetPlayer(i, PlayerDescriptor.FromString(lines[i]));
                    }
                    break;
            }
        }
    }
}
