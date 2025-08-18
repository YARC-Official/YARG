using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Helpers;

namespace YARG.Settings
{
    /// <summary>
    /// A venue auto-generation preset; to be used in case a given chart
    /// does not include manually charted lighting/camera cues.
    /// </summary>
    public class VenueAutoGenerationPreset
    {
        public static string DefaultPath =>
            Path.Combine(PathHelper.StreamingAssetsPath, "DefaultVenuePreset.json");

        private class VenueAutoGenerationFile
        {
            public string CameraPacing;
            public AutoGenerationSectionFile DefaultSectionPreset;
            public List<AutoGenerationSectionFile> SectionPresets;
        }

        private class AutoGenerationSectionFile
        {
            public List<string> SectionRegexes = new();
            public List<string> AllowedLightPresets = new();
            public List<string> AllowedPostProcs = new();

            public uint KeyframeRate = 2;
            public uint LightPresetBlendIn;
            public uint PostProcBlendIn;

            // TODO: add when we have characters / directed camera cuts
            // public DirectedCameraCutType DirectedCutAtStart;
            public bool BonusFxAtStart;

            public string CameraPacingOverride;
        }

        private class AutoGenerationSectionPreset
        {
            public List<string> SectionRegexes = new();
            public List<LightingType> AllowedLightPresets = new();
            public List<PostProcessingType> AllowedPostProcs = new();

            public uint KeyframeRate = 2;
            public uint LightPresetBlendIn;
            public uint PostProcBlendIn;

            // TODO: add when we have characters / directed camera cuts
            // public DirectedCameraCutType DirectedCutAtStart;
            public bool BonusFxAtStart;

            public CameraPacingPreset? CameraPacingOverride;
        }

        /// <summary>
        /// Possible camera pacing values.
        /// </summary>
        private enum CameraPacingPreset
        {
            Minimal,
            Slow,
            Medium,
            Fast,
            Crazy
        }

        private CameraPacingPreset _cameraPacing;
        private AutoGenerationSectionPreset _defaultSectionPreset;
        private List<AutoGenerationSectionPreset> _sectionPresets;

        public VenueAutoGenerationPreset(string path)
        {
            _cameraPacing = CameraPacingPreset.Medium;
            _defaultSectionPreset = new AutoGenerationSectionPreset();
            _defaultSectionPreset.AllowedLightPresets.Add(LightingType.Default);
            _defaultSectionPreset.AllowedPostProcs.Add(PostProcessingType.Default);
            _sectionPresets = new List<AutoGenerationSectionPreset>();

            if (File.Exists(path))
            {
                ReadPresetFromFile(path);
            }
            else
            {
                YargLogger.LogFormatWarning("Auto-generation preset file not found: {0}", path);
            }
        }

        private void ReadPresetFromFile(string path)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<VenueAutoGenerationFile>(File.ReadAllText(path));
                _cameraPacing = StringToCameraPacing(json.CameraPacing);

                if (json.DefaultSectionPreset is not null)
                {
                    _defaultSectionPreset = PresetFileToPresetSection(json.DefaultSectionPreset);
                }
                else
                {
                    YargLogger.LogFormatWarning("Default auto-generation preset not found in file: {0}", path);
                }

                foreach (var preset in json.SectionPresets)
                {
                    _sectionPresets.Add(PresetFileToPresetSection(preset));
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading auto-gen preset {path}");
            }
        }

        private AutoGenerationSectionPreset PresetFileToPresetSection(AutoGenerationSectionFile file)
        {
            // Fill in standard values
            var preset = new AutoGenerationSectionPreset
            {
                SectionRegexes = file.SectionRegexes,
                KeyframeRate = file.KeyframeRate,
                LightPresetBlendIn = file.LightPresetBlendIn,
                PostProcBlendIn = file.PostProcBlendIn,
                // TODO: add when we have characters / directed camera cuts
                // preset.DirectedCutAtStart = file.DirectedCutAtStart,
                BonusFxAtStart = file.BonusFxAtStart,
            };

            if (file.CameraPacingOverride is not null)
            {
                preset.CameraPacingOverride = StringToCameraPacing(file.CameraPacingOverride);
            }

            // Fill in light presets, converting their string values to their ingame values
            var allowedLightPresets = new List<LightingType>();
            foreach (string str in file.AllowedLightPresets)
            {
                if (VenueLookup.VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(str.Trim(), out var eventData))
                {
                    allowedLightPresets.Add(VenueLookup.LightingLookup[eventData]);
                }
                else
                {
                    YargLogger.LogFormatWarning("Invalid light preset: {0}", str);
                }
            }
            preset.AllowedLightPresets = allowedLightPresets;

            // Fill in post-procs, converting their string values to their in-game values
            var allowedPostProcs = new List<PostProcessingType>();
            foreach (string str in file.AllowedPostProcs)
            {
                if (VenueLookup.VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(str.Trim(), out var eventData) &&
                    eventData.type == VenueLookup.Type.PostProcessing)
                {
                    allowedPostProcs.Add(VenueLookup.PostProcessLookup[eventData.text]);
                }
                else
                {
                    YargLogger.LogFormatWarning("Invalid post-proc: {0}", str);
                }
            }
            preset.AllowedPostProcs = allowedPostProcs;

            return preset;
        }

