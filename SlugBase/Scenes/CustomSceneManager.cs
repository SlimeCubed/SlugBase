﻿using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Menu;
using RWCustom;

namespace SlugBase
{
    internal static class CustomSceneManager
    {
        // Passed in as a folder to some functions to mark that they should load a custom resource instead
        internal const string resourceFolderName = "SlugBase Resources";

        private static ResourceOverride<CustomScene> sceneOverride;
        private static ResourceOverride<CustomSlideshow> slideshowOverride;

        internal static AttachedField<MenuIllustration, SceneImage> customRep = new AttachedField<MenuIllustration, SceneImage>();
        internal static AttachedField<MenuScene, CustomScene> customSceneRep = new AttachedField<MenuScene, CustomScene>();
        internal static AttachedField<SlideShow, CustomSlideshow> customSlideshowRep = new AttachedField<SlideShow, CustomSlideshow>();

        internal static void ApplyHooks()
        {
            On.Menu.SlideShowMenuScene.ApplySceneSpecificAlphas += SlideShowMenuScene_ApplySceneSpecificAlphas;
            On.RainWorldGame.ExitToVoidSeaSlideShow += RainWorldGame_ExitToVoidSeaSlideShow;
            On.Menu.SlugcatSelectMenu.StartGame += SlugcatSelectMenu_StartGame;
            On.Menu.SlideShow.ctor += SlideShow_ctor;
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            On.Menu.MenuIllustration.LoadFile_1 += MenuIllustration_LoadFile_1;
        }

        #region Hooks

        private static void SlideShowMenuScene_ApplySceneSpecificAlphas(On.Menu.SlideShowMenuScene.orig_ApplySceneSpecificAlphas orig, SlideShowMenuScene self)
        {
            orig(self);

            foreach(MenuObject obj in self.subObjects)
            {
                if (!(obj is MenuIllustration illust)) continue;
                if (!customRep.TryGet(illust, out SceneImage img)) continue;

                if(img.AlphaKeys != null)
                    illust.setAlpha = img.AlphaAtTime(self.displayTime);
            }
        }

        // Automatically override outro cutscenes
        private static void RainWorldGame_ExitToVoidSeaSlideShow(On.RainWorldGame.orig_ExitToVoidSeaSlideShow orig, RainWorldGame self)
        {
            orig(self);

            // Override the outro if this character has the corresponding slideshow
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(self.StoryCharacter);
            if (ply == null) return;
            if (ply.HasSlideshow("Outro"))
                OverrideNextSlideshow(ply, "Outro");
        }

        // Automatically override intro cutscenes
        private static void SlugcatSelectMenu_StartGame(On.Menu.SlugcatSelectMenu.orig_StartGame orig, SlugcatSelectMenu self, int storyGameCharacter)
        {
            orig(self, storyGameCharacter);

            if (!self.restartChecked && self.manager.rainWorld.progression.IsThereASavedGame(storyGameCharacter)) return;

            // Only continue to the slideshow if this character has an intro slideshow
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(storyGameCharacter);
            if (ply == null) return;
            if(ply.HasSlideshow("Intro") && !Input.GetKey("s"))
            {
                OverrideNextSlideshow(ply, "Intro");
                self.manager.upcomingProcess = null;
                self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.SlideShow);
            } else
            {
                self.manager.upcomingProcess = null;
                self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
            }
        }

