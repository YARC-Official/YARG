using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YARG.Core;
using YARG.Core.Chart;
using UnityEngine;

//using MoonVenueEvent = MoonscraperChartEditor.Song.VenueEvent;

namespace YARG.Venue
{
    /// <summary>
    /// A venue auto-generation preset; to be used incase a given chart does not include manually charted lighting/camera cues.
    /// </summary>
    public class VenueAutogenerationPreset
    {
        private CameraPacingPreset CameraPacing;
        private AutogenerationSectionPreset DefaultSectionPreset;
        private List<AutogenerationSectionPreset> SectionPresets;
        
        public VenueAutogenerationPreset(string path)
        {
            CameraPacing = CameraPacingPreset.Medium;
            DefaultSectionPreset = new AutogenerationSectionPreset();
            DefaultSectionPreset.AllowedLightPresets.Add(LightingType.Default);
            DefaultSectionPreset.AllowedPostProcs.Add(PostProcessingType.Default);
            SectionPresets = new List<AutogenerationSectionPreset>();
            if (File.Exists(path))
            {
                ReadPresetFromFile(path);
            }
            else
            {
                Debug.LogWarning("Auto-generation preset file not found: " + path);
            }
        }

        private void ReadPresetFromFile(string path)
        {
            try
            {
                var o = JObject.Parse(File.ReadAllText(path));
                var cameraPacing = (string)o.SelectToken("camera_pacing");
                CameraPacing = StringToCameraPacing(cameraPacing);
                var defaultSectionRead = false;

                foreach (var sectionPreset in (JObject)o.SelectToken("section_presets"))
                {
                    var value = JObjectToSectionPreset((JObject)sectionPreset.Value);
                    value.SectionName = sectionPreset.Key;
                    if (sectionPreset.Key.ToLower().Trim() == "default")
                    {
                        DefaultSectionPreset = value;
                        if (defaultSectionRead)
                        {
                            Debug.LogWarning("Multiple default sections found in preset: " + path);
                        }
                        defaultSectionRead = true;
                    }
                    else
                    {
                        SectionPresets.Add(value);
                    }
                }
                if (!defaultSectionRead)
                {
                    Debug.LogWarning("Missing default section in preset: " + path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while loading auto-gen preset {path}");
                Debug.LogException(ex);
            }
        }

        public SongChart GenerateLightingEvents(SongChart chart)
        {
            var lastTick = chart.GetLastTick();
            var resolution = chart.Resolution;
            var latestLighting = LightingType.Intro;
            var latestPostProc = PostProcessingType.Default;
            var latestBonusFxState = false;
            // Add initial state
            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, 0, 0));
            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, 0, 0));
            foreach (var section in chart.Sections)
            {
                // Find which section preset to use...
                var sectionPreset = DefaultSectionPreset;
                var matched = false;
                foreach (var preset in SectionPresets)
                {
                    var nameToMatch = section.Name.ToLower().Trim().Replace("-","").Replace(" ","_");
                    foreach (string practiceSecion in preset.PracticeSections)
                    {
                        var regexString = "^" + Regex.Escape(practiceSecion).Replace("\\*", ".*") + "$"; 
                        if (Regex.IsMatch(nameToMatch, regexString))
                        {
                            sectionPreset = preset;
                            matched = true;
                            Debug.Log("Section " + section.Name + " matched practice section " + practiceSecion);
                            break;
                        }
                    }
                    if (matched)
                    {
                        break;
                    }
                }
                if (!matched)
                {
                    Debug.Log("No match found for section " + section.Name + "; using default autogen section");
                }
                // BonusFx at start
                if (sectionPreset.BonusFxAtStart && !latestBonusFxState) // Avoid multiple BonusFx in a row
                {
                    chart.VenueTrack.Stage.Add(new StageEffectEvent(StageEffect.BonusFx, VenueEventFlags.None, section.Time, section.Tick));
                }
                latestBonusFxState = sectionPreset.BonusFxAtStart;
                // Actually generate lighting
                var currentLighting = latestLighting;
                foreach (var lighting in sectionPreset.AllowedLightPresets)
                {
                    if (lighting != latestLighting)
                    {
                        currentLighting = lighting;
                        break;
                    }
                }
                if (currentLighting != latestLighting) // Only generate new events if lighting's changed
                {
                    if (sectionPreset.LightPresetBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.LightPresetBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, chart.SyncTrack.TickToTime(blendTick), blendTick));
                        }
                    }
                    chart.VenueTrack.Lighting.Add(new LightingEvent(currentLighting, section.Time, section.Tick));
                    latestLighting = currentLighting;
                }
                else if (LightingIsManual(currentLighting))
                {
                    chart.VenueTrack.Lighting.Add(new LightingEvent(LightingType.Keyframe_Next, section.Time, section.Tick));
                }
                // Generate next keyframes
                if (LightingIsManual(currentLighting))
                {
                    uint nextTick = section.Tick + (resolution * sectionPreset.KeyframeRate);
                    while (nextTick < lastTick && nextTick < section.TickEnd)
                    {
                        chart.VenueTrack.Lighting.Add(new LightingEvent(LightingType.Keyframe_Next, chart.SyncTrack.TickToTime(nextTick), nextTick));
                        nextTick += (resolution * sectionPreset.KeyframeRate);
                    }
                }
                // Generate post-procs
                var currentPostProc = latestPostProc;
                foreach (var postProc in sectionPreset.AllowedPostProcs)
                {
                    if (postProc != latestPostProc)
                    {
                        currentPostProc = postProc;
                        break;
                    }
                }
                if (currentPostProc != latestPostProc) // Only generate new events if post-proc's changed
                {
                    if (sectionPreset.PostProcBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.PostProcBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, chart.SyncTrack.TickToTime(blendTick), blendTick));
                        }
                    }
                    chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(currentPostProc, section.Time, section.Tick));
                    latestPostProc = currentPostProc;
                }
            }
            // Reorder lighting track (Next keyframes and blend-in events might be unordered)
            chart.VenueTrack.Lighting.Sort((x,y) => x.Tick.CompareTo(y.Tick));
            // Generate performer spotlight events (basically copying solo data for guitar/bass/keys/drums info their respective performer spotlight info)
            AddSoloAsSpotlight(ref chart, chart.FiveFretGuitar, Performer.Guitar);
            AddSoloAsSpotlight(ref chart, chart.FiveFretBass, Performer.Bass);
            AddSoloAsSpotlight(ref chart, chart.Keys, Performer.Keyboard);
            AddSoloAsSpotlightDrums(ref chart);
            // TODO: Add singalong events based on HARM2 phrases if any (add to the first two that are available in this order: guitar/bass/keys/drums)
            // Reorder performer track (spotlight and singalong events will be out of order)
            chart.VenueTrack.Performer.Sort((x,y) => x.Tick.CompareTo(y.Tick));
            return chart;
        }

        public void GenerateCameraCutEvents(SongChart chart)
        {
            // TODO: camera cut generator function
        }

        private void AddSoloAsSpotlight(ref SongChart chart, InstrumentTrack<GuitarNote> track, Performer performer)
        {
            if (track.Difficulties[Difficulty.Expert].IsOccupied())
            {
                AddSoloAsSpotlight(ref chart, track.Difficulties[Difficulty.Expert].Phrases, performer);
            }
        }

        private void AddSoloAsSpotlightDrums(ref SongChart chart)
        {
            if (chart.ProDrums.Difficulties[Difficulty.Expert].IsOccupied())
            {
                AddSoloAsSpotlight(ref chart, chart.ProDrums.Difficulties[Difficulty.Expert].Phrases, Performer.Drums);
            }
            else if (chart.FourLaneDrums.Difficulties[Difficulty.Expert].IsOccupied())
            {
                AddSoloAsSpotlight(ref chart, chart.FourLaneDrums.Difficulties[Difficulty.Expert].Phrases, Performer.Drums);
            }
            else if (chart.FiveLaneDrums.Difficulties[Difficulty.Expert].IsOccupied())
            {
                AddSoloAsSpotlight(ref chart, chart.FiveLaneDrums.Difficulties[Difficulty.Expert].Phrases, Performer.Drums);
            }
        }

        private void AddSoloAsSpotlight(ref SongChart chart, List<Phrase> phrases, Performer performer)
        {
            foreach (var phrase in phrases)
            {
                if (phrase.Type == PhraseType.Solo)
                {
                    chart.VenueTrack.Performer.Add(new PerformerEvent(PerformerEventType.Spotlight, performer, phrase.Time, phrase.TimeLength, phrase.Tick, phrase.TickLength));
                }
            }
        }

        private bool LightingIsManual(LightingType lighting)
        {
            return lighting is
                LightingType.Default or
                LightingType.Dischord or
                LightingType.Chorus or
                LightingType.Cool_Manual or
                LightingType.Stomp or
                LightingType.Verse or
                LightingType.Warm_Manual;
        }

        private AutogenerationSectionPreset JObjectToSectionPreset(JObject o)
        {
            AutogenerationSectionPreset sectionPreset = new AutogenerationSectionPreset();
            foreach (var parameter in o)
            {
                switch (parameter.Key.ToLower().Trim())
                {
                    case "practice_sections":
                        var practiceSections = new List<string>();
                        foreach (string section in (JArray)parameter.Value)
                        {
                            practiceSections.Add(section);
                        }
                        sectionPreset.PracticeSections = practiceSections;
                        break;
                    case "allowed_lightpresets":
                        var allowedLightPresets = new List<LightingType>();
                        foreach (string key in (JArray)parameter.Value)
                        {
                            var keyTrim = key.Trim();
                            if (VenueLookup.VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData))
                            {
                                allowedLightPresets.Add(VenueLookup.LightingLookup[eventData]);
                            }
                            else
                            {
                                Debug.LogWarning("Invalid light preset: " + key);
                            }
                        }
                        sectionPreset.AllowedLightPresets = allowedLightPresets;
                        break;
                    case "allowed_postprocs":
                        var allowedPostProcs = new List<PostProcessingType>();
                        foreach (string key in (JArray)parameter.Value)
                        {
                            var keyTrim = key.Trim();
                            if (VenueLookup.VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData) && eventData.type == VenueLookup.Type.PostProcessing)
                            {
                                allowedPostProcs.Add(VenueLookup.PostProcessLookup[eventData.text]);
                            }
                            else
                            {
                                Debug.LogWarning("Invalid post-proc: " + key);
                            }
                        }
                        sectionPreset.AllowedPostProcs = allowedPostProcs;
                        break;
                    case "keyframe_rate":
                        sectionPreset.KeyframeRate = (uint)parameter.Value;
                        break;
                    case "lightpreset_blendin":
                        sectionPreset.LightPresetBlendIn = (uint)parameter.Value;
                        break;
                    case "postproc_blendin":
                        sectionPreset.PostProcBlendIn = (uint)parameter.Value;
                        break;
                    case "dircut_at_start":
                        // TODO: add when we have characters / directed camera cuts
                        break;
                    case "bonusfx_at_start":
                        sectionPreset.BonusFxAtStart = (bool)parameter.Value;
                        break;
                    case "camera_pacing":
                        sectionPreset.CameraPacingOverride = StringToCameraPacing((string)parameter.Value);
                        break;
                    default:
                        Debug.LogWarning("Unknown section preset parameter: " + parameter.Key);
                        break;
                }
            }
            return sectionPreset;
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
                    Debug.LogWarning("Invalid camera pacing in auto-gen preset: " + cameraPacing);
                    return CameraPacingPreset.Medium;
            }
        }

        private class AutogenerationSectionPreset
        {
            public string SectionName; // probably useless
            public List<string> PracticeSections; // i.e. "*verse*" which applies to "Verse 1", "Verse 2", etc.
            public List<LightingType> AllowedLightPresets;
            public List<PostProcessingType> AllowedPostProcs;
            public uint KeyframeRate;
            public uint LightPresetBlendIn;
            public uint PostProcBlendIn;
            // public DirectedCameraCutType DirectedCutAtStart; // TODO: add when we have characters / directed camera cuts
            public bool BonusFxAtStart;
            public CameraPacingPreset? CameraPacingOverride;

            public AutogenerationSectionPreset()
            {
                // Default values
                SectionName = "";
                PracticeSections = new List<string>();
                AllowedLightPresets = new List<LightingType>();
                AllowedPostProcs = new List<PostProcessingType>();
                KeyframeRate = 2;
                LightPresetBlendIn = 0;
                PostProcBlendIn = 0;
                BonusFxAtStart = false;
                CameraPacingOverride = null;
            }
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
    }
}