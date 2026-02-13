using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class MainHUDPaddingAdjuster : GameplayBehaviour
    {
        [SerializeField]
        private float _topPaddingForVocals = 200f;

        protected override void OnChartLoaded(SongChart chart)
        {
            // At the time the chart is loaded, all of the players should be initialized/added

            bool usePadding = GameManager.YargPlayers
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