        // Same as below, but for slideshows
        private static void SlideShow_ctor(On.Menu.SlideShow.orig_ctor orig, SlideShow self, ProcessManager manager, SlideShow.SlideShowID slideShowID)
        {
            // Automatically override slideshows if the current character has a slideshow by the same name
            SlugBaseCharacter currentPlayer = PlayerManager.GetCustomPlayer(manager.rainWorld.progression.PlayingAsSlugcat);

            if (currentPlayer != null)
            {
                string slideshowName = self.slideShowID.ToString();
                if (slideshowOverride == null && currentPlayer.HasSlideshow(slideshowName))
                    OverrideNextSlideshow(currentPlayer, slideshowName);
            }

            if (slideshowOverride == null)
            {
                orig(self, manager, slideShowID);
                return;
            }

            try
            {
                // Call the original constructor, save a reference to the loading label
                // This will always be empty, due to the ID of -1
                FLabel loadingLabel = manager.loadingLabel;
                orig(self, manager, (SlideShow.SlideShowID)(-1));

                // Undo RemoveLoadingLabel and NextScene
                manager.loadingLabel = loadingLabel;
                Futile.stage.AddChild(loadingLabel);
                self.current = -1;

                // Load a custom scene

                SlugBaseCharacter owner = slideshowOverride.Character;
                CustomSlideshow builtSlideshow;
                try
                {
                    builtSlideshow = owner.BuildSlideshow(slideshowOverride.ResourceName);
                }
                catch(Exception e)
                {
                    Debug.LogError($"Failed to load slideshow: {slideshowOverride.ResourceName}");
                    Debug.LogException(e);
                    return;
                }
                slideshowOverride.Load(builtSlideshow);
                customSlideshowRep[self] = builtSlideshow;
                List<SlideshowSlide> slides = builtSlideshow.Slides;

                // Chose a destination process
                if (builtSlideshow.NextProcess == null)
                {
                    switch (slideShowID)
                    {
                        case SlideShow.SlideShowID.WhiteIntro:
                        case SlideShow.SlideShowID.YellowIntro:
                            self.nextProcess = ProcessManager.ProcessID.Game;
                            break;
                        case SlideShow.SlideShowID.WhiteOutro:
                        case SlideShow.SlideShowID.YellowOutro:
                        case SlideShow.SlideShowID.RedOutro:
                            self.nextProcess = ProcessManager.ProcessID.Credits;
                            break;
                        default:
                            // Take a best guess
                            // Accidentally going to the game is better than accidentally going to the credits
                            self.nextProcess = ProcessManager.ProcessID.Game;
                            break;
                    }
                }
                else
                {
                    self.nextProcess = builtSlideshow.NextProcess.Value;
                }

                // Custom music
                if (manager.musicPlayer != null && !string.IsNullOrEmpty(builtSlideshow.Music))
                {
                    self.waitForMusic = builtSlideshow.Music;
                    self.stall = true;
                    manager.musicPlayer.MenuRequestsSong(self.waitForMusic, 1.5f, 40f);
                }

                // Custom playlist
                float time = 0f;
                float endTime;
                self.playList.Clear();
                foreach (SlideshowSlide slide in slides)
                {
                    if (!slide.Enabled) continue;
                    endTime = time + slide.Duration;
                    self.playList.Add(new SlideShow.Scene(MenuScene.SceneID.Empty, time, time + slide.FadeIn, endTime - slide.FadeOut));
                    time = endTime;
                }

                // Preload the scenes
                self.preloadedScenes = new SlideShowMenuScene[self.playList.Count];
                try
                {
                    for (int i = 0; i < self.preloadedScenes.Length; i++)
                    {
                        MenuScene.SceneID id = MenuScene.SceneID.Empty;
                        if (builtSlideshow.Owner.HasScene(slides[i].SceneName))
                        {
                            // Prioritize this character's scenes
                            OverrideNextScene(builtSlideshow.Owner, builtSlideshow.Slides[i].SceneName, slideshowOverride.Filter);
                        }
                        else
                        {
                            ClearSceneOverride();
                            try
                            {
                                // ... then try existing scenes
                                id = Custom.ParseEnum<MenuScene.SceneID>(slides[i].SceneName);
                            }
                            catch (Exception)
                            {
                                // ... and default to Empty
                                id = MenuScene.SceneID.Empty;
                            }
                        }
                        self.preloadedScenes[i] = new SlideShowMenuScene(self, self.pages[0], id);
                        self.preloadedScenes[i].Hide();

                        List<Vector3> camPath = self.preloadedScenes[i].cameraMovementPoints;
                        camPath.Clear();
                        camPath.AddRange(slides[i].CameraPath);
                    }
                }
                finally
                {
                    ClearSceneOverride();
                }
            }
            finally
            {
                ClearSlideshowOverride();
            }
            manager.RemoveLoadingLabel();
            self.NextScene();
        }

