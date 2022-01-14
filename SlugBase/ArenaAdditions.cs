using System;
using System.IO;
using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;
using System.Linq;
using MonoMod.RuntimeDetour;
using MenuColors = Menu.Menu.MenuColors;
using SlugBase.Config;

namespace SlugBase
{
    /// <summary>
    /// Controls added arena mode settings and functionality.
    /// </summary>
    public static partial class ArenaAdditions
    {
        private static readonly AttachedField<ArenaSetup, CharacterSelectGroup> arenaCharacters = new AttachedField<ArenaSetup, CharacterSelectGroup>();
        private static readonly AttachedField<AbstractCreature, SlugcatStats> arenaStats = new AttachedField<AbstractCreature, SlugcatStats>();

        private static readonly PlayerDescriptor survivorDesc = new PlayerDescriptor(0);
        private static string arenaSettingsPath;

        internal static void ApplyHooks()
        {
            On.Menu.PlayerResultBox.ctor += PlayerResultBox_ctor;
            On.Menu.MultiplayerMenu.Update += MultiplayerMenu_Update;
            new Hook(
                typeof(Player).GetProperty(nameof(Player.slugcatStats)).GetGetMethod(),
                new Func<Func<Player, SlugcatStats>, Player, SlugcatStats>(Player_get_slugcatStats)
            );
            On.ArenaSetup.SaveToFile += ArenaSetup_SaveToFile;
            On.ArenaSetup.LoadFromFile += ArenaSetup_LoadFromFile;
            On.ArenaGameSession.ctor += ArenaGameSession_ctor;
            On.Menu.MultiplayerMenu.ctor += MultiplayerMenu_ctor;

            arenaSettingsPath = Path.Combine(SaveManager.GetSaveFileDirectory(), "arenaSetup.txt");
        }

        /// <summary>
        /// Gets the currently selected arena character.
        /// </summary>
        /// <param name="setup">The <see cref="ArenaSetup"/> that this selection is associated with.</param>
        /// <returns>A representation of the selected arena character.</returns>
        public static PlayerDescriptor GetSelectedArenaCharacter(ArenaSetup setup)
        {
            if (setup != null
                && arenaCharacters.TryGet(setup, out var characters))
            {
                PlayerDescriptor selectedChar = null;
                for (int i = 0; i < setup.playersJoined.Length; i++)
                {
                    if (!setup.playersJoined[i]) continue;

                    if (selectedChar == null)
                    {
                        selectedChar = characters.GetPlayer(i);
                    }
                    else
                    {
                        if (selectedChar != characters.GetPlayer(i))
                        {
                            selectedChar = null;
                            break;
                        }
                    }
                }

                if (selectedChar != null)
                    return selectedChar;
            }

            return survivorDesc;
        }

        /// <summary>
        /// Gets the currently selected arena character for the given player number.
        /// </summary>
        /// <param name="setup">The <see cref="ArenaSetup"/> that this selection is associated with.</param>
        /// <param name="playerNumber">The player number to check. This should be between 0 and 3, inclusive.</param>
        /// <returns>A representation of the selected arena character.</returns>
        public static PlayerDescriptor GetSelectedArenaCharacter(ArenaSetup setup, int playerNumber)
        {
            if (setup == null || !arenaCharacters.TryGet(setup, out var characters))
                return GetSelectedArenaCharacter(setup);
            else
                return characters.GetPlayer(playerNumber);
        }

        private static bool GetArenaPortrait(PlayerDescriptor ply, int playerNumber, bool dead, out string folder, out string file)
        {
            if(ply != null
                && ply.type == PlayerDescriptor.Type.SlugBase
                && ply.player.HasArenaPortrait(playerNumber, dead))
            {
                folder = CustomSceneManager.resourceFolderName;
                file = $"{ply.player.Name}\\Illustrations\\MultiplayerPortrait{playerNumber}{(dead ? "0" : "1")}";
                return true;
            }
            else
            {
                folder = null;
                file = $"MultiplayerPortrait{playerNumber}{(dead ? "0" : "1")}";
                return false;
            }
        }

        #region Hooks

