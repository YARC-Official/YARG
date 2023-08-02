using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    [ExecuteInEditMode]
    public class EditorStarDisplay : MonoBehaviour
    {
        [Header("Star Sprites")]
        [SerializeField]
        private Sprite starSpriteEmpty;

        [SerializeField]
        private Sprite starSpriteStandard;

        [SerializeField]
        private Sprite starSpriteGold;

        [SerializeField]
        private Sprite starSpriteBrutal;

        [Space]
        [SerializeField]
        private AspectRatioFitter aspectRatioFitter;

        [SerializeField]
        private Image[] starImages;

        [SerializeField]
        private int stars;

    }
}