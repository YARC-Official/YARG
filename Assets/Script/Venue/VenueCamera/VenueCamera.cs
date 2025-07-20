using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay;

namespace YARG.Venue.VenueCamera
{
    [RequireComponent(typeof(Camera))]
    public class VenueCamera : GameplayBehaviour
    {
        [SerializeField]
        private CameraManager _cameraManager;
        [FormerlySerializedAs("_volume")]
        [SerializeField]
        public VolumeProfile Volume;
        [SerializeField]
        public CameraManager.CameraLocation CameraLocation;

        [Space]
        [SerializeField]
        [Header("Camera Cut Subjects For This Camera")]
        public List<CameraCutEvent.CameraCutSubject> CameraCutSubjects;

        private PostProcessingType _currentEffect = PostProcessingType.Default;
        private VolumeProfile _profile;

        private float _originalBloom;
		private bool _originalBloomState;
		private float _originalBloomThreshold;
		private bool _originalBloomTState;

        private ColorParameter _sepiaToneColor;

        private TextureCurve _invertCurve;
        private TextureCurve _defaultCurve;
        private TextureCurve _copierCurve;
        private TextureCurve _brightCurve;

        private TextureCurveParameter _invertCurveParam;
        private TextureCurveParameter _defaultCurveParam;
        private TextureCurveParameter _copierCurveParam;
        private TextureCurveParameter _brightCurveParam;

        private ClampedFloatParameter _defaultGrainIntensity;
        private ClampedFloatParameter _defaultGrainResponse;
        private ClampedFloatParameter _activeGrainIntensity = new(1.0f, 1.0f, 0.0f);
        private ClampedFloatParameter _activeGrainResponse = new(0.0f, 1.0f, 0.0f);

        private Color _greenTint = new(0.0f, 1.0f, 0.65f, 1.0f);
        private Color _blueTint  = new(0.2f, 0.7f, 1.0f, 1.0f);

        private Camera         _camera;

        private List<CurveAnimation> _curveAnimations = new();
        private List<FloatAnimation> _floatAnimations = new();
        private List<ClampedFloatAnimation> _clampedFloatAnimations = new();
        private List<ColorAnimation> _colorAnimations = new();

        protected override void GameplayAwake()
        {
            _camera = GetComponent<Camera>();
            _profile = Volume;
            if (_profile.TryGet<Bloom>(out var bloom))
            {
                _originalBloom = bloom.intensity.value;
				_originalBloomState = bloom.intensity.overrideState;
				_originalBloomThreshold = bloom.threshold.value;
				_originalBloomTState = bloom.threshold.overrideState;
            }

            if (_profile.TryGet<FilmGrain>(out var grain))
            {
                _defaultGrainIntensity = grain.intensity;
                _defaultGrainResponse = grain.response;
            }

            var bounds = new Vector2(0, 1);
            _defaultCurve = new TextureCurve(new AnimationCurve(new Keyframe(0f,0f,1f,1f), new Keyframe(1f,1f,1f,1f)), 0.5f, false, in bounds);
            _invertCurve = new TextureCurve(new AnimationCurve(new Keyframe(0,0.550f,-1f,-6f), new Keyframe(0.25f,0f,0f,0f)), 0.5f, false, in bounds);
            _copierCurve = new TextureCurve(new AnimationCurve(new Keyframe(0,0), new Keyframe(0.089f,0.078f),
                new Keyframe(0.227f, 0), new Keyframe(0.356f, 0.993f), new Keyframe(1, 1)), 0.5f, false, in bounds);
			_brightCurve = new TextureCurve(new AnimationCurve(new Keyframe(0f,0.07f), new Keyframe(0.25f,1f)), 0.5f, false, in bounds);


            _defaultCurveParam = new TextureCurveParameter(_defaultCurve);
            _invertCurveParam = new TextureCurveParameter(_invertCurve, true);
            _copierCurveParam = new TextureCurveParameter(_copierCurve, true);
            _brightCurveParam = new TextureCurveParameter(_brightCurve, true);
        }

