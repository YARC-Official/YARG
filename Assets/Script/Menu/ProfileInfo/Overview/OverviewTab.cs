using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Localization;
using YARG.Scores;

namespace YARG.Menu.ProfileInfo.Overview
{
    public class OverviewTab : MonoBehaviour
    {
        [SerializeField]
        private ProfileInfoMenu _profileInfoMenu;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _profileName;
        [SerializeField]
        private TextMeshProUGUI _profileExtras;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _totalScore;
        [SerializeField]
        private TextMeshProUGUI _totalStars;
        [SerializeField]
        private TextMeshProUGUI _totalFcs;

        private void OnEnable()
        {
            var profile = _profileInfoMenu.CurrentProfile;
            var scores = ScoreContainer.GetAllPlayerScores(profile.Id);

            _profileName.text = profile.Name;
            _profileExtras.text = Localize.KeyFormat("Menu.ProfileInfo.Stats.SongPlays", scores.Count);

            // Make sure to cast to double to prevent overflows
            _totalScore.text = scores
                .Select(i => (double) i.Score)
                .Sum()
                .ToString("N0");
            _totalStars.text = scores
                .Select(i => (double) i.Stars.GetStarCount())
                .Sum()
                .ToString("N0");
            _totalFcs.text = scores
                .LongCount(i => i.IsFc)
                .ToString("N0");
        }
    }
}