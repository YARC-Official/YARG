using Cysharp.Text;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Song;
using YARG.Localization;
using YARG.Menu.MusicLibrary;
using YARG.Player;
using YARG.Scores;

namespace YARG.Menu.DifficultySelect
{
    public class HighScoreDisplay : MonoBehaviour
    {
        [SerializeField]
        private GameObject _content;
        [SerializeField]
        private TextMeshProUGUI _label;
        [SerializeField]
        private InstrumentDifficultyView _instrumentDifficultyView;
        [SerializeField]
        private StarView _starView;
        [SerializeField]
        private TextMeshProUGUI _scoreText;

        private void Awake()
        {
            _label.text = Localize.Key("Menu.DifficultySelect", "HighScore");
        }

        public void Show(SongEntry song, YargPlayer player, Instrument instrument, Difficulty difficulty)
        {
            // The best result for the selected instrument and difficulty, using the
            // High Score History setting's metric (score vs percentage). Drum kits pick a
            // specific drum chart here, so its score isn't merged with the other drum charts
            var record = ScoreContainer.GetPreferredHighScoreForDifficulty(
                song.Hash, player.Profile.Id, instrument, difficulty);

            if (record is null)
            {
                Hide();
                return;
            }

            _content.SetActive(true);

            _instrumentDifficultyView.SetInfo(new ViewType.ScoreInfo
            {
                Score = record.Score,
                Difficulty = record.Difficulty,
                Percent = record.GetPercent(),
                Instrument = record.Instrument,
                IsFc = record.IsFc
            });

            _starView.SetStars(record.Stars);

            var scoreColor = record.IsFc ? "#ffd029" : "#ffffff";
            _scoreText.SetTextFormat("<mspace=.5em><color={1}>{0:N0}</color></mspace>",
                record.Score, scoreColor);
        }

        public void Hide()
        {
            _content.SetActive(false);
        }
    }
}
