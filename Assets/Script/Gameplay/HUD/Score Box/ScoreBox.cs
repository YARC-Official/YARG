using System;
using TMPro;
using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class ScoreBox : MonoBehaviour
    {
        private const string SCORE_PREFIX = "<mspace=0.538em>";

        [SerializeField]
        private TextMeshProUGUI scoreText;

        [SerializeField]
        private TextMeshProUGUI songProgressBar;

        private GameManager _gameManager;
        private StarDisplay _starDisplay;

        private int _bandScore;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _starDisplay = GetComponentInChildren<StarDisplay>();
        }

        // Start is called before the first frame update
        private void Start()
        {
            scoreText.text = SCORE_PREFIX + "0";
        }

        // Update is called once per frame
        private void Update()
        {
            if (_gameManager.Paused)
            {
                return;
            }

            if (_gameManager.BandScore != _bandScore)
            {
                _bandScore = _gameManager.BandScore;
                scoreText.text = SCORE_PREFIX + _bandScore.ToString("N0");

                UpdateStars();
            }
        }

        private void UpdateStars()
        {
            double totalStarCount = 0;

            foreach (var player in _gameManager.Players)
            {
                if (player.StarScoreThresholds[0] == 0)
                {
                    continue;
                }

                int fullStars = 5;
                while (fullStars >= 0 && player.Score < player.StarScoreThresholds[fullStars])
                {
                    fullStars--;
                }

                fullStars++;

                totalStarCount += fullStars;

                double progressToNextStar = 0;
                if (fullStars == 0)
                {
                    progressToNextStar = player.Score / (double)player.StarScoreThresholds[fullStars];
                }
                else if (fullStars < 6)
                {
                    int previousStarThreshold = player.StarScoreThresholds[fullStars - 1];
                    progressToNextStar = (player.Score - previousStarThreshold) /
                        (double)(player.StarScoreThresholds[fullStars] - previousStarThreshold);
                }

                totalStarCount += progressToNextStar;
            }

            _starDisplay.SetStars(totalStarCount);
        }
    }
}