using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Player;

namespace YARG.Menu.ScoreScreen
{
    public abstract class ScoreCard<T> : MonoBehaviour where T : BaseStats
    {
        [SerializeField]
        private ModifierIcon _modifierIconPrefab;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _accuracyPercent;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _playerName;
        [SerializeField]
        private TextMeshProUGUI _instrument;
        [SerializeField]
        private TextMeshProUGUI _difficulty;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _score;
        [SerializeField]
        private StarView _starView;
        [SerializeField]
        private Transform _modifierIconContainer;

        [Space]
        [SerializeField]
        private Image _instrumentIcon;

        [Space]
        [SerializeField]
        private GameObject _tagGameObject;
        [SerializeField]
        private TextMeshProUGUI _tagText;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _notesHit;
        [SerializeField]
        private TextMeshProUGUI _maxStreak;
        [SerializeField]
        private TextMeshProUGUI _notesMissed;
        [SerializeField]
        private TextMeshProUGUI _starpowerPhrases;

        private ScoreCardColorizer _colorizer;

        protected bool IsHighScore;
        protected YargPlayer Player;
        protected T Stats;

        private void Awake()
        {
            _colorizer = GetComponent<ScoreCardColorizer>();
        }

        public void Initialize(bool isHighScore, YargPlayer player, T stats)
        {
            IsHighScore = isHighScore;
            Player = player;
            Stats = stats;
        }

        public virtual void SetCardContents()
        {
            _playerName.text = Player.Profile.Name;

            _instrument.text = Player.Profile.CurrentInstrument.ToLocalizedName();
            _difficulty.text = Player.Profile.CurrentDifficulty.ToDisplayName();

            // Set percent
            _accuracyPercent.text = $"{Mathf.FloorToInt(Stats.Percent * 100f)}%";


            // Set background and foreground colors
            if (Player.Profile.IsBot)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gray);
                ShowTag("Bot");
            }
            else if (Stats.MaxCombo == Stats.TotalNotes)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Gold);
                ShowTag("Full Combo");
            }
            else if (IsHighScore)
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                ShowTag("High Score");
            }
            else
            {
                _colorizer.SetCardColor(ScoreCardColorizer.ScoreCardColor.Blue);
                HideTag();
            }

            _score.text = Stats.TotalScore.ToString("N0");
            _starView.SetStars((int) Stats.Stars);

            _notesHit.text = $"{WrapWithColor(Stats.NotesHit)} / {Stats.TotalNotes}";
            _maxStreak.text = WrapWithColor(Stats.MaxCombo);
            _notesMissed.text = WrapWithColor(Stats.NotesMissed);
            _starpowerPhrases.text = $"{WrapWithColor(Stats.StarPowerPhrasesHit)} / {Stats.TotalStarPowerPhrases}";

            // Set background icon
            _instrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{Player.Profile.CurrentInstrument.ToResourceName()}]")
                .WaitForCompletion();

            // Set engine preset icons
            ModifierIcon.SpawnEnginePresetIcons(_modifierIconPrefab, _modifierIconContainer,
                Player.EnginePreset, Player.Profile.CurrentInstrument.ToGameMode());

            // Set modifier icons
            foreach (var modifier in EnumExtensions<Modifier>.Values)
            {
                if (modifier == Modifier.None) continue;

                if (!Player.Profile.IsModifierActive(modifier)) continue;

                var icon = Instantiate(_modifierIconPrefab, _modifierIconContainer);
                icon.InitializeForModifier(modifier);
            }
        }

        private void ShowTag(string tagText)
        {
            _tagGameObject.SetActive(true);
            _tagText.text = tagText;
        }

        private void HideTag()
        {
            _tagGameObject.SetActive(false);
        }

        protected string WrapWithColor(object s)
        {
            return
                $"<font-weight=700><color=#{ColorUtility.ToHtmlStringRGB(_colorizer.CurrentColor)}>" +
                $"{s}</color></font-weight>";
        }
    }
}