using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using static YARG.Themes.ThemeManager;

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

        public Dictionary<ThemeNoteType, GameObject> GetNoteModelsForVisualStyle(VisualStyle style, bool starPower)
        {
            var parent = style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys   => _fiveFretNotes,
                VisualStyle.FourLaneDrums  => _fourLaneNotes,
                VisualStyle.FiveLaneDrums  => _fiveLaneNotes,
                VisualStyle.ProKeys        => _proKeysNotes,
                _ => throw new Exception("Unreachable.")
            };

            if (parent == null) throw new Exception($"Theme does not support visual style `{style}`!");

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
                ThemeManager.FRET_PREFAB_NAME      => GetFretModelForVisualStyle(style),
                ThemeManager.KICK_FRET_PREFAB_NAME => _kickFret,
                ThemeManager.WHITE_KEY_PREFAB_NAME => _whiteKey,
                ThemeManager.BLACK_KEY_PREFAB_NAME => _blackKey,
                _                                  => throw new Exception("Unreachable.")
            };
        }

        private GameObject GetFretModelForVisualStyle(VisualStyle style)
        {
            return style switch
            {
                VisualStyle.FiveFretGuitar or
                VisualStyle.FiveLaneKeys => _fiveFretFret,
                VisualStyle.FourLaneDrums  => _fourLaneFret,
                VisualStyle.FiveLaneDrums  => _fiveLaneFret,
                _  => throw new Exception("Unreachable.")
            };
        }
    }
}