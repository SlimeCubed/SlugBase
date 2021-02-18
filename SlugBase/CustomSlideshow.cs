using System;
using System.Collections.Generic;
using UnityEngine;
using RWCustom;
using JsonObj = System.Collections.Generic.Dictionary<string, object>;
using JsonList = System.Collections.Generic.List<object>;

namespace SlugBase
{
    using static CustomScene;

    /// <summary>
    /// Represents a slideshow added by a SlugBase character.
    /// </summary>
    public class CustomSlideshow
    {
        /// <summary>
        /// The SlugBase character that owns this slideshow.
        /// </summary>
        public SlugBaseCharacter Owner { get; private set; }

        /// <summary>
        /// The name of this slideshow.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// A list of slides to display during this slideshow.
        /// </summary>
        public List<SlideshowSlide> Slides { get; private set; }

        /// <summary>
        /// The name of the music track to play during this slideshow.
        /// </summary>
        public string Music { get; set; }

        /// <summary>
        /// The ID of the process to move to move to after the slideshow.
        /// If this is null, an ID will be chosen based on the <see cref="Menu.SlideShow.SlideShowID"/> of the
        /// slideshow that this replaced.
        /// </summary>
        public ProcessManager.ProcessID? NextProcess { get; set; }

        /// <summary>
        /// Creates an empty slideshow.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="name"></param>
        public CustomSlideshow(SlugBaseCharacter owner, string name)
        {
            NextProcess = null;
            Owner = owner;
            Name = name;
            Slides = new List<SlideshowSlide>();
        }

        /// <summary>
        /// Creates a slideshow from a JSON string.
        /// </summary>
        public CustomSlideshow(SlugBaseCharacter owner, string name, string json) : this(owner, name, json.dictionaryFromJson()) { }

        /// <summary>
        /// Creates a slideshow from a JSON object.
        /// </summary>
        public CustomSlideshow(SlugBaseCharacter owner, string name, JsonObj data) : this(owner, name)
        {
            foreach(var pair in data)
                LoadValue(pair.Key, pair.Value);
        }

        private void LoadValue(string name, object value)
        {
            try
            {
                switch (name)
                {
                    case "slides":
                        foreach (JsonObj slideData in (JsonList)value)
                            Slides.Add(new SlideshowSlide(this, slideData));
                        break;
                    case "music":
                        Music = Convert.ToString(value);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Slideshow property \"{name}\" cannot hold a value of type \"{value.GetType().Name}\"!");
                Debug.LogException(e);
            }
        }
    }

    /// <summary>
    /// Represents a single slide of a <see cref="CustomSlideshow"/>.
    /// </summary>
    public class SlideshowSlide
    {
        /// <summary>
        /// The <see cref="CustomSlideshow"/> that this slide is a part of.
        /// </summary>
        public CustomSlideshow Owner { get; private set; }

        /// <summary>
        /// The name of the scene that this slide displays.
        /// This is first checked against the owner's scenes, then against <see cref="Menu.MenuScene.SceneID"/>.
        /// </summary>
        public string SceneName { get; set; }

        /// <summary>
        /// The path that the camera takes while viewing this scene.
        /// X and Y are the camera's position, Z is the camera's focal depth.
        /// </summary>
        public List<Vector3> CameraPath { get; private set; }

        /// <summary>
        /// The time in seconds this image remains on screen, including both fades.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// The time in seconds this image takes to fade in.
        /// </summary>
        public float FadeIn { get; set; }

        /// <summary>
        /// The time in seconds this image takes to fade out.
        /// </summary>
        public float FadeOut { get; set; }

        /// <summary>
        /// True if this slide should appear in the slideshow.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Creates an empty slideshow slide.
        /// </summary>
        public SlideshowSlide(CustomSlideshow owner)
        {
            Enabled = true;
            Owner = owner;
            CameraPath = new List<Vector3>();
            Duration = 0f;
            FadeIn = 0.75f;
            FadeOut = 0.75f;
        }

        /// <summary>
        /// Creates a single slide from a JSON string.
        /// </summary>
        public SlideshowSlide(CustomSlideshow owner, string json) : this(owner, json.dictionaryFromJson()) { }

        /// <summary>
        /// Creates a single slide from a JSON object.
        /// </summary>
        public SlideshowSlide(CustomSlideshow owner, JsonObj data) : this(owner)
        {
            Duration = -1f;

            foreach (var pair in data)
                LoadValue(pair.Key, pair.Value);

            if (SceneName == null) throw new ArgumentException("Missing \"name\"!", nameof(data));
            if (Duration < 0f) throw new ArgumentException("Missing or invalid \"duration\" value!", nameof(data));
            if(CameraPath.Count == 0)
            {
                CameraPath.Add(new Vector3(0f, 0f, 3f));
            }
        }

        private void LoadValue(string name, object value)
        {
            try
            {
                switch (name)
                {
                    case "name": SceneName = Convert.ToString(value); break;
                    case "duration": Duration = Convert.ToSingle(value); break;
                    case "fadein": FadeIn = Convert.ToSingle(value); break;
                    case "fadeout": FadeOut = Convert.ToSingle(value); break;
                    case "campath":
                        {
                            JsonList list = (JsonList)value;
                            for (int i = 0; i < list.Count - 2; i += 3)
                            {
                                CameraPath.Add(new Vector3(
                                    Convert.ToSingle(list[i + 0]),
                                    Convert.ToSingle(list[i + 1]),
                                    Convert.ToSingle(list[i + 2])
                                ));
                            }
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Slide property \"{name}\" cannot hold a value of type \"{value.GetType().Name}\"!");
                Debug.LogException(e);
            }
        }
    }
}
