using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Menu;
using RWCustom;

namespace SlugBase
{

    // PUBLIC SECTION //

    /// <summary>
    /// Allows custom slugcats to add 
    /// </summary>
    public static class CustomScenes
    {
        /// <summary>
        /// Determines if this image should be included in the scene.
        /// </summary>
        /// <remarks>
        /// This is intended to filter items based on <see cref="CustomSceneImage.Tags"/>.
        /// </remarks>
        /// <param name="image">The <see cref="CustomSceneImage"/> instance to check.</param>
        /// <returns>True if this image should be in the scene, false otherwise.</returns>
        public delegate bool SceneImageFilter(CustomSceneImage image);

        /// <summary>
        /// Represents a single image in a custom scene.
        /// This should be created with <see cref="Parse(string)"/>.
        /// </summary>
        public class CustomSceneImage
        {
            internal string assetName;
            internal Vector2 pos;
            internal float depth;
            internal List<string> tags;
            internal bool dirty = false;
            internal string specialType;
            internal OnCreateSceneImage onCreate;

            internal delegate bool OnCreateSceneImage(MenuScene self);

            /// <summary>
            /// The name of the file containing the image.
            /// </summary>
            public string AssetName
            {
                get => assetName;
                set { dirty = true; assetName = value; }
            }

            /// <summary>
            /// The position of the bottom left of this image
            /// </summary>
            public Vector2 Pos
            {
                get => pos;
                set { dirty = true; pos = value; }
            }

            /// <summary>
            /// How far into the screen this illustration should be drawn.
            /// The higher this value, the less this image moves when the mouse is moved.
            /// This should be less than zero if the image is flat.
            /// </summary>
            public float Depth
            {
                get => depth;
                set { dirty = true; depth = value; }
            }

            /// <summary>
            /// Marks that this scene image was added by internal code instead of from resources, and therefore should not be edited or saved.
            /// </summary>
            public bool Special => specialType != null;

            /// <summary>
            /// A list of all tags applied to this image.
            /// </summary>
            /// <remarks>
            /// This may defined by your mod, or be one of several built-in tags:
            /// <list type="bullet">
            ///     <item>
            ///         <term>MARK</term>
            ///         <description>This image only appears on the select screen if the player has the mark of communication.
            ///         Set an <see cref="AssetName"/> of "DEFAULT" to use the default mark image, but centered on this position and depth.</description>
            ///     </item>
            ///     <item>
            ///         <term>GLOW</term>
            ///         <description>This image only appears on the select screen if the player is glowing.
            ///         Set an <see cref="AssetName"/> of "DEFAULT" to use the default glow image, but centered on this position and depth.</description>
            ///     </item>
            ///     <item>
            ///         <term>SHADER-string</term>
            ///         <description>Set this image to use the shader specified after the tag.
            ///         These are first checked against <see cref="MenuDepthIllustration.MenuShader"/>.
            ///         If the image is flat or there is no corresponding MenuShader, then the shader with this name is used.</description>
            ///     </item>
            ///     <item>
            ///         <term>FLATMODE</term>
            ///         <description>This image will only appear in flat mode, and all others without it will be hidden in flat mode.
            ///         This should be used in conjunction with an <see cref="Depth"/> less than zero.</description>
            ///     </item>
            ///     <item>
            ///         <term>CRISP</term>
            ///         <description>Disable antialiasing for this image.</description>
            ///     </item>
            ///     <item>
            ///         <term>FOCUS</term>
            ///         <description>Marks this image as a candidate for camera focus.
            ///         If no images are marked as such, then the camera will occasionally focus at the depth of a random image.</description>
            ///     </item>
            ///     <item>
            ///         <term>ALPHA-float</term>
            ///         <description>Sets this image's opacity to equal the floating point value following the tag.
            ///         This value should be between 0 and 1, inclusive.</description>
            ///     </item>
            /// </list>
            /// </remarks>
            public ReadOnlyCollection<string> Tags
            {
                get => tags.AsReadOnly();
            }

            private CustomSceneImage() {
                tags = new List<string>();
            }

