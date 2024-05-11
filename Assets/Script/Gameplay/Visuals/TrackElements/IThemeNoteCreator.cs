using System.Collections.Generic;
using UnityEngine;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public interface IThemeNoteCreator
    {
        /// <summary>
        /// Called once during the creation of the <b>first</b> themed note prefab.
        /// </summary>
        /// <param name="models">
        /// A dictionary of <see cref="ThemeNoteType"/> <see cref="GameObject"/> pairs.
        /// </param>
        /// <param name="starPowerModels">
        /// Same as the <paramref name="models"/> parameter but for starpower variants.
        /// The key for a specific <see cref="ThemeNoteType"/> may not be present if the theme
        /// doesn't specify a star power variant.
        /// </param>
        public void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels);
    }
}