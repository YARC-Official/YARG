using System;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;

namespace YARG.Menu
{
    public class StarView : MonoBehaviour
    {
        public enum StarType
        {
            Standard,
            Gold,
            Brutal
        }

        [Header("Star Graphics")]
        [SerializeField]
        private Sprite _emptyStar;
        [SerializeField]
        private Sprite _standardStar;
        [SerializeField]
        private Sprite _goldStar;
        [SerializeField]
        private Sprite _brutalStar;

        [Space]
        [SerializeField]
        private Image[] _starImages;

        public void SetStars(StarAmount stars)
        {
            if (stars == StarAmount.None)
            {
                SetStars(0);
            }
            else if (stars is >= StarAmount.Star1 and <= StarAmount.Star5)
            {
                SetStars((int) stars);
            }
            else
            {
                SetStars(5, stars switch
                {
                    StarAmount.StarBrutal => StarType.Brutal,
                    StarAmount.StarGold => StarType.Gold,
                    _ => throw new Exception("Unreachable.")
                });
            }
        }

        public void SetStars(int n, StarType type)
        {
            n = Mathf.Clamp(n, 0, _starImages.Length);

            var s = type switch
            {
                StarType.Standard => _standardStar,
                StarType.Gold     => _goldStar,
                StarType.Brutal   => _brutalStar,
                _                 => throw new Exception("Unreachable.")
            };

            for (int i = 0; i < _starImages.Length; i++)
            {
                var star = _starImages[i];

                if (i < n)
                {
                    star.sprite = s;
                }
                else
                {
                    star.sprite = _emptyStar;
                }
            }
        }

        public void SetStars(int n)
        {
            if (n <= 5)
            {
                SetStars(n, StarType.Standard);
            }
            else
            {
                SetStars(5, StarType.Gold);
            }
        }
    }
}