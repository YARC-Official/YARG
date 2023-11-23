using System.Collections.Generic;
using UnityEngine;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public interface IThemePrefabCreator
    {
        public void SetModels(Dictionary<ThemeNoteType, GameObject> models);
    }
}