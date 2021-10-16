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
    /// <summary>
    /// The core of adding a custom character to be registered with <see cref="PlayerManager.RegisterCharacter(SlugBaseCharacter)"/>.
    /// You must derive a class from this to represent the character to add.
    /// </summary>
    public abstract class SlugBaseCharacter
    {
        internal int useSpawns;
        // Try not to use this for anything saved!
        // This is for situations where an index is expected by the vanilla game or other mods
        internal FormatVersion version;
        private int slugcatIndex = -1;
        private bool devMode;
        private EnabledState enabledState;

        /// <summary>
        /// Create a new custom character.
        /// </summary>
        /// <param name="name">The name of the custom character, containing only alphanumericals, underscores, and spaces.</param>
        /// <param name="version">The version this mod was first built with.</param>
        /// <param name="useSpawns">
        /// The character to copy creatures and world state from.
        /// The value should be 0 (survivor), 1 (monk) or 2 (hunter).
        /// Values outside of this range are allowed, but the vanilla game's world files are not set up to use them correctly.
        /// </param>
        /// <remarks>
        /// The name of this character must be unique; other mods that add a character of this same name will throw an exception
        /// when registering their character. If your name is likely to cause conflicts (such as any of the vanilla achievement
        /// names), then consider prefixing your player's name with some other text, like the author's name.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when input name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input name is empty or contains illegal characters.</exception>
        public SlugBaseCharacter(string name, FormatVersion version, int useSpawns = 0)
        {
            if (name == null) throw new ArgumentNullException("Name may not be null.", nameof(name));
            if (name == "") throw new ArgumentException("Name may not be empty.", nameof(name));
            if (!PlayerManager.IsValidCharacterName(name)) throw new ArgumentException("Name must contain only alphanumericals, underscores, and spaces.", nameof(name));
            Name = name;

            this.useSpawns = useSpawns;
            this.version = version;
        }

        /// <summary>
        /// Enables or disables developer mode.
        /// </summary>
        /// <remarks>
        /// Setting this to true prevents the default implementations of <see cref="HasScene(string)"/>
        /// and <see cref="HasSlideshow(string)"/> from caching their results. Doing this will lead to
        /// more file operations, but will allow new scenes to be added without restarting the game.
        /// </remarks>
        public bool DevMode {
            get => devMode;
            set {
                if (value) Debug.Log($"WARNING! Developer mode has been enabled for SlugBase character \"{Name}\"!");
                devMode = value;
            }
        }

        /// <summary>
        /// The slugcat index of this character.
        /// </summary>
        /// <remarks>
        /// Use this value only when absolutely necessary.
        /// This value may be reassigned between sessions or when in menus.
        /// </remarks>
        public int SlugcatIndex {
            get => slugcatIndex > -1 ? slugcatIndex : slugcatIndex = SlugBaseMod.FirstCustomIndex + PlayerManager.customPlayers.IndexOf(this);
            internal set => slugcatIndex = value;
        }

        //////////////
        // GAMEPLAY //
        //////////////

        /// <summary>
        /// The room that this player begins in when starting a new game.
        /// Any values other than null or "SU_C04" may need code to determine where to place the player.
        /// </summary>
        public virtual string StartRoom => null;

        /// <summary>
        /// Called once as a game starts from the select screen or multiplayer menu.
        /// </summary>
        /// <remarks>
        /// This is called as soon possible, the moment that user's character choice is locked in.
        /// </remarks>
        protected virtual void Prepare() { }

        /// <summary>
        /// True if the current RainWorldGame instance is using this character.
        /// </summary>
        public bool Enabled => enabledState == EnabledState.Enabled;

        /// <summary>
        /// Called once when a game is started as this character.
        /// </summary>
        protected abstract void Enable();

        /// <summary>
        /// Called once when a game is ended as this character.
        /// </summary>
        protected abstract void Disable();

        internal void EnableInternal()
        {
            while (enabledState != EnabledState.Enabled)
                NextState();
        }

        internal void DisableInternal()
        {
            while (enabledState != EnabledState.Disabled)
                NextState();
        }

        internal void PrepareInternal()
        {
            while (enabledState != EnabledState.Prepared)
                NextState();
        }

        private void NextState()
        {
            try
            {
                switch (enabledState)
                {
                    case EnabledState.Disabled:
                        enabledState = EnabledState.Prepared;
                        Prepare();
                        break;
                    case EnabledState.Prepared:
                        enabledState = EnabledState.Enabled;

                        if (PlayerManager.CurrentCharacter != null)
                            Debug.Log($"Character mismatch! Enabled \"{Name}\" while playing as \"{PlayerManager.CurrentCharacter.Name}\"!");
                        PlayerManager.CurrentCharacter = this;

                        Enable();
                        break;
                    case EnabledState.Enabled:
                        enabledState = EnabledState.Disabled;
                        
                        if (PlayerManager.CurrentCharacter == this)
                            PlayerManager.CurrentCharacter = null;
                        else
                            Debug.Log($"Character mismatch! Disabled \"{Name}\" while playing as \"{PlayerManager.CurrentCharacter?.Name ?? "None"}\"!");

                        Disable();
                        break;
                }
            }
            catch(Exception e)
            {
                Debug.LogException(new Exception($"Failed to change enabled state for SlugBase character \"{Name}\".", e));
            }
        }

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
            stats.name = (SlugcatStats.Name)SlugcatIndex;
            GetStats(stats);
        }

        /// <summary>
        /// Called once after the room containing the player is loaded on a new save. Note that <paramref name="room"/> isn't necessarily <see cref="StartRoom"/>, so make sure to check <see cref="AbstractRoom.name"/> before running any room-specific scripts.
        /// </summary>
        /// <param name="room">The room containing the player.</param>
        public virtual void StartNewGame(Room room)
        {
        }

        /// <summary>
        /// Get the amount of food that this character needs to sleep and the total amount that it can hold.
        /// Defaults to Survivor's food meter.
        /// </summary>
        /// <remarks>
        /// Changing this will result in the tutorial text being incorrect. Consider changing it, or
        /// disabling it by setting <see cref="HasGuideOverseer"/> to false.
        /// </remarks>
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

        /// <summary>
        /// True if the tutorial overseer follows this character.
        /// Defaults to true.
        /// </summary>
        public virtual bool HasGuideOverseer => true;

        /// <summary>
        /// True if dreams can play when this character is selected.
        /// Defaults to false.
        /// </summary>
        public virtual bool HasDreams => false;

        /////////////
        // DISPLAY //
        /////////////

        /// <summary>
        /// The name of the character.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The name to display to the user, such as on the player select screen.
        /// </summary>
        /// <remarks>
        /// Defaults to your character's internal name.
        /// This should start with "The", and the first letter of each word should be capitalized.
        /// </remarks>
        public virtual string DisplayName => Name;

        /// <summary>
        /// The description of this SlugBase character to be displayed on the player select screen.
        /// All instances of "&lt;LINE&gt;" will be replaced with a line break.
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Get the color used for this character's UI and in-game sprites.
        /// </summary>
        /// <returns>The color to use, or null to use the default for this save slot.</returns>
        public virtual Color? SlugcatColor() => null;

        /// <summary>
        /// Gets the colors of this character's eyes.
        /// </summary>
        /// <returns>The color to use, or null to use the default for this save slot.</returns>
        public virtual Color? SlugcatEyeColor() => null;

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

        /// <summary>
        /// Determines whether this character should be shown on the select screen, shown on the select screen but not able to be chosen, or hidden completely.
        /// This is checked once when the select menu is first opened.
        /// </summary>
        public virtual SelectMenuAccessibility GetSelectMenuState(SlugcatSelectMenu menu) => SelectMenuAccessibility.Available;

        /// <summary>
        /// Returns a summary of this character's save file or null if no save file exists.
        /// </summary>
        /// <param name="rainWorld">The current <see cref="RainWorld"/> instance.</param>
        /// <returns></returns>
        public SaveManager.SlugBaseSaveSummary GetSaveSummary(RainWorld rainWorld)
        {
            return SaveManager.GetSaveSummary(rainWorld, Name, rainWorld.options.saveSlot);
        }

        /// <summary>
        /// The slugcat index to use for the world, such as placed object filters or creature spawns.
        /// </summary>
        /// <remarks>
        /// This may be set through the <c>useSpawns</c> parameter of <see cref="SlugBaseCharacter(string, FormatVersion, int)"/>.
        /// </remarks>
        public int WorldCharacter => useSpawns;

        /// <summary>
        /// Describes if a character should be shown on the character select menu and whether it should be selectable.
        /// </summary>
        public enum SelectMenuAccessibility
        {
            /// <summary>
            /// This character is shown and can be selected.
            /// </summary>
            Available,
            /// <summary>
            /// This character is shown but may not be selected. Consider overriding <see cref="BuildScene(string)"/> to replace images in "SelectMenu" with greyed-out versions.
            /// </summary>
            Locked,
            /// <summary>
            /// This character does not have an entry on the select menu.
            /// </summary>
            Hidden
        }

        //////////////////////////
        // SCENES AND RESOURCES //
        //////////////////////////

        /// <summary>
        /// Gets the default directory that contains resources for this character.
        /// </summary>
        protected internal string DefaultResourcePath => Path.Combine(PlayerManager.ResourceDirectory, Name);

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
                //Debug.Log($"Getting resource: {DefaultResourcePath}\\{string.Join("\\", path)}");
                return File.OpenRead(Path.Combine(DefaultResourcePath, string.Join(Path.DirectorySeparatorChar.ToString(), path)));
            }
            catch //(Exception e)
            {
                //Debug.Log(e);
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
        public virtual bool HasScene(string sceneName)
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
        public virtual bool HasSlideshow(string slideshowName)
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
        public enum FormatVersion
        {
            /// <summary>
            /// The current version. Use this one.
            /// </summary>
            V1 = 0
        }

        private enum EnabledState
        {
            Disabled,
            Prepared,
            Enabled
        }
    }
}
