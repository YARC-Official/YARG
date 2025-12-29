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

        private const float MAX_ANIM_DELAY = 2f;
        private const float LOCAL_ANIM_OFFSET = 0.1f;
        private const float MIN_TIME_TO_FIRST_NOTE = 1.0f;

        private const float ANIM_BASE_TO_PEAK_INTERVAL = 0.333f;
        private const float ANIM_PEAK_TO_VALLEY_INTERVAL = 0.167f;
        private const float ANIM_INIT_ROTATION = -60f;
        private const float ANIM_PEAK_ROTATION = 2f;

        private const float PUNCH_ANIM_DURATION = 0.25f;
        private const float PUNCH_DISTANCE      = 0.03f;

        private const float SCOOP_ANIM_DURATION = 0.0833f;
        private const float SCOOP_DOWN_DISTANCE = 1f;
        private const float SCOOP_UP_DISTANCE   = 0.25f;

        private float _globalAnimDelay;

        private float _currentBounce;

        private bool _highwayRaised = false;

        private GameManager  _gameManager;
        private CameraPreset _preset;

        private Sequence _punchLeft;
        private Sequence _punchRight;

        private Sequence _scoop;
        private Sequence _raise;
        private Sequence _lower;

        private Sequence _bounce;

        private float _cameraY;

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
            var camera = GetComponent<Camera>();
            // FOV
            camera.fieldOfView = preset.FieldOfView;

            // Position
            transform.localPosition = new Vector3(
                0f,
                preset.PositionY,
                preset.PositionZ - 6f);

            // Rotation
            transform.localRotation = Quaternion.Euler(preset.Rotation, 0f, 0f);

            // Set camera preset
            _preset = preset;

            // Initialize sequences
            InitializeSequences();

            _gameManager = FindAnyObjectByType<GameManager>();

            // If there is no game manager, we are in preview mode and none of this should happen
            if (_gameManager == null)
            {
                return;
            }

            // Set the highway raise delay

            // +2 because song time starts at -2, not 0.
            var timeToFirstNote = _gameManager.Chart.GetFirstNoteStartTime() + 2;
            var animLength = ANIM_BASE_TO_PEAK_INTERVAL + ANIM_PEAK_TO_VALLEY_INTERVAL;

            // The delay can be up to MAX_ANIM_DELAY, but should not be longer than firstNote - (animLength + 1)
            var latestStart = timeToFirstNote - (animLength + MIN_TIME_TO_FIRST_NOTE);
            _globalAnimDelay = Mathf.Clamp((float) latestStart, 0f, MAX_ANIM_DELAY);

            // Animate the highway raise
            if (!_gameManager.IsPractice && SettingsManager.Settings.EnableHighwayAnimation.Value)
            {
                if (_highwayRaised)
                {
                    return;
                }

                RaiseHighway(true);
                _highwayRaised = true;
            }
        }

        public void Bounce()
        {
            _bounce.Restart();
        }

        public void Lower(bool isGameplayEnd)
        {
            if (_highwayRaised)
            {
                LowerHighway(isGameplayEnd);
                _highwayRaised = false;
            }
        }

        public void Punch()
        {
            PunchHighway();
        }

        public void Scoop()
        {
            ScoopHighway();
        }

        private void InitializeSequences()
        {
            if (!SettingsManager.Settings.EnableHighwayAnimation.Value)
            {
                // If animations are disabled, just make empty sequences
                _raise = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                _lower = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                _bounce = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                _punchLeft = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                _punchRight = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                _scoop = DOTween.Sequence().SetAutoKill(false).Pause().SetLink(gameObject);
                return;
            }

            _raise = DOTween.Sequence()
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation + ANIM_PEAK_ROTATION), ANIM_BASE_TO_PEAK_INTERVAL)
                    .SetEase(Ease.OutCirc))
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation), ANIM_PEAK_TO_VALLEY_INTERVAL)
                    .SetEase(Ease.InOutSine))
                .AppendCallback(InitializeBounce)
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);

            _lower = DOTween.Sequence()
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation + ANIM_PEAK_ROTATION), ANIM_PEAK_TO_VALLEY_INTERVAL)
                    .SetEase(Ease.InOutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation + ANIM_INIT_ROTATION), ANIM_BASE_TO_PEAK_INTERVAL)
                    .SetEase(Ease.InCirc))
                .SetUpdate(true)
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);

            var leftVector = new Vector3(-PUNCH_DISTANCE, 0f, 0f);
            var rightVector = new Vector3(PUNCH_DISTANCE, 0f, 0f);

            _punchLeft = DOTween.Sequence()
                .Append(transform.DOPunchPosition(leftVector, PUNCH_ANIM_DURATION, 1, 1f, false))
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);

            _punchRight = DOTween.Sequence()
                .Append(transform.DOPunchPosition(rightVector, PUNCH_ANIM_DURATION, 1, 1f, false))
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);

            _scoop = DOTween.Sequence()
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation - SCOOP_DOWN_DISTANCE), SCOOP_ANIM_DURATION / 4f)
                    .SetEase(Ease.OutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation + SCOOP_UP_DISTANCE), SCOOP_ANIM_DURATION / 2f)
                    .SetEase(Ease.InOutSine))
                .Append(transform
                    .DORotate(new Vector3().WithX(_preset.Rotation), SCOOP_ANIM_DURATION / 4f)
                    .SetEase(Ease.InOutSine))
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);
        }

        private void InitializeBounce()
        {
            var bounceAmount = BOUNCE_UNITS * SettingsManager.Settings.KickBounceMultiplier.Value;

            // Have to invert bounceAmount since we're moving the camera, not the track
            var strength = new Vector3(0f, bounceAmount * -1, 0f);

            _bounce = DOTween.Sequence().Append(
                transform.DOPunchPosition(strength, 0.125f, 10, 1f, false))
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);
        }

        private void RaiseHighway(bool isGameplayStart)
        {
            transform.localRotation = Quaternion.Euler(new Vector3().WithX(_preset.Rotation + ANIM_INIT_ROTATION));

            var basePlayer = GetComponentInParent<BasePlayer>();
            float delay = isGameplayStart
                ? basePlayer.transform.GetSiblingIndex() * LOCAL_ANIM_OFFSET + _globalAnimDelay
                : 0f;

            // TODO: This will need to be reworked when it is possible for the highway to raise and lower other
            //  than at the beginning and end of song
            _raise.PrependInterval(delay).Restart();
        }

        // NOTE: Requires SONG_END_DELAY; will not animate until https://github.com/YARC-Official/YARG/pull/993 is in.
        private void LowerHighway(bool isGameplayEnd)
        {
            // TODO: Remove this when bandmate saving is implemented
            _scoop?.Kill();

            transform.localRotation = Quaternion.Euler(new Vector3().WithX(_preset.Rotation));

            var basePlayer = GetComponentInParent<BasePlayer>();
            float delay = isGameplayEnd
                ? basePlayer.transform.GetSiblingIndex() * LOCAL_ANIM_OFFSET
                : 0f;

            // TODO: This will need to be reworked when it is possible for the highway to raise and lower other
            //  than at the beginning and end of song
            _lower.PrependInterval(delay).Restart();
        }

        private void PunchHighway()
        {
            // 50% chance left or right to lessen visual fatigue
            if (Random.Range(0f, 1f) > 0.5f)
            {
                _punchLeft.Restart();
            }
            else
            {
                _punchRight.Restart();
            }
        }

        private void ScoopHighway()
        {
            // Very quick blast down, up and back to origin for SP activation
            _scoop.Restart();
        }
    }
}