        // Add SlugBase character resources as a "virtual file" that illustrations may be read from
        // Folder: "SlugBase Resources"
        // File: "PlayerName\Dir1\Dir2\...\DirN\Image.png"
        private static void MenuIllustration_LoadFile_1(On.Menu.MenuIllustration.orig_LoadFile_1 orig, Menu.MenuIllustration self, string folder)
        {
            Texture2D customTex;
            if (folder == resourceFolderName && ((customTex = LoadTextureFromResources(self.fileName)) != null))
            {
                self.texture = customTex;
                self.texture.wrapMode = TextureWrapMode.Clamp;
                if (self.crispPixels)
                {
                    self.texture.anisoLevel = 0;
                    self.texture.filterMode = FilterMode.Point;
                }
                HeavyTexturesCache.LoadAndCacheAtlasFromTexture(self.fileName, self.texture);
                return;
            }
            else orig(self, folder);
        }

        private static void MenuScene_BuildScene(On.Menu.MenuScene.orig_BuildScene orig, MenuScene self)
        {
            // Automatically override scenes if the current character has a scene by the same name
            SlugBaseCharacter currentPlayer = PlayerManager.GetCustomPlayer(self.menu.manager.rainWorld.progression.PlayingAsSlugcat);

            if (currentPlayer != null)
            {
                string sceneName = self.sceneID.ToString();
                if (sceneOverride == null && currentPlayer.HasScene(sceneName))
                    OverrideNextScene(currentPlayer, sceneName);
            }

            if (sceneOverride != null)
            {
                try
                {
                    self.sceneFolder = resourceFolderName;

                    var owner = sceneOverride.Character;
                    var builtScene = owner.BuildScene(sceneOverride.ResourceName);
                    sceneOverride.Load(builtScene);
                    customSceneRep[self] = builtScene;
                    if (sceneOverride.Filter != null)
                        builtScene.ApplyFilter(sceneOverride.Filter);

                    // Check for flatmode support
                    bool hasFlatmode = false;
                    foreach (var img in builtScene.Images)
                    {
                        if (img.HasTag("FLATMODE"))
                        {
                            hasFlatmode = true;
                            break;
                        }
                    }

                    // Load all images into the scene
                    for (int imgIndex = 0; imgIndex < builtScene.Images.Count; imgIndex++)
                    {
                        var img = builtScene.Images[imgIndex];

                        // Hide disabled images
                        if (!img.Enabled) continue;

                        // Allow images to use their own sprites
                        if (!img.OnBuild(self)) continue;

                        // Skip this image if it is flatmode only and flatmode is disabled, and vice versa
                        bool flat = img.depth < 0f;
                        bool flatmodeOnly = hasFlatmode && img.HasTag("flatmode");
                        if (hasFlatmode && (self.flatMode != flatmodeOnly)) continue;

                        // Parse alpha
                        float alpha = img.GetProperty<float?>("alpha") ?? 1f;

                        string assetPath = $"{owner.Name}\\Scenes\\{builtScene.Name}\\{img.assetName}";
                        Vector2 pos = img.Pos;
                        bool crisp = img.HasTag("CRISP");
                        string shaderName = img.GetProperty<string>("shader");
                        FShader shader = null;

                        MenuIllustration illust;
                        if (flat)
                        {
                            // It's Friday

                            // Parse shader
                            if (shaderName != null)
                            {
                                if (!self.menu.manager.rainWorld.Shaders.TryGetValue(shaderName, out shader)) shader = null;
                            }

                            // Add a flat illustration
                            illust = new MenuIllustration(self.menu, self, self.sceneFolder, assetPath, pos, crisp, false);
                            if (shader != null)
                                illust.sprite.shader = shader;
                        }
                        else
                        {
                            // Parse shader
                            MenuDepthIllustration.MenuShader menuShader = MenuDepthIllustration.MenuShader.Normal;
                            if (shaderName != null)
                            {
                                try
                                {
                                    menuShader = Custom.ParseEnum<MenuDepthIllustration.MenuShader>(shaderName);
                                    shader = null;
                                }
                                catch
                                {
                                    if (!self.menu.manager.rainWorld.Shaders.TryGetValue(shaderName, out shader)) shader = null;
                                    menuShader = MenuDepthIllustration.MenuShader.Normal;
                                }
                            }

                            // Add an illustration with depth
                            illust = new MenuDepthIllustration(self.menu, self, self.sceneFolder, assetPath, pos, img.Depth, menuShader);

                            // Apply crisp pixels
                            if (crisp) illust.sprite.element.atlas.texture.filterMode = FilterMode.Point;
                        }

                        // Apply tags
                        if (shader != null) illust.sprite.shader = shader;
                        illust.setAlpha = alpha;
                        self.AddIllustration(illust);

                        // Link back to the custom scene image
                        customRep[illust] = img;
                    }

                    // Add idle depths
                    if (self is InteractiveMenuScene ims)
                    {
                        ims.idleDepths = new List<float>();
                        List<object> depths = builtScene.GetProperty<List<object>>("idledepths");
                        if (depths != null)
                        {
                            for (int i = 0; i < depths.Count; i++)
                            {
                                if (depths[i] is double depth)
                                    ims.idleDepths.Add((float)depth);
                            }
                        }
                    }

                }
                finally { ClearSceneOverride(); }
            }
            else
            {
                orig(self);
            }
        }

