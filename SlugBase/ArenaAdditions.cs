using System;
using System.IO;
using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;

namespace SlugBase
{
    /// <summary>
    /// Controls added arena mode settings and functionality.
    /// </summary>
    public static class ArenaAdditions
    {
        internal static AttachedField<ArenaSetup, PlayerDescriptor> arenaCharacter = new AttachedField<ArenaSetup, PlayerDescriptor>();

        private static string arenaSettingsPath;

        internal static void ApplyHooks()
        {
            On.Menu.MultiplayerMenu.Singal += MultiplayerMenu_Singal;
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
            if (setup == null) return new PlayerDescriptor(0);
            return arenaCharacter.TryGet(setup, out PlayerDescriptor ret) ? ret : new PlayerDescriptor(0);
        }

        #region Hooks

        private static void MultiplayerMenu_Singal(On.Menu.MultiplayerMenu.orig_Singal orig, MultiplayerMenu self, MenuObject sender, string message)
        {
            orig(self, sender, message);
            if(self.manager.upcomingProcess == ProcessManager.ProcessID.Game && arenaCharacter.TryGet(self.GetArenaSetup, out var ply))
            {
                if(ply.type == PlayerDescriptor.Type.SlugBase)
                    ply.player.Prepare();
            }
        }

        private static void ArenaSetup_SaveToFile(On.ArenaSetup.orig_SaveToFile orig, ArenaSetup self)
        {
            orig(self);
            if (arenaCharacter.TryGet(self, out var ply))
                File.WriteAllText(arenaSettingsPath, ply.ToString());
            else
                File.Delete(arenaSettingsPath);
        }

        private static void ArenaSetup_LoadFromFile(On.ArenaSetup.orig_LoadFromFile orig, ArenaSetup self)
        {
            orig(self);
            try
            {
                arenaCharacter[self] = PlayerDescriptor.FromString(File.ReadAllText(arenaSettingsPath));
            } catch(Exception e)
            {
                Debug.LogException(new FormatException("Invalid arena settings format. This error is not fatal.", e));
                arenaCharacter.Unset(self);
            }
        }

        // Change slugcat stats
        private static void ArenaGameSession_ctor(On.ArenaGameSession.orig_ctor orig, ArenaGameSession self, RainWorldGame game)
        {
            orig(self, game);

            if (arenaCharacter.TryGet(self.game.manager.arenaSetup, out var ply))
            {
                switch (ply.type)
                {
                    case PlayerDescriptor.Type.SlugBase:
                        ply.player.GetStatsInternal(self.characterStats);
                        break;
                    case PlayerDescriptor.Type.Vanilla:
                        if (ply.index != 0)
                            self.characterStats = new SlugcatStats(ply.index, false);
                        break;
                }
            }
        }

        // Add a player selector
        private static void MultiplayerMenu_ctor(On.Menu.MultiplayerMenu.orig_ctor orig, MultiplayerMenu self, ProcessManager manager)
        {
            orig(self, manager);

            // Place relative to the first join button
            self.pages[0].subObjects.Add(new PlayerSelector(self, self.pages[0], self.playerJoinButtons[0].pos + new Vector2(703f - 706f, 441f - 480f)));
        }

        #endregion Hooks

        // The list of selector icons
        internal class PlayerSelector : RectangularMenuObject
        {
            private const float spacing = 3f;
            private const float height = 31f;
            private const float width = 460f;

            public float scroll;
            public float scrollTarget;
            public List<PlayerButton> buttons;

            public PlayerSelector(Menu.Menu menu, MenuObject owner, Vector2 pos) : base(menu, owner, pos, new Vector2(0f, 0f))
            {
                buttons = new List<PlayerButton>();

                // Non-SlugBase slugcats
                foreach(SlugcatStats.Name name in Enum.GetValues(typeof(SlugcatStats.Name)))
                {
                    buttons.Add(new PlayerButton(this, new PlayerDescriptor((int)name), new Vector2()));
                }

                // SlugBase slugcats
                foreach(SlugBaseCharacter player in PlayerManager.customPlayers)
                {
                    buttons.Add(new PlayerButton(this, new PlayerDescriptor(player), new Vector2()));
                }

                foreach (PlayerButton button in buttons)
                    subObjects.Add(button);

                // Select one of the buttons to start with
                if (arenaCharacter.TryGet(menu.manager.arenaSetup, out var ply))
                {
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        if (buttons[i].player.Equals(ply))
                        {
                            buttons[i].SetSelected(true);
                            break;
                        }
                    }
                } else
                {
                    arenaCharacter[menu.manager.arenaSetup] = new PlayerDescriptor(0);
                    buttons[0].SetSelected(true);
                }
            }

            public override void Update()
            {
                base.Update();

                scroll = Custom.LerpAndTick(scroll, scrollTarget, 0.2f, 1f);
                scrollTarget = Mathf.Max(scrollTarget, 0f);
                scroll = Mathf.Max(scroll, 0f);

                float pos = -scroll;
                for(int i = 0; i < buttons.Count; i++)
                {
                    buttons[i].Move(pos);
                    if(buttons[i].playerSelected)
                    {
                        // Keep the selected button from going off of the right side of the selector
                        scrollTarget = -Mathf.Max(-scrollTarget, pos + buttons[i].size.x - width);
                        // ... and the left side too
                        scrollTarget = -Mathf.Min(-scrollTarget, pos);
                    }
                    pos += buttons[i].size.x + spacing;
                }
            }