            /// <summary>
            /// Loads a <see cref="CustomSceneImage"/> from a string.
            /// </summary>
            /// <exception cref="FormatException">Thrown when the input string is not formatted correctly.</exception>
            public static CustomSceneImage Parse(string input)
            {
                CustomSceneImage img = new CustomSceneImage();

                // Parse tags
                int tagSplit = input.IndexOf(":");
                if(tagSplit >= 0)
                {
                    string[] tags = input.Substring(tagSplit + 1).Trim().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                    input = input.Substring(0, tagSplit).Trim();

                    for (int i = 0; i < tags.Length; i++)
                        img.tags.Add(tags[i].Trim());
                }

                // Parse required arguments
                string[] args = input.Split(new string[] { "," }, StringSplitOptions.None);

                if (args.Length < 1) throw new FormatException("Not enough arguments in input string!");

                // Trim whitespace from arguments
                for (int i = 0; i < args.Length; i++)
                    args[i] = args[i].Trim();

                img.assetName = args[0];
                if (args.Length >= 4)
                {
                    try
                    {
                        img.pos.x = float.Parse(args[1]);
                        img.pos.y = float.Parse(args[2]);
                        img.depth = float.Parse(args[3]);
                    }
                    catch (Exception e)
                    {
                        throw new FormatException("Image position format is invalid!", e);
                    }
                }
                else
                {
                    img.dirty = true;
                    img.pos = new Vector2();
                    img.depth = 0f;
                }

                return img;
            }

            internal static CustomSceneImage CreateSpecial(string type, string fromInput = null, OnCreateSceneImage onCreate = null)
            {
                CustomSceneImage img;
                if (fromInput != null)
                    img = Parse(fromInput);
                else
                    img = new CustomSceneImage();
                img.specialType = type;
                img.onCreate = onCreate;
                return img;
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"{assetName}, {pos.x}, {pos.y}, {depth}");
                if (tags.Count > 0)
                {
                    sb.Append(" : ");
                    sb.Append(string.Join(", ", tags.ToArray()));
                }
                return sb.ToString();
            }

