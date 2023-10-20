using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public class MainHUDPaddingAdjuster : MonoBehaviour
    {
        [SerializeField]
        private float _topPaddingForVocals = 128f;

        private void Start()
        {
            bool usePadding = PlayerContainer.Players
                .Where(player => !player.SittingOut)
                .Any(player => player.Profile.GameMode == GameMode.Vocals);

            if (usePadding)
            {
                var rt = GetComponent<RectTransform>();
                rt.offsetMax = new Vector2(rt.offsetMax.x, -_topPaddingForVocals);
            }
        }
    }
}