            public override void GrafUpdate(float timeStacker)
            {
                base.GrafUpdate(timeStacker);
            }

            public void CloseAll()
            {
                foreach(PlayerButton button in buttons)
                {
                    button.SetSelected(false);
                }
            }

            // A selector icon
            internal class PlayerButton : ButtonTemplate
            {
                public bool playerSelected;
                public PlayerDescriptor player;

                private RoundedRect back;
                private CreatureSymbol icon;
                private FLabel name;
                private bool snap;

                private PlayerSelector Selector => (PlayerSelector)owner;

                public PlayerButton(PlayerSelector owner, PlayerDescriptor player, Vector2 pos) : base(owner.menu, owner, pos, new Vector2(height, height))
                {
                    this.player = player;

                    back = new RoundedRect(menu, this, new Vector2(), size, true);
                    back.addSize = new Vector2(-2f, -2f);
                    subObjects.Add(back);

                    icon = new CreatureSymbol(MultiplayerUnlocks.SymbolDataForSandboxUnlock(MultiplayerUnlocks.SandboxUnlockID.Slugcat), Container);
                    icon.Show(false);
                    icon.showFlash = 0f;
                    icon.showFlash = 0f;
                    icon.myColor = player.Color;

                    name = new FLabel("font", player.name);
                    name.anchorX = 0f;
                    name.anchorY = 0.5f;
                    Container.AddChild(name);

                    // Don't animate when first showing the screen
                    snap = true;
                }

                public override void RemoveSprites()
                {
                    base.RemoveSprites();

                    icon.RemoveSprites();
                    name.RemoveFromContainer();
                }

                public void SetSelected(bool selected)
                {
                    playerSelected = selected;
                }

                public override void Clicked()
                {
                    base.Clicked();

                    menu.PlaySound(SoundID.MENU_Checkbox_Check);

                    Selector.CloseAll();
                    SetSelected(true);
                    arenaCharacter[menu.manager.arenaSetup] = player;
                }

                public override void Update()
                {
                    base.Update();
                    icon.Update();

                    //if (Selected && !playerSelected)
                    //{
                    //    Selector.CloseAll();
                    //    SetSelected(true);
                    //}

                    // Change button width to include the name when selected
                    float targetWidth = height;
                    if (playerSelected)
                        targetWidth = height + 15f + name.textRect.width;
                    size.x = Custom.LerpAndTick(size.x, targetWidth, 0.2f, 2f);

                    if(snap)
                    {
                        snap = false;
                        size.x = targetWidth;
                    }

                    // Change button size a little when selected
                    back.addSize.x = Mathf.Clamp(back.addSize.x + (playerSelected ? 0.5f : -0.5f), -1f, 1f);
                    back.addSize.y = back.addSize.x;

                    // Update name alpha
                    float nameAlpha = name.alpha;
                    if (playerSelected && Mathf.Abs(size.x - targetWidth) <= 7f)
                        nameAlpha = Custom.LerpAndTick(nameAlpha, 1, 0.25f, 0.1f);
                    else
                        nameAlpha = 0f;

                    if (name.alpha != nameAlpha) name.alpha = nameAlpha;

                    back.size = size;
                }

                public void Move(float offset)
                {
                    pos = new Vector2(offset, 0f);
                }

                public override void GrafUpdate(float timeStacker)
                {
                    base.GrafUpdate(timeStacker);

                    Vector2 drawPos = DrawPos(timeStacker);
                    icon.Draw(timeStacker, drawPos + new Vector2(height, height) / 2f);
                    name.x = drawPos.x + height + 5f + 0.1f;
                    name.y = drawPos.y + height / 2f + 0.1f;
                }
            }
        }

        // Indices won't be assigned yet, so just an int isn't enough
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
            /// The color of this character, as gotten with <see cref="PlayerGraphics.SlugcatColor(int)"/>.
            /// </summary>
            public Color Color
            {
                get
                {
                    switch (type)
                    {
                        case Type.SlugBase:
                            return player.SlugcatColor() ?? PlayerGraphics.SlugcatColor(0);
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
                    if (typeSplit == -1) return new PlayerDescriptor(0);
                    Type t = Custom.ParseEnum<Type>(input.Substring(0, typeSplit));

                    // Fill data
                    switch (t)
                    {
                        case Type.Vanilla:
                            return new PlayerDescriptor((int)Custom.ParseEnum<SlugcatStats.Name>(input.Substring(typeSplit + 1)));
                        case Type.SlugBase:
                            {
                                SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(input.Substring(typeSplit + 1));
                                if (ply == null) return new PlayerDescriptor(0);
                                return new PlayerDescriptor(ply);
                            }
                        default:
                            return new PlayerDescriptor(0);
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
        }
    }
}