        public SongChart GenerateLightingEvents(SongChart chart)
        {
            var lastTick = chart.GetLastTick();
            var resolution = chart.Resolution;
            var latestLighting = LightingType.Intro;
            var latestPostProc = PostProcessingType.Default;
            var latestBonusFxState = false;

            // Add initial state
            chart.VenueTrack.AutoGenerated = true;
            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, 0, 0));
            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, 0, 0));

            if (chart.Sections.Count == 0)
            {
                YargLogger.LogWarning("No sections found in chart, using default lighting");
                chart.VenueTrack.Lighting.Add(new LightingEvent(_defaultSectionPreset.AllowedLightPresets[0], 0, 0));
            }

            foreach (var section in chart.Sections)
            {
                // Find which section preset to use...
                var sectionPreset = _defaultSectionPreset;
                foreach (var preset in _sectionPresets)
                {
                    // Rename section names such as "Pre-Chorus 2" to "prechorus_2" for proper matching
                    var nameToMatch = section.Name
                        .ToLowerInvariant()
                        .Trim()
                        .Replace("-","")
                        .Replace(" ","_");

                    // See if any of the regexes match
                    bool matched = false;
                    foreach (string regex in preset.SectionRegexes)
                    {
                        if (!Regex.IsMatch(nameToMatch, $"^{regex}$")) continue;

                        sectionPreset = preset;
                        matched = true;

                        break;
                    }

                    if (matched) break;
                }

                // BonusFx at start, avoid multiple BonusFx in a row
                if (sectionPreset.BonusFxAtStart && !latestBonusFxState)
                {
                    chart.VenueTrack.Stage.Add(new StageEffectEvent(
                        StageEffect.BonusFx,
                        VenueEventFlags.None,
                        section.Time,
                        section.Tick));
                }
                latestBonusFxState = sectionPreset.BonusFxAtStart;

                // Actually generate lighting events
                var currentLighting = latestLighting;
                foreach (var lighting in sectionPreset.AllowedLightPresets)
                {
                    if (lighting != latestLighting)
                    {
                        currentLighting = lighting;
                        break;
                    }
                }

                // Only generate new events if lighting's changed
                if (currentLighting != latestLighting)
                {
                    if (sectionPreset.LightPresetBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.LightPresetBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.Lighting.Add(new LightingEvent(
                                latestLighting,
                                chart.SyncTrack.TickToTime(blendTick),
                                blendTick));
                        }
                    }

                    chart.VenueTrack.Lighting.Add(new LightingEvent(currentLighting, section.Time, section.Tick));
                    latestLighting = currentLighting;
                }
                else if (LightingIsManual(currentLighting))
                {
                    // Add next keyframe if staying on the same (manual) lighting
                    chart.VenueTrack.Lighting.Add(new LightingEvent(
                        LightingType.KeyframeNext,
                        section.Time,
                        section.Tick));
                }

                // Generate next keyframes
                if (LightingIsManual(currentLighting))
                {
                    uint nextTick = section.Tick + (resolution * sectionPreset.KeyframeRate);
                    while (nextTick < lastTick && nextTick < section.TickEnd)
                    {
                        chart.VenueTrack.Lighting.Add(new LightingEvent(
                            LightingType.KeyframeNext,
                            chart.SyncTrack.TickToTime(nextTick),
                            nextTick));
                        nextTick += resolution * sectionPreset.KeyframeRate;
                    }
                }

                // Generate post-proc events
                var currentPostProc = latestPostProc;
                foreach (var postProc in sectionPreset.AllowedPostProcs)
                {
                    if (postProc == latestPostProc) continue;

                    currentPostProc = postProc;
                    break;
                }

                // Only generate new events if post-proc's changed
                if (currentPostProc != latestPostProc)
                {
                    if (sectionPreset.PostProcBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.PostProcBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(
                                latestPostProc,
                                chart.SyncTrack.TickToTime(blendTick),
                                blendTick));
                        }
                    }

                    chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(
                        currentPostProc,
                        section.Time,
                        section.Tick));
                    latestPostProc = currentPostProc;
                }
            }

            // Reorder lighting track (Next keyframes and blend-in events might be unordered)
            chart.VenueTrack.Lighting.Sort((x, y) => x.Tick.CompareTo(y.Tick));
            // Reorder stage track (BonusFX and previously generated fog events might be unordered)
            chart.VenueTrack.Stage.Sort((x, y) => x.Tick.CompareTo(y.Tick));

            // Generate performer spotlight events (basically copying solo data for guitar/bass/keys/drums info
            // their respective performer spotlight info).
            AddSoloAsSpotlight(ref chart, chart.FiveFretGuitar, Performer.Guitar);
            AddSoloAsSpotlight(ref chart, chart.FiveFretBass, Performer.Bass);
            AddSoloAsSpotlight(ref chart, chart.Keys, Performer.Keyboard);
            AddSoloAsSpotlightDrums(ref chart);

            // TODO: Add singalong events based on HARM2 phrases if any (add to the first two that are available in this order: guitar/bass/keys/drums)

            // Reorder performer track (spotlight and singalong events will be out of order)
            chart.VenueTrack.Performer.Sort((x,y) => x.Tick.CompareTo(y.Tick));
            return chart;
        }

        private void AddSoloAsSpotlight(ref SongChart chart, InstrumentTrack<GuitarNote> track, Performer performer)
        {
            if (track.TryGetDifficulty(Difficulty.Expert, out var diffTrack))
            {
                AddSoloAsSpotlight(ref chart, diffTrack.Phrases, performer);
            }
        }

        private void AddSoloAsSpotlightDrums(ref SongChart chart)
        {
            if (chart.ProDrums.TryGetDifficulty(Difficulty.Expert, out var diffTrack))
            {
                AddSoloAsSpotlight(ref chart, diffTrack.Phrases, Performer.Drums);
            }
            else if (chart.FourLaneDrums.TryGetDifficulty(Difficulty.Expert, out diffTrack))
            {
                AddSoloAsSpotlight(ref chart, diffTrack.Phrases, Performer.Drums);
            }
            else if (chart.FiveLaneDrums.TryGetDifficulty(Difficulty.Expert, out diffTrack))
            {
                AddSoloAsSpotlight(ref chart, diffTrack.Phrases, Performer.Drums);
            }
        }

        private void AddSoloAsSpotlight(ref SongChart chart, List<Phrase> phrases, Performer performer)
        {
            foreach (var phrase in phrases)
            {
                if (phrase.Type == PhraseType.Solo)
                {
                    chart.VenueTrack.Performer.Add(new PerformerEvent(
                        PerformerEventType.Spotlight,
                        performer,
                        phrase.Time,
                        phrase.TimeLength,
                        phrase.Tick,
                        phrase.TickLength));
                }
            }
        }

        private bool LightingIsManual(LightingType lighting)
        {
            return lighting is
                LightingType.Default or
                LightingType.Dischord or
                LightingType.Chorus or
                LightingType.CoolManual or
                LightingType.Stomp or
                LightingType.Verse or
                LightingType.WarmManual;
        }

        private CameraPacingPreset StringToCameraPacing(string cameraPacing)
        {
            switch (cameraPacing.ToLower().Trim())
            {
                case "minimal":
                    return CameraPacingPreset.Minimal;
                case "slow":
                    return CameraPacingPreset.Slow;
                case "medium":
                    return CameraPacingPreset.Medium;
                case "fast":
                    return CameraPacingPreset.Fast;
                case "crazy":
                    return CameraPacingPreset.Crazy;
                default:
                    YargLogger.LogFormatWarning("Invalid camera pacing in auto-gen preset: {0}", cameraPacing);
                    return CameraPacingPreset.Medium;
            }
        }

        public bool ChartHasFog(SongChart chart)
        {
            foreach (var stageEvent in chart.VenueTrack.Stage)
            {
                if (stageEvent.Effect == StageEffect.FogOn || stageEvent.Effect == StageEffect.FogOff)
                    return true;
            }
            return false;
        }

        public SongChart GenerateFogEvents(SongChart chart)
        {
            var lastTick = chart.GetLastTick();
            var resolution = chart.Resolution;
            const uint startInterval = 8 * 4;
            const uint fogOnInterval = 32 * 4;
            const uint fogOffInterval = 8 * 4;

            uint nextTick = (uint) (resolution * startInterval);
            while (nextTick < lastTick)
            {
                chart.VenueTrack.Stage.Add(new StageEffectEvent(
                    StageEffect.FogOn,
                    VenueEventFlags.None,
                    chart.SyncTrack.TickToTime(nextTick),
                    nextTick));
                uint fogOffTick = nextTick + (uint) (resolution * fogOffInterval);
                if (fogOffTick < lastTick)
                {
                    chart.VenueTrack.Stage.Add(new StageEffectEvent(
                        StageEffect.FogOff,
                        VenueEventFlags.None,
                        chart.SyncTrack.TickToTime(fogOffTick),
                        fogOffTick));
                }
                else
                {
                    break;
                }
                nextTick += (uint) (resolution * fogOnInterval);
            }

            return chart;
        }
    }
}