        #endregion Hooks

        internal static ResourceOverride<CustomScene> OverrideNextScene(SlugBaseCharacter ply, string customSceneName, SceneImageFilter filter = null)
        {
            return sceneOverride = new ResourceOverride<CustomScene>(ply, customSceneName, filter);
        }

        internal static void ClearSceneOverride()
        {
            sceneOverride = null;
        }

        internal static ResourceOverride<CustomSlideshow> OverrideNextSlideshow(SlugBaseCharacter ply, string customSlideshowName, SceneImageFilter filter = null)
        {
            return slideshowOverride = new ResourceOverride<CustomSlideshow>(ply, customSlideshowName, filter);
        }

        internal static void ClearSlideshowOverride()
        {
            slideshowOverride = null;
        }

        private static Texture2D LoadTextureFromResources(string fileName)
        {
            string[] args = fileName.Split('\\');
            if (args.Length < 2) return null;
            SlugBaseCharacter ply = PlayerManager.GetCustomPlayer(args[0]);
            if (ply == null) return null;

            string[] resourcePath = new string[args.Length - 1];
            for (int i = 0; i < resourcePath.Length; i++)
                resourcePath[i] = args[i + 1];

            if (resourcePath.Length > 0)
                resourcePath[resourcePath.Length - 1] = Path.ChangeExtension(resourcePath[resourcePath.Length - 1], "png");

            // Load the image resource from disk
            Texture2D tex = new Texture2D(1, 1);
            using (Stream imageData = ply.GetResource(resourcePath))
            {

                if (imageData == null)
                {
                    Debug.LogException(new FileNotFoundException($"Could not find image for SlugBase character: \"{ply.Name}:{string.Join("\\", resourcePath)}\"."));
                    return null;
                }

                if (imageData.Length > int.MaxValue)
                    throw new FormatException($"Image resource may not be more than {int.MaxValue} bytes!");

                BinaryReader br = new BinaryReader(imageData);
                byte[] buffer = br.ReadBytes((int)imageData.Length);

                tex.LoadImage(buffer);
            }

            return tex;
        }

        internal class ResourceOverride<T>
        {
            public SlugBaseCharacter Character { get; }
            public string ResourceName { get; }
            public SceneImageFilter Filter { get; }

            public event LoadHandler OnLoad;

            public ResourceOverride(SlugBaseCharacter character, string resource, SceneImageFilter filter = null)
            {
                Character = character;
                ResourceName = resource;
                Filter = filter;
            }

            public delegate void LoadHandler(T loadedResource);

            internal void Load(T builtScene)
            {
                OnLoad?.Invoke(builtScene);
            }
        }
    }

    /// <summary>
    /// Determines if this image should be included in the scene.
    /// </summary>
    /// <remarks>
    /// This is intended to filter items based on <see cref="SceneImage.GetProperty{T}(string)"/>.
    /// </remarks>
    /// <param name="image">The <see cref="SceneImage"/> instance to check.</param>
    /// <returns>True if this image should be in the scene, false otherwise.</returns>
    public delegate bool SceneImageFilter(SceneImage image);
}