            /// <summary>
            /// Check if this image has the specified tag and retrieve the value associated with it.
            /// Tag names and values are separated by a single "-".
            /// </summary>
            /// <param name="tagName">The name of the tag to search for.</param>
            /// <param name="subValue">The value added after the tag.</param>
            public bool HasTag(string tagName, out string subValue)
            {
                subValue = string.Empty;
                foreach(string tag in tags)
                {
                    if (tag == tagName) return true;
                    int split = tag.IndexOf('-');
                    if (split == -1) continue;
                    if(tag.Substring(0, split) == tagName)
                    {
                        subValue = tag.Substring(split + 1);
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Check if this image has the specified tag.
            /// </summary>
            /// <param name="tagName">The name of the tag to search for.</param>
            /// <returns></returns>
            public bool HasTag(string tagName) => HasTag(tagName, out _);
        }

        // INTERNAL SECTION //

        // Passed in as a folder to some functions to mark that they should load a custom resource instead
        internal const string resourceFolderName = "SlugBase Resources";

        internal static void ApplyHooks()
        {
            On.Menu.MenuScene.BuildScene += MenuScene_BuildScene;
            On.Menu.MenuIllustration.LoadFile_1 += MenuIllustration_LoadFile_1;
        }

        #region Hooks

        // Add custom slugcat resources as a "virtual file" that illustrations may be read from
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
            if (nextScenePly != null)
            {
                try
                {
                    if (self is InteractiveMenuScene ims)
                        ims.idleDepths = new List<float>();

                    self.sceneFolder = resourceFolderName;

                    // Check for flatmode support
                    bool hasFlatmode = false;
                    foreach (var img in nextSceneImages)
                    {
                        if (img.HasTag("FLATMODE"))
                        {
                            hasFlatmode = true;
                            break;
                        }
                    }

                    // Load all images into the scene
                    for(int i = 0; i < nextSceneImages.Count; i++)
                    {
                        var img = nextSceneImages[i];

                        if (!(img.onCreate?.Invoke(self) ?? true))
                            continue;

                        // Skip this image if it is flatmode only and flatmode is disabled, and vice versa
                        bool flat = img.depth < 0f;
                        bool flatmodeOnly = hasFlatmode && img.HasTag("FLATMODE");
                        float alpha = 1f;
                        if (hasFlatmode && (self.flatMode != flatmodeOnly)) continue;

                        // Parse alpha
                        if (img.HasTag("ALPHA", out string alphaString)) float.TryParse(alphaString, out alpha);

                        string assetPath = $"{nextScenePly.Name}\\Scenes\\{nextSceneName}\\{img.assetName}";
                        Vector2 pos = img.Pos;
                        bool crisp = img.HasTag("CRISP");

                        if (flat)
                        {
                            // It's Friday

                            // Parse shader
                            FShader shader = null;
                            if (img.HasTag("SHADER", out string shaderName))
                            {
                                if (!self.menu.manager.rainWorld.Shaders.TryGetValue(shaderName, out shader)) shader = null;
                            }

                            // Add a flat illustration
                            MenuIllustration illust = new MenuIllustration(self.menu, self, self.sceneFolder, assetPath, pos, crisp, false);
                            illust.setAlpha = alpha;
                            self.AddIllustration(illust);
                            if (shader != null)
                                illust.sprite.shader = shader;
                        } else
                        {
                            // Parse shader
                            FShader shader = null;
                            MenuDepthIllustration.MenuShader menuShader = MenuDepthIllustration.MenuShader.Normal;
                            if (img.HasTag("SHADER", out string shaderName))
                            {
                                try
                                {
                                    menuShader = Custom.ParseEnum<MenuDepthIllustration.MenuShader>(shaderName);
                                    shader = null;
                                }
                                catch (Exception)
                                {
                                    if (!self.menu.manager.rainWorld.Shaders.TryGetValue(shaderName, out shader)) shader = null;
                                    menuShader = MenuDepthIllustration.MenuShader.Normal;
                                }
                            }

                            // Add an illustration with depth
                            MenuDepthIllustration illust = new MenuDepthIllustration(self.menu, self, self.sceneFolder, assetPath, pos, img.Depth, menuShader);
                            illust.setAlpha = alpha;
                            self.AddIllustration(illust);

                            // Apply shader
                            if(shader != null) illust.sprite.shader = shader;
                            // Apply crisp pixels
                            if (crisp) illust.sprite.element.atlas.texture.filterMode = FilterMode.Point;
                            // Add idle depth
                            if (self is InteractiveMenuScene && img.HasTag("FOCUS")) (self as InteractiveMenuScene).idleDepths.Add(img.Depth - 0.1f);
                        }
                    }

                } finally
                {
                    nextScenePly = null;
                    nextSceneImages = null;
                    nextSceneName = null;
                }
            }
            else
            {
                orig(self);
            }
        }

        #endregion Hooks

        private static CustomPlayer nextScenePly;
        private static List<CustomSceneImage> nextSceneImages;
        private static string nextSceneName;
        internal static void OverrideNextScene(CustomPlayer ply, string customSceneName, SceneImageFilter filter = null)
        {
            nextScenePly = ply;
            nextSceneImages = ply.BuildScene(customSceneName);
            nextSceneName = customSceneName;
            if(filter != null)
            {
                for(int i = nextSceneImages.Count - 1; i >= 0; i--)
                {
                    if (!filter(nextSceneImages[i]))
                        nextSceneImages.RemoveAt(i);
                }
            }
        }

        // Adds an image with the specified depth into the next scene
        internal static void InsertImage(CustomSceneImage img)
        {
            for(int i = 0; i < nextSceneImages.Count; i++)
            {
                if(nextSceneImages[i].depth <= img.depth)
                {
                    nextSceneImages.Insert(i, img);
                    break;
                }
            }
        }

        private static Texture2D LoadTextureFromResources(string fileName)
        {
            string[] args = fileName.Split('\\');
            if (args.Length < 2) return null;
            CustomPlayer ply = PlayerManager.GetCustomPlayer(args[0]);
            if (ply == null) return null;

            string[] resourcePath = new string[args.Length - 1];
            for (int i = 0; i < resourcePath.Length; i++)
                resourcePath[i] = args[i + 1];

            // Load the image resource from disk
            Texture2D tex = new Texture2D(1, 1);
            Stream imageData = ply.GetResource(resourcePath);

            if (imageData == null)
            {
                Debug.LogException(new FileNotFoundException($"Could not find resource for custom player: \"{ply.Name}:{string.Join("\\", resourcePath)}\"."));
                return null;
            }

            if (imageData.Length > int.MaxValue)
                throw new FormatException($"Image resource may not be more than {int.MaxValue} bytes!");

            BinaryReader br = new BinaryReader(imageData);
            byte[] buffer = br.ReadBytes((int)imageData.Length);

            tex.LoadImage(buffer);

            return tex;
        }
    }
}
