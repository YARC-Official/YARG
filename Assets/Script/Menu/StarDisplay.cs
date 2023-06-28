using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;

namespace YARG.UI
{
    public enum StarType
    {
        Empty,
        Standard,
        Gold,
        Brutal
    }

    [ExecuteInEditMode]
    public class StarDisplay : MonoBehaviour
    {
        [Header("Star Graphics")]
        [SerializeField]
        private Sprite starGraphicEmpty;

        [SerializeField]
        private Sprite starGraphicStandard;

        [SerializeField]
        private Sprite starGarphicGold;

        [SerializeField]
        private Sprite starGarphicBrutal;

        [Space]
        [SerializeField]
        private AspectRatioFitter aspectRatioContainer;

        [SerializeField]
        private List<Image> stars;

        /* for editor preview */
        [SerializeField]
        private int _stars = 0;

        [SerializeField]
        private StarType _type = StarType.Empty;

        void Awake()
        {
            if (Application.isEditor && !Application.isPlaying) SetStars(0);
        }

        /// <summary>
        /// Set the displayed stars. You should call this method right after instantiating.
        /// </summary>
        /// <param name="n">Number of stars to show.</param>
        /// <param name="type">Type of stars to show.</param>
        [SerializeField]
        public void SetStars(int n, StarType type = StarType.Standard)
        {
            n = math.clamp(n, 0, stars.Count);

            if (n == 0)
            {
                stars[0].gameObject.SetActive(true);
                stars[0].sprite = starGraphicEmpty;
                for (int i = 1; i < stars.Count; ++i)
                {
                    stars[i].gameObject.SetActive(false);
                }

                aspectRatioContainer.aspectRatio = 1;
                _stars = n;
                _type = StarType.Empty;
                return;
            }

            var s = type switch
            {
                StarType.Empty    => starGraphicEmpty,
                StarType.Standard => starGraphicStandard,
                StarType.Gold     => starGarphicGold,
                StarType.Brutal   => starGarphicBrutal,
                _                 => throw new ArgumentException("invalid StarType")
            };

            for (int i = 0; i < stars.Count; ++i)
            {
                if (i < n)
                {
                    stars[i].sprite = s;
                    stars[i].gameObject.SetActive(true);
                }
                else
                {
                    stars[i].gameObject.SetActive(false);
                }
            }

            aspectRatioContainer.aspectRatio = n;
            _stars = n;
            _type = type;
        }

        void Update()
        {
            if (Application.isEditor && !Application.isPlaying) SetStars(_stars, _type);
        }
    }
}