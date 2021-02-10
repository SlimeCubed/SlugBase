using System;
using System.IO;
using RWCustom;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using Menu;
using System.Text;

namespace SlugBase
{
    using static CustomSceneManager;

    /// <summary>
    /// The core of adding a custom slugcat to be added with <see cref="PlayerManager.RegisterPlayer(CustomPlayer)"/>.
    /// You must derive a class from this to represent the character to add.
    /// </summary>
    public abstract class CustomPlayer
    {
        internal int useSpawns;
        // Try not to use this for anything saved!
        // This is for situations where an index is expected by the vanilla game or other mods
        internal int slugcatIndex = -1;
        internal PlayerFormatVersion version;
        private bool devMode;

        /// <summary>
        /// Create a new custom slugcat.
        /// </summary>
        /// <param name="name">The name of the custom slugcat, containing only alphanumericals, underscores, and spaces.</param>
        /// <param name="useSpawns">
        /// The slugcat to copy creatures and world state from.
        /// The value should be 0 (survivor), 1 (monk) or 2 (hunter).
        /// Values outside of this range are allowed, but the vanilla game's world files are not set up to use them correctly.
        /// </param>
        /// <remarks>
        /// The name of this slugcat must be unique; other mods that add a slugcat of this same name will throw an exception
        /// when registering their character. If your name is likely to cause conflicts (such as any of the vanilla achievement
        /// names), then consider prefixing your player's name with some other text, like the author's name.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when input name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input name is empty or contains illegal characters.</exception>
        public CustomPlayer(string name, PlayerFormatVersion version, int useSpawns = 0)
        {
            Name = "NULL";
            if (name == null) throw new ArgumentNullException("Slugcat name may not be null.", nameof(name));
            if (name == "") throw new ArgumentException("Slugcat name may not be empty.", nameof(name));
            if (!Regex.IsMatch(name, "^[\\w ]+$")) throw new ArgumentException("Slugcat name must contain only alphanumericals, underscores, and spaces.", nameof(name));
            Name = name;

            this.useSpawns = useSpawns;
            this.version = version;
        }

        /// <summary>
        /// Enables or disables developer mode.
        /// </summary>
        public bool DevMode {
            get => devMode;
            set {
                if (value) Debug.Log($"WARNING! Developer mode has been enabled for custom slugcat \"{Name}\"!");
                devMode = value;
            }
        }


        //////////////
        // GAMEPLAY //
        //////////////

        /// <summary>
        /// The room that this player begins in when starting a new game.
        /// Any values other than the default, "SU_C04", may need code to determine where to place the player.
        /// </summary>
        public virtual string StartRoom => "SU_C04";

        /// <summary>
        /// Called once as a game starts from the select screen or multiplayer menu.
        /// </summary>
        /// <remarks>
        /// This is called as soon possible, the moment that user's character choice is locked in.
        /// Effects that must apply before the <see cref="RainWorldGame"/> instance is created may be done here.
        /// </remarks>
        protected internal virtual void Prepare() { }

        /// <summary>
        /// Called once when a game is started as this character.
        /// </summary>
        protected internal abstract void Enable();

        /// <summary>
        /// Called once when a game is ended as this character.
        /// </summary>
        protected internal abstract void Disable();

        /// <summary>
        /// Modifies a <see cref="SlugcatStats"/> instance to contain the stats for this character.
        /// </summary>
        /// <remarks>
        /// By default, this is the exact same as Survivor's stats.
        /// Make sure to take <see cref="SlugcatStats.malnourished"/> into account.
        /// </remarks>
        /// <param name="stats">The instance of <see cref="SlugcatStats"/> to modify.</param>
        protected internal virtual void GetStats(SlugcatStats stats)
        {
        }

        internal void GetStatsInternal(SlugcatStats stats)
        {
            stats.throwingSkill = stats.malnourished ? 0 : 1;
            stats.name = (SlugcatStats.Name)slugcatIndex;
            GetStats(stats);
        }

