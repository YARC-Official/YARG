using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
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
        private Image _profilePicture;
        [SerializeField]
        private RawImage _avatar;
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
            var scores = ScoreContainer.GetAllScoresByPlayerId(profile.Id);

            _profilePicture.enabled = true;
            _avatar.gameObject.SetActive(false);
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

            if (profile.Avatar == null)
            {
                return;
            }

            _avatar.gameObject.SetActive(true);
            _avatar.texture = profile.Avatar.LoadTexture(false);
            _profilePicture.enabled = false;
        }

        private void OnDisable()
        {
            if (_profileInfoMenu.CurrentProfile.Avatar != null)
            {
                Destroy(_avatar.texture);
                _avatar.texture = null;
            }
        }
    }
}