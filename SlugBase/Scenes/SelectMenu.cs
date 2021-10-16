using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using MonoMod.RuntimeDetour;
using System.Reflection;
using UnityEngine;
using System.IO;
using Menu;
using HUD;
using System.Runtime.CompilerServices;
using System.Linq;
using RWCustom;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SlugBase
{
    using static CustomSceneManager;

    internal static class SelectMenu
    {
        private static bool selectMenuShimActive = false;
        private static float altRestartUp = 0f;

        public static void ApplyHooks()
        {
            On.Menu.SlugcatSelectMenu.UpdateStartButtonText += SlugcatSelectMenu_UpdateStartButtonText;
            new Hook(
                typeof(SlugcatSelectMenu.SlugcatPageContinue).GetProperty("saveGameData", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(),
                typeof(SelectMenu).GetMethod(nameof(SlugcatPageContinue_get_saveGameData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            );
            On.Menu.SlugcatSelectMenu.GetSaveGameData += SlugcatSelectMenu_GetSaveGameData;
            On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
            On.Menu.SlugcatSelectMenu.Update += SlugcatSelectMenu_Update;
            On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
            On.Menu.HoldButton.ctor += HoldButton_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPage.AddImage += SlugcatPage_AddImage;
            On.Menu.SlugcatSelectMenu.SlugcatPage.ctor += SlugcatPage_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.ctor += SlugcatPageNewGame_ctor;
            On.HUD.KarmaMeter.Draw += KarmaMeter_Draw;
        }

        // Stop GetSaveGameData from inlining
        private static void SlugcatSelectMenu_UpdateStartButtonText(On.Menu.SlugcatSelectMenu.orig_UpdateStartButtonText orig, SlugcatSelectMenu self)
        {
            self.startButton.fillTime = (!self.restartChecked) ? 40f : 120f;
            if (self.saveGameData[self.slugcatPageIndex] == null || self.restartChecked)
                self.startButton.menuLabel.text = self.Translate("NEW GAME");
            else if (self.slugcatPages[self.slugcatPageIndex].slugcatNumber == 2 && self.redIsDead)
                self.startButton.menuLabel.text = self.Translate("STATISTICS");
            else
                self.startButton.menuLabel.text = self.Translate("CONTINUE");
        }

        // Stop GetSaveGameData from inlining
        private static SlugcatSelectMenu.SaveGameData SlugcatPageContinue_get_saveGameData(Func<SlugcatSelectMenu.SlugcatPageContinue, SlugcatSelectMenu.SaveGameData> orig, SlugcatSelectMenu.SlugcatPageContinue self)
        {
            if(self.menu is SlugcatSelectMenu ssm)
                return ssm.saveGameData[self.SlugcatPageIndex];
            else
                return orig(self);
        }

        // The original took a slugcat index as an input, indexed into slugcatColorOrder, and returned the save game at that slot
        // slugcatColorOrder is a list of slugcat indices, indexed by page number
        // Indexing into this list is not guaranteed to return the correct output, but it works for the vanilla slugcats
        private static SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_GetSaveGameData(On.Menu.SlugcatSelectMenu.orig_GetSaveGameData orig, SlugcatSelectMenu self, int slugcatColor)
        {
            int i = Array.IndexOf(self.slugcatColorOrder, slugcatColor);
            if (i < 0 || i >= self.saveGameData.Length) return null;
            return self.saveGameData[i];
        }

        // The select menu relies on a manifest of save information
        // In vanilla, this is either pulled from the current game or mined from the progression file
        // If the indicated slugcat is added by slugbase, instead mine from the custom save file
        private static SlugcatSelectMenu.SaveGameData SlugcatSelectMenu_MineForSaveData(On.Menu.SlugcatSelectMenu.orig_MineForSaveData orig, ProcessManager manager, int slugcat)
        {
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(slugcat);
            if(ply != null)
            {
                SaveState save = manager.rainWorld.progression.currentSaveState;
                if (save != null && save.saveStateNumber == slugcat)
                {
                    return new SlugcatSelectMenu.SaveGameData
                    {
                        karmaCap        = save.deathPersistentSaveData.karmaCap,
                        karma           = save.deathPersistentSaveData.karma,
                        karmaReinforced = save.deathPersistentSaveData.reinforcedKarma,
                        shelterName     = save.denPosition,
                        cycle           = save.cycleNumber,
                        hasGlow         = save.theGlow,
                        hasMark         = save.deathPersistentSaveData.theMark,
                        redsExtraCycles = save.redExtraCycles,
                        food            = save.food,
                        redsDeath       = save.deathPersistentSaveData.redsDeath,
                        ascended        = save.deathPersistentSaveData.ascended
                    };
                }
                return SaveManager.GetCustomSaveData(manager.rainWorld, ply.Name, manager.rainWorld.options.saveSlot);
            }
            return orig(manager, slugcat);
        }

        // Lock the select button when necessary
        private static void SlugcatSelectMenu_Update(On.Menu.SlugcatSelectMenu.orig_Update orig, SlugcatSelectMenu self)
        {
            // This is before orig because it must fetch the character before scrolling logic applies
            // Otherwise, there's a single frame where the button flashes
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(self.slugcatColorOrder[self.slugcatPageIndex]);

            if (ply == null)
            {
                altRestartUp = 0f;
                orig(self);
            }
            else
            {
                var state = ply.GetSelectMenuState(self);

                if (state == SlugBaseCharacter.SelectMenuAccessibility.MustRestart && !self.restartAvailable)
                {
                    altRestartUp = Mathf.Max(self.restartUp, Custom.LerpAndTick(altRestartUp, 1f, 0.07f, 0.025f));
                    self.restartUp = altRestartUp;
                    if (altRestartUp == 1f)
                        self.restartAvailable = true;
                }
                else
                    altRestartUp = 0f;

                orig(self);

                bool locked = false;
                switch (state)
                {
                    case SlugBaseCharacter.SelectMenuAccessibility.Locked: locked = true; break;
                    case SlugBaseCharacter.SelectMenuAccessibility.Hidden: locked = true; break;
                    case SlugBaseCharacter.SelectMenuAccessibility.MustRestart: locked = !self.restartChecked; break;
                }

                if (locked) self.startButton.GetButtonBehavior.greyedOut = true;

                
            }
        }

        // Change some data associated with custom slugcat pages
        private static void SlugcatPage_ctor(On.Menu.SlugcatSelectMenu.SlugcatPage.orig_ctor orig, SlugcatSelectMenu.SlugcatPage self, Menu.Menu menu, MenuObject owner, int pageIndex, int slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(pageIndex);
            if (ply != null) {
                self.colorName = ply.Name;
                self.effectColor = ply.SlugcatColor() ?? Color.white;
            }
        }

        // Override select scenes for SlugBase characters
        private static void SlugcatPage_AddImage(On.Menu.SlugcatSelectMenu.SlugcatPage.orig_AddImage orig, SlugcatSelectMenu.SlugcatPage self, bool ascended)
        {
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(self.slugcatNumber);
            
            // Do not modify scenes for any non-SlugBase slugcats
            if(ply == null)
            {
                orig(self, ascended);
                return;
            }

            // Use Survivor's default scenes on the select menu
            string sceneName = ascended ? "SelectMenuAscended" : "SelectMenu";
            if (!ply.HasScene(sceneName))
            {
                orig(self, ascended);
                
                // Fix the scene position being off
                if(self.sceneOffset == default(Vector2))
                    self.sceneOffset = new Vector2(-10f, 100f);
                
                // Fix the wrong scene loading in when ascended
                if (ascended && self.slugcatImage.sceneID == MenuScene.SceneID.Slugcat_White)
                {
                    self.slugcatImage.RemoveSprites();
                    self.RemoveSubObject(self.slugcatImage);

                    self.slugcatImage = new InteractiveMenuScene(self.menu, self, MenuScene.SceneID.Ghost_White);
                    self.subObjects.Add(self.slugcatImage);
                }

                return;
            }

            // Make sure it doesn't crash if the mark or glow is missing
            self.markSquare = new FSprite("pixel") { isVisible = false };
            self.markGlow = new FSprite("pixel") { isVisible = false };
            self.glowSpriteA = new FSprite("pixel") { isVisible = false };
            self.glowSpriteB = new FSprite("pixel") { isVisible = false };


            // This function intentionally does not call the original
            // If this mod has claimed a slot, it seems best to not let other mods try to change this screen

            // Taken from SlugcatPage.AddImage
            self.imagePos = new Vector2(683f, 484f);
            self.sceneOffset = new Vector2(0f, 0f);


            // Load a custom character's select screen from resources
            CustomScene scene = OverrideNextScene(ply, sceneName, img =>
            {
                if (img.HasTag("MARK") && !self.HasMark) return false;
                if (img.HasTag("GLOW") && !self.HasGlow) return false;
                return true;
            });

            // Parse selectmenux and selectmenuy
            self.sceneOffset.x = scene.GetProperty<float?>("selectmenux") ?? 0f;
            self.sceneOffset.y = scene.GetProperty<float?>("selectmenuy") ?? 0f;
            Debug.Log($"Scene offset for {ply.Name}: {self.sceneOffset}");

            // Slugcat depth, used for positioning the glow and mark
            self.slugcatDepth = scene.GetProperty<float?>("slugcatdepth") ?? 3f;

            // Add mark
            MarkImage mark = new MarkImage(scene, self.slugcatDepth + 0.1f);
            scene.InsertImage(mark);

            // Add glow
            GlowImage glow = new GlowImage(scene, self.slugcatDepth + 0.1f);
            scene.InsertImage(glow);

            try
            {
                self.slugcatImage = new InteractiveMenuScene(self.menu, self, MenuScene.SceneID.Slugcat_White); // This scene will be immediately overwritten
            } finally { ClearSceneOverride(); }
            self.subObjects.Add(self.slugcatImage);

            // Find the relative mark and glow positions
            self.markOffset = mark.Pos - new Vector2(self.MidXpos, self.imagePos.y + 150f) + self.sceneOffset;
            self.glowOffset = glow.Pos - new Vector2(self.MidXpos, self.imagePos.y) + self.sceneOffset;
        }

        // Add custom slugcat select screens
        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, SlugcatSelectMenu self, ProcessManager manager)
        {
            try
            {
                selectMenuShimActive = true;
                orig(self, manager);
            }
            finally
            {
                selectMenuShimActive = false;
            }
        }

        // Shim to add the slugcat pages at the correct layer
        // The hold button is the first object that needs to be layed above the scenes
        private static void HoldButton_ctor(On.Menu.HoldButton.orig_ctor orig, HoldButton self, Menu.Menu menu, MenuObject owner, string displayText, string singalText, Vector2 pos, float fillTime)
        {
            if (selectMenuShimActive && singalText == "START" && menu is SlugcatSelectMenu ssm)
            {
                AddSlugBaseScenes(ssm);
                selectMenuShimActive = false;
            }
            orig(self, menu, owner, displayText, singalText, pos, fillTime);
        }

        // Add all SlugBase characters to the select screen
        private static void AddSlugBaseScenes(SlugcatSelectMenu self)
        {
            int selectedSlugcat = self.manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;

            List<SlugBaseCharacter> plys = PlayerManager.customPlayers;
            List<SlugBaseCharacter> visiblePlys = plys.Where(c => c.GetSelectMenuState(self) != SlugBaseCharacter.SelectMenuAccessibility.Hidden).ToList();

            int origLength = self.slugcatColorOrder.Length;

            // First, try to find the highest taken slugcat index
            for (int i = 0; i < self.slugcatColorOrder.Length; i++)
                SlugBaseMod.FirstCustomIndex = Math.Max(self.slugcatColorOrder[i] + 1, SlugBaseMod.FirstCustomIndex);

            // Assign each character a unique index
            int nextCustomIndex = SlugBaseMod.FirstCustomIndex;
            for (int i = 0; i < plys.Count; i++)
                plys[i].SlugcatIndex = nextCustomIndex++;

            // Add SlugBase characters to the page order
            Array.Resize(ref self.slugcatColorOrder, origLength + visiblePlys.Count);
            for (int i = origLength; i < self.slugcatColorOrder.Length; i++)
                self.slugcatColorOrder[i] = visiblePlys[i - origLength].SlugcatIndex;

            // Retrieve save data
            Array.Resize(ref self.saveGameData, origLength + visiblePlys.Count);

            for (int i = 0; i < visiblePlys.Count; i++)
            {
                self.saveGameData[origLength + i] = SlugcatSelectMenu.MineForSaveData(self.manager, visiblePlys[i].SlugcatIndex);
            }

            // Add a new page to the menu
            Array.Resize(ref self.slugcatPages, origLength + visiblePlys.Count);

            for (int i = 0; i < visiblePlys.Count; i++)
            {
                int o = origLength + i;
                if (self.saveGameData[o] != null)
                {
                    self.slugcatPages[o] = new SlugcatSelectMenu.SlugcatPageContinue(self, null, o + 1, self.slugcatColorOrder[o]);
                }
                else
                {
                    self.slugcatPages[o] = new SlugcatSelectMenu.SlugcatPageNewGame(self, null, o + 1, self.slugcatColorOrder[o]);
                }

                // Select the correct page
                if (selectedSlugcat == self.slugcatColorOrder[o])
                    self.slugcatPageIndex = o;

                self.pages.Add(self.slugcatPages[o]);
            }
        }

        // Change select screen name and description
        private static void SlugcatPageNewGame_ctor(On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.orig_ctor orig, SlugcatSelectMenu.SlugcatPageNewGame self, Menu.Menu menu, MenuObject owner, int pageIndex, int slugcatNumber)
        {
            orig(self, menu, owner, pageIndex, slugcatNumber);

            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(slugcatNumber);
            if(ply != null)
            {
                self.difficultyLabel.text = ply.DisplayName.ToUpper();
                self.infoLabel.text = ply.Description.Replace("<LINE>", Environment.NewLine);
            }
        }

        // Fix position calculation of the karma meter to use timeStacker instead of timer
        // This results in a smooth animation
        private static void KarmaMeter_Draw(On.HUD.KarmaMeter.orig_Draw orig, KarmaMeter self, float timeStacker)
        {
            orig(self, timeStacker);
            if (self.hud?.owner?.GetOwnerType() == HUD.HUD.OwnerType.CharacterSelect)
            {
                if (self.karmaSprite == null) return;
                Vector2 pos = self.DrawPos(timeStacker);
                self.karmaSprite.x = pos.x;
                self.karmaSprite.y = pos.y;
                if (self.showAsReinforced)
                {
                    if (self.ringSprite != null)
                    {
                        self.ringSprite.x = pos.x;
                        self.ringSprite.y = pos.y;
                    }
                    if (self.vectorRingSprite != null)
                    {
                        self.vectorRingSprite.x = pos.x;
                        self.vectorRingSprite.y = pos.y;
                    }
                }
            }
        }

        private class MarkImage : SceneImage
        {
            public MarkImage(CustomScene owner, float depth) : base(owner)
            {
                Pos = new Vector2(owner.GetProperty<float?>("markx") ?? 683f, owner.GetProperty<float?>("marky") ?? 484f);
                Depth = depth;
            }

            public override string DisplayName => "MARK";

            public override bool ShouldBeSaved => false;

            protected internal override bool OnBuild(MenuScene scene)
            {
                if (!(scene.owner is SlugcatSelectMenu.SlugcatPage sp)) return false;

                if (sp.HasMark)
                {
                    sp.markSquare = new FSprite("pixel", true);
                    sp.markSquare.scale = 14f;
                    sp.markSquare.color = Color.Lerp(sp.effectColor, Color.white, 0.7f);
                    sp.Container.AddChild(sp.markSquare);
                    sp.markGlow = new FSprite("Futile_White", true);
                    sp.markGlow.shader = sp.menu.manager.rainWorld.Shaders["FlatLight"];
                    sp.markGlow.color = sp.effectColor;
                    sp.Container.AddChild(sp.markGlow);
                } else
                {
                    sp.markSquare = new FSprite("pixel") { isVisible = false };
                    sp.markGlow = new FSprite("pixel") { isVisible = false };
                }
                return false;
            }

            public override void OnSave(Dictionary<string, object> scene)
            {
                scene["markx"] = Pos.x;
                scene["marky"] = Pos.y;
            }
        }

        private class GlowImage : SceneImage
        {
            public GlowImage(CustomScene owner, float depth) : base(owner)
            {
                Pos = new Vector2(owner.GetProperty<float?>("glowx") ?? 683f, owner.GetProperty<float?>("glowy") ?? 484f);
                Debug.Log($"Glow is at {Pos}");
                Depth = depth;
            }

            public override string DisplayName => "GLOW";

            public override bool ShouldBeSaved => false;

            protected internal override bool OnBuild(MenuScene scene)
            {
                if (!(scene.owner is SlugcatSelectMenu.SlugcatPage sp)) return false;

                if (sp.HasMark)
                {
                    sp.glowSpriteB = new FSprite("Futile_White");
                    sp.glowSpriteB.shader = sp.menu.manager.rainWorld.Shaders["FlatLightNoisy"];
                    sp.Container.AddChild(sp.glowSpriteB);
                    sp.glowSpriteA = new FSprite("Futile_White");
                    sp.glowSpriteA.shader = sp.menu.manager.rainWorld.Shaders["FlatLightNoisy"];
                    sp.Container.AddChild(sp.glowSpriteA);
                } else
                {
                    sp.glowSpriteA = new FSprite("pixel") { isVisible = false };
                    sp.glowSpriteB = new FSprite("pixel") { isVisible = false };
                }

                return false;
            }

            public override void OnSave(Dictionary<string, object> scene)
            {
                scene["glowx"] = Pos.x;
                scene["glowy"] = Pos.y;
            }
        }
    }
}
