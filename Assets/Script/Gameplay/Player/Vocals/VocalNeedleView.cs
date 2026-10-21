using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Vocals;
using YARG.Helpers;

namespace YARG.Gameplay.Player
{
    public sealed class VocalNeedleView
    {
        private const float POSITION_LERP = 30f;
        private const float APPEAR_SNAP_MULTIPLIER = 10f;
        private const float ROTATION_LERP = 25f;
        private const float MAX_ROTATION = 12f;
        private const float OUTLINE_WIDTH = 7f;
        private static readonly int OutlineWidthID = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineColorID = Shader.PropertyToID("_OutlineColor");

        private readonly Transform _positionRoot;
        private readonly GameObject _visualContainer;
        private readonly MeshRenderer _renderer;
        private readonly Transform _needleTransform;
        private readonly ParticleGroup _particles;
        private readonly ParticleSystem[] _particleSystems;
        private readonly MaterialPropertyBlock _properties = new();
        private readonly Vector3 _initialPosition;
        private readonly Quaternion _initialRotation;
        private int _displayPart = -1;
        private bool _outlined;

        public VocalNeedleView(Transform positionRoot, GameObject visualContainer, MeshRenderer renderer,
            Transform needleTransform, ParticleGroup particles)
        {
            _positionRoot = positionRoot;
            _visualContainer = visualContainer;
            _renderer = renderer;
            _needleTransform = needleTransform;
            _particles = particles;
            _particleSystems = particles.GetComponentsInChildren<ParticleSystem>(true);
            _initialPosition = positionRoot.localPosition;
            _initialRotation = needleTransform.localRotation;
            renderer.GetPropertyBlock(_properties);
            Reset();
        }

        public void Update(VocalNote target, float pitch, bool singing, bool hitting, int displayPart,
            GameManager gameManager, VocalsEngineParameters parameters, bool hide)
        {
            if (!singing || hide)
            {
                Stop();
                return;
            }

            float positionRate = POSITION_LERP;
            if (!_visualContainer.activeSelf)
            {
                _visualContainer.SetActive(true);
                positionRate *= APPEAR_SNAP_MULTIPLIER;
            }

            float targetPitch = target?.PitchAtSongTime(gameManager.SongTime) ?? -1f;
            float rotation = 0f;
            bool onNote = target != null && hitting;
            SetFeedback(onNote && !gameManager.Rewinding, displayPart);

            if (onNote)
            {
                if (!target.IsNonPitched)
                {
                    var (distance, _) = PitchDistance(targetPitch, pitch);
                    float deadZone = parameters.PitchWindowPerfect / 2;
                    float adjusted = distance >= 0 ? Mathf.Max(0, distance - deadZone) : Mathf.Min(0, distance + deadZone);
                    float window = parameters.PitchWindow - deadZone;
                    rotation = window > 0 ? Mathf.Clamp(adjusted / window, -1, 1) * MAX_ROTATION : 0;
                    pitch = targetPitch;
                }
                else
                {
                    pitch += 12f;
                }
            }
            else if (target != null && !target.IsNonPitched)
            {
                var (_, octaveShift) = PitchDistance(targetPitch, pitch);
                pitch = pitch % 12f + 12f * ((int) (targetPitch / 12f) + octaveShift);
            }
            else
            {
                pitch += 12f;
            }

            var position = _positionRoot.localPosition;
            position.z = Mathf.Lerp(position.z, gameManager.VocalTrack.GetPosForPitch(pitch), Time.deltaTime * positionRate);
            _positionRoot.localPosition = position;
            _needleTransform.rotation = Quaternion.Lerp(_needleTransform.rotation,
                Quaternion.Euler(0, rotation + 90f, 0), Time.deltaTime * ROTATION_LERP);
        }

        private void SetFeedback(bool enabled, int part)
        {
            part = Mathf.Clamp(part, 0, VocalTrack.Colors.Length - 1);
            if (enabled) _particles.Play();
            else _particles.Stop();

            if (_displayPart == part && _outlined == enabled) return;
            var color = VocalTrack.Colors[part];
            _properties.SetFloat(OutlineWidthID, enabled ? OUTLINE_WIDTH : 0);
            _properties.SetColor(OutlineColorID, color);
            _renderer.SetPropertyBlock(_properties);
            foreach (var system in _particleSystems)
            {
                var main = system.main;
                main.startColor = color;
            }
            _displayPart = part;
            _outlined = enabled;
        }

        public void Stop()
        {
            SetFeedback(false, _displayPart);
            _visualContainer.SetActive(false);
        }

        public void Reset()
        {
            Stop();
            foreach (var system in _particleSystems) system.Clear();
            _positionRoot.localPosition = _initialPosition;
            _needleTransform.localRotation = _initialRotation;
        }

        private static (float Distance, int OctaveShift) PitchDistance(float target, float other)
        {
            target %= 12f;
            other %= 12f;
            float closest = other - target;
            int octaveShift = 0;
            float upper = other + 12f - target;
            if (Mathf.Abs(upper) < Mathf.Abs(closest))
            {
                closest = upper;
                octaveShift = 1;
            }
            float lower = other - 12f - target;
            if (Mathf.Abs(lower) < Mathf.Abs(closest))
            {
                closest = lower;
                octaveShift = -1;
            }
            return (closest, octaveShift);
        }
    }
}
