using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Gameplay.Player;
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
                int octaveIndex = i % 12;

                if (octaveIndex is 1 or 3 or 6 or 8 or 10)
                {
                    // Black keys

                    var fret = Instantiate(blackKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        blackPositionIndex * KeySpacing + _blackKeyOffset, 0f, 0f);

                    _keys.Add(fret.GetComponent<Fret>());

                    // Keys 3 and 10 have gaps after them, so add two in that case
                    blackPositionIndex++;
                    if (octaveIndex is 3 or 10)
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

                    // White keys don't have any gaps
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