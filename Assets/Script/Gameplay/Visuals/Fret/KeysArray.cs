using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public class KeysArray : MonoBehaviour
    {
        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private float _whiteKeyOffset;
        [SerializeField]
        private float _blackKeyOffset;

        public float KeySpacing => _trackWidth / ProKeysPlayer.WHITE_KEY_VISIBLE_COUNT;

        private readonly List<Fret> _keys = new();

        public void Initialize(ThemePreset themePreset)
        {
            var whiteKeyPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, GameMode.ProKeys,
                ThemeManager.WHITE_KEY_PREFAB_NAME);
            var blackKeyPrefab = ThemeManager.Instance.CreateFretPrefabFromTheme(themePreset, GameMode.ProKeys,
                ThemeManager.BLACK_KEY_PREFAB_NAME);

            // Pro-keys always starts at C

            _keys.Clear();
            int whitePositionIndex = 0;
            int blackPositionIndex = 0;

            for (int i = 0; i < ProKeysPlayer.TOTAL_KEY_COUNT; i++)
            {
                // The index within the octave (0-11)
                int noteIndex = i % 12;

                if (PianoHelper.IsBlackKey(noteIndex))
                {
                    // Black keys

                    var fret = Instantiate(blackKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        blackPositionIndex * KeySpacing + _blackKeyOffset, 0f, 0f);

                    _keys.Add(fret.GetComponent<Fret>());

                    blackPositionIndex++;
                    if (PianoHelper.IsGapOnNextBlackKey(noteIndex))
                    {
                        blackPositionIndex++;
                    }
                }
                else
                {
                    // White keys

                    var fret = Instantiate(whiteKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        whitePositionIndex * KeySpacing + _whiteKeyOffset, 0f, 0f);

                    _keys.Add(fret.GetComponent<Fret>());

                    whitePositionIndex++;
                }
            }
        }

        public float GetKeyX(int index)
        {
            return _keys[index].transform.localPosition.x;
        }
    }
}