        // Override portraits when ending an arena game
        private static void PlayerResultBox_ctor(On.Menu.PlayerResultBox.orig_ctor orig, PlayerResultBox self, Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size, ArenaSitting.ArenaPlayer player, int index)
        {
            orig(self, menu, owner, pos, size, player, index);

            var character = GetSelectedArenaCharacter(menu.manager.arenaSetup, player.playerNumber);

            if (GetArenaPortrait(character, player.playerNumber, self.DeadPortraint, out string folder, out string file))
            {
                MenuIllustration portrait = new MenuIllustration(
                    menu: menu,
                    owner: self,
                    folderName: folder,
                    fileName: file,
                    pos: new Vector2(self.size.y / 2f, self.size.y / 2f),
                    crispPixels: true,
                    anchorCenter: true);
                MenuIllustration oldPortrait = self.portrait;

                self.portrait = portrait;

                self.RemoveSubObject(oldPortrait);
                self.subObjects.Add(portrait);

                portrait.sprite.MoveBehindOtherNode(oldPortrait.sprite);
                oldPortrait.RemoveSprites();
            }
        }

        // Update player portraits when arena character selections change
        private static readonly AttachedField<PlayerJoinButton, PlayerDescriptor> joinButtonCharacter = new AttachedField<PlayerJoinButton, PlayerDescriptor>();
        private static void MultiplayerMenu_Update(On.Menu.MultiplayerMenu.orig_Update orig, MultiplayerMenu self)
        {
            orig(self);

            for (int i = 0; i < self.playerJoinButtons.Length; i++)
            {
                var joinButton = self.playerJoinButtons[i];
                var playerDesc = GetSelectedArenaCharacter(self.GetArenaSetup, i);
                var lastChar = joinButtonCharacter[joinButton];

                if (lastChar != playerDesc)
                {
                    joinButtonCharacter[joinButton] = playerDesc;

                    if (GetArenaPortrait(playerDesc, i, false, out string folder, out string file)
                        || GetArenaPortrait(lastChar, i, false, out _, out _))
                    {
                        if (joinButton.portrait.folderName != folder || joinButton.portrait.fileName != file)
                            ReplaceJoinButton(self, i, folder, file);
                    }
                }
            }
        }

        private static void ReplaceJoinButton(MultiplayerMenu menu, int i, string folder, string fileName)
        {
            PlayerJoinButton pjb = menu.playerJoinButtons[i];

            MenuIllustration portrait = new MenuIllustration(menu, pjb, folder, fileName, pjb.size / 2f, true, true);
            MenuIllustration oldPortrait = pjb.portrait;

            pjb.portrait = portrait;

            pjb.RemoveSubObject(oldPortrait);
            pjb.subObjects.Add(portrait);

            portrait.sprite.MoveBehindOtherNode(oldPortrait.sprite);
            oldPortrait.RemoveSprites();
        }

        private static SlugcatStats Player_get_slugcatStats(Func<Player, SlugcatStats> orig, Player self)
        {
            var origStats = orig(self);

            var game = self.abstractPhysicalObject.world.game;
            if (game.IsArenaSession)
            {
                var setup = game.rainWorld.processManager.arenaSetup;

                bool anyCustomCharacters = false;

                for(int i = 0; i < setup.playersJoined.Length; i++)
                {
                    if (!setup.playersJoined[i]) continue;

                    if(!GetSelectedArenaCharacter(setup, i).Equals(survivorDesc))
                    {
                        anyCustomCharacters = true;
                        break;
                    }
                }

                if (anyCustomCharacters)
                {
                    if (!arenaStats.TryGet(self.abstractCreature, out var newStats))
                    {
                        var cha = GetSelectedArenaCharacter(setup, self.playerState.playerNumber);
                        arenaStats[self.abstractCreature] = newStats = new SlugcatStats(cha.type == PlayerDescriptor.Type.SlugBase ? cha.player.SlugcatIndex : cha.index, origStats.malnourished);
                    }

                    if (newStats.malnourished != origStats.malnourished)
                        newStats = new SlugcatStats((int)newStats.name, origStats.malnourished);

                    return newStats;
                }
            }

            return origStats;
        }

        private static void ArenaSetup_SaveToFile(On.ArenaSetup.orig_SaveToFile orig, ArenaSetup self)
        {
            orig(self);
            if (arenaCharacters.TryGet(self, out var characters))
                File.WriteAllText(arenaSettingsPath, characters.ToString());
            else
                File.Delete(arenaSettingsPath);
        }

