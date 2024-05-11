using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;

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

        public Dictionary<ThemeNoteType, GameObject> GetNoteModelsForGameMode(GameMode gameMode, bool starPower)
        {
            var parent = gameMode switch
            {
                GameMode.FiveFretGuitar => _fiveFretNotes,
                GameMode.FourLaneDrums  => _fourLaneNotes,
                GameMode.FiveLaneDrums  => _fiveLaneNotes,
                GameMode.ProKeys        => _proKeysNotes,
                _ => throw new Exception("Unreachable.")
            };

            if (parent == null) throw new Exception($"Theme does not support game mode `{gameMode}`!");

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

        public GameObject GetModelForGameMode(GameMode gameMode, string name)
        {
            return name switch
            {
                ThemeManager.FRET_PREFAB_NAME      => GetFretModelForGameMode(gameMode),
                ThemeManager.KICK_FRET_PREFAB_NAME => _kickFret,
                ThemeManager.WHITE_KEY_PREFAB_NAME => _whiteKey,
                ThemeManager.BLACK_KEY_PREFAB_NAME => _blackKey,
                _                                  => throw new Exception("Unreachable.")
            };
        }

        private GameObject GetFretModelForGameMode(GameMode gameMode)
        {
            return gameMode switch
            {
                GameMode.FiveFretGuitar => _fiveFretFret,
                GameMode.FourLaneDrums  => _fourLaneFret,
                GameMode.FiveLaneDrums  => _fiveLaneFret,
                _  => throw new Exception("Unreachable.")
            };
        }
    }
}