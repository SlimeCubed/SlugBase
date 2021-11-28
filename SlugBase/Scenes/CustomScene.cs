using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Menu;
using RWCustom;
using JsonObj = System.Collections.Generic.Dictionary<string, object>;
using JsonList = System.Collections.Generic.List<object>;

namespace SlugBase
{
    /// <summary>
    /// Represents a scene added by a SlugBase character.
    /// </summary>
    public class CustomScene
    {
        internal JsonObj properties = new JsonObj();
        internal bool dirty;

        /// <summary>
        /// The images this scene contains.
        /// </summary>
        public List<SceneImage> Images { get; private set; }

        /// <summary>
        /// The character that this scene was loaded from.
        /// </summary>
        public SlugBaseCharacter Owner { get; private set; }

        /// <summary>
        /// This scene's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Creates an empty scene.
        /// </summary>
        public CustomScene(SlugBaseCharacter owner, string name)
        {
            Images = new List<SceneImage>();
            Owner = owner;
            Name = name;
        }

        /// <summary>
        /// Creates a scene from a JSON string.
        /// </summary>
        public CustomScene(SlugBaseCharacter owner, string name, string json) : this(owner, name, json?.dictionaryFromJson()) { }

        /// <summary>
        /// Creates a scene from a JSON object.
        /// </summary>
        public CustomScene(SlugBaseCharacter owner, string name, JsonObj data) : this(owner, name)
        {
            if (owner == null) throw new ArgumentNullException(nameof(name));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (data == null) throw new ArgumentNullException(nameof(data), "Scene JSON data is null! This may be due to invalid JSON.");

            foreach(var pair in data)
                LoadValue(pair.Key, pair.Value);
        }

