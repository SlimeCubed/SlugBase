using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlugBase
{
    /// <summary>
    /// Marks a field of a <see cref="CustomSaveState"/> to be saved automatically.
    /// </summary>
    /// <remarks>
    /// Fields marked with this will be serialized using <see href="https://github.com/gering/Tiny-JSON">Tiny JSON</see>.
    /// Not all classes will support this. If you have a field that has special requirements to be serialized, override
    /// <see cref="CustomSaveState.Save(Dictionary{string, string})"/> and <see cref="CustomSaveState.Load(Dictionary{string, string})"/> instead.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class SavedFieldAttribute : Attribute
    {
        /// <summary>
        /// True if this value should not reset upon death.
        /// </summary>
        public bool PersistOnDeath { get; }

        /// <summary>
        /// Marks a field to be automatically saved.
        /// </summary>
        /// <param name="persistOnDeath">True if this value should not reset upon death.</param>
        public SavedFieldAttribute(bool persistOnDeath = false)
        {
            PersistOnDeath = persistOnDeath;
        }
    }
}