        private static void ArenaSetup_LoadFromFile(On.ArenaSetup.orig_LoadFromFile orig, ArenaSetup self)
        {
            orig(self);
            try
            {
                var characters = arenaCharacters[self] = CharacterSelectGroup.FromString(File.ReadAllText(arenaSettingsPath));
            }
            catch //(Exception e)
            {
                // This was too confusing for users
                //Debug.LogException(new FormatException("Invalid arena settings format. This error is not fatal.", e));
                arenaCharacters.Unset(self);
            }
        }

        // Change slugcat stats
        private static void ArenaGameSession_ctor(On.ArenaGameSession.orig_ctor orig, ArenaGameSession self, RainWorldGame game)
        {
            orig(self, game);

            var worldChar = GetSelectedArenaCharacter(self.game.manager.arenaSetup);
            switch(worldChar.type)
            {
                case PlayerDescriptor.Type.SlugBase:
                    self.characterStats = new SlugcatStats(worldChar.player.SlugcatIndex, false);
                    break;
                case PlayerDescriptor.Type.Vanilla:
                    if (worldChar.index != 0)
                        self.characterStats = new SlugcatStats(worldChar.index, false);
                    break;
            }
        }

        // Add a player selector
        private static void MultiplayerMenu_ctor(On.Menu.MultiplayerMenu.orig_ctor orig, MultiplayerMenu self, ProcessManager manager)
        {
            orig(self, manager);

            // Place relative to the first join button
            self.pages[0].subObjects.Add(new ArenaCharacterSelector(self, self.pages[0]));
        }

        #endregion Hooks

        // The list of name plates
        internal class ArenaCharacterSelector : MenuObject
        {
            private static readonly Vector2 characterSelectorOffset = new Vector2(0f, -37f);

            private readonly CharacterSelectButton[] namePlates;

            public ArenaCharacterSelector(MultiplayerMenu menu, MenuObject owner) : base(menu, owner)
            {
                if (!arenaCharacters.TryGet(menu.GetArenaSetup, out var characters))
                    arenaCharacters[menu.GetArenaSetup] = characters = new CharacterSelectGroup();

                namePlates = new CharacterSelectButton[menu.playerJoinButtons.Length];
                for(int i = 0; i < namePlates.Length; i++)
                {
                    var pjb = menu.playerJoinButtons[i];
                    namePlates[i] = new CharacterSelectButton(menu, this, pjb.pos + characterSelectorOffset, pjb.size.x, characters, i);
                    subObjects.Add(namePlates[i]);
                }
            }

            public override void Update()
            {
                base.Update();

                var menu = (MultiplayerMenu)this.menu;
                for(int i = 0; i < menu.playerJoinButtons.Length; i++)
                {
                    menu.playerJoinButtons[i].buttonBehav.greyedOut = namePlates[i].Expanded;
                }
            }
        }

        /// <summary>
        /// Represents a Rain World character.
        /// </summary>
        public class PlayerDescriptor
        {
            /// <summary>
            /// The way a character was added to the game.
            /// </summary>
            public enum Type : byte {
                /// <summary>
                /// The character is either a vanilla character, or was added by mods other than SlugBase.
                /// </summary>
                Vanilla,
                /// <summary>
                /// The character was added by SlugBase.
                /// </summary>
                SlugBase
            }

            /// <summary>
            /// The way this character was added to the game.
            /// </summary>
            public readonly Type type;
            /// <summary>
            /// This character's name.
            /// </summary>
            public readonly string name;
            /// <summary>
            /// This character's slugcat index, or -1 if it was added by SlugBase.
            /// </summary>
            public readonly int index;
            /// <summary>
            /// The SlugBase character this represents, or null if it was not added by SlugBase.
            /// </summary>
            public readonly SlugBaseCharacter player;

            /// <inheritdoc cref="SlugBaseCharacter.MultiInstance"/>
            public bool MultiInstance => type != Type.SlugBase || player.MultiInstance;

            /// <summary>
            /// Creates a representation of a SlugBase character.
            /// </summary>
            /// <param name="customPlayer">The character to represent.</param>
            public PlayerDescriptor(SlugBaseCharacter customPlayer)
            {
                type = Type.SlugBase;
                name = customPlayer.DisplayName;
                player = customPlayer;
                index = -1;
            }

