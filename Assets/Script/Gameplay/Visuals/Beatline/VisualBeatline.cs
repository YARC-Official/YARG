using System;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class VisualBeatline : MonoBehaviour, IPoolable
    {
        private const float REMOVE_POINT = -4f;

        private const float WEAK_BEAT_SCALE   = 0.04f;
        private const float STRONG_BEAT_SCALE = 0.08f;
        private const float MEASURE_SCALE     = 0.13f;

        private const float WEAK_BEAT_ALPHA = 0.2f;
        private const float STRONG_BEAT_ALPHA = 0.40f;
        private const float MEASURE_ALPHA = 0.8f;

        private GameManager  _gameManager;
        private BasePlayer   _player;
        private MeshRenderer _meshRenderer;
        private Transform    _meshTransform;

        public Pool ParentPool { get; set; }

        public Beatline BeatlineRef;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();
            _player = GetComponentInParent<BasePlayer>();
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            _meshTransform = _meshRenderer.transform;

            gameObject.SetActive(false);
        }

        private void Update()
        {
            float noteSpeed = _player.Player.Profile.NoteSpeed;

            float z = BasePlayer.STRIKE_LINE_POS
                + (float) (BeatlineRef.Time - _gameManager.SongTime)
                * noteSpeed;

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT)
            {
                ParentPool.Return(this);
            }
        }

        private void InitializeBeatline()
        {
            transform.localPosition = new Vector3(
                BasePlayer.TRACK_WIDTH / 5f * 3f - BasePlayer.TRACK_WIDTH / 2f - 1f / 5f,
                0f, 0f);

            float yScale;
            float alpha;

            switch (BeatlineRef.Type)
            {
                case BeatlineType.Measure:
                    yScale = MEASURE_SCALE;
                    alpha = MEASURE_ALPHA;
                    break;
                case BeatlineType.Strong:
                    yScale = STRONG_BEAT_SCALE;
                    alpha = STRONG_BEAT_ALPHA;
                    break;
                case BeatlineType.Weak:
                    yScale = WEAK_BEAT_SCALE;
                    alpha = WEAK_BEAT_ALPHA;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(BeatlineRef.Type));
            }

            _meshTransform.localScale = _meshTransform.localScale.WithY(yScale);

            var material = _meshRenderer.material;
            var color = material.color;
            color.a = alpha;

            material.color = color;
        }

        public void EnableFromPool()
        {
            gameObject.SetActive(true);
            InitializeBeatline();
            Update();
        }

        public void DisableIntoPool()
        {
            gameObject.SetActive(false);
        }
    }
}