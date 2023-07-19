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

        private int _bandScore;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
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
            }
        }
    }
}