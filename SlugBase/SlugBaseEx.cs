using Menu;

namespace SlugBase
{
    /// <summary>
    /// Extension helper methods for SlugBase mods.
    /// </summary>
    public static class SlugBaseEx
    {
        /// <summary>
        /// Finds the save file associated with this player.
        /// </summary>
        /// <typeparam name="T">The save file type to search for. This will likely be a child of <see cref="CustomSaveState"/>.</typeparam>
        /// <param name="player">The player that owns the save file.</param>
        /// <param name="save">The save file that was found.</param>
        /// <returns>True if a save file of the appropriate type was found, false otherwise.</returns>
        public static bool TryGetSave<T>(this Player player, out T save) where T : SaveState
        {
            save = null;
            return player.room?.game?.TryGetSave(out save) ?? false;
        }

        /// <summary>
        /// Finds the save file associated with this session.
        /// </summary>
        /// <typeparam name="T">The save file type to search for. This will likely be a child of <see cref="CustomSaveState"/>.</typeparam>
        /// <param name="game">The current <see cref="RainWorldGame"/> instance.</param>
        /// <param name="save">The save file that was found.</param>
        /// <returns>A save file or null if it was not found or did not match the given type.</returns>
        public static bool TryGetSave<T>(this RainWorldGame game, out T save) where T : SaveState
        {
            if (game?.GetStorySession?.saveState is T t)
            {
                save = t;
                return true;
            }
            else
            {
                save = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the <see cref="SceneImage"/> associated with this illustration if it was built from a <see cref="SlugBaseCharacter"/>'s resources.
        /// </summary>
        /// <param name="illustration">The menu illustration to check.</param>
        /// <returns>The <see cref="SceneImage"/> associated with this illustration or null if it was not built from a <see cref="SlugBaseCharacter"/>'s resources.</returns>
        public static SceneImage GetCustomImage(this MenuIllustration illustration)
        {
            return CustomSceneManager.customRep[illustration];
        }
    }
}
