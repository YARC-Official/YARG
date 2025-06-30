using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Venue.VenueCamera;

namespace YARG.Venue.VenueCamera
{
    public class VenueCamera : MonoBehaviour
    {
        [SerializeField]
        private CameraManager _cameraManager;
        [SerializeField]
        private VolumeProfile _volume;
        [SerializeField]
        public CameraManager.CameraLocation CameraLocation;

        [Space]
        [SerializeField]
        [Header("Camera Cut Subjects For This Camera")]
        public List<CameraCutEvent.CameraCutSubject> CameraCutSubjects;

        private PostProcessingType _currentEffect = PostProcessingType.Default;
        private VolumeProfile _profile;

        private float _originalBloom;

        private ColorParameter _sepiaToneColor;

        private TextureCurve _invertCurve;
        private TextureCurve _defaultCurve;

        private TextureCurveParameter _invertCurveParam;
        private TextureCurveParameter _defaultCurveParam;

        private ClampedFloatParameter _defaultGrainIntensity;
        private ClampedFloatParameter _defaultGrainResponse;
        private ClampedFloatParameter _activeGrainIntensity = new(1.0f, 1.0f, 0.0f);
        private ClampedFloatParameter _activeGrainResponse = new(0.0f, 1.0f, 0.0f);

        private Color _greenTint = new(0.0f, 1.0f, 0.0f, 1.0f);
        private Color _blueTint  = new(0.0f, 0.0f, 1.0f, 1.0f);


        private void Awake()
        {
            _profile = _volume;
            if (_profile.TryGet<Bloom>(out var bloom))
            {
                _originalBloom = bloom.intensity.value;
            }

            if (_profile.TryGet<FilmGrain>(out var grain))
            {
                _defaultGrainIntensity = grain.intensity;
                _defaultGrainResponse = grain.response;
            }

            var bounds = new Vector2(0, 1);
            _defaultCurve = new TextureCurve(new AnimationCurve(new Keyframe(0,0), new Keyframe(1,1)), 0.5f, false, in bounds);
            _invertCurve = new TextureCurve(new AnimationCurve(new Keyframe(1,1), new Keyframe(0,0)), 0.5f, false, in bounds);
            _defaultCurveParam = new TextureCurveParameter(_defaultCurve);
            _invertCurveParam = new TextureCurveParameter(_invertCurve, true);
        }

        private void Update()
        {
            var newEffect = _cameraManager.CurrentEffect;

            if (newEffect != _currentEffect)
            {
                SetCameraPostProcessing(newEffect);
                _currentEffect = newEffect;
            }
        }

        public void SetProfile(VolumeProfile profile)
        {
            _profile = profile;
        }

        public VolumeProfile GetProfile()
        {
            return _profile;
        }

        public void SetCameraPostProcessing(PostProcessingType newEffect)
        {
            // First reset any existing effects, because they aren't supposed to stack
            ResetCameraEffect();

            // Now set the new effect
            switch (newEffect)
            {
                case PostProcessingType.Default:
                    break;
                case PostProcessingType.Bloom:
                    SetBloom(true);
                    break;
                case PostProcessingType.Bright: // I don't actually have any idea what this one is supposed to do
                    SetBrightness(true);
                    break;
                case PostProcessingType.Contrast: // This is just my best guess
                    SetContrast(true);
                    break;
                case PostProcessingType.Posterize:
                    SetPosterize(true);
                    break;
                case PostProcessingType.PhotoNegative:
                    SetInvertedColors(true);
                    break;
                case PostProcessingType.Mirror:
                    SetMirror(true);
                    break;
                case PostProcessingType.BlackAndWhite:
                case PostProcessingType.Scanlines_BlackAndWhite:
                    // TODO: Actually implement the scanlines part
                    SetBlackAndWhite(true);
                    break;
                case PostProcessingType.SepiaTone:
                    SetSepiaTone(true);
                    break;
                case PostProcessingType.SilverTone:
                    SetSilverTone(true);
                    break;
                case PostProcessingType.Scanlines_Blue:
                    SetBlueTint(true);
                    break;
                case PostProcessingType.Scanlines_Security:
                    SetGreenTint(true);
                    break;
                case PostProcessingType.Grainy_Film:
                    SetGrainy(true);
                    break;
            }
        }

