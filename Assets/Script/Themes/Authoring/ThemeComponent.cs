﻿using System;
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

        [Space]
        [SerializeField]
        private GameObject _fiveFretFret;
        [SerializeField]
        private GameObject _fourLaneFret;
        [SerializeField]
        private GameObject _fiveLaneFret;

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

        public GameObject GetModelForGameMode<T>(GameMode gameMode)
        {
            // TODO: Make this cleaner

            if (typeof(T) == typeof(ThemeFret))
            {
                return GetFretModelForGameMode(gameMode);
            }

            if (typeof(T) == typeof(ThemeKickFret))
            {
                return _kickFret;
            }

            throw new Exception("Unreachable.");
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