        private void Update()
        {
            var newEffect = _cameraManager.CurrentEffect;

            if (newEffect != _currentEffect)
            {
                if (!_cameraManager.EffectSet)
                {
                    SetCameraPostProcessing(newEffect);
                    _cameraManager.EffectSet = true;
                }

                _currentEffect = newEffect;
            }

            // The rest of this isn't our problem if we aren't the active camera
            if (!_camera.enabled)
            {
                return;
            }

            UpdateAnimations();
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
            var found = true;

            // First reset any existing effects, because they aren't supposed to stack
            ResetCameraEffect();

            // Now set the new effect
            switch (newEffect)
            {
                case PostProcessingType.Default:
                    break;
                case PostProcessingType.Bloom:
                    SetLowFrameRate(true);
                    SetBloom(true);
                    break;
                case PostProcessingType.Bright:
                    SetBrightness(true);
					SetBloom(true);
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
                    // TODO: This is supposed to also have a "psychadelic coloring" effect
                    // "Polarizes everything to green/orange with some blue and purple here and there"
                    SetMirror(true);
                    break;
                case PostProcessingType.BlackAndWhite:
                    SetBlackAndWhite(true);
                    break;
                case PostProcessingType.Choppy_BlackAndWhite:
                    SetLowFrameRate(true);
                    SetBlackAndWhite(true);
                    SetBadCopier(true);
                    break;
                case PostProcessingType.Scanlines_BlackAndWhite:
                    SetBlackAndWhite(true);
                    SetScanline(true);
                    break;
                case PostProcessingType.Polarized_BlackAndWhite:
                    SetBlackAndWhite(true);
                    SetPosterize(true);
                    break;
                case PostProcessingType.SepiaTone:
                    SetSepiaTone(true);
					SetBloom(true);
                    break;
                case PostProcessingType.SilverTone:
                    SetSilverTone(true);
					SetBloom(true);
                    break;
                case PostProcessingType.Scanlines:
                    SetScanline(true);
					SetBloom(true);
                    break;
                case PostProcessingType.Scanlines_Blue:
                    SetBlueTint(true);
					SetContrast(true);
					SetBloom(true);
                    SetScanline(true);
                    break;
                case PostProcessingType.Scanlines_Security:
                    SetGrainy(true);
                    SetGreenTint(true);
					SetContrast(true);
					SetBloom(true);
                    SetScanline(true);
                    break;
                case PostProcessingType.Grainy_Film:
                    SetGrainy(true);
                    SetExposure(true, -0.75f);
                    SetBloom(true);
                    break;
                case PostProcessingType.Grainy_ChromaticAbberation:
                    SetGrainy(true);
                    SetChromaticAberration(true);
                    SetDesaturation(true, -35f);
                    break;
                case PostProcessingType.Trails_Flickery: // TODO: Add a flicker effect for this
                case PostProcessingType.Trails:
                    SetTrail(true, 0.55f);
					SetBloom(true);
                    break;
                case PostProcessingType.Trails_Desaturated:
                    SetPosterize(true, 10);
                    SetTrail(true, 0.67f);
                    SetDesaturation(true);
                    break;
                case PostProcessingType.Trails_Long:
                    SetTrail(true, 0.67f);
                    break;
                case PostProcessingType.Trails_Spacey:
                    SetTrail(true, 0.9f);
                    SetDesaturation(true, 20f);
                    break;
                case PostProcessingType.Desaturated_Red: // TODO: This almost certainly needs drastic adjustment
                    SetDesaturatedRed(true);
                    break;
                case PostProcessingType.Desaturated_Blue:
                    SetDesaturatedBlue(true);
                    SetBloom(true);
                    break;
                case PostProcessingType.PhotoNegative_RedAndBlack:
                    SetPhotoNegativeRedAndBlack(true);
                    break;
                case PostProcessingType.Contrast_Green:
                    SetContrastGreen(true);
                    SetBloom(true);
                    break;
                case PostProcessingType.Contrast_Blue:
                    SetContrastBlue(true);
                    SetBloom(true);
                    break;
                case PostProcessingType.Contrast_Red:
                    SetContrastRed(true);
                    SetBloom(true);
                    break;
                default:
                    found = false;
                    break;
            }

            if (!found)
            {
                YargLogger.LogFormatDebug("Unsupported post processing effect: {0}", newEffect.ToString());
            }
            else
            {
                YargLogger.LogFormatDebug("Set post processing effect to: {0}", newEffect.ToString());
            }
        }

