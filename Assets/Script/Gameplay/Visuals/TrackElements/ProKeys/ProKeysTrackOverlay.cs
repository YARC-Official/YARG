using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Gameplay.Player;
using YARG.Helpers;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Visuals
{
    public class ProKeysTrackOverlay : MonoBehaviour
    {
        [SerializeField]
        private GameObject _keyOverlayPrefab;

        [SerializeField]
        private float _trackWidth = 2f;

        [Space]
        [SerializeField]
        private float _overlayOffset;

        public float KeySpacing => _trackWidth / ProKeysPlayer.WHITE_KEY_VISIBLE_COUNT;

        private List<Material> _overlays = new();

        private static readonly int KeyHeld   = Shader.PropertyToID("_KeyHeld");
        private static readonly int HeldColor = Shader.PropertyToID("_HeldColor");

        public void Initialize(TrackPlayer player, ColorProfile.ProKeysColors colors)
        {
            int overlayPositionIndex = 0;

            for (int i = 0; i < ProKeysPlayer.TOTAL_KEY_COUNT; i++)
            {
                int noteIndex = i % 12;
                int octaveIndex = i / 12;

                // Only create overlays on white keys
                if (PianoHelper.IsWhiteKey(noteIndex))
                {
                    var overlay = Instantiate(_keyOverlayPrefab, transform);
                    overlay.SetActive(true);
                    overlay.transform.localPosition = new Vector3(
                        overlayPositionIndex * KeySpacing + _overlayOffset, 0f, 0f);

                    var material = overlay.GetComponentInChildren<MeshRenderer>().material;

                    // Get the group index (two groups per octave)
                    int index = octaveIndex * 2 + (PianoHelper.IsLowerHalfKey(noteIndex) ? 0 : 1);
                    var color = colors.GetOverlayColor(index).ToUnityColor();
                    color.a = 0.2f;
                    material.color = color;

                    color.a = 0.5f;
                    material.SetColor(HeldColor, color);

                    material.SetFade(player.ZeroFadePosition, player.FadeSize);
                    _overlays.Add(material);

                    overlayPositionIndex++;
                }
            }
        }

        public void SetKeyHeld(int keyIndex, bool held)
        {
            if(keyIndex < 0 || keyIndex >= _overlays.Count)
                return;

            YargLogger.LogFormatDebug("Setting key held: {0}", keyIndex);

            _overlays[keyIndex].SetFloat(KeyHeld, held ? 1f : 0f);
        }
    }
}
