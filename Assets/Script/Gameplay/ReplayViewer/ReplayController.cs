using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Replays;

namespace YARG.Gameplay.ReplayViewer
{
    public class ReplayController : MonoBehaviour
    {
        private GameManager _gameManager;
        private Replay      _replay;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
        }

        private void Start()
        {
            if (!_gameManager.IsReplay)
            {
                gameObject.SetActive(false);
                return;
            }

            _gameManager.ChartLoaded += OnChartLoaded;
        }

        private void OnChartLoaded(SongChart chart)
        {
            _replay = _gameManager.Replay;

            _gameManager.ChartLoaded -= OnChartLoaded;
        }
    }
}