        private void ResetCameraEffect()
        {
            switch (_currentEffect)
            {
                case PostProcessingType.Default:
                    break;
                case PostProcessingType.Bloom:
                    SetLowFrameRate(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Bright:
                    SetBrightness(false);
					SetBloom(false);
                    break;
                case PostProcessingType.Contrast:
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
                    SetBlackAndWhite(false);
                    break;
                case PostProcessingType.Choppy_BlackAndWhite:
                    SetLowFrameRate(false);
                    SetBlackAndWhite(false);
                    SetBadCopier(false);
                    break;
                case PostProcessingType.Polarized_BlackAndWhite:
                    SetBlackAndWhite(false);
                    SetPosterize(false);
                    break;
                case PostProcessingType.SepiaTone:
                    SetSepiaTone(false);
					SetBloom(false);
                    break;
                case PostProcessingType.SilverTone:
                    SetSilverTone(false);
					SetBloom(false);
                    break;
                case PostProcessingType.Scanlines:
                    SetScanline(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Scanlines_BlackAndWhite:
                    SetBlackAndWhite(false);
                    SetScanline(false);
                    break;
                case PostProcessingType.Scanlines_Blue:
                    SetBlueTint(false);
					SetContrast(false);
					SetBloom(false);
                    SetScanline(false);
                    break;
                case PostProcessingType.Scanlines_Security:
                    SetGrainy(false);
                    SetGreenTint(false);
					SetContrast(false);
					SetBloom(false);
                    SetScanline(false);
                    break;
                case PostProcessingType.Grainy_Film:
                    SetGrainy(false);
                    SetExposure(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Grainy_ChromaticAbberation:
                    SetGrainy(false);
                    SetChromaticAberration(false);
                    SetDesaturation(false);
                    break;
                case PostProcessingType.Trails_Desaturated:
                    SetPosterize(false);
                    SetDesaturation(false);
                    SetTrail(false);
                    break;
                case PostProcessingType.Trails:
                case PostProcessingType.Trails_Long:
                case PostProcessingType.Trails_Flickery:
                    SetTrail(false);
                    break;
                case PostProcessingType.Trails_Spacey:
                    SetTrail(false);
                    SetDesaturation(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Desaturated_Red:
                    SetDesaturatedRed(false);
                    break;
                case PostProcessingType.Desaturated_Blue:
                    SetDesaturatedBlue(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.PhotoNegative_RedAndBlack:
                    SetPhotoNegativeRedAndBlack(false);
                    break;
                case PostProcessingType.Contrast_Green:
                    SetContrastGreen(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Contrast_Blue:
                    SetContrastBlue(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.Contrast_Red:
                    SetContrastRed(false);
                    SetBloom(false);
                    break;
            }
        }

        private void SetContrastGreen(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            if (!_profile.TryGet<ChannelMixer>(out var channelMixer))
            {
                return;
            }

            SetAnimation(colorAdjustments.postExposure, enabled ? 1f : 0f, 0.25f, enabled);
            SetAnimation(channelMixer.greenOutGreenIn, enabled ? 10f : 100f, 0.25f, enabled);

            // colorAdjustments.postExposure.value = enabled ? 1f : 0f;
            // colorAdjustments.postExposure.overrideState = enabled;
            //
            // channelMixer.greenOutGreenIn.value = enabled ? 10f : 100f;
            // channelMixer.greenOutGreenIn.overrideState = enabled;
        }

        private void SetContrastBlue(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            if (!_profile.TryGet<ChannelMixer>(out var channelMixer))
            {
                return;
            }

            SetAnimation(colorAdjustments.postExposure, enabled ? 1f : 0f, 0.25f, enabled);
            SetAnimation(channelMixer.blueOutBlueIn, enabled ? 10f : 100f, 0.25f, enabled);

            // colorAdjustments.postExposure.value = enabled ? 1f : 0f;
            // colorAdjustments.postExposure.overrideState = enabled;
            //
            // channelMixer.blueOutBlueIn.value = enabled ? 10f : 100f;
            // channelMixer.blueOutBlueIn.overrideState = enabled;
        }

        private void SetContrastRed(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            if (!_profile.TryGet<ChannelMixer>(out var channelMixer))
            {
                return;
            }

            SetAnimation(colorAdjustments.postExposure, enabled ? 1f : 0f, 0.25f, enabled);
            SetAnimation(channelMixer.redOutRedIn, enabled ? 10f : 100f, 0.25f, enabled);

            // colorAdjustments.postExposure.value = enabled ? 1f : 0f;
            // colorAdjustments.postExposure.overrideState = enabled;
            //
            // channelMixer.redOutRedIn.value = enabled ? 10f : 100f;
            // channelMixer.redOutRedIn.overrideState = enabled;
        }

        private void SetExposure(bool enabled, float strength = 0f)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.postExposure, enabled ? strength : 0f, 0.25f, enabled);

            // colorAdjustments.postExposure.value = enabled ? strength : 0f;
            // colorAdjustments.postExposure.overrideState = enabled;
        }

        private void SetChromaticAberration(bool enabled, float strength = 0.35f)
        {
            if (!_profile.TryGet<ChromaticAberration>(out var chromaticAberration))
            {
                return;
            }

            SetAnimation(chromaticAberration.intensity, enabled ? strength : 0, 0.25f, enabled);

            // chromaticAberration.intensity.value = enabled ? strength : 0;
            // chromaticAberration.intensity.overrideState = enabled;
        }

        public void SetLowFrameRate(bool enabled, int divisor = 5)
        {
            if (!_profile.TryGet<SlowFPSComponent>(out var slowFPS))
            {
                return;
            }

            slowFPS.SkipFrames.value = enabled ? divisor : 1;
            slowFPS.SkipFrames.overrideState = enabled;
            slowFPS.active = enabled;
        }

        private void SetDesaturatedRed(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            var bounds = new Vector2(0, 1);

            // We need to define this during setup, but it's too ugly to put up top for now
            var lumVSatCurve =
                new TextureCurve(new AnimationCurve(new Keyframe(0, 0.525f), new Keyframe(0.639f, 0.085f)), 0.5f, false,
                    in bounds);
            // TODO: Properly set the tangents on the keyframes
            var greenCurve =
                new TextureCurve(
                    new AnimationCurve(new Keyframe(0, 0.0f), new Keyframe(0.5f, 0.0f), new Keyframe(1, 1)), 0.5f,
                    false, in bounds);
            var blueCurve =
                new TextureCurve(
                    new AnimationCurve(new Keyframe(0, 0.0f), new Keyframe(0.437f, 0.0f), new Keyframe(0.659f, 0.174f),
                        new Keyframe(1, 1)), 0.5f, false, in bounds);

            var lumVSatCurveParam = new TextureCurveParameter(lumVSatCurve, true);
            var greenCurveParam = new TextureCurveParameter(greenCurve, true);
            var blueCurveParam = new TextureCurveParameter(blueCurve, true);

            SetAnimation(colorCurves.lumVsSat, enabled ? lumVSatCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.green, enabled ? greenCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.blue, enabled ? blueCurveParam : _defaultCurveParam, 0.25f, enabled);

            // colorCurves.lumVsSat.value = enabled ? lumVSatCurve : _defaultCurve;
            // colorCurves.green.value = enabled ? greenCurve : _defaultCurve;
            // colorCurves.blue.value = enabled ? blueCurve : _defaultCurve;
            // colorCurves.lumVsSat.overrideState = enabled;
            // colorCurves.green.overrideState = enabled;
            // colorCurves.blue.overrideState = enabled;
            // colorCurves.active = enabled;
        }

        private void SetDesaturatedBlue(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            var bounds = new Vector2(0, 1);

            var hueVsSatCurve = new TextureCurve(new AnimationCurve(new Keyframe(0, 0.152f),
                new Keyframe(0.361f, 0.152f), new Keyframe(0.367f, 0.5f), new Keyframe(0.677f, 0.5f),
                new Keyframe(0.683f, 0.152f), new Keyframe(1, 0.152f)), 0.5f, false, in bounds);

            var hueVsSatCurveParam = new TextureCurveParameter(hueVsSatCurve, true);

            var blueFilterColor = new Color(0.413f, 0.559f, 0.962f, 1f);

            SetAnimation(colorCurves.hueVsSat, enabled ? hueVsSatCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorAdjustments.postExposure, enabled ? 1.5f : 0f, 0.25f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? blueFilterColor : Color.white, 0.25f, enabled);

            // colorAdjustments.postExposure.value = enabled ? 1.5f : 0f;
            // colorAdjustments.postExposure.overrideState = enabled;
            // colorAdjustments.colorFilter.value = enabled ? blueFilterColor : Color.white;
            // colorAdjustments.colorFilter.overrideState = enabled;
            //
            // colorCurves.hueVsSat.value = enabled ? hueVsSatCurve : _defaultCurve;
            // colorCurves.hueVsSat.overrideState = enabled;
        }

        private void SetPhotoNegativeRedAndBlack(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            var bounds = new Vector2(0, 1);

            var flatZeroCurve = new TextureCurve(new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0)), 0.5f,
                false, in bounds);
            var redCurve =
                new TextureCurve(
                    new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.069f, 0.734f), new Keyframe(0.5f, 0.0f),
                        new Keyframe(1, 1)), 0.5f, false, in bounds);
            var masterCurve = new TextureCurve(
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.297f, 0), new Keyframe(0.519f, 0.519f), new Keyframe(1, 0)),
                0.5f, false, in bounds);

