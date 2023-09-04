using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Data;
using YARG.Player;

namespace YARG.Menu.ScoreScreen
{
    public abstract class ScoreCard<T> : MonoBehaviour where T : BaseStats
    {
        [SerializeField]
        private TextMeshProUGUI _playerName;

        [SerializeField]
        private TextMeshProUGUI _instrument;

        [SerializeField]
        private TextMeshProUGUI _difficulty;

        [SerializeField]
        private TextMeshProUGUI _accuracyPercent;

        [SerializeField]
        private TextMeshProUGUI _score;

        [SerializeField]
        private TextMeshProUGUI _notesHit;

        [SerializeField]
        private TextMeshProUGUI _maxStreak;

        [SerializeField]
        private TextMeshProUGUI _notesMissed;

        [SerializeField]
        private TextMeshProUGUI _starpowerPhrases;

        [SerializeField]
        private Image _instrumentIcon;

        protected YargPlayer Player;
        protected T Stats;

        public void Initialize(YargPlayer player, T stats)
        {
            Player = player;
            Stats = stats;
        }

        public virtual void SetCardContents()
        {
            _playerName.text = Player.Profile.Name;

            _instrument.text = Player.Profile.Instrument.ToLocalizedName();
            _difficulty.text = Player.Profile.Difficulty.ToDisplayName();

            // Set percent
            var totalNotes = Stats.NotesHit + Stats.NotesMissed;
            if (totalNotes == 0)
            {
                _accuracyPercent.text = "0%";
            }
            else
            {
                _accuracyPercent.text = $"{Mathf.FloorToInt((float) Stats.NotesHit / totalNotes * 100f)}%";
            }

            _score.text = Stats.Score.ToString();

            _notesHit.text = $"{Stats.NotesHit} / {totalNotes}";
            _maxStreak.text = Stats.MaxCombo.ToString();
            _notesMissed.text = Stats.NotesMissed.ToString();
            _starpowerPhrases.text = $"{Stats.PhrasesHit} / {Stats.PhrasesHit + Stats.PhrasesMissed}";

            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{Player.Profile.Instrument.ToResourceName()}]")
                .WaitForCompletion();
        }
    }
}