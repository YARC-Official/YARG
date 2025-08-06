using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Core.Game;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Player;
using YARG.Settings;
using YARG.Settings.Preview;

namespace YARG.Gameplay.Visuals
{
    public class CameraPositioner : MonoBehaviour
    {
        private const float BOUNCE_UNITS = 0.03f;
        private const float SPEED = 0.25f;

        private const float GLOBAL_ANIM_DELAY = 2f;
        private const float LOCAL_ANIM_OFFSET = 0.1f;

        private const float ANIM_BASE_TO_PEAK_INTERVAL = 0.333f;
        private const float ANIM_PEAK_TO_VALLEY_INTERVAL = 0.167f;
        private const float ANIM_INIT_ROTATION = -60f;
        private const float ANIM_PEAK_ROTATION = 2f;

        private const float PUNCH_ANIM_DURATION = 0.25f;
        private const float PUNCH_DISTANCE      = 0.03f;

        private const float SCOOP_ANIM_DURATION = 0.125f;
        private const float SCOOP_DOWN_DISTANCE = 2f;
        private const float SCOOP_UP_DISTANCE   = 0.5f;

        private float _currentBounce;

        private GameManager  _gameManager;
        private CameraPreset _preset;
        private Coroutine    _coroutine;

        private void Start()
        {
            // Set anti-aliasing
            var info = GetComponent<UniversalAdditionalCameraData>();
            if (SettingsManager.Settings.LowQuality.Value)
            {
                info.antialiasing = AntialiasingMode.None;
            }
            else
            {
                info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                info.antialiasingQuality = AntialiasingQuality.Low;
            }
        }

        public void Initialize(CameraPreset preset)
        {
            // FOV
            GetComponent<Camera>().fieldOfView = preset.FieldOfView;

            // Position
            transform.localPosition = new Vector3(
                0f,
                preset.PositionY,
                preset.PositionZ - 6f);

            // Rotation
            transform.localRotation = Quaternion.Euler(preset.Rotation, 0f, 0f);

            // Set camera preset
            _preset = preset;

            _gameManager = FindObjectOfType<GameManager>();
            // Animate the highway raise
            if (_gameManager != null && !_gameManager.IsPractice)
            {
                _coroutine = StartCoroutine(RaiseHighway(_preset, true));
            }
        }

        private void Update()
        {
            if (_currentBounce <= 0f) return;

            float speed = Time.deltaTime * SPEED;

            _currentBounce -= speed;
            transform.Translate(Vector3.up * speed, Space.World);
        }

        public void Bounce()
        {
            // Prevent bounce from stacking up
            transform.Translate(Vector3.up * _currentBounce, Space.World);

            // Remember that we should be moving in the opposite direction
            // because we're moving the camera, not the track.
            _currentBounce = BOUNCE_UNITS * SettingsManager.Settings.KickBounceMultiplier.Value;
            transform.Translate(Vector3.down * _currentBounce, Space.World);
        }

        public void Lower(bool isGameplayEnd)
        {
            _coroutine = StartCoroutine(LowerHighway(_preset, isGameplayEnd));
        }

        public void Punch()
        {
            _coroutine = StartCoroutine(PunchHighway(_preset));
        }

        public void Scoop()
        {
            _coroutine = StartCoroutine(ScoopHighway(_preset));
        }

        private void OnDestroy()
        {
            if (_coroutine != null)
            {
                StopCoroutine(_coroutine);
            }
        }

        private IEnumerator RaiseHighway(CameraPreset preset, bool isGameplayStart)
        {
            transform.localRotation = Quaternion.Euler(new Vector3().WithX(preset.Rotation + ANIM_INIT_ROTATION));

            var basePlayer = GetComponentInParent<BasePlayer>();
            float delay = isGameplayStart
                ? basePlayer.transform.GetSiblingIndex() * LOCAL_ANIM_OFFSET + GLOBAL_ANIM_DELAY
                : 0f;

            yield return DOTween.Sequence()
                .PrependInterval(delay)
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation + ANIM_PEAK_ROTATION), ANIM_BASE_TO_PEAK_INTERVAL)
                    .SetEase(Ease.OutCirc))
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation), ANIM_PEAK_TO_VALLEY_INTERVAL)
                    .SetEase(Ease.InOutSine));
        }

        // NOTE: Requires SONG_END_DELAY; will not animate until https://github.com/YARC-Official/YARG/pull/993 is in.
        private IEnumerator LowerHighway(CameraPreset preset, bool isGameplayEnd)
        {
            transform.localRotation = Quaternion.Euler(new Vector3().WithX(preset.Rotation));

            var basePlayer = GetComponentInParent<BasePlayer>();
            float delay = isGameplayEnd
                ? basePlayer.transform.GetSiblingIndex() * LOCAL_ANIM_OFFSET
                : 0f;

            yield return DOTween.Sequence()
                .PrependInterval(delay)
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation + ANIM_PEAK_ROTATION), ANIM_PEAK_TO_VALLEY_INTERVAL)
                    .SetEase(Ease.InOutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation + ANIM_INIT_ROTATION), ANIM_BASE_TO_PEAK_INTERVAL)
                    .SetEase(Ease.InCirc));
        }

        private IEnumerator PunchHighway(CameraPreset preset)
        {
            // 50% chance left or right to lessen visual fatigue
            float xPosDelta = PUNCH_DISTANCE * (Random.Range(0f, 1f) > 0.5f ? -1 : 1);

            yield return DOTween.Sequence()
                .Append(transform
                    .DOPunchPosition(new Vector3()
                            .WithX(xPosDelta)
                            .WithY(PUNCH_DISTANCE),
                        PUNCH_ANIM_DURATION, 1, 1f, false));
        }

        private IEnumerator ScoopHighway(CameraPreset preset)
        {
            // Very quick blast down, up and back to origin for SP activation
            yield return DOTween.Sequence()
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation - SCOOP_DOWN_DISTANCE), SCOOP_ANIM_DURATION / 4f)
                    .SetEase(Ease.OutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation + SCOOP_UP_DISTANCE), SCOOP_ANIM_DURATION / 2f)
                    .SetEase(Ease.InOutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(preset.Rotation), SCOOP_ANIM_DURATION / 4f)
                    .SetEase(Ease.InOutSine));
        }
    }
}
