using System;
using System.IO;
using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;
using System.Linq;
using MonoMod.RuntimeDetour;
using MenuColors = Menu.Menu.MenuColors;

namespace SlugBase
{
    /// <summary>
    /// Controls added arena mode settings and functionality.
    /// </summary>
    public static class ArenaAdditions
    {
        internal static readonly AttachedField<ArenaSetup, List<PlayerDescriptor>> arenaCharacters = new AttachedField<ArenaSetup, List<PlayerDescriptor>>();
        private static readonly AttachedField<AbstractCreature, SlugcatStats> arenaStats = new AttachedField<AbstractCreature, SlugcatStats>();

        private static readonly PlayerDescriptor survivorDesc = new PlayerDescriptor(0);
        private static string arenaSettingsPath;


        internal static void ApplyHooks()
        {
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
                && arenaCharacters.TryGet(setup, out var characters)
                && characters?.Count > 0)
            {
                PlayerDescriptor selectedChar = null;
                for (int i = 0; i < setup.playersJoined.Length; i++)
                {
                    if (!setup.playersJoined[i]) continue;

                    if (selectedChar == null)
                    {
                        if (i >= characters.Count) break;
                        selectedChar = characters[i];
                    }
                    else
                    {
                        if (i >= characters.Count || !selectedChar.Equals(characters[i]))
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
            if (setup == null
                || !arenaCharacters.TryGet(setup, out var characters)
                || characters == null
                || playerNumber < 0 || playerNumber >= characters.Count)
                return GetSelectedArenaCharacter(setup);

            return characters[playerNumber];
        }

        #region Hooks

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
                File.WriteAllLines(arenaSettingsPath, characters.Select(c => c.ToString()).ToArray());
            else
                File.Delete(arenaSettingsPath);
        }

        private static void ArenaSetup_LoadFromFile(On.ArenaSetup.orig_LoadFromFile orig, ArenaSetup self)
        {
            orig(self);
            try
            {
                var characters = arenaCharacters[self] = new List<PlayerDescriptor>();

                foreach(var line in File.ReadAllLines(arenaSettingsPath))
                    characters.Add(PlayerDescriptor.FromString(line));
            }
            catch(Exception e)
            {
                Debug.LogException(new FormatException("Invalid arena settings format. This error is not fatal.", e));
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
            self.pages[0].subObjects.Add(new PlayerSelector(self, self.pages[0]));
        }

        #endregion Hooks

        // The list of name plates
        internal class PlayerSelector : MenuObject
        {
            private static readonly Vector2 namePlateOffset = new Vector2(0f, -37f);
            const float namePlateHeight = 30f;
            const float buttonHeight = 22f;
            const float buttonMargin = 1f;
            const float buttonXOffset = 3f;

            private readonly PlayerNamePlate[] namePlates;

            public PlayerSelector(MultiplayerMenu menu, MenuObject owner) : base(menu, owner)
            {
                namePlates = new PlayerNamePlate[menu.playerJoinButtons.Length];
                for(int i = 0; i < namePlates.Length; i++)
                {
                    namePlates[i] = new PlayerNamePlate(menu, this, menu.playerJoinButtons[i], i);
                    subObjects.Add(namePlates[i]);
                }
            }

            // An individual name plate made up of a box and many player labels
            private class PlayerNamePlate : ButtonTemplate
            {
                public float childOffset;
                public bool expand;
                public readonly int playerNumber;
                private readonly RoundedRect back;
                private readonly RoundedRect selectRect;
                private readonly List<PlayerLabel> labels;
                private readonly float expandedHeight;
                private PlayerJoinButton portrait;
                private int SelectedLabel
                {
                    get
                    {
                        if (arenaCharacters.TryGet(menu.manager.arenaSetup, out var characters)
                            && playerNumber >= 0f && playerNumber < characters.Count)
                        {
                            var cha = characters[playerNumber];
                            return Math.Max(labels.FindIndex(l => l.player.Equals(cha)), 0);
                        }
                        else
                            return 0;
                    }
                }

                public PlayerNamePlate(Menu.Menu menu, PlayerSelector owner, PlayerJoinButton portrait, int playerNumber) : base(menu, owner, portrait.pos + namePlateOffset, new Vector2(portrait.size.x, namePlateHeight))
                {
                    this.portrait = portrait;
                    this.playerNumber = playerNumber;

                    back = new RoundedRect(menu, this, new Vector2(), size, true);
                    subObjects.Add(back);
                    selectRect = new RoundedRect(menu, this, new Vector2(), size, false);
                    subObjects.Add(selectRect);

                    labels = new List<PlayerLabel>();

                    float y = 0f;
                    int i = 0;

                    // Add non-SlugBase slugcats
                    foreach(var name in Enum.GetValues(typeof(SlugcatStats.Name)))
                    {
                        if (PlayerManager.GetCustomPlayer((int)name) != null) continue;

                        var label = new PlayerLabel(menu, this, new PlayerDescriptor((int)name), i++);
                        labels.Add(label);
                        y += buttonHeight;
                    }

                    // Add SlugBase slugcats
                    foreach (var cha in PlayerManager.GetCustomPlayers())
                    {
                        Color baseColor = PlayerGraphics.SlugcatColor(cha.SlugcatIndex);
                        var label = new PlayerLabel(menu, this, new PlayerDescriptor(cha), i++);
                        labels.Add(label);
                        y += buttonHeight;
                    }

                    subObjects.AddRange(labels.Select(l => (MenuObject)l));

                    // This sets the minimum height to cover the portrait entirely
                    //expandedHeight = Mathf.Max(portrait.pos.y + portrait.size.y - pos.y, labels.Count * buttonHeight + namePlateHeight - buttonHeight);

                    // This sets the height to fit the buttons exactly
                    expandedHeight = labels.Count * buttonHeight + namePlateHeight - buttonHeight;
                }

                public override void Clicked()
                {
                    base.Clicked();

                    if (!expand)
                    {
                        expand = true;
                        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
                    }
                }

                public override void Singal(MenuObject sender, string message)
                {
                    switch(message)
                    {
                        case "SELECT":
                            expand = !expand;
                            break;
                        default:
                            base.Singal(sender, message);
                            break;
                    }
                }

                public override void Update()
                {
                    // Updating the children before updaing the size of this element is important to keep them in sync
                    // Without it, the children are delayed by a small amount
                    Vector2 nextSize = size;
                    nextSize.y = Custom.LerpAndTick(nextSize.y, expand ? expandedHeight : namePlateHeight, 0.1f, 10f);

                    back.fillAlpha = Custom.LerpMap(nextSize.y, namePlateHeight, -namePlateOffset.y, Mathf.Lerp(0.3f, 0.6f, buttonBehav.col), 1f);
                    if (buttonBehav.clicked)
                    {
                        back.addSize = new Vector2(0f, 0f);
                        selectRect.addSize = new Vector2(0f, 0f);
                    }
                    else
                    {
                        back.addSize = new Vector2(10f, 6f) * (buttonBehav.sizeBump + 0.5f * Mathf.Sin(buttonBehav.extraSizeBump * Mathf.PI));
                        selectRect.addSize = new Vector2(2f, -2f) * (buttonBehav.sizeBump + 0.5f * Mathf.Sin(buttonBehav.extraSizeBump * Mathf.PI));
                    }


                    float deltaY = nextSize.y - size.y;

                    float targetOffset = (namePlateHeight - buttonHeight) / 2f - (expand ? 0f : SelectedLabel * buttonHeight);
                    if(Mathf.Sign(deltaY) == Mathf.Sign(targetOffset - childOffset) && deltaY != 0f)
                    {
                        childOffset = Mathf.MoveTowards(childOffset, targetOffset, Mathf.Abs(deltaY));
                    }
                    else
                    {
                        childOffset = targetOffset;
                    }

                    base.Update();

                    size = nextSize;
                    back.size = size;
                    selectRect.size = size;
                    portrait.buttonBehav.greyedOut = expand || size.y > -namePlateOffset.y;
                    buttonBehav.greyedOut = size.y > namePlateHeight;

                    // Stop the selector from going offscreen
                    pos = portrait.pos + namePlateOffset;
                    float heightAboveScreen = pos.y + size.y - Futile.screen.height;
                    if (heightAboveScreen > 0f)
                        pos.y -= heightAboveScreen;
                }

                public override void GrafUpdate(float timeStacker)
                {
                    base.GrafUpdate(timeStacker);
                    
                    Color fillColor = Color.Lerp(Menu.Menu.MenuRGB(MenuColors.Black), Menu.Menu.MenuRGB(MenuColors.White), Mathf.Lerp(buttonBehav.lastFlash, buttonBehav.flash, timeStacker));
                    for (int i = 0; i < 9; i++)
                    {
                        back.sprites[i].color = fillColor;
                    }
                    float selectAlpha = 0.5f + 0.5f * Mathf.Sin(Mathf.Lerp(buttonBehav.lastSin, buttonBehav.sin, timeStacker) / 30f * Mathf.PI * 2f);
                    selectAlpha *= buttonBehav.sizeBump;

                    for (int j = 0; j < 8; j++)
                    {
                        selectRect.sprites[j].color = MyColor(timeStacker);
                        selectRect.sprites[j].alpha = selectAlpha;
                    }
                }

                public override Color MyColor(float timeStacker)
                {
                    if (expand || buttonBehav.greyedOut)
                        return Menu.Menu.MenuRGB(MenuColors.MediumGrey);
                    else
                        return base.MyColor(timeStacker);
                }
            }

            // An icon and name label
            private class PlayerLabel : ButtonTemplate
            {
                public readonly PlayerDescriptor player;
                private readonly int order;
                private readonly CreatureSymbol icon;
                private readonly FLabel name;
                private PlayerNamePlate Owner => (PlayerNamePlate)owner;

                public PlayerLabel(Menu.Menu menu, PlayerNamePlate owner, PlayerDescriptor player, int order) : base(menu, owner, new Vector2(), new Vector2(owner.size.x, buttonHeight - buttonMargin * 2f))
                {
                    this.player = player;
                    this.order = order;

                    icon = new CreatureSymbol(MultiplayerUnlocks.SymbolDataForSandboxUnlock(MultiplayerUnlocks.SandboxUnlockID.Slugcat), Container);
                    icon.Show(false);
                    icon.lastShowFlash = 0f;
                    icon.showFlash = 0f;
                    icon.myColor = player.Color;

                    // Remove "The" from the start of names
                    var prettyName = player.name;
                    if (prettyName.StartsWith("The ", StringComparison.OrdinalIgnoreCase))
                        prettyName = prettyName.Substring(4).TrimStart();

                    name = new FLabel("font", prettyName);
                    name.anchorX = 0f;
                    name.anchorY = 0.5f;
                    Container.AddChild(name);
                }

                public override void RemoveSprites()
                {
                    base.RemoveSprites();

                    icon.RemoveSprites();
                    name.RemoveFromContainer();
                }

                public override void Update()
                {
                    base.Update();
                    icon.Update();

                    pos.x = buttonMargin;
                    pos.y = buttonMargin + buttonHeight * order + Owner.childOffset;
                    buttonBehav.greyedOut = pos.y < 0f || pos.y + size.y > Owner.size.y;
                }

                public override void Clicked()
                {
                    base.Clicked();

                    if (!arenaCharacters.TryGet(menu.manager.arenaSetup, out var characters))
                        arenaCharacters[menu.manager.arenaSetup] = characters = new List<PlayerDescriptor>();

                    if(player.type == PlayerDescriptor.Type.SlugBase && !player.player.MultiInstance)
                    {
                        // For single-instance characters, replace all other selections
                        while (characters.Count < 4)
                            characters.Add(survivorDesc);
                        for (int i = 0; i < characters.Count; i++)
                            characters[i] = new PlayerDescriptor(player.player);
                    }
                    else
                    {
                        // For multi-instance characters, replace all other single-instance selections
                        for(int i = 0; i < characters.Count; i++)
                        {
                            if(characters[i].type == PlayerDescriptor.Type.SlugBase && !characters[i].player.MultiInstance)
                            {
                                characters[i] = player;
                            }
                        }

                        while (characters.Count <= Owner.playerNumber)
                            characters.Add(survivorDesc);

                        characters[Owner.playerNumber] = player;
                    }

                    menu.PlaySound(SoundID.MENU_Button_Successfully_Assigned);

                    Singal(this, "SELECT");
                }

                public override void GrafUpdate(float timeStacker)
                {
                    base.GrafUpdate(timeStacker);

                    var drawPos = DrawPos(timeStacker) + new Vector2(0.1f, 0.1f);
                    var relDrawPos = Vector2.Lerp(lastPos, pos, timeStacker);
                    var drawSize = DrawSize(timeStacker);

                    var iconPos = drawPos;
                    iconPos.x += buttonXOffset;
                    iconPos.y += drawSize.y / 2f;
                    iconPos.x += drawSize.y / 2f;
                    icon.Draw(timeStacker, iconPos);

                    name.SetPosition(iconPos + new Vector2(drawSize.y / 2f + 5f, 0f));

                    bool visible = relDrawPos.y >= 0f && relDrawPos.y + drawSize.y <= Owner.DrawSize(timeStacker).y;
                    icon.symbolSprite.isVisible = visible;
                    name.isVisible = visible;
                    name.color = Color.Lerp(Menu.Menu.MenuColor(MenuColors.MediumGrey).rgb, Color.white, Mathf.Lerp(buttonBehav.lastCol, buttonBehav.col, timeStacker));
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
        }
    }
}
