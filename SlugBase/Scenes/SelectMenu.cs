﻿using System;
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

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SlugBase
{
    using static CustomSceneManager;

    internal static class SelectMenu
    {
        public static void ApplyHooks()
        {
            On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
            On.Menu.SlugcatSelectMenu.UpdateStartButtonText += SlugcatSelectMenu_UpdateStartButtonText;
            new Hook(
                typeof(SlugcatSelectMenu.SlugcatPageContinue).GetProperty("saveGameData", BindingFlags.Public | BindingFlags.Instance).GetGetMethod(),
                typeof(SelectMenu).GetMethod(nameof(SlugcatPageContinue_get_saveGameData), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            );
            On.Menu.SlugcatSelectMenu.GetSaveGameData += SlugcatSelectMenu_GetSaveGameData;
            On.Menu.SlugcatSelectMenu.MineForSaveData += SlugcatSelectMenu_MineForSaveData;
            On.Menu.SlugcatSelectMenu.ctor += SlugcatSelectMenu_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPage.AddImage += SlugcatPage_AddImage;
            On.Menu.SlugcatSelectMenu.SlugcatPage.ctor += SlugcatPage_ctor;
            On.Menu.SlugcatSelectMenu.SlugcatPageNewGame.ctor += SlugcatPageNewGame_ctor;
            On.HUD.KarmaMeter.Draw += KarmaMeter_Draw;
        }

        // Call Prepare on the character that is about to start
        // This should give the most time possible to respond before the game starts
        // A mod could change which CRS regions are active, or start loading something
        // ... in another thread, the join it in Enable
        private static void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, SlugcatSelectMenu self, int storyGameCharacter)
        {
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(storyGameCharacter);
            if(ply != null)
                ply.Prepare();
            orig(self, storyGameCharacter);
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
                int slot = manager.rainWorld.options.saveSlot;
                return SaveManager.GetCustomSaveData(manager.rainWorld, ply.Name, slot);
            }
            return orig(manager, slugcat);
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
        private static void SlugcatSelectMenu_ctor(On.Menu.SlugcatSelectMenu.orig_ctor orig, Menu.SlugcatSelectMenu self, ProcessManager manager)
        {
            int selectedSlugcat = manager.rainWorld.progression.miscProgressionData.currentlySelectedSinglePlayerSlugcat;

            orig(self, manager);

            List<SlugBaseCharacter> plys = PlayerManager.customPlayers;
            int origLength = self.slugcatColorOrder.Length;

            // Add all SlugBase characters to the select screen
            // All other player mods should change this array, so we have a nice lower bound for indices we can take

            // Find the next available slugcat index, skipping Nightcat
            int firstCustomIndex = 4;
            
            // Take color order into account
            for (int i = 0; i < self.slugcatColorOrder.Length; i++)
                firstCustomIndex = Math.Max(self.slugcatColorOrder[i] + 1, firstCustomIndex);
            
            // Take slugcat names into account
            foreach(SlugcatStats.Name name in Enum.GetValues(typeof(SlugcatStats.Name)))
                firstCustomIndex = Math.Max((int)name + 1, firstCustomIndex);

            int nextCustomIndex = firstCustomIndex;

            // Add SlugBase characters to the page order and assign empty slots a default value
            Array.Resize(ref self.slugcatColorOrder, origLength + plys.Count);
            for(int i = origLength; i < self.slugcatColorOrder.Length; i++)
                self.slugcatColorOrder[i] = -1;

            for (int i = 0; i < plys.Count; i++)
            {
                // Assign each player a unique index, then save it to the page order
                // This will cause weird behavior if the user skips over the title screen using EDT, so... don't do that
                self.slugcatColorOrder[origLength + i] = nextCustomIndex;
                plys[i].slugcatIndex = nextCustomIndex++;
            }

            // Retrieve save data
            Array.Resize(ref self.saveGameData, origLength + plys.Count);

            for(int i = 0; i < plys.Count; i++)
            {
                self.saveGameData[origLength + i] = SlugcatSelectMenu.MineForSaveData(self.manager, plys[i].slugcatIndex);
            }

            // Add a new page to the menu
            Array.Resize(ref self.slugcatPages, origLength + plys.Count);

            for(int i = 0; i < plys.Count; i++)
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

                // Make sure the start button reflects the changed slugcat index
                self.UpdateStartButtonText();
                self.UpdateSelectedSlugcatInMiscProg();
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