        private void ResetCameraEffect()
        {
            switch (_currentEffect)
            {
                case PostProcessingType.Default:
                    break;
                case PostProcessingType.Bloom:
                    SetBloom(false);
                    break;
                case PostProcessingType.Bright: // I don't actually have any idea what this one is supposed to do
                    SetBrightness(false);
                    break;
                case PostProcessingType.Contrast: // This is just my best guess
                    SetContrast(false);
                    break;
                case PostProcessingType.Posterize:
                    SetPosterize(false);
                    break;
                case PostProcessingType.PhotoNegative:
                    SetInvertedColors(false);
                    break;
                case PostProcessingType.Mirror:
                    SetMirror(false);
                    break;
                case PostProcessingType.BlackAndWhite:
                case PostProcessingType.Scanlines_BlackAndWhite:
                    // TODO: Actually implement a scan line shader
                    SetBlackAndWhite(false);
                    break;
                case PostProcessingType.SepiaTone:
                    SetSepiaTone(false);
                    break;
                case PostProcessingType.SilverTone:
                    SetSilverTone(false);
                    break;
                case PostProcessingType.Scanlines_Blue:
                    SetBlueTint(false);
                    break;
                case PostProcessingType.Scanlines_Security:
                    SetGreenTint(false);
                    break;
                case PostProcessingType.Grainy_Film:
                    SetGrainy(false);
                    break;
            }
        }

        private void SetInvertedColors(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            colorCurves.master = enabled ? _invertCurveParam : _defaultCurveParam;
            colorCurves.active = enabled;
        }

        private void SetPosterize(bool enabled)
        {
            // TODO: Implement a shader to do this
            YargLogger.LogDebug("Posterize PostProcessingType is not yet supported!");
        }

        private void SetContrast(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }
            colorAdjustments.contrast.value = enabled ? 25.0f : 1.0f;
        }

        private void SetBrightness(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            // maybe we set postexposure here?
        }

        private void SetMirror(bool enabled)
        {
            // Mirror the camera horizontally
            // TODO: Actually implement screen mirroring
            YargLogger.LogDebug("Mirror PostProcessingType is not yet supported!");
        }

        private void SetBlackAndWhite(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            colorAdjustments.saturation.value = enabled ? -100.0f : 0.0f;
            colorAdjustments.saturation.overrideState = enabled;
            colorAdjustments.contrast.value = enabled ? 100f : 1.0f;
            colorAdjustments.contrast.overrideState = enabled;
            colorAdjustments.postExposure.value = enabled ? 1.5f : 1.0f;
            colorAdjustments.postExposure.overrideState = enabled;
        }

        private void SetSilverTone(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }
            colorAdjustments.saturation.value = enabled ? -100.0f : 0.0f;
            colorAdjustments.saturation.overrideState = enabled;
        }

        private void SetSepiaTone(bool enabled)
        {
            if (!_profile.TryGet<ChannelMixer>(out var mixer))
            {
                return;
            }

            YargLogger.LogFormatDebug("Sepia Tone: {0}", enabled ? "Enabled" : "Disabled");

            mixer.redOutRedIn.value = enabled ? 39.3f : 100.0f;
            mixer.greenOutRedIn.value = enabled ? 34.9f : 0.0f;
            mixer.blueOutRedIn.value = enabled ? 27.2f : 0.0f;

            mixer.redOutGreenIn.value = enabled ? 76.9f : 0.0f;
            mixer.greenOutGreenIn.value = enabled ? 68.6f : 100.0f;
            mixer.blueOutGreenIn.value = enabled ? 53.4f : 0.0f;

            mixer.blueOutRedIn.value = enabled ? 18.9f : 0.0f;
            mixer.greenOutRedIn.value = enabled ? 16.8f : 0.0f;
            mixer.blueOutBlueIn.value = enabled ? 13.1f : 100.0f;

            mixer.redOutRedIn.overrideState = enabled;
            mixer.redOutGreenIn.overrideState = enabled;
            mixer.redOutBlueIn.overrideState = enabled;
            mixer.greenOutRedIn.overrideState = enabled;
            mixer.greenOutGreenIn.overrideState = enabled;
            mixer.greenOutBlueIn.overrideState = enabled;
            mixer.blueOutRedIn.overrideState = enabled;
            mixer.blueOutGreenIn.overrideState = enabled;
            mixer.blueOutBlueIn.overrideState = enabled;

            mixer.active = enabled;

        }


        private void SetBloom(bool enabled)
        {
            if (!_profile.TryGet<Bloom>(out var bloom))
            {
                return;
            }

            bloom.intensity.value = enabled ? 1.0f : _originalBloom;
        }

        private void SetGreenTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            colorAdjustments.colorFilter.value = enabled ? _greenTint : Color.white;
            colorAdjustments.colorFilter.overrideState = enabled;
        }

        private void SetBlueTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            colorAdjustments.colorFilter.value = enabled ? _blueTint : Color.white;
            colorAdjustments.colorFilter.overrideState = enabled;
        }

        private void SetGrainy(bool enabled)
        {
            if (!_profile.TryGet<FilmGrain>(out var grain))
            {
                return;
            }

            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            colorAdjustments.contrast.value = enabled ? 20.0f : 0.0f;
            grain.intensity = enabled ? _activeGrainIntensity : _defaultGrainIntensity;
            grain.response = enabled ? _activeGrainResponse : _defaultGrainResponse;
        }
    }
}