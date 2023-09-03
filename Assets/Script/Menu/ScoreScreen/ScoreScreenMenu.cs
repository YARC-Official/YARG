using UnityEngine;
using YARG.Player;

namespace YARG.Menu.ScoreScreen
{
    public class ScoreScreenMenu : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;

        [Space]
        [SerializeField]
        private GuitarScoreCard _guitarCardPrefab;
        [SerializeField]
        private DrumsScoreCard _drumsCardPrefab;

        private void OnEnable()
        {
            CreateScoreCards();
        }

        private void CreateScoreCards()
        {
            foreach (var player in PlayerContainer.Players)
            {
                // player.Profile.
            }
        }
    }
}