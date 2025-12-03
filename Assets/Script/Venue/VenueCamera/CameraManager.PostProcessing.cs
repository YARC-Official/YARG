using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Gameplay;
using YARG.Settings;
using YARG.Venue.VolumeComponents;
using Random = UnityEngine.Random;

namespace YARG.Venue.VenueCamera
{
    public partial class CameraManager
    {
        [SerializeField]
        private VolumeProfile _profile;
        private List<PostProcessingEvent> _postProcessingEvents;
        private int                       _currentEventIndex;
        public  PostProcessingEvent       PreviousEffect { get; private set; }
        public  PostProcessingEvent       CurrentEffect { get; private set; }
        public  PostProcessingEvent       NextEffect    { get; private set; }
        public  bool                      EffectSet     { get; set; }

        private float _originalBloom;
        private bool  _originalBloomState;
        private float _originalBloomThreshold;
        private bool  _originalBloomTState;

        private ColorParameter _sepiaToneColor;

        private TextureCurve _invertCurve;
        private TextureCurve _defaultCurve;
        private TextureCurve _copierCurve;
        private TextureCurve _brightCurve;
        private TextureCurve _flatHalfCurve;

        private TextureCurveParameter _invertCurveParam;
        private TextureCurveParameter _defaultCurveParam;
        private TextureCurveParameter _copierCurveParam;
        private TextureCurveParameter _brightCurveParam;

        private ClampedFloatParameter _defaultGrainIntensity;
        private ClampedFloatParameter _defaultGrainResponse;
        private ClampedFloatParameter _activeGrainIntensity = new(1.0f, 1.0f, 0.0f);
        private ClampedFloatParameter _activeGrainResponse  = new(0.0f, 1.0f, 0.0f);

        private readonly Color _greenTint = new(0.0f, 1.0f, 0.65f, 1.0f);
        private readonly Color _blueTint  = new(0.0f, 0.7f, 2.0f, 1.0f);
        private          Color _desatTint = new(1.3f, 0.7f, 0.7f, 1.0f);

        private readonly List<CurveAnimation>        _curveAnimations        = new();
        private readonly List<FloatAnimation>        _floatAnimations        = new();
        private readonly List<ClampedFloatAnimation> _clampedFloatAnimations = new();
        private readonly List<ColorAnimation>        _colorAnimations        = new();
        private readonly List<ClampedIntAnimation>   _clampedIntAnimations   = new();

