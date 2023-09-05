using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Player;
using YARG.Song;

namespace YARG.Menu.ScoreScreen
{
    public class ScoreScreenMenu : MonoBehaviour
    {
        [SerializeField]
        private Transform _cardContainer;
        [SerializeField]
        private Image _sourceIcon;
        [SerializeField]
        private TextMeshProUGUI _songTitle;
        [SerializeField]
        private TextMeshProUGUI _artistName;

        [Space]
        [SerializeField]
        private GuitarScoreCard _guitarCardPrefab;
        [SerializeField]
        private DrumsScoreCard _drumsCardPrefab;

        private async UniTask OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Continue", () =>
                {
                    GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
                })
            }, true));

            // Set text
            var song = GlobalVariables.Instance.CurrentSong;
            _songTitle.text = song.Name;
            _artistName.text = song.Artist;

            // Put the scores in!
            CreateScoreCards();

            // Set the icon. This is async, so we have to do it last so everything loads in.
            _sourceIcon.sprite = await SongSources.SourceToIcon(song.Source);
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