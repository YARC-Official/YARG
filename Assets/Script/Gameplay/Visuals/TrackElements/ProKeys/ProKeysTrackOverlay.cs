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
        private float _whiteKeyOffset;
        [SerializeField]
        private float _blackKeyOffset;

        public float KeySpacing => _trackWidth / ProKeysPlayer.WHITE_KEY_VISIBLE_COUNT;

        public void Initialize(ThemePreset themePreset)
        {
            int whitePositionIndex = 0;
            int blackPositionIndex = 0;

            for (int i = 0; i < ProKeysPlayer.TOTAL_KEY_COUNT; i++)
            {
                // The index within the octave (0-11)
                int octaveIndex = i % 12;

                if (octaveIndex is 1 or 3 or 6 or 8 or 10)
                {
                    // Black keys

                    // var keyOverlay = Instantiate(_keyOverlayPrefab, transform);
                    // keyOverlay.SetActive(true);
                    // keyOverlay.transform.localPosition = new Vector3(
                    //     blackPositionIndex * KeySpacing + _blackKeyOffset, 0f, 0f);
                    //
                    // //_keys.Add(keyOverlay.GetComponent<Fret>());
                    //
                    // // Keys 3 and 10 have gaps after them, so add two in that case
                    // blackPositionIndex++;
                    // if (octaveIndex is 3 or 10)
                    // {
                    //     blackPositionIndex++;
                    // }
                }
                else
                {
                    // White keys

                    var fret = Instantiate(_keyOverlayPrefab, transform);
                    fret.SetActive(true);
                    fret.transform.localScale = fret.transform.localScale.WithX(0.1f);

                    // Dont know why it needs * 5 but it works
                    fret.transform.localPosition = new Vector3(
                        (whitePositionIndex * KeySpacing + _whiteKeyOffset) * 5, 0f, 0f);

                    // Temporary colors probably
                    var matColor = whitePositionIndex switch
                    {
                        < 3            => ColorProfile.Default.FiveFretGuitar.RedNote.ToUnityColor(),
                        >= 3 and < 7   => ColorProfile.Default.FiveFretGuitar.YellowNote.ToUnityColor(),
                        >= 7 and < 10  => ColorProfile.Default.FiveFretGuitar.BlueNote.ToUnityColor(),
                        >= 10 and < 14 => ColorProfile.Default.FiveFretGuitar.GreenNote.ToUnityColor(),
                        _              => ColorProfile.Default.FiveFretGuitar.OrangeNote.ToUnityColor()
                    };

                    matColor.a = 0.25f;

                    fret.GetComponentInChildren<MeshRenderer>().material.color = matColor;

                    //_keys.Add(fret.GetComponent<Fret>());

                    // White keys don't have any gaps
                    whitePositionIndex++;
                }
            }
        }
    }
}