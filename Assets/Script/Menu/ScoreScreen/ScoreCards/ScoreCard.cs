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
        private StarView _starView;

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

        private ScoreCardColorizer _colorizer;

        protected YargPlayer Player;
        protected T Stats;

        private void Awake()
        {
            _colorizer = GetComponent<ScoreCardColorizer>();
        }

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

            // Set background and foreground colors
            if (Player.Profile.IsBot)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gray);
            }
            else if (Stats.MaxCombo == totalNotes)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gold);
            }
            else
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
            }

            _score.text = Stats.Score.ToString();
            _starView.SetStars(Stats.Stars);

            _notesHit.text = $"{WrapWithColor(Stats.NotesHit)} / {totalNotes}";
            _maxStreak.text = WrapWithColor(Stats.MaxCombo);
            _notesMissed.text = WrapWithColor(Stats.NotesMissed);
            _starpowerPhrases.text = $"{WrapWithColor(Stats.PhrasesHit)} / {Stats.PhrasesHit + Stats.PhrasesMissed}";

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{Player.Profile.Instrument.ToResourceName()}]")
                .WaitForCompletion();
        }

        protected string WrapWithColor(object s)
        {
            return
                $"<font-weight=700><color=#{ColorUtility.ToHtmlStringRGB(_colorizer.CurrentColor)}>" +
                $"{s}</color></font-weight>";
        }
    }
}