        public void InitializePostProcessing()
        {
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
            _defaultCurve =
                new TextureCurve(new AnimationCurve(new Keyframe(0f, 0f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f)), 0.5f,
                    false, in bounds);
            _invertCurve =
                new TextureCurve(new AnimationCurve(new Keyframe(0, 0.550f, -1f, -6f), new Keyframe(0.25f, 0f, 0f, 0f)),
                    0.5f, false, in bounds);
            _copierCurve = new TextureCurve(new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.089f, 0.078f),
                new Keyframe(0.227f, 0), new Keyframe(0.356f, 0.993f), new Keyframe(1, 1)), 0.5f, false, in bounds);
            _brightCurve = new TextureCurve(new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(0.25f, 1f)), 0.5f,
                false, in bounds);
            _flatHalfCurve = new TextureCurve(new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1.0f, 0.5f)),
                0.5f, false, in bounds);


            _defaultCurveParam = new TextureCurveParameter(_defaultCurve);
            _invertCurveParam = new TextureCurveParameter(_invertCurve, true);
            _copierCurveParam = new TextureCurveParameter(_copierCurve, true);
            _brightCurveParam = new TextureCurveParameter(_brightCurve, true);

            _isPostProcessingEnabled = SettingsManager.Settings.VenuePostProcessing.Value;
            SettingsManager.Settings.VenuePostProcessing.OnChange += SetPostProcessingEnabled;
        }

        public void SetCameraPostProcessing(PostProcessingEvent newEffect)
        {
            var found = true;
            float duration = 0.0f;

            // Reset any existing effects, because they aren't supposed to stack
            ResetCameraEffect();

            //exit early if Post processing disabled
            if (!_isPostProcessingEnabled)
            {
                return;
            }

            if (newEffect.Type == PreviousEffect.Type && NextEffect != null)
            {
                // In this case, we start animating toward NextEffect now
                duration = (float) (NextEffect.Time - newEffect.Time);
                newEffect = NextEffect;
            }

            // Now set the new effect
            switch (newEffect.Type)
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
                    SetGrainy(true);
                    // SetBadCopier(true);
                    SetContrast(true);
                    break;
                case PostProcessingType.Scanlines_BlackAndWhite:
                    SetBlackAndWhite(true);
                    SetScanline(true);
                    break;
                case PostProcessingType.Polarized_BlackAndWhite:
                    SetBlackAndWhite(true);
                    SetContrast(true);
                    // SetPosterize(true);
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
					SetDesaturatedRed(true);
					SetTrail(true);
					SetContrast(true);
					break;
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
                    // SetDesaturation(true, 20f);
                    SetBrightness(true);
                    SetBloom(true);
                    SetChromaticAberration(true);
                    break;
                case PostProcessingType.Desaturated_Red: // TODO: This almost certainly needs drastic adjustment
                    SetDesaturatedRed(true);
                    break;
                case PostProcessingType.Desaturated_Blue:
                    SetDesaturatedBlue(true);
                    SetBloom(true);
                    break;
                case PostProcessingType.PhotoNegative_RedAndBlack:
                    SetInvertedColors(true);
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
                case PostProcessingType.Polarized_RedAndBlue:
                    SetPsychRB(true);
                    break;
                default:
                    found = false;
                    break;
            }

            if (!found)
            {
                YargLogger.LogFormatDebug("Unsupported post processing effect: {0}", newEffect.Type.ToString());
            }
            else
            {
                YargLogger.LogFormatDebug("Set post processing effect to: {0}", newEffect.Type.ToString());
            }
        }

        private void ResetCameraEffect()
        {
            if (PreviousEffect == null)
            {
                return;
            }

            switch (PreviousEffect.Type)
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
                    // TODO: This is supposed to also have a "psychadelic coloring" effect
                    // "Polarizes everything to green/orange with some blue and purple here and there"
                    SetMirror(false);
                    break;
                case PostProcessingType.BlackAndWhite:
                    SetBlackAndWhite(false);
                    break;
                case PostProcessingType.Choppy_BlackAndWhite:
                    SetLowFrameRate(false);
                    SetBlackAndWhite(false);
					SetGrainy(false);
                    SetContrast(false);
                    break;
                case PostProcessingType.Scanlines_BlackAndWhite:
                    SetBlackAndWhite(false);
                    SetScanline(false);
                    break;
                case PostProcessingType.Polarized_BlackAndWhite:
                    SetBlackAndWhite(false);
                    // SetPosterize(false);
                    SetContrast(false);
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
                case PostProcessingType.Trails_Flickery: // TODO: Add a flicker effect for this
					SetDesaturatedRed(false);
					SetTrail(false);
					SetContrast(false);
					break;
                case PostProcessingType.Trails:
                    SetTrail(false);
					SetBloom(false);
                    break;
                case PostProcessingType.Trails_Desaturated:
                    SetPosterize(false);
                    SetTrail(false);
                    SetDesaturation(false);
                    break;
                case PostProcessingType.Trails_Long:
                    SetTrail(false);
                    break;
                case PostProcessingType.Trails_Spacey:
                    SetTrail(false, 0.9f);
                    SetBrightness(false);
					SetBloom(false);
                    SetChromaticAberration(false);
                    break;
                case PostProcessingType.Desaturated_Red: // TODO: This almost certainly needs drastic adjustment
                    SetDesaturatedRed(false);
                    break;
                case PostProcessingType.Desaturated_Blue:
                    SetDesaturatedBlue(false);
                    SetBloom(false);
                    break;
                case PostProcessingType.PhotoNegative_RedAndBlack:
                    SetInvertedColors(false);
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
				case PostProcessingType.Polarized_RedAndBlue:
					SetPsychRB(false);
					break;
            }
        }

        private void ResetAllEffects()
        {
            _colorAnimations.Clear();
            _curveAnimations.Clear();
            _floatAnimations.Clear();
            _clampedFloatAnimations.Clear();
            _clampedIntAnimations.Clear();
            SetLowFrameRate(false);
            SetBloom(false);
            SetBrightness(false);
            SetContrast(false);
            SetPosterize(false);
            SetInvertedColors(false);
            SetMirror(false);
            SetBlackAndWhite(false);
            SetGrainy(false);
            SetScanline(false);
            SetSepiaTone(false);
            SetSilverTone(false);
            SetBlueTint(false);
            SetGreenTint(false);
            SetExposure(false);
            SetChromaticAberration(false);
            SetDesaturation(false);
            SetDesaturatedRed(false);
            SetDesaturatedBlue(false);
            SetTrail(false);
            SetPhotoNegativeRedAndBlack(false);
            SetContrastGreen(false);
            SetContrastBlue(false);
            SetContrastRed(false);
            SetPsychRB(false);
        }

        private void SetContrastGreen(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.contrast, enabled ? 30f : 0f, 0.01f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? _desatTint : Color.white, 0.01f, enabled);
            SetAnimation(colorAdjustments.hueShift, enabled ? 90f : 0f, 0.01f, enabled);
        }

        private void SetContrastBlue(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.contrast, enabled ? 30f : 0f, 0.01f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? _desatTint : Color.white, 0.01f, enabled);
            SetAnimation(colorAdjustments.hueShift, enabled ? -90f : 0f, 0.01f, enabled);
        }

        private void SetContrastRed(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.contrast, enabled ? 30f : 0f, 0.01f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? _desatTint : Color.white, 0.01f, enabled);
        }

        private void SetExposure(bool enabled, float strength = 0f)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.postExposure, enabled ? strength : 0f, 0.01f, enabled);
        }

        private void SetChromaticAberration(bool enabled, float strength = 0.35f)
        {
            if (!_profile.TryGet<ChromaticAberration>(out var chromaticAberration))
            {
                return;
            }

            SetAnimation(chromaticAberration.intensity, enabled ? strength : 0, 0.01f, enabled);
        }

        public void SetLowFrameRate(bool enabled, int divisor = 5)
        {
            if (!_profile.TryGet<SlowFPSComponent>(out var slowFPS))
            {
                return;
            }

            slowFPS.Divisor.value = enabled ? divisor : 1;
            slowFPS.Divisor.overrideState = enabled;
            slowFPS.active = enabled;
        }

        private void SetDesaturatedRed(bool enabled)
        {
			if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

			Color desatRed = new Color(1.1f, 0.9f, 0.9f);

            SetAnimation(colorAdjustments.contrast, enabled ? 30f : 0f, 0.01f, enabled);
			SetAnimation(colorAdjustments.colorFilter, enabled ? desatRed : Color.white, 0.01f, enabled);
			SetAnimation(colorAdjustments.saturation, enabled ? -50f : 0f, 0.01f, enabled);
        }

        private void SetDesaturatedBlue(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

			Color DesatBlue = new Color(0f, 0f, 4f);

            SetAnimation(colorAdjustments.saturation, enabled ? -90f : 0f, 0.01f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? DesatBlue : Color.white, 0.01f, enabled);
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

            var flatZeroCurveParam = new TextureCurveParameter(flatZeroCurve, true);

            SetAnimation(colorCurves.green, enabled ? flatZeroCurve : _defaultCurve, 0.01f, enabled);
            SetAnimation(colorCurves.blue, enabled ? flatZeroCurve : _defaultCurve, 0.01f, enabled);
        }

        private void SetInvertedColors(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _invertCurve : _defaultCurve, 0.01f, enabled);
        }

        private void SetPosterize(bool enabled, int steps = 4)
        {
            if (!_profile.TryGet<PosterizeComponent>(out var posterize))
            {
                return;
            }

            SetAnimation(posterize.Steps, enabled ? steps : posterize.Steps.max, 0.01f, enabled);
        }

        private void SetContrast(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.contrast, enabled ? 50.0f : 1.0f, 0.01f, enabled);
        }

        private void SetBrightness(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _brightCurve : _defaultCurve, 0.01f, enabled);

			if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, (float)(enabled ? 50.0f : 0.0f), 0.01f, enabled);
        }

        private void SetMirror(bool enabled)
        {
            if (!_profile.TryGet<MirrorComponent>(out var mirror))
            {
                return;
            }

            // Randomize the wipe type
            var max = mirror.wipeIndex.max;
            mirror.wipeIndex.value = enabled ? Random.Range(0, max) : 3;
            mirror.wipeIndex.overrideState = enabled;

            // Make sure the wipe time doesn't exceed the time until the next effect
            // For now we'll default to a max of 0.5 seconds
            var wipeTime = 0.5f;
            if (NextEffect != null)
            {
                wipeTime = Mathf.Min((float) (NextEffect.Time - CurrentEffect.Time), wipeTime);
            }

            mirror.wipeTime.value = enabled ? wipeTime : 0.5f;
            mirror.wipeTime.overrideState = enabled;

            // Set the start time to unity's conception of the current time
            mirror.startTime.value = enabled ? Time.time : 0f;
            mirror.startTime.overrideState = enabled;

            mirror.enabled.value = enabled;
            mirror.enabled.overrideState = enabled;
        }

        private void SetDesaturation(bool enabled, float strength = -50.0f)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, (float)(enabled ? strength : 0.0f), 0.01f, enabled);
        }

        private void SetBlackAndWhite(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, (float)(enabled ? -100.0f : 0.0f), 0.01f, enabled);
            SetAnimation(colorAdjustments.contrast, (float)(enabled ? -10f : 1.0f), 0.01f, enabled);
		}

        private void SetBadCopier(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            SetAnimation(colorCurves.master, enabled ? _copierCurve : _defaultCurve, 0.01f, enabled);
        }

        private void SetSilverTone(bool enabled)
        {
            if(!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.saturation, (float)(enabled ? -100.0f : 0.0f), 0.01f, enabled);
        }

        private void SetSepiaTone(bool enabled)
        {
            if (!_profile.TryGet<ChannelMixer>(out var mixer))
            {
                return;
            }

            SetAnimation(mixer.redOutRedIn, enabled ? 39.3f : 100f, 0.01f, enabled);
            SetAnimation(mixer.greenOutRedIn, enabled ? 34.9f : 0f, 0.01f, enabled);
            SetAnimation(mixer.blueOutRedIn, enabled ? 27.2f : 0f, 0.01f, enabled);

            SetAnimation(mixer.redOutGreenIn, enabled ? 76.9f : 0f, 0.01f, enabled);
            SetAnimation(mixer.greenOutGreenIn, enabled ? 68.6f : 100f, 0.01f, enabled);
            SetAnimation(mixer.blueOutGreenIn, enabled ? 53.4f : 0f, 0.01f, enabled);

            SetAnimation(mixer.blueOutRedIn, enabled ? 18.9f : 0f, 0.01f, enabled);
            SetAnimation(mixer.greenOutRedIn, enabled ? 16.8f : 0f, 0.01f, enabled);
            SetAnimation(mixer.blueOutBlueIn, enabled ? 13.1f : 100f, 0.01f, enabled);
        }


        private void SetBloom(bool enabled)
        {
            if (!_profile.TryGet<Bloom>(out var bloom))
            {
                return;
            }

            if (enabled)
            {
                SetAnimation(bloom.intensity, 1.0f, 0.01f, true);
                SetAnimation(bloom.threshold, 0.6f, 0.01f, true);
            }
            else
            {
                SetAnimation(bloom.intensity, _originalBloom, 0.01f, _originalBloomState);
                SetAnimation(bloom.threshold, _originalBloomThreshold, 0.01f, _originalBloomTState);
            }
        }

        private void SetGreenTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

			SetAnimation(colorAdjustments.saturation, enabled ? -20f : 0f, 0.01f, enabled);
            SetAnimation(colorAdjustments.colorFilter, enabled ? _greenTint : Color.white, 0.01f, enabled);
        }

        private void SetBlueTint(bool enabled)
        {
            if (!_profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                return;
            }

            SetAnimation(colorAdjustments.colorFilter, enabled ? _blueTint : Color.white, 0.01f, enabled);
        }

        private void SetScanline(bool enabled)
        {
            if (!_profile.TryGet<ScanlineComponent>(out var scanline))
            {
                return;
            }

            SetAnimation(scanline.intensity, enabled ? 0.6f : 0.0f, 0.01f, enabled);

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
            SetAnimation(trail.length, enabled ? intensity : 0.0f, 0.01f, enabled);;
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

            SetAnimation(colorAdjustments.contrast, enabled ? 20.0f : 0.0f, 0.01f, enabled);
            SetAnimation(grain.intensity, enabled ? 1.0f : 0.25f, 0.01f, enabled);
            SetAnimation(grain.response, enabled ? 0.0f : 0.8f, 0.01f, enabled);
        }

		private void SetPsychRB(bool enabled)
        {
            if (!_profile.TryGet<ColorCurves>(out var colorCurves))
            {
                return;
            }

            var bounds = new Vector2(0, 1);

            var flatZeroCurve = new TextureCurve(new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 0)), 0.5f,
                false, in bounds);

            var flatZeroCurveParam = new TextureCurveParameter(flatZeroCurve, true);

            SetAnimation(colorCurves.green, enabled ? flatZeroCurve : _defaultCurve, 0.01f, enabled);
            SetAnimation(colorCurves.blue, enabled ? _invertCurve : _defaultCurve, 0.01f, enabled);
        }

        public void SetAnimation(ClampedFloatParameter target, float endValue, float duration, bool finalOverrideState)
        {
            if (CurrentEffect.Type == PreviousEffect.Type)
            {
                // Duration is the time between current and next, unless next doesn't exist, in which case we do nothing
                // because there is no change
                if (NextEffect == null  || CurrentEffect.Type == NextEffect.Type)
                {
                    return;
                }

                // The - 0.02f is to make sure that this animation will complete before the disable call for the next
                // effect
                duration = (float) (NextEffect.Time - CurrentEffect.Time) - 0.02f;
            }

            var anim = new ClampedFloatAnimation(endValue, duration, target, finalOverrideState);

            // Disable is happening before enable again, so if we find something is animating this target, kill it
            if (!finalOverrideState)
            {
                _clampedFloatAnimations.RemoveAll(t => t.Equals(anim));
            }

            _clampedFloatAnimations.Add(anim);

            if (duration > 0.01f)
            {
                YargLogger.LogDebug(
                    $"Animating post processing from {CurrentEffect.Type} to {NextEffect.Type} for {duration} seconds");
            }
        }

        public void SetAnimation(FloatParameter target, float endValue, float duration, bool finalOverrideState)
        {
            if (CurrentEffect.Type == PreviousEffect.Type)
            {
                // Duration is the time between current and next, unless next doesn't exist, in which case we do nothing
                // because there is no change
                if (NextEffect == null || CurrentEffect.Type == NextEffect.Type)
                {
                    return;
                }

                duration = (float) (NextEffect.Time - CurrentEffect.Time) - 0.02f;
            }

            var anim = new FloatAnimation(endValue, duration, target, finalOverrideState);

            if (!finalOverrideState)
            {
                _floatAnimations.RemoveAll(t => t.Equals(anim));
            }

            _floatAnimations.Add(anim);
            if (duration > 0.01f)
            {
                YargLogger.LogDebug(
                    $"Animating post processing from {CurrentEffect.Type} to {NextEffect.Type} for {duration} seconds");
            }
        }

        public void SetAnimation(TextureCurveParameter target, TextureCurve endValue, float duration, bool finalOverrideState)
        {
            if (CurrentEffect.Type == PreviousEffect.Type)
            {
                // Duration is the time between current and next, unless next doesn't exist, in which case we do nothing
                // because there is no change
                if (NextEffect == null || CurrentEffect.Type == NextEffect.Type)
                {
                    return;
                }

                duration = (float) (NextEffect.Time - CurrentEffect.Time) - 0.02f;
            }

            var anim = new CurveAnimation(endValue, duration, target, finalOverrideState);

            if (!finalOverrideState)
            {
                _curveAnimations.RemoveAll(t => t.Equals(anim));
            }

            _curveAnimations.Add(anim);
            YargLogger.LogDebug($"Adding new curve animation at frame {Time.frameCount}");
            if (duration > 0.01f)
            {
                YargLogger.LogDebug(
                    $"Animating post processing from {CurrentEffect.Type} to {NextEffect.Type} for {duration} seconds");
            }
        }

        public void SetAnimation(ColorParameter target, Color endValue, float duration, bool finalOverrideState)
        {
            if (CurrentEffect.Type == PreviousEffect.Type)
            {
                // Duration is the time between current and next, unless next doesn't exist, in which case we do nothing
                // because there is no change
                if (NextEffect == null || CurrentEffect.Type == NextEffect.Type)
                {
                    return;
                }

                duration = (float) (NextEffect.Time - CurrentEffect.Time) - 0.02f;
            }

            var anim = new ColorAnimation(endValue, duration, target, finalOverrideState);

            if (!finalOverrideState)
            {
                _colorAnimations.RemoveAll(t => t.Equals(anim));
            }

            _colorAnimations.Add(anim);
            if (duration > 0.01f)
            {
                YargLogger.LogDebug(
                    $"Animating post processing from {CurrentEffect.Type} to {NextEffect.Type} for {duration} seconds");
            }
        }

        public void SetAnimation(ClampedIntParameter target, int endValue, float duration, bool finalOverrideState)
        {
            if (CurrentEffect.Type == PreviousEffect.Type)
            {
                // Duration is the time between current and next, unless next doesn't exist, in which case we do nothing
                // because there is no change
                if (NextEffect == null || CurrentEffect.Type == NextEffect.Type)
                {
                    return;
                }

                duration = (float) (NextEffect.Time - CurrentEffect.Time) - 0.02f;
            }

            var anim = new ClampedIntAnimation(endValue, duration, target, finalOverrideState);

            if (!finalOverrideState)
            {
                _clampedIntAnimations.RemoveAll(t => t.Equals(anim));
            }

            _clampedIntAnimations.Add(anim);
            if (duration > 0.01f)
            {
                YargLogger.LogDebug(
                    $"Animating post processing from {CurrentEffect.Type} to {NextEffect.Type} for {duration} seconds");
            }
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
                        YargLogger.LogDebug($"Removing Expired Curve Animation at frame {Time.frameCount}");
                        _curveAnimations.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (_clampedIntAnimations.Count > 0)
            {
                for (int i = 0; i < _clampedIntAnimations.Count; i++)
                {
                    var intAnimation = _clampedIntAnimations[i];
                    if (intAnimation.Update())
                    {
                        _clampedIntAnimations.RemoveAt(i);
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

        private void UpdatePostProcessing()
        {
            UpdateAnimations();

            // Check for a change in post processing type, if we have a volume to work with in the first place
            if (_volumeSet)
            {
                while (_currentEventIndex < _postProcessingEvents.Count &&
                    _postProcessingEvents[_currentEventIndex].Time <= GameManager.VisualTime)
                {
                    var effect = _postProcessingEvents[_currentEventIndex];

                    // Yes, we do need all of these for full compatibility
                    PreviousEffect = CurrentEffect;
                    CurrentEffect = effect;

                    if (_currentEventIndex < _postProcessingEvents.Count - 1)
                    {
                        NextEffect = _postProcessingEvents[_currentEventIndex + 1];
                    }
                    else
                    {
                        NextEffect = null;
                    }

                    _currentEventIndex++;
                    SetCameraPostProcessing(effect);
                }
            }
        }

        private void ResetPostProcessing(double time)
        {
            ResetCameraEffect();

            _currentEventIndex = 0;
            while (_currentEventIndex < _postProcessingEvents.Count && _postProcessingEvents[_currentEventIndex].Time < time)
            {
                _currentEventIndex++;
            }

            // Fix up CurrentEffect, PreviousEffect, and NextEffect
            if (_currentEventIndex > 0)
            {
                PreviousEffect = _postProcessingEvents[_currentEventIndex - 1];
            }
            else
            {
                PreviousEffect = null;
            }

            if (_currentEventIndex + 1 < _postProcessingEvents.Count)
            {
                NextEffect = _postProcessingEvents[_currentEventIndex + 1];
            }
            else
            {
                NextEffect = null;
            }

            if (_currentEventIndex < _postProcessingEvents.Count)
            {
                CurrentEffect = _postProcessingEvents[_currentEventIndex];
                SetCameraPostProcessing(CurrentEffect);
            }
        }

        private void SetPostProcessingEnabled(bool value)
        {
            _isPostProcessingEnabled = value;
            if (!value)
            {
                YargLogger.LogInfo("trying to remove pp");
                ResetAllEffects();
            }
            else if (CurrentEffect != null)
            {
                SetCameraPostProcessing(new PostProcessingEvent(CurrentEffect.Type, GameManager.VisualTime, CurrentEffect.Tick));
            }
        }
    }

    // Important note: equality for *Animation depends only on whether their targets reference the same parameter
    public class CurveAnimation : IEquatable<CurveAnimation>
    {
        public TextureCurve   StartCurve;
        public TextureCurve   EndCurve;
        public double         StartTime;
        public double         Duration;
        public Keyframe[]     StartPoints;


        public TextureCurveParameter Target;

        private          TextureCurve _interpolatedCurve;
        private          bool         _started = false;
        private          bool         _finalOverrideState;
        private          TextureCurve _workingCurve;

        private float _elapsedTime;
        private float Length => (float)(Duration - StartTime);

        public bool Equals(CurveAnimation other)
        {
            return other != null && ReferenceEquals(Target, other.Target);
        }

        public CurveAnimation(TextureCurve startCurve, TextureCurve endCurve, double startTime, double endTime, TextureCurveParameter target)
        {
            StartCurve = startCurve;
            EndCurve = endCurve;
            StartTime = startTime;
            Duration = endTime;
            StartPoints = GetInitialKeyframes(StartCurve, EndCurve);
            Target = target;

            _interpolatedCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
            _workingCurve = new TextureCurve(new AnimationCurve(StartPoints), 0.5f, false, new Vector2(0, 1));
        }

        public CurveAnimation(TextureCurve endCurve, float duration,
            TextureCurveParameter target, bool finalOverrideState = false)
        {
            StartCurve = target.value;
            EndCurve = endCurve;
            Duration = duration;
            StartPoints = GetInitialKeyframes(StartCurve, EndCurve);
            Target = target;
            _finalOverrideState = finalOverrideState;

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
            if (_elapsedTime >= Duration)
            {
                // We're done, so set EndCurve
                Target.value = EndCurve;
                Target.overrideState = _finalOverrideState;
                Target.value.SetDirty();
                return true;
            }

            if (!_started)
            {
                _started = true;
                Target.value = _interpolatedCurve;
                Target.overrideState = true;
            }
            else
            {
                _elapsedTime += Time.deltaTime;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = (float) (_elapsedTime / Duration);

            // Lerp all the keyframes in the curve
            // Annoyingly, we have to create an entire new curve every step
            var newCurve = new AnimationCurve();
            for (var i = 0; i < EndCurve.length; i++)
            {
                var point = Mathf.Lerp(_interpolatedCurve[i].value, EndCurve[i].value, (float) normalizedTime);
                newCurve.AddKey(new Keyframe(EndCurve[i].time, point));
            }

            Target.value = new TextureCurve(newCurve, 0.5f, false, new Vector2(0, 1));
            Target.value.SetDirty();

            return false;
        }
    }

    public class ClampedIntAnimation : IEquatable<ClampedIntAnimation>
    {
        private readonly int                 _startValue;
        private readonly int                 _endValue;
        private readonly ClampedIntParameter _param;
        private readonly float               _duration;
        private readonly bool                _finalOverrideState;
        private          float               _elapsedTime;
        private          bool                _started;

        public bool Equals(ClampedIntAnimation other)
        {
            return other != null && ReferenceEquals(_param, other._param);
        }

        public ClampedIntAnimation(int endValue, float duration, ClampedIntParameter target, bool finalOverrideState = false)
        {
            _startValue = target.value;
            _endValue = endValue;
            _duration = duration;
            _finalOverrideState = finalOverrideState;
            _elapsedTime = 0;
            _param = target;
        }

        public bool Update()
        {
            if (_elapsedTime >= _duration)
            {
                _param.value = _endValue;
                _param.overrideState = _finalOverrideState;
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue;
                _param.overrideState = true;
            }
            else
            {
                _elapsedTime += Time.deltaTime;
            }

            _param.Interp(_startValue, _endValue, (float) (_elapsedTime / _duration));
            return false;
        }
    }

    public class ClampedFloatAnimation : IEquatable<ClampedFloatAnimation>
    {
        private readonly float                 _startValue;
        private readonly float                 _endValue;
        private readonly double                _startTime;
        private readonly double                _duration;
        private readonly GameManager           _gameManager;
        private readonly ClampedFloatParameter _param;
        private          float                 _elapsedTime;

        private bool _started = false;
        private bool _finalOverrideState;

        private float Length => (float) _duration - (float) _startTime;

        public bool Equals(ClampedFloatAnimation other)
        {
            return other != null && ReferenceEquals(_param, other._param);
        }

        public ClampedFloatAnimation(float startValue, float endValue, double startTime, double endTime,
            ClampedFloatParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _duration = endTime;
            _gameManager = gameManager;
            _param = param;
            _elapsedTime = 0;
        }

        public ClampedFloatAnimation(float endValue, float duration, ClampedFloatParameter param, bool finalOverrideState = false)
        {
            _startValue = param.value;
            _endValue = endValue;
            _duration = duration;
            _param = param;
            _elapsedTime = 0;
            _finalOverrideState = finalOverrideState;
        }

        // This returns a bool so that the caller can remove us from the list when we are no longer needed
        public bool Update()
        {
            // if (_gameManager.RealVisualTime < _startTime)
            // {
            //     // Nothing to do yet
            //     return false;
            // }

            if (_elapsedTime >= _duration)
            {
                // We're done, so set _endValue
                _param.value = _endValue;
                _param.overrideState = _finalOverrideState;
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue;
                _param.overrideState = true;
            }
            else
            {
                _elapsedTime += Time.deltaTime;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = (float) (_elapsedTime / _duration);
            _param.Interp(_startValue, _endValue, normalizedTime);
            // _param.value = Mathf.Lerp(_startValue, _endValue, (float) normalizedTime);

            return false;
        }
    }

    public class FloatAnimation : IEquatable<FloatAnimation>
    {
        private readonly float                 _startValue;
        private readonly float                 _endValue;
        private readonly double                _startTime;
        private readonly double                _duration;
        private readonly GameManager           _gameManager;
        private readonly FloatParameter _param;
        private          float                 _elapsedTime;

        private bool _started = false;
        private bool _finalOverrideState;

        public bool Equals(FloatAnimation other)
        {
            return other != null && ReferenceEquals(_param, other._param);
        }

        private float Length => (float) _duration - (float) _startTime;

        public FloatAnimation(float startValue, float endValue, double startTime, double endTime, FloatParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _duration = endTime;
            _gameManager = gameManager;
            _param = param;
        }

        public FloatAnimation(float endValue, float duration, FloatParameter param, bool finalOverrideState = false)
        {
            _startValue = param.value;
            _endValue = endValue;
            _duration = duration;
            _param = param;
            _finalOverrideState = finalOverrideState;
        }

        // This returns a bool so that the caller can remove us from the list when we are no longer needed
        public bool Update()
        {
            // if (_gameManager.RealVisualTime < _startTime)
            // {
            //     // Nothing to do yet
            //     return false;
            // }

            if (_elapsedTime >= _duration)
            {
                // We're done, so set _endValue
                _param.value = _endValue;
                _param.overrideState = _finalOverrideState;
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue;
                _param.overrideState = true;
            }
            else
            {
                _elapsedTime += Time.deltaTime;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = (float) (_elapsedTime / _duration);
            _param.Interp(_startValue, _endValue, normalizedTime);
            // _param.value = Mathf.Lerp(_startValue, _endValue, (float) normalizedTime);
            return false;
        }
    }

    public class ColorAnimation : IEquatable<ColorAnimation>
    {
        private readonly ColorParameter        _startValue;
        private readonly Color                 _endValue;
        private readonly double                _startTime;
        private readonly double                _duration;
        private readonly GameManager           _gameManager;
        private readonly ColorParameter        _param;
        private float _elapsedTime;

        private bool _started = false;
        private bool _finalOverrideState;

        private float Length => (float) _duration - (float) _startTime;

        public bool Equals(ColorAnimation other)
        {
            return other != null && ReferenceEquals(_param, other._param);
        }

        public ColorAnimation(ColorParameter startValue, Color endValue, double startTime, double endTime, ColorParameter param, GameManager gameManager)
        {
            _startValue = startValue;
            _endValue = endValue;
            _startTime = startTime;
            _duration = endTime;
            _gameManager = gameManager;
            _param = param;
        }

        public ColorAnimation(Color endValue, float duration, ColorParameter param, bool finalOverrideState = false)
        {
            _startValue = param;
            _endValue = endValue;
            _duration = duration;
            _param = param;
            _finalOverrideState = finalOverrideState;
        }

        public bool Update()
        {
            // if (_gameManager.RealVisualTime < _startTime)
            // {
            //     // Nothing to do yet
            //     return false;
            // }

            if (_elapsedTime >= _duration)
            {
                // We're done, so set _endValue
                _param.value = _endValue;
                _param.overrideState = _finalOverrideState;
                return true;
            }

            if (!_started)
            {
                _started = true;
                _param.value = _startValue.value;
                _param.overrideState = true;
            }
            else
            {
                _elapsedTime += Time.deltaTime;
            }

            // We are between start and end, so we need to normalize the time span so we can lerp
            // Starts at (almost) 0 and increases to 1 at EndTime
            var normalizedTime = (float) (_elapsedTime / _duration);
            _param.Interp(_startValue.value, _endValue, normalizedTime);
            return false;
        }
    }
}
