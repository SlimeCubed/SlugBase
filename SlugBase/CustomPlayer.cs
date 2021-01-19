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
    using static CustomScenes;

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
            GetStats(stats);
        }

        /// <summary>
        /// Get the amount of food that this character needs to sleep and the total amount that it can hold.
        /// Defaults to Survivor's food meter.
        /// </summary>
        /// <param name="maxFood">The amount of food that this character can hold.</param>
        /// <param name="foodToSleep">The amount of food that this character needs to sleep.</param>
        protected internal virtual void GetFoodMeter(out int maxFood, out int foodToSleep)
        {
            maxFood = 7;
            foodToSleep = 4;
        }

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
        protected string DefaultResourcePath => Path.Combine(PlayerManager.ResourceDirectory, Name);

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
        /// <param name="progression"></param>
        /// <returns></returns>
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
        /// </remarks>
        /// <param name="path">The relative location of the resource.</param>
        /// <returns>A stream of data for the specified resource, or null if the resource does not exist.</returns>
        public virtual Stream GetResource(params string[] path)
        {
            try
            {
                Debug.Log($"Loading {Name} resource from \"{string.Join("/", path)}\"");
                return File.OpenRead(Path.Combine(DefaultResourcePath, string.Join(Path.DirectorySeparatorChar.ToString(), path)));
            } catch(Exception e)
            {
                Debug.Log($"Failed: {e}");
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
            Stream stream = GetResource(path);
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

        /// <summary>
        /// Retrieves a list of images to display for the given scene.
        /// </summary>
        /// <remarks>
        /// This is intended to be overridden if you need more control over which images are loaded in a scene.
        /// For example, this may be changed to enable or disable background elements based on save state.
        /// </remarks>
        /// <param name="sceneName">The name of the scene to build.</param>
        public virtual List<CustomSceneImage> BuildScene(string sceneName)
        {
            List<CustomSceneImage> images = new List<CustomSceneImage>();
            StreamReader sr = new StreamReader(GetResource("Scenes", sceneName, "settings.txt"));
            string line;
            while((line = sr.ReadLine()) != null)
            {
                try
                {
                    images.Add(CustomSceneImage.Parse(line));
                } catch(Exception e)
                {
                    Debug.LogException(e);
                    break;
                }
            }
            return images;
        }

        /// <summary>
        /// Checks whether or not this player defines a custom scene.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check for.</param>
        /// <returns>True if the scene exists, false if it does not.</returns>
        public bool HasScene(string sceneName)
        {
            using (Stream res = GetResource("Scenes", sceneName, "settings.txt"))
                return res != null;
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
