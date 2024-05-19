using UnityEngine;
using YARG.Core.Game;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

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

        public void Initialize(TrackPlayer player, ThemePreset themePreset)
        {
            int overlayPositionIndex = 0;

            for (int i = 0; i < ProKeysPlayer.TOTAL_KEY_COUNT; i++)
            {
                // The index within the octave (0-11)
                int octaveIndex = i % 12;

                // Only on white keys
                if (octaveIndex is not (1 or 3 or 6 or 8 or 10))
                {
                    var overlay = Instantiate(_keyOverlayPrefab, transform);
                    overlay.SetActive(true);
                    overlay.transform.localPosition = new Vector3(
                        overlayPositionIndex * KeySpacing + _overlayOffset, 0f, 0f);

                    var material = overlay.GetComponentInChildren<MeshRenderer>().material;

                    // Temporary colors probably
                    var color = overlayPositionIndex switch
                    {
                        < 3            => ColorProfile.Default.FiveFretGuitar.RedNote.ToUnityColor(),
                        >= 3 and < 7   => ColorProfile.Default.FiveFretGuitar.YellowNote.ToUnityColor(),
                        >= 7 and < 10  => ColorProfile.Default.FiveFretGuitar.BlueNote.ToUnityColor(),
                        >= 10 and < 14 => ColorProfile.Default.FiveFretGuitar.GreenNote.ToUnityColor(),
                        _              => ColorProfile.Default.FiveFretGuitar.OrangeNote.ToUnityColor()
                    };
                    color.a = 0.2f;
                    material.color = color;

                    material.SetFade(player.ZeroFadePosition, player.FadeSize);

                    // White keys don't have any gaps
                    overlayPositionIndex++;
                }
            }
        }
    }
}