        /// <summary>
        /// Get the amount of food that this character needs to sleep and the total amount that it can hold.
        /// Defaults to Survivor's food meter.
        /// </summary>
        /// <param name="maxFood">The amount of food that this character can hold.</param>
        /// <param name="foodToSleep">The amount of food that this character needs to sleep.</param>
        public virtual void GetFoodMeter(out int maxFood, out int foodToSleep)
        {
            maxFood = 7;
            foodToSleep = 4;
        }

        /// <summary>
        /// Checks whether or not this player can eat the meat of a certain creature.
        /// By default, this returns the same as the vanilla game, which is true for dead centipedes and false for all other creatures.
        /// </summary>
        /// <param name="player">The player that is trying to eat.</param>
        /// <param name="creature">The creature that the player is tring to eat.</param>
        /// <returns>True if the creature can be eaten, false otherwise.</returns>
        public virtual bool CanEatMeat(Player player, Creature creature) {
            return player.CanEatMeat(creature);
        }

        /// <summary>
        /// True if this character receives a quarter pips for non-meat foods.
        /// Defaults to false.
        /// </summary>
        public virtual bool QuarterFood => false;

        /// <summary>
        /// True if karma gates unlock permanently for this character, such as when playing as Monk.
        /// Defaults to false.
        /// </summary>
        public virtual bool GatesPermanentlyUnlock => false;

        /// <summary>
        /// Gets how many minutes this cycle should last.
        /// Defaults to null.
        /// </summary>
        /// <returns>The number of minutes the cycle should last, or null to use the default.</returns>
        public virtual float? GetCycleLength() => null;

        /// <summary>
        /// True if this character can skip the temple guardians with a flashbang.
        /// Defaults to true.
        /// </summary>
        public virtual bool CanSkipTempleGuards => true;

        /////////////
        // DISPLAY //
        /////////////

        /// <summary>
        /// The name of the custom slugcat.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the default directory that contains resources for this slugcat.
        /// </summary>
        protected internal string DefaultResourcePath => Path.Combine(PlayerManager.ResourceDirectory, Name);

        /// <summary>
        /// The name to display to the user, such as on the player select screen.
        /// </summary>
        /// <remarks>
        /// This defaults to your custom slugcat's internal name.
        /// This should be overridden for localization, or if the name the user is shown must differ from its internal name.
        /// </remarks>
        public virtual string DisplayName => Name;

        /// <summary>
        /// The description of this custom player to be displayed on the player select screen.
        /// All instances of "&lt;LINE&gt;" will be replaced with a line break.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Get the color used for this slugcat's UI and in-game sprites.
        /// </summary>
        /// <returns>The color to use, or null to use the default for this save slot.</returns>
        public virtual Color? SlugcatColor() => null;

        ///////////
        // SAVES //
        ///////////

        /// <summary>
        /// Creates an empty save state for this character.
        /// </summary>
        /// <remarks>
        /// If your character needs to save more data, this may be overridden 
        /// to return a new instance of a child class of <see cref="CustomSaveState"/>.
        /// </remarks>
        public virtual CustomSaveState CreateNewSave(PlayerProgression progression) => new CustomSaveState(progression, this);


        ///////////////
        // RESOURCES //
        ///////////////

