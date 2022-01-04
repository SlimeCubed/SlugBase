using System;
using System.Collections.Generic;
using Menu;
using RWCustom;
using UnityEngine;
using System.Linq;
using MenuColors = Menu.Menu.MenuColors;
using PlayerDescriptor = SlugBase.ArenaAdditions.PlayerDescriptor;

namespace SlugBase.Config
{
    /// <summary>
    /// A menu object that selects from all available player characters.
    /// </summary>
    public class CharacterSelectButton : ButtonTemplate
    {
        const float selectorHeight = 30f;
        const float buttonHeight = 22f;
        const float buttonMargin = 1f;
        const float buttonXOffset = 3f;

        private float childOffset;
        private bool expand;
        private readonly int playerNumber;
        private readonly RoundedRect back;
        private readonly RoundedRect selectRect;
        private readonly List<CharacterLabel> labels;
        private readonly float expandedHeight;

        /// <summary>
        /// The <see cref="CharacterSelectGroup"/> this button belongs to.
        /// </summary>
        public CharacterSelectGroup Group { get; }

        /// <summary>
        /// The character this player has selected.
        /// </summary>
        public PlayerDescriptor SelectedCharacter => Group.GetPlayer(playerNumber);

        /// <summary>
        /// True if the button's menu is opening or fully open, false otherwise.
        /// </summary>
        public bool Active => expand;

        /// <summary>
        /// True if the button's menu is open or in its opening or closing animation.
        /// </summary>
        /// <remarks>
        /// Menu objects that could be covered by this button's menu should be disabled while this is true.
        /// </remarks>
        public bool Expanded => expand || size.y > selectorHeight;

        private int SelectedLabel
        {
            get
            {
                var character = Group.GetPlayer(playerNumber);
                return Math.Max(labels.FindIndex(l => l.player == character), 0);
            }
        }

        /// <summary>
        /// Creates a new character select button.
        /// </summary>
        /// <param name="menu">The menu this button belongs to.</param>
        /// <param name="owner">The menu object this button is a sub-object of.</param>
        /// <param name="pos">The position of this button relative to its <paramref name="owner"/>.</param>
        /// <param name="width">The width of this button in pixels.</param>
        /// <param name="group">The <see cref="CharacterSelectGroup"/> this button belongs to. <see langword="null"/> may be passed in to create a new group.</param>
        /// <param name="playerNumber">The player number this button should display.</param>
        public CharacterSelectButton(Menu.Menu menu, MenuObject owner, Vector2 pos, float width, CharacterSelectGroup group, int playerNumber) : base(menu, owner, pos, new Vector2(width, selectorHeight))
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            this.playerNumber = playerNumber;
            Group = group ?? new CharacterSelectGroup();

            back = new RoundedRect(menu, this, new Vector2(), size, true);
            subObjects.Add(back);
            selectRect = new RoundedRect(menu, this, new Vector2(), size, false);
            subObjects.Add(selectRect);

            labels = new List<CharacterLabel>();

            float y = 0f;
            int i = 0;

            // Add non-SlugBase slugcats
            foreach(var name in Enum.GetValues(typeof(SlugcatStats.Name)))
            {
                if (PlayerManager.GetCustomPlayer((int)name) != null) continue;

                var label = new CharacterLabel(menu, this, new PlayerDescriptor((int)name), i++);
                labels.Add(label);
                y += buttonHeight;
            }

            // Add SlugBase slugcats
            foreach (var cha in PlayerManager.GetCustomPlayers())
            {
                Color baseColor = PlayerGraphics.SlugcatColor(cha.SlugcatIndex);
                var label = new CharacterLabel(menu, this, new PlayerDescriptor(cha), i++);
                labels.Add(label);
                y += buttonHeight;
            }

            subObjects.AddRange(labels.Select(l => (MenuObject)l));

            // This sets the height to fit the buttons exactly
            expandedHeight = labels.Count * buttonHeight + selectorHeight - buttonHeight;
        }

        /// <summary>
        /// Executed by the menu system when this button is clicked. This should not be called normally.
        /// </summary>
        public override void Clicked()
        {
            base.Clicked();

            if (!expand)
            {
                expand = true;
                menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
            }
        }

        /// <summary>
        /// Executed by the menu system when a signal is sent from a parent object. This should not be called normally.
        /// </summary>
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

        /// <summary>
        /// Executed by the menu system every fixed-timestep update. This should not be called normally.
        /// </summary>
        public override void Update()
        {
            // Updating the children before updaing the size of this element is important to keep them in sync
            // Without it, the children are delayed by a small amount
            Vector2 nextSize = size;
            nextSize.y = Custom.LerpAndTick(nextSize.y, expand ? expandedHeight : selectorHeight, 0.1f, 10f);

            back.fillAlpha = Custom.LerpMap(nextSize.y, selectorHeight, 35f, Mathf.Lerp(0.3f, 0.6f, buttonBehav.col), 1f);
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

            float targetOffset = (selectorHeight - buttonHeight) / 2f - (expand ? 0f : SelectedLabel * buttonHeight);
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
            buttonBehav.greyedOut = size.y > selectorHeight;

            // Stop the selector from going offscreen
            float heightAboveScreen = ScreenPos.y + size.y - Futile.screen.height;
            if (heightAboveScreen > 0f)
                pos.y -= heightAboveScreen;
        }

        /// <summary>
        /// Executed by the menu system every frame. This should not be called normally.
        /// </summary>
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

        /// <summary>
        /// Used by the menu system to determine what color this button should be. This should not be called normally.
        /// </summary>
        public override Color MyColor(float timeStacker)
        {
            if (expand || buttonBehav.greyedOut)
                return Menu.Menu.MenuRGB(MenuColors.MediumGrey);
            else
                return base.MyColor(timeStacker);
        }

        // An icon and name label
        private class CharacterLabel : ButtonTemplate
        {
            public readonly PlayerDescriptor player;
            private readonly int order;
            private readonly CreatureSymbol icon;
            private readonly FLabel name;
            private CharacterSelectButton Owner => (CharacterSelectButton)owner;

            public CharacterLabel(Menu.Menu menu, CharacterSelectButton owner, PlayerDescriptor player, int order) : base(menu, owner, new Vector2(), new Vector2(owner.size.x, buttonHeight - buttonMargin * 2f))
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

                Owner.Group.SetPlayer(Owner.playerNumber, player);

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
}