        private void LoadValue(string name, object value)
        {
            try
            {
                switch (name)
                {
                    case "images":
                        foreach (JsonObj imageData in (JsonList)value)
                            Images.Add(new SceneImage(this, imageData));
                        break;
                    default:
                        properties[name.ToLower()] = value;
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Scene property \"{name}\" cannot hold a value of type \"{value.GetType().Name}\"!");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Inserts an image into the list according to its depth.
        /// </summary>
        public void InsertImage(SceneImage newImage)
        {
            for (int i = 0; i < Images.Count; i++)
            {
                if (Images[i].depth < newImage.depth)
                {
                    Images.Insert(i, newImage);
                    return;
                }
            }
            Images.Add(newImage);
        }

        internal static T GetPropFromDict<T>(string key, JsonObj dict)
        {
            if (!dict.TryGetValue(key.ToLower(), out object o)) return default(T);
            if (o == null) return default(T);

            try
            {
                // Allow nullables
                Type nullableType = Nullable.GetUnderlyingType(typeof(T));
                if (nullableType != null)
                    return (T)Convert.ChangeType(o, nullableType);

                return (T)Convert.ChangeType(o, typeof(T));
            }
            catch (Exception)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Retrieves a named value from this scene's JSON, or the type's default value if does not exist.
        /// These may be defined by the engine (see remarks), or by another mod.
        /// </summary>
        /// <remarks>
        /// This may defined by your mod, or be one of several built-in tags:
        /// <list type="bullet">
        ///     <item>
        ///         <term>MarkX (float)</term>
        ///         <description>The X position of the default mark of communication.</description>
        ///     </item>
        ///     <item>
        ///         <term>MarkY (float)</term>
        ///         <description>The Y position of the default mark of communication.</description>
        ///     </item>
        ///     <item>
        ///         <term>GlowX (float)</term>
        ///         <description>The X position of the default player glow.</description>
        ///     </item>
        ///     <item>
        ///         <term>GlowY (float)</term>
        ///         <description>The Y position of the default player glow.</description>
        ///     </item>
        ///     <item>
        ///         <term>SlugcatDepth (float)</term>
        ///         <description>The depth of the slugcat in the select screen. This is used when positioning the mark and glow.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        public T GetProperty<T>(string key) => GetPropFromDict<T>(key, properties);

        /// <summary>
        /// Attaches a named value to this scene.
        /// </summary>
        public void SetProperty(string key, object value)
        {
            SetDirty();
            InternalSetProperty(key, value);
        }

        private void InternalSetProperty(string key, object value)
        {
            properties[key.ToLower()] = value;
        }

        internal void SetDirty()
        {
            dirty = true;
        }

        private JsonList SaveImages()
        {
            JsonList obj = new JsonList();
            foreach (SceneImage image in Images)
                if (image.ShouldBeSaved)
                    obj.Add(image.ToJsonObj());
            return obj;
        }

        internal JsonObj ToJsonObj()
        {
            JsonObj obj = new JsonObj();
            obj["images"] = SaveImages();
            return obj;
        }

        /// <summary>
        /// Creates a JSON string that represents this object.
        /// </summary>
        /// <returns>A JSON string.</returns>
        public override string ToString()
        {
            return ToJsonObj().toJson();
        }

        /// <summary>
        /// Disables images in this scene based on a filter.
        /// </summary>
        /// <param name="filter">A delegate that returns false for any images that should be hidden.</param>
        public void ApplyFilter(SceneImageFilter filter)
        {
            foreach (var img in Images)
                if (!filter(img))
                    img.Enabled = false;
        }
    }

    /// <summary>
    /// Represents a single image in a <see cref="CustomScene"/>.
    /// </summary>
    /// <remarks>
    /// This class may be inherited if a scene image requires more functionality than <see cref="MenuIllustration"/> can provide.
    /// Consider using <see cref="CustomScene.InsertImage(SceneImage)"/> to add illustrations manually.
    /// </remarks>
    public class SceneImage
    {
        internal string assetName;
        internal Vector2 pos;
        internal float depth = -1f;
        internal JsonObj properties = new JsonObj();
        internal JsonObj tempProperties;
        internal bool dirty = false;

        /// <summary>
        /// The name of the file containing the image.
        /// </summary>
        public string AssetName
        {
            get => assetName;
            set { SetDirty(); assetName = value; }
        }

        /// <summary>
        /// The scene this image is a part of.
        /// </summary>
        public CustomScene Owner { get; private set; }

        /// <summary>
        /// The position of the bottom left of this image
        /// </summary>
        public Vector2 Pos
        {
            get => pos;
            set { SetDirty(); pos = value; }
        }

        /// <summary>
        /// How far into the screen this illustration should be drawn.
        /// The higher this value, the less this image moves when the mouse is moved.
        /// This should be less than zero if the image is flat.
        /// </summary>
        public float Depth
        {
            get => depth;
            set { SetDirty(); depth = value; }
        }

        /// <summary>
        /// True if this image should be shown in the scene, false otherwise.
        /// This has no effect once the scene's illustrations have been loaded.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// True if this image should be saved to the scene. This does not affect <see cref="OnSave(JsonObj)"/>.
        /// Defaults to true.
        /// </summary>
        public virtual bool ShouldBeSaved => true;
        
        /// <summary>
        /// The name to display when showing this image in the scene editor.
        /// </summary>
        public virtual string DisplayName => System.IO.Path.GetFileName(AssetName);

        /// <summary>
        /// A list of alpha keyframes. This may be null.
        /// </summary>
        /// <remarks>
        /// X values correspond to time, Y values correspond to this image's alpha at this time.
        /// Time values range from 0 to 1.
        /// Keyframes will be linearly interpolated between during a slideshow.
        /// </remarks>
        public List<Vector2> AlphaKeys { get; set; }

        /// <summary>
        /// Creates a blank scene image.
        /// </summary>
        public SceneImage(CustomScene owner)
        {
            Enabled = true;
            Owner = owner;
        }

        /// <summary>
        /// Creates a scene image from a JSON string.
        /// </summary>
        public SceneImage(CustomScene owner, string data) : this(owner, data.dictionaryFromJson()) { }

        /// <summary>
        /// Creates a scene image from a JSON object.
        /// </summary>
        public SceneImage(CustomScene owner, JsonObj json) : this(owner)
        {
            foreach (var pair in json)
                LoadValue(pair.Key, pair.Value);
        }

        private void LoadValue(string name, object value)
        {
            try
            {
                switch (name)
                {
                    case "x": pos.x = Convert.ToSingle(value); break;
                    case "y": pos.y = Convert.ToSingle(value); break;
                    case "depth": depth = Convert.ToSingle(value); break;
                    case "name": assetName = (string)value; break;
                    case "alphakeys":
                        {
                            AlphaKeys = new List<Vector2>();
                            JsonList list = (JsonList)value;
                            for (int i = 0; i < list.Count - 1; i += 2)
                            {
                                AlphaKeys.Add(new Vector2(
                                    Convert.ToSingle(list[i + 0]),
                                    Convert.ToSingle(list[i + 1])
                                ));
                            }
                        }
                        break;
                    default: InternalSetProperty(name, value); break;
                }
            } catch(Exception e)
            {
                Debug.Log($"Image property \"{name}\" cannot hold a value of type \"{value.GetType().Name}\"!");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Converts this image into a JSON string.
        /// </summary>
        /// <returns>A JSON string.</returns>
        public override string ToString() => ToJsonObj().toJson();

        /// <summary>
        /// Finds the alpha of this image at <paramref name="t"/> by sampling from <see cref="AlphaKeys"/>.
        /// </summary>
        /// <param name="t">The time to sample at, typically between 0 and 1.</param>
        /// <returns>The alpha that this image should have at this point. This does not take other alpha modifiers into account.</returns>
        public float AlphaAtTime(float t)
        {
            var keys = AlphaKeys;

            if (keys.Count == 0) return 1f;
            if (keys.Count == 1) return keys[0].y;

            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (keys[i + 1].x >= t)
                {
                    return Custom.LerpMap(t, keys[i].x, keys[i + 1].x, keys[i].y, keys[i + 1].y);
                }
            }

            return keys[keys.Count - 1].y;
        }

        /// <summary>
        /// Called before a scene is saved.
        /// </summary>
        /// <param name="scene">A JSON object representing the scene to be saved.</param>
        public virtual void OnSave(JsonObj scene)
        {
        }

        /// <summary>
        /// Called when this image's graphics should be added to a scene.
        /// Defaults to true.
        /// </summary>
        /// <remarks>
        /// This may be overridden to add graphics to the scene without using a <see cref="MenuIllustration"/>.
        /// </remarks>
        /// <returns>True if this image should create a <see cref="MenuIllustration"/>.</returns>
        protected internal virtual bool OnBuild(MenuScene scene)
        {
            return true;
        }

        /// <summary>
        /// Retrieves a named value from this image's JSON, or the type's default value if does not exist.
        /// These may be defined by the engine (see remarks), or by another mod.
        /// </summary>
        /// <remarks>
        /// This may defined by your mod, or be one of several built-in tags:
        /// <list type="bullet">
        ///     <item>
        ///         <term>Mark (bool)</term>
        ///         <description>This image only appears on the select screen if the player has the mark of communication.</description>
        ///     </item>
        ///     <item>
        ///         <term>Glow (bool)</term>
        ///         <description>True if this image should only appear on the select screen if the player is glowing.</description>
        ///     </item>
        ///     <item>
        ///         <term>Shader (string)</term>
        ///         <description>Set this image to use the specified shader.
        ///         These are first checked against <see cref="MenuDepthIllustration.MenuShader"/>.
        ///         If the image is flat or there is no corresponding MenuShader, then the shader with this name is used.</description>
        ///     </item>
        ///     <item>
        ///         <term>Flatmode (bool)</term>
        ///         <description>True if this image should only appear in flat mode, and all others without it will be hidden in flat mode.
        ///         This should be used in conjunction with a <see cref="Depth"/> less than zero.</description>
        ///     </item>
        ///     <item>
        ///         <term>Crisp (bool)</term>
        ///         <description>True to disable antialiasing for this image.</description>
        ///     </item>
        ///     <item>
        ///         <term>Focus (bool)</term>
        ///         <description>Marks this image as a candidate for camera focus.
        ///         If no images are marked as such, then the camera will occasionally focus at the depth of a random image.</description>
        ///     </item>
        ///     <item>
        ///         <term>Alpha (float)</term>
        ///         <description>Sets this image's opacity. This value should be between 0 and 1, inclusive.</description>
        ///     </item>
        ///     <item>
        ///         <term>Fade (float)</term>
        ///         <description>This image will fade out when the map is opened on the sleep and death screens.</description>
        ///     </item>
        /// </list>
        /// </remarks>
        /// <returns>An instance of <typeparamref name="T"/>, or <typeparamref name="T"/>'s default value if no matching key was found.</returns>
        public T GetProperty<T>(string key) => CustomScene.GetPropFromDict<T>(key, properties);

        /// <summary>
        /// Attaches a named value to this image.
        /// </summary>
        public void SetProperty(string key, object value)
        {
            dirty = true;
            InternalSetProperty(key, value);
        }

        private void InternalSetProperty(string key, object value)
        {
            properties[key.ToLower()] = value;
        }

        /// <summary>
        /// Like <see cref="GetProperty{T}(string)"/>, but only for temporary values.
        /// </summary>
        /// <returns>An instance of <typeparamref name="T"/>, or <typeparamref name="T"/>'s default value if no matching key was found.</returns>
        public T GetTempProperty<T>(string key)
        {
            if (tempProperties == null) return default(T);
            return CustomScene.GetPropFromDict<T>(key, tempProperties);
        }

        /// <summary>
        /// Attaches a temporary value to this image. This will not be saved with the scene.
        /// </summary>
        public void SetTempProperty(string key, object value)
        {
            if (tempProperties == null) tempProperties = new JsonObj();
            tempProperties[key.ToLower()] = value;
        }

        private void SetDirty()
        {
            dirty = true;
            Owner?.SetDirty();
        }

        internal JsonObj ToJsonObj()
        {
            JsonObj obj = new JsonObj();
            foreach (var pair in properties)
                obj.Add(pair.Key, pair.Value);
            obj["x"] = pos.x;
            obj["x"] = pos.y;
            obj["depth"] = depth;
            obj["name"] = assetName;
            return obj;
        }

        /// <summary>
        /// Check if this image has the specified property, and that the property's value is not false.
        /// </summary>
        public bool HasTag(string tagName)
        {
            object prop = GetProperty<object>(tagName);
            return prop != null && ((prop is bool) ? (bool)prop : true);
        }
    }
}