        /// <summary>
        /// Gets a stream containing the specified resource.
        /// By default this reads from a file inside of <see cref="DefaultResourcePath"/>.
        /// </summary>
        /// <remarks>
        /// This is intended to be overridden to load from different locations, such as from embedded resources via <see cref="Assembly.GetManifestResourceStream(string)"/>.
        /// This should return null if the resource does not exist, or there was a problem accessing the resource.
        /// Make sure to dispose of it!
        /// </remarks>
        /// <param name="path">The relative location of the resource.</param>
        /// <returns>A stream of data for the specified resource, or null if the resource does not exist.</returns>
        public virtual Stream GetResource(params string[] path)
        {
            try
            {
                //Debug.Log($"Loading {Name} resource from \"{string.Join("/", path)}\"");
                return File.OpenRead(Path.Combine(DefaultResourcePath, string.Join(Path.DirectorySeparatorChar.ToString(), path)));
            } catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a resource, then decodes it as a UTF-8 string.
        /// </summary>
        /// <param name="path">The relative location of the resource.</param>
        /// <returns>A string if the resource was found, or null if the resource does not exist or was not valid UTF-8.</returns>
        public string GetStringResource(params string[] path)
        {
            using (Stream stream = GetResource(path))
            {
                if (stream == null) return null;
                try
                {
                        StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                        return sr.ReadToEnd();
                } catch(Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Retrieves information about a custom scene.
        /// </summary>
        /// <remarks>
        /// This is intended to be overridden if you need more control over which images are loaded in a scene.
        /// For example, this may be changed to enable or disable background elements based on save state.
        /// <para>The returned images should be ordered from lowest to highest depth.</para>
        /// </remarks>
        /// <param name="sceneName">The name of the scene to build.</param>
        public virtual CustomScene BuildScene(string sceneName)
        {
            using (Stream resource = GetResource("Scenes", sceneName, "scene.json"))
            {
                using (StreamReader sr = new StreamReader(resource))
                {
                    return new CustomScene(this, sceneName, sr.ReadToEnd());
                }
            }
        }

        /// <summary>
        /// Retrieves information about a custom slideshow.
        /// </summary>
        /// <remarks>
        /// This is intended to be overridden if you need to change which images are loaded for a slideshow
        /// without changing the JSON file.
        /// </remarks>
        /// <param name="slideshowName">The name of the slideshow to build.</param>
        public virtual CustomSlideshow BuildSlideshow(string slideshowName)
        {
            using (Stream resource = GetResource("Slideshows", $"{slideshowName}.json"))
            {
                using (StreamReader sr = new StreamReader(resource))
                {
                    return new CustomSlideshow(this, slideshowName, sr.ReadToEnd());
                }
            }
        }

        private readonly Dictionary<string, bool> hasSceneCache = new Dictionary<string, bool>();
        /// <summary>
        /// Checks whether or not this player defines a custom scene.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check for.</param>
        /// <returns>True if the scene exists, false if it does not.</returns>
        public bool HasScene(string sceneName)
        {
            if (!DevMode && hasSceneCache.TryGetValue(sceneName, out bool hasScene)) return hasScene;
            using (Stream res = GetResource("Scenes", sceneName, "scene.json"))
            {
                hasScene = res != null;
                if(!DevMode)
                    hasSceneCache[sceneName] = hasScene;
                return hasScene;
            }
        }

        private readonly Dictionary<string, bool> hasSlideshowCache = new Dictionary<string, bool>();
        /// <summary>
        /// Checks whether or not this player defines a custom slideshow.
        /// </summary>
        /// <param name="slideshowName">The name of the slideshow to check for.</param>
        /// <returns>True if the slideshow exists, false if it does not.</returns>
        public bool HasSlideshow(string slideshowName)
        {
            if (!DevMode && hasSlideshowCache.TryGetValue(slideshowName, out bool hasSlideshow)) return hasSlideshow;
            using (Stream res = GetResource("Slideshows", $"{slideshowName}.json"))
            {
                hasSlideshow = res != null;
                if (!DevMode)
                    hasSlideshowCache[slideshowName] = hasSlideshow;
                return hasSlideshow;
            }
        }

        /// <summary>
        /// Replaces the next <see cref="MenuScene"/> with a scene loaded from this character's resources.
        /// <see cref="ClearSceneOverride"/> may be used to abort this.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        /// <param name="filter">A delegate that returns true for each image that should be shown in the scene, or null to show all.</param>
        protected void OverrideNextScene(string sceneName, SceneImageFilter filter)
        {
            CustomSceneManager.OverrideNextScene(this, sceneName, filter);
        }

        /// <summary>
        /// Aborts a scene override from <see cref="OverrideNextScene(string, SceneImageFilter)"/>.
        /// </summary>
        protected void ClearSceneOverride()
        {
            CustomSceneManager.ClearSceneOverride();
        }

        /////////////////////////////
        // BACKWARDS COMPATIBILITY //
        /////////////////////////////

        /// <summary>
        /// Indicates which version this mod was built with.
        /// </summary>
        /// <remarks>
        /// This is intended to help preserve backwards compatibility, should any otherwise breaking changes apply to new versions.
        /// When first creating a mod, you should always use the most recent version.
        /// </remarks>
        public enum PlayerFormatVersion
        {
            V_0_1 = 0
        }
    }
}