            /// <summary>
            /// Creates a representation of a vanilla character, or a character added by a mod other than SlugBase.
            /// </summary>
            /// <param name="slugcatIndex">The slugcat index of the character to represent.</param>
            public PlayerDescriptor(int slugcatIndex)
            {
                type = Type.Vanilla;
                name = ((SlugcatStats.Name)slugcatIndex).ToString();
                switch (name)
                {
                    case "White": name = "The Survivor"; break;
                    case "Yellow": name = "The Monk"; break;
                    case "Red": name = "The Hunter"; break;
                }
                index = slugcatIndex;
            }

            /// <summary>
            /// The default color of this character, as gotten with <see cref="PlayerGraphics.SlugcatColor(int)"/>.
            /// </summary>
            public Color Color
            {
                get
                {
                    switch (type)
                    {
                        case Type.SlugBase:
                            return PlayerGraphics.SlugcatColor(player.SlugcatIndex);
                        case Type.Vanilla:
                        default:
                            return PlayerGraphics.SlugcatColor(index);
                    }
                }
            }

            /// <summary>
            /// Saves this player representation to a string.
            /// SlugBase characters will be saved as their names, vanilla characters
            /// will be saved as their name according to <see cref="SlugcatStats.Name"/>.
            /// </summary>
            /// <returns>A string representation of this character.</returns>
            public override string ToString()
            {
                string o = type.ToString();
                o += "-";
                switch (type)
                {
                    case Type.SlugBase:
                        o += player.Name;
                        break;
                    case Type.Vanilla:
                        o += Enum.GetName(typeof(SlugcatStats.Name), (SlugcatStats.Name)index);
                        break;
                }
                return o;
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="input"></param>
            /// <returns></returns>
            public static PlayerDescriptor FromString(string input)
            {
                try
                {
                    // Find type
                    int typeSplit = input.IndexOf('-');
                    if (typeSplit == -1) return survivorDesc;
                    Type t = Custom.ParseEnum<Type>(input.Substring(0, typeSplit));

                    // Fill data
                    switch (t)
                    {
                        case Type.Vanilla:
                            return new PlayerDescriptor((int)Custom.ParseEnum<SlugcatStats.Name>(input.Substring(typeSplit + 1)));
                        case Type.SlugBase:
                            {
                                SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(input.Substring(typeSplit + 1));
                                if (ply == null) return survivorDesc;
                                return new PlayerDescriptor(ply);
                            }
                        default:
                            return survivorDesc;
                    }
                }
                catch (Exception e)
                {
                    throw new ArgumentException("Failed to parse character descriptor string.", nameof(input), e);
                }
            }

            /// <summary>
            /// Gets a hash code for this character.
            /// </summary>
            /// <returns>A hash code representing this character.</returns>
            public override int GetHashCode()
            {
                switch (type)
                {
                    case Type.Vanilla: return index.GetHashCode();
                    case Type.SlugBase: return player.GetHashCode();
                    default: return base.GetHashCode();
                }
            }

            /// <summary>
            /// Tests whether this represents the same character as another object.
            /// </summary>
            /// <param name="obj">The <see cref="PlayerDescriptor"/> to compare to.</param>
            /// <returns>True if this and <paramref name="obj"/> represent the same character.</returns>
            public override bool Equals(object obj)
            {
                if (!(obj is PlayerDescriptor otherDesc)) return false;
                if (otherDesc.type != type) return false;
                switch (type)
                {
                    case Type.Vanilla: return otherDesc.index == index;
                    case Type.SlugBase: return otherDesc.player == player;
                }
                return base.Equals(obj);
            }

            /// <summary>
            /// Tests if two <see cref="PlayerDescriptor"/>s refer to the same character.
            /// </summary>
            public static bool operator ==(PlayerDescriptor a, PlayerDescriptor b)
            {
                return a is null ? b is null : a.Equals(b);
            }
            
            /// <summary>
            /// Tests if two <see cref="PlayerDescriptor"/>s do not refer to the same character.
            /// </summary>
            public static bool operator !=(PlayerDescriptor a, PlayerDescriptor b)
            {
                return !(a == b);
            }
        }
    }
}
