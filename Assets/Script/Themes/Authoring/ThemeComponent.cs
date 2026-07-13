using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;


#if UNITY_EDITOR
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
#endif

namespace YARG.Themes
{
    // WARNING: Changing this could break themes or venues!
    //
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    public class ThemeComponent : MonoBehaviour
    {
        [SerializeField]
        private GameObject _fiveFretNotes;
        [SerializeField]
        private GameObject _fourLaneNotes;
        [SerializeField]
        private GameObject _fiveLaneNotes;
        [SerializeField]
        private GameObject _proKeysNotes;

        [Space]
        [SerializeField]
        private GameObject _fiveFretFret;
        [SerializeField]
        private GameObject _fourLaneFret;
        [SerializeField]
        private GameObject _fiveLaneFret;

        [Space]
        [SerializeField]
        private GameObject _whiteKey;
        [SerializeField]
        private GameObject _blackKey;

        [Space]
        [SerializeField]
        private GameObject _kickFret;

        public const string THEME_META_NAME = "theme_meta";

        public Dictionary<ThemeNoteType, GameObject> GetNoteModelsForVisualStyle(VisualStyle style, bool starPower)
        {
            var parent = style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys => _fiveFretNotes,
                VisualStyle.FourLaneDrums => _fourLaneNotes,
                VisualStyle.FiveLaneDrums => _fiveLaneNotes,
                VisualStyle.ProKeys => _proKeysNotes,
                _ => null // future VisualStyle values — caller falls back to default
            };

            if (parent == null) return new Dictionary<ThemeNoteType, GameObject>();

            var dict = new Dictionary<ThemeNoteType, GameObject>();

            // Fetch all of the theme notes
            var themeNotes = parent.GetComponentsInChildren<ThemeNote>();
            foreach (var themeNote in themeNotes)
            {
                // Make sure we choose the correct variant
                if (themeNote.StarPowerVariant != starPower) continue;

                dict.Add(themeNote.NoteType, themeNote.gameObject);
            }

            return dict;
        }

        public GameObject GetModelForVisualStyle(VisualStyle style, string name)
        {
            return name switch
            {
                ThemeManager.FRET_PREFAB_NAME => GetFretModelForVisualStyle(style),
                ThemeManager.KICK_FRET_PREFAB_NAME => _kickFret,
                ThemeManager.WHITE_KEY_PREFAB_NAME => _whiteKey,
                ThemeManager.BLACK_KEY_PREFAB_NAME => _blackKey,
                _ => null
            };
        }

        private GameObject GetFretModelForVisualStyle(VisualStyle style)
        {
            return style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys => _fiveFretFret,
                VisualStyle.FourLaneDrums => _fourLaneFret,
                VisualStyle.FiveLaneDrums => _fiveLaneFret,
                _ => null // future VisualStyle values — caller falls back to default
            };
        }

#if UNITY_EDITOR
        private const string THEME_PREFAB_NAME = "ThemeRoot";
        private const string THEME_PREFAB_PATH = "Assets/" + THEME_PREFAB_NAME + ".prefab";
        private const string THEME_META_PATH = "Assets/" + THEME_META_NAME + ".asset";
        private const string BUNDLE_OSX_SUFFIX = "_metal.bytes";
        private const string BACKGOUND_OSX_MATERIAL_PREFIX = "_metal_";

        [ContextMenu("Export Theme")]
        public void ExportTheme()
        {
            string path = EditorUtility.SaveFilePanel("Export Theme", string.Empty, "theme", "yargtheme");
            if (string.IsNullOrEmpty(path)) return;

            string fileName = Path.GetFileNameWithoutExtension(path);
            TextAsset metaAsset = null;

            // Determine SupportedStyles from non-null fields
            var supportedStyles = new List<VisualStyle>();
            if (_fiveFretNotes != null)
            {
                supportedStyles.Add(VisualStyle.FiveFretGuitar);
                supportedStyles.Add(VisualStyle.FiveLaneKeys);
            }
            if (_fourLaneNotes != null) supportedStyles.Add(VisualStyle.FourLaneDrums);
            if (_fiveLaneNotes != null) supportedStyles.Add(VisualStyle.FiveLaneDrums);
            if (_proKeysNotes != null) supportedStyles.Add(VisualStyle.ProKeys);

            // 5. Create metadata TextAsset
            var preset = new ThemePreset(fileName, false)
            {
                Type = "ThemePreset",
                Id = Guid.NewGuid(),
                CustomBundlePath = "theme.bundle",
                SupportedStyles = supportedStyles,
                FormatVersion = 1
            };

            string jsonText = JsonConvert.SerializeObject(preset, Formatting.Indented);
            metaAsset = new TextAsset(jsonText);
            metaAsset.name = THEME_META_NAME;
            AssetDatabase.CreateAsset(metaAsset, THEME_META_PATH);

            BackgroundHelper.Export(gameObject, BackgroundHelper.ExportType.Generic, new string[] { THEME_META_PATH }, path);

            AssetDatabase.DeleteAsset(THEME_META_PATH);
        }
#endif
    }
}
