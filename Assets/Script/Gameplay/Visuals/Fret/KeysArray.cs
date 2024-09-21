using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Game;
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

        private TrackPlayer _player;

        private readonly List<Fret> _keys = new();

        private static readonly int IndexId = Shader.PropertyToID("_Index");

        public void Initialize(TrackPlayer player, ThemePreset themePreset, ColorProfile.ProKeysColors colors)
        {
            _player = player;

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
                int octaveIndex = i / 12;

                if (ProKeysUtilities.IsBlackKey(noteIndex))
                {
                    // Black keys

                    var fret = Instantiate(blackKeyPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localPosition = new Vector3(
                        blackPositionIndex * KeySpacing + _blackKeyOffset, 0f, 0f);

                    int group = octaveIndex * 2 + (ProKeysUtilities.IsLowerHalfKey(noteIndex) ? 0 : 1);
                    var color = colors.GetBlackKeyColor(group);

                    var fretComp = fret.GetComponent<Fret>();
                    fretComp.Initialize(color, color, color);

                    var material = fret.GetComponentInChildren<MeshRenderer>().material;
                    material.SetFloat(IndexId, player.PlayerIndex);

                    _keys.Add(fretComp);

                    blackPositionIndex++;
                    if (ProKeysUtilities.IsGapOnNextBlackKey(noteIndex))
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

                    var color = colors.WhiteKey;

                    var fretComp = fret.GetComponent<Fret>();
                    fretComp.Initialize(color, color, color);

                    var material = fret.GetComponentInChildren<MeshRenderer>().material;
                    material.SetFloat(IndexId, player.PlayerIndex);

                    _keys.Add(fretComp);

                    whitePositionIndex++;
                }
            }
        }

        public float GetKeyX(int index)
        {
            return _keys[index].transform.localPosition.x;
        }

        public void SetPressed(int index, bool pressed)
        {
            _keys[index].SetPressed(pressed);
        }

        public void PlayHitAnimation(int index)
        {
            _keys[index].PlayHitAnimation();
            _keys[index].PlayHitParticles();
        }
    }
}