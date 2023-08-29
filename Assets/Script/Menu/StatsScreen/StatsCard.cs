using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Data;
using YARG.Player;

namespace YARG.Menu.StatsScreen
{

    public abstract class StatsCard<T> : MonoBehaviour where T : BaseStats
    {

        [SerializeField]
        private TextMeshProUGUI _playerName;

        [SerializeField]
        private TextMeshProUGUI _accuracyPercent;

        [SerializeField]
        private TextMeshProUGUI _score;

        [SerializeField]
        private TextMeshProUGUI _difficulty;

        [SerializeField]
        private TextMeshProUGUI _notesHit;

        [SerializeField]
        private TextMeshProUGUI _maxStreak;

        [SerializeField]
        private TextMeshProUGUI _notesMissed;

        [SerializeField]
        private TextMeshProUGUI _phrasesHit;

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

            // Set instrument icon

            var totalNotes = Stats.NotesHit + Stats.NotesMissed;
            if (totalNotes == 0)
            {
                _accuracyPercent.text = "0%";
            }
            else
            {
                _accuracyPercent.text = $"{Stats.NotesHit / totalNotes}%";
            }

            _difficulty.text = Player.Profile.Difficulty.ToDisplayName();

            _notesHit.text = $"{Stats.NotesHit} / {totalNotes}";
            _maxStreak.text = Stats.MaxCombo.ToString();
            _notesMissed.text = Stats.NotesMissed.ToString();

            _score.text = Stats.Score.ToString();
        }

    }
}