            var flatZeroCurveParam = new TextureCurveParameter(flatZeroCurve, true);
            var redCurveParam = new TextureCurveParameter(redCurve, true);
            var masterCurveParam = new TextureCurveParameter(masterCurve, true);

            SetAnimation(colorCurves.hueVsHue, enabled ? _invertCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.green, enabled ? flatZeroCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.blue, enabled ? flatZeroCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.red, enabled ? redCurveParam : _defaultCurveParam, 0.25f, enabled);
            SetAnimation(colorCurves.master, enabled ? masterCurveParam : _defaultCurveParam, 0.25f, enabled);

            // colorCurves.hueVsHue.value = enabled ? _invertCurve : _defaultCurve;
            // colorCurves.green.value = enabled ? flatZeroCurve : _defaultCurve;
            // colorCurves.blue.value = enabled ? flatZeroCurve : _defaultCurve;
            // colorCurves.red.value = enabled ? redCurve : _defaultCurve;
            // colorCurves.master.value = enabled ? masterCurve : _defaultCurve;
            // colorCurves.hueVsHue.overrideState = enabled;
            // colorCurves.green.overrideState = enabled;
            // colorCurves.blue.overrideState = enabled;
            // colorCurves.red.overrideState = enabled;
            // colorCurves.master.overrideState = enabled;
            // colorCurves.active = enabled;
        }

