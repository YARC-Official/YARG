using System;
using UnityEngine;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Input;
using YARG.Menu.Navigation;
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
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Continue", () =>
                {
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                })
            }, true));

            CreateScoreCards();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void CreateScoreCards()
        {
            foreach (var score in GlobalVariables.Instance.ScoreScreenStats.PlayerScores)
            {
                switch (score.Player.Profile.Instrument.ToGameMode())
                {
                    case GameMode.FiveFretGuitar:
                    {
                        var card = Instantiate(_guitarCardPrefab, _cardContainer);
                        card.Initialize(score.Player, score.Stats as GuitarStats);
                        card.SetCardContents();
                        break;
                    }
                    case GameMode.FourLaneDrums:
                    {
                        var card = Instantiate(_drumsCardPrefab, _cardContainer);
                        card.Initialize(score.Player, score.Stats as DrumsStats);
                        card.SetCardContents();
                        break;
                    }
                }
            }
        }
    }
}