        private void SetInvertedColors(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _invertCurveParam : _defaultCurveParam, 0.25f, enabled);

            // colorCurves.active = enabled;
            // colorCurves.master.value = enabled ? _invertCurve : _defaultCurve;
            // colorCurves.master.overrideState = enabled;
        }

        private void SetPosterize(bool enabled, int steps = 4)
        {
            if (!_profile.TryGet<PosterizeComponent>(out var posterize))
            {
                return;
            }
            posterize.Steps.value = enabled ? steps : 0;
            posterize.Steps.overrideState = enabled;
        }

        private void SetContrast(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.contrast, enabled ? 50.0f : 1.0f, 0.25f, enabled);

            // colorAdjustments.contrast.value = enabled ? 50.0f : 1.0f;
            // colorAdjustments.contrast.overrideState = enabled;
        }

        private void SetBrightness(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _brightCurveParam : _defaultCurveParam, 0.25, enabled);

            // colorCurves.master.value = enabled ? _brightCurve : _defaultCurve;
            // colorCurves.master.overrideState = enabled;
            // colorCurves.active = enabled;

			if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, enabled ? 50.0f : 0.0f, 0.25, enabled);

            // colorAdjustments.saturation.value = enabled ? 50.0f : 0.0f;
            // colorAdjustments.saturation.overrideState = enabled;
        }

        private void SetMirror(bool enabled)
        {
            if (!_profile.TryGet<MirrorComponent>(out var mirror))
            {
                return;
            }

            mirror.enabled.value = enabled;
            mirror.enabled.overrideState = enabled;
        }

        private void SetDesaturation(bool enabled, float strength = -50.0f)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, enabled ? strength : 0.0f, 0.25, enabled);

            // colorAdjustments.saturation.value = enabled ? strength : 0.0f;
            // colorAdjustments.saturation.overrideState = enabled;
        }

        private void SetBlackAndWhite(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, enabled ? -100.0f : 0.0f, 0.25, enabled);
            SetAnimation(colorAdjustments.contrast, enabled ? 100f : 1.0f, 0.25, enabled);
            SetAnimation(colorAdjustments.postExposure, enabled ? 1.5f : 1.0f, 0.25, enabled);

            // colorAdjustments.saturation.value = enabled ? -100.0f : 0.0f;
            // colorAdjustments.saturation.overrideState = enabled;
            // colorAdjustments.contrast.value = enabled ? 100f : 1.0f;
            // colorAdjustments.contrast.overrideState = enabled;
            // colorAdjustments.postExposure.value = enabled ? 1.5f : 1.0f;
            // colorAdjustments.postExposure.overrideState = enabled;
        }

        private void SetBadCopier(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _copierCurveParam : _defaultCurveParam, 0.25, enabled);

            // colorCurves.master.value = enabled ? _copierCurve : _defaultCurve;
            // colorCurves.master.overrideState = enabled;
            // colorCurves.active = enabled;
        }

        private void SetSilverTone(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, enabled ? -100.0f : 0.0f, 0.25, enabled);

            // colorAdjustments.saturation.value = enabled ? -100.0f : 0.0f;
            // colorAdjustments.saturation.overrideState = enabled;
        }

        private void SetSepiaTone(bool enabled)
        {
            if (!_profile.TryGet<ChannelMixer>(out var mixer))
            {
                return;
            }

            SetAnimation(mixer.redOutRedIn, enabled ? 39.3f : 100f, 0.25f, enabled);
            SetAnimation(mixer.greenOutRedIn, enabled ? 34.9f : 0f, 0.25f, enabled);
            SetAnimation(mixer.blueOutRedIn, enabled ? 27.2f : 0f, 0.25f, enabled);

            SetAnimation(mixer.redOutGreenIn, enabled ? 76.9f : 0f, 0.25f, enabled);
            SetAnimation(mixer.greenOutGreenIn, enabled ? 68.6f : 100f, 0.25f, enabled);
            SetAnimation(mixer.blueOutGreenIn, enabled ? 53.4f : 0f, 0.25f, enabled);

            SetAnimation(mixer.blueOutRedIn, enabled ? 18.9f : 0f, 0.25f, enabled);
            SetAnimation(mixer.greenOutRedIn, enabled ? 16.8f : 0f, 0.25f, enabled);
            SetAnimation(mixer.blueOutBlueIn, enabled ? 13.1f : 100f, 0.25f, enabled);

            // mixer.redOutRedIn.value = enabled ? 39.3f : 100.0f;
            // mixer.greenOutRedIn.value = enabled ? 34.9f : 0.0f;
            // mixer.blueOutRedIn.value = enabled ? 27.2f : 0.0f;
            //
            // mixer.redOutGreenIn.value = enabled ? 76.9f : 0.0f;
            // mixer.greenOutGreenIn.value = enabled ? 68.6f : 100.0f;
            // mixer.blueOutGreenIn.value = enabled ? 53.4f : 0.0f;
            //
            // mixer.blueOutRedIn.value = enabled ? 18.9f : 0.0f;
            // mixer.greenOutRedIn.value = enabled ? 16.8f : 0.0f;
            // mixer.blueOutBlueIn.value = enabled ? 13.1f : 100.0f;
            //
            // mixer.redOutRedIn.overrideState = enabled;
            // mixer.redOutGreenIn.overrideState = enabled;
            // mixer.redOutBlueIn.overrideState = enabled;
            // mixer.greenOutRedIn.overrideState = enabled;
            // mixer.greenOutGreenIn.overrideState = enabled;
            // mixer.greenOutBlueIn.overrideState = enabled;
            // mixer.blueOutRedIn.overrideState = enabled;
            // mixer.blueOutGreenIn.overrideState = enabled;
            // mixer.blueOutBlueIn.overrideState = enabled;
            //
            // mixer.active = enabled;
        }


        private void SetBloom(bool enabled)
        {
            if (!_profile.TryGet<Bloom>(out var bloom))
            {
                return;
            }

            if (enabled)
            {
                SetAnimation(bloom.intensity, 1.0f, 0.25f, enabled);
                SetAnimation(bloom.threshold, 0.6f, 0.25f, enabled);
            }
            else
            {
                SetAnimation(bloom.intensity, _originalBloom, 0.25f, enabled);
                SetAnimation(bloom.threshold, _originalBloomThreshold, 0.25f, enabled);
            }

            // bloom.intensity.value = enabled ? 1.0f : _originalBloom;
            // bloom.intensity.overrideState = enabled || _originalBloomState;
			// bloom.threshold.value = enabled ? 0.6f : _originalBloomThreshold;
            // bloom.threshold.overrideState = enabled || _originalBloomTState;
        }

        private void SetGreenTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.colorFilter, enabled ? _greenTint : Color.white, 0.25f, enabled);
            //
            // colorAdjustments.colorFilter.value = enabled ? _greenTint : Color.white;
            // colorAdjustments.colorFilter.overrideState = enabled;
        }

        private void SetBlueTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.colorFilter, enabled ? _blueTint : Color.white, 0.25f, enabled);
            //
            // colorAdjustments.colorFilter.value = enabled ? _blueTint : Color.white;
            // colorAdjustments.colorFilter.overrideState = enabled;
        }

        private void SetScanline(bool enabled)
        {
            if (!_profile.TryGet<ScanlineComponent>(out var scanline))
            {
                return;
            }

            scanline.intensity.value = enabled ? 0.6f : 0.0f;
            scanline.intensity.overrideState = enabled;
            // This should really be ~1/4 of the screen resolution
            scanline.scanlineCount.value = 190;
            scanline.scanlineCount.overrideState = enabled;
        }

        private void SetTrail(bool enabled, float intensity = 0.6f)
        {
            if (!_profile.TryGet<TrailsComponent>(out var trail))
            {
                return;
            }

            trail.length.value = enabled ? intensity : 0.0f;
            trail.length.overrideState = enabled;
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

            SetAnimation(colorAdjustments.contrast, enabled ? 20.0f : 0.0f, 0.25f, enabled);
            SetAnimation(grain.intensity, enabled ? 1.0f : 0.25f, 0.25f, enabled);
            SetAnimation(grain.response, enabled ? 0.0f : 0.8f, 0.25f, enabled);

            // colorAdjustments.contrast.value = enabled ? 20.0f : 0.0f;
            // grain.intensity.value = enabled ? 1.0f : 0.25f;
            // grain.intensity.overrideState = enabled;
            // grain.response.value = enabled ? 0.0f : 0.8f;
            // grain.response.overrideState = enabled;
        }

        // TODO: make the animation code actually work so we can actually set the animation...
        private void SetAnimation(ClampedFloatParameter target, float endValue, double endTime, bool enabling)
        {
            var anim = new ClampedFloatAnimation(endValue, endTime, target, GameManager);
            // _clampedFloatAnimations.Add(anim);

            target.value = endValue;
            target.overrideState = enabling;
        }

        private void SetAnimation(FloatParameter target, float endValue, double endTime, bool enabling)
        {
            var anim = new FloatAnimation(endValue, endTime, target, GameManager);
            // _floatAnimations.Add(anim);

            target.value = endValue;
            target.overrideState = enabling;
        }

        private void SetAnimation(TextureCurveParameter target, TextureCurveParameter endValue, double endTime, bool enabling)
        {
            var anim = new CurveAnimation(endValue.value, endTime, GameManager, target);
            // _curveAnimations.Add(anim);

            target.value = endValue.value;
            target.overrideState = enabling;
            target.value.SetDirty();
        }

        private void SetAnimation(ColorParameter target, Color endValue, double endTime, bool enabling)
        {
            var anim = new ColorAnimation(endValue, endTime, target, GameManager);
            // _colorAnimations.Add(anim);

            target.value = endValue;
            target.overrideState = enabling;
        }

        private void UpdateAnimations()
        {
            if (_curveAnimations.Count > 0)
            {
                for (int i = 0; i < _curveAnimations.Count; i++)
                {
                    var curveAnimation = _curveAnimations[i];
                    if (curveAnimation.Update())
                    {
                        _curveAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (_floatAnimations.Count > 0)
            {
                for (int i = 0; i < _floatAnimations.Count; i++)
                {
                    var floatAnimation = _floatAnimations[i];
                    if (floatAnimation.Update())
                    {
                        _floatAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (_clampedFloatAnimations.Count > 0)
            {
                for (int i = 0; i < _clampedFloatAnimations.Count; i++)
                {
                    var floatAnimation = _clampedFloatAnimations[i];
                    if (floatAnimation.Update())
                    {
                        _clampedFloatAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (_colorAnimations.Count > 0)
            {
                for (int i = 0; i < _colorAnimations.Count; i++)
                {
                    var colorAnimation = _colorAnimations[i];
                    if (colorAnimation.Update())
                    {
                        _colorAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }

    public class CurveAnimation
    {
        public TextureCurve   StartCurve;
        public TextureCurve   EndCurve;
        public double         StartTime;
        public double         EndTime;
        public GameManager    GameManager;
        public Keyframe[]     StartPoints;


        public TextureCurveParameter Target;

        private          TextureCurve _interpolatedCurve;
        private          bool         _started = false;
        private          TextureCurve _workingCurve;

        public CurveAnimation(TextureCurve startCurve, TextureCurve endCurve, double startTime, double endTime, GameManager gameManager, TextureCurveParameter target)
        {
            StartCurve = startCurve;
            EndCurve = endCurve;
            StartTime = startTime;
            EndTime = endTime + startTime;
            GameManager = gameManager;
            StartPoints = GetInitialKeyframes(StartCurve, EndCurve);
            Target = target;

            _interpolatedCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
            _workingCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
        }

        public CurveAnimation(TextureCurve endCurve, double endTime,
            GameManager gameManager, TextureCurveParameter target)
        {
            StartCurve = target.value;
            EndCurve = endCurve;
            StartTime = gameManager.RealVisualTime;
            EndTime = endTime + StartTime;
            GameManager = gameManager;
            StartPoints = GetInitialKeyframes(StartCurve, EndCurve);
            Target = target;

            _interpolatedCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
            _workingCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
        }

        private static Keyframe[] GetInitialKeyframes(TextureCurve startCurve, TextureCurve endCurve)
        {
            // Thankfully AnimationCurve.Evaluate can save us from math I'm too uneducated to understand
            var startPoints = new Keyframe[endCurve.length];
            for (var i = 0; i < endCurve.length; i++)
            {
                // For each of the end points, get its x (time) and find the y that it _would_ have if it were on
                // the start curve
                startPoints[i] = new Keyframe(endCurve[i].time, startCurve.Evaluate(endCurve[i].time));
            }

            return startPoints;
        }

        // Returns true once the animation has completed
        public bool Update()
        {
            if (GameManager.RealVisualTime < StartTime)
            {
                // Nothing to do yet
                return false;
            }

            if (GameManager.RealVisualTime >= EndTime)
            {
                // We're done
                return true;
            }

            if (!_started)
            {
                _started = true;
                Target.value = _interpolatedCurve;
                Target.overrideState = true;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = 1 - ((EndTime - GameManager.RealVisualTime) / (EndTime - StartTime));

            // Lerp all the keyframes in the curve
            // Annoyingly, we have to create an entire new curve every step
            var newCurve = new AnimationCurve();
            for (var i = 0; i < EndCurve.length; i++)
            {
                var point = Mathf.Lerp(_interpolatedCurve[i].value, EndCurve[i].value, (float) normalizedTime);
                newCurve.AddKey(new Keyframe(EndCurve[i].time, point));
            }

            Target.value = new TextureCurve(newCurve, 0.5f, false, new Vector2(0, 1));

            return false;
        }
    }

    public class ClampedFloatAnimation
    {
        private readonly float                 _startValue;
        private readonly float                 _endValue;
        private readonly double                _startTime;
        private readonly double                _endTime;
        private readonly GameManager           _gameManager;
        private readonly ClampedFloatParameter _param;

        private bool _started = false;

        public ClampedFloatAnimation(float startValue, float endValue, double startTime, double endTime,
            ClampedFloatParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _endTime = endTime + startTime;
            _gameManager = gameManager;
            _param = param;
        }

        public ClampedFloatAnimation(float endValue, double endTime, ClampedFloatParameter param,
            GameManager gameManager)
        {
            _startValue = param.value;
            _endValue = endValue;
            _startTime = gameManager.RealVisualTime;
            _endTime = endTime + _startTime;
            _gameManager = gameManager;
            _param = param;
        }

        // This returns a bool so that the caller can remove us from the list when we are no longer needed
        public bool Update()
        {
            if (_gameManager.RealVisualTime < _startTime)
            {
                // Nothing to do yet
                return false;
            }

            if (_gameManager.RealVisualTime >= _endTime)
            {
                // We're done
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue;
                _param.overrideState = true;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = 1 - ((_endTime - _gameManager.RealVisualTime) / (_endTime - _startTime));

            _param.value = Mathf.Lerp(_startValue, _endValue, (float) normalizedTime);
            return false;
        }
    }

    public class FloatAnimation
    {
        private readonly float                 _startValue;
        private readonly float                 _endValue;
        private readonly double                _startTime;
        private readonly double                _endTime;
        private readonly GameManager           _gameManager;
        private readonly FloatParameter _param;

        private bool _started = false;

        public FloatAnimation(float startValue, float endValue, double startTime, double endTime, FloatParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _endTime = endTime + startTime;
            _gameManager = gameManager;
            _param = param;
        }

        public FloatAnimation(float endValue, double endTime, FloatParameter param, GameManager gameManager)
        {
            _startValue = param.value;
            _endValue = endValue;
            _startTime = gameManager.RealVisualTime;
            _endTime = endTime + _startTime;
            _gameManager = gameManager;
            _param = param;
        }

        // This returns a bool so that the caller can remove us from the list when we are no longer needed
        public bool Update()
        {
            if (_gameManager.RealVisualTime < _startTime)
            {
                // Nothing to do yet
                return false;
            }

            if (_gameManager.RealVisualTime >= _endTime)
            {
                // We're done
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue;
                _param.overrideState = true;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = 1 - ((_endTime - _gameManager.RealVisualTime) / (_endTime - _startTime));

            _param.value = Mathf.Lerp(_startValue, _endValue, (float) normalizedTime);
            return false;
        }
    }

    public class ColorAnimation
    {
        private readonly ColorParameter        _startValue;
        private readonly Color                 _endValue;
        private readonly double                _startTime;
        private readonly double                _endTime;
        private readonly GameManager           _gameManager;
        private readonly ColorParameter        _param;

        private bool _started = false;

        public ColorAnimation(ColorParameter startValue, Color endValue, double startTime, double endTime, ColorParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _endTime = endTime + startTime;
            _gameManager = gameManager;
            _param = param;
        }

        public ColorAnimation(Color endValue, double endTime, ColorParameter param, GameManager gameManager)
        {
            _startValue = param;
            _endValue = endValue;
            _startTime = gameManager.RealVisualTime;
            _endTime = endTime + _startTime;
            _gameManager = gameManager;
            _param = param;
        }

        public bool Update()
        {
            if (_gameManager.RealVisualTime < _startTime)
            {
                // Nothing to do yet
                return false;
            }

            if (_gameManager.RealVisualTime >= _endTime)
            {
                // We're done
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue.value;
                _param.overrideState = true;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = 1 - ((_endTime - _gameManager.RealVisualTime) / (_endTime - _startTime));

            _param.value = Color.Lerp(_startValue.value, _endValue, (float) normalizedTime);
            return false;
        }
    }
}