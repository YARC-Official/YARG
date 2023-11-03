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
        public CameraPacing CameraPacing;
        public AutogenerationSectionPreset DefaultSectionPreset;
        public List<AutogenerationSectionPreset> SectionPresets;
        
        public VenueAutogenerationPreset(string path)
        {
            CameraPacing = CameraPacing.Medium;
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
                JObject o = JObject.Parse(File.ReadAllText(path));
                string cameraPacing = (string)o.SelectToken("camera_pacing");
                CameraPacing = StringToCameraPacing(cameraPacing);
                bool defaultSectionRead = false;

                foreach (var sectionPreset in (JObject)o.SelectToken("section_presets"))
                {
                    AutogenerationSectionPreset value = JObjectToSectionPreset((JObject)sectionPreset.Value);
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
            uint lastTick = chart.GetLastTick();
            uint resolution = chart.Resolution;
            LightingType latestLighting = LightingType.Intro;
            PostProcessingType latestPostProc = PostProcessingType.Default;
            bool latestBonusFxState = false;
            // Add initial state
            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, 0, 0));
            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, 0, 0));
            foreach (Section section in chart.Sections)
            {
                // Find which section preset to use...
                AutogenerationSectionPreset sectionPreset = DefaultSectionPreset;
                bool matched = false;
                foreach (AutogenerationSectionPreset preset in SectionPresets)
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
                LightingType currentLighting = latestLighting;
                foreach (LightingType lighting in sectionPreset.AllowedLightPresets)
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
                PostProcessingType currentPostProc = latestPostProc;
                foreach (PostProcessingType postProc in sectionPreset.AllowedPostProcs)
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
            return chart;
        }

        public void GenerateCameraCutEvents(SongChart chart)
        {
            // TODO: camera cut generator function
        }

        private void AddSoloAsSpotlight(ref SongChart chart, InstrumentTrack<GuitarNote> track, Performer performer)
        {
            if (track.Difficulties.ContainsKey(Difficulty.Expert))
            {
                AddSoloAsSpotlight(ref chart, track.Difficulties[Difficulty.Expert].Phrases, performer);
            }
        }

        private void AddSoloAsSpotlightDrums(ref SongChart chart)
        {
            if (chart.ProDrums.Difficulties.ContainsKey(Difficulty.Expert))
            {
                AddSoloAsSpotlight(ref chart, chart.ProDrums.Difficulties[Difficulty.Expert].Phrases, Performer.Drums);
            }
            else if (chart.FourLaneDrums.Difficulties.ContainsKey(Difficulty.Expert))
            {
                AddSoloAsSpotlight(ref chart, chart.FourLaneDrums.Difficulties[Difficulty.Expert].Phrases, Performer.Drums);
            }
            else if (chart.FiveLaneDrums.Difficulties.ContainsKey(Difficulty.Expert))
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
            return lighting == LightingType.Default ||
                   lighting == LightingType.Dischord ||
                   lighting == LightingType.Chorus ||
                   lighting == LightingType.Cool_Manual ||
                   lighting == LightingType.Stomp ||
                   lighting == LightingType.Verse ||
                   lighting == LightingType.Warm_Manual;
        }

        private AutogenerationSectionPreset JObjectToSectionPreset(JObject o)
        {
            AutogenerationSectionPreset sectionPreset = new AutogenerationSectionPreset();
            foreach (var parameter in o)
            {
                switch (parameter.Key.ToLower().Trim())
                {
                    case "practice_sections":
                        List<string> practiceSections = new List<string>();
                        foreach (string section in (JArray)parameter.Value)
                        {
                            practiceSections.Add(section);
                        }
                        sectionPreset.PracticeSections = practiceSections;
                        break;
                    case "allowed_lightpresets":
                        List<LightingType> allowedLightPresets = new List<LightingType>();
                        foreach (string key in (JArray)parameter.Value)
                        {
                            var keyTrim = key.Trim();
                            if (VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData))
                            {
                                allowedLightPresets.Add(LightingLookup[eventData]);
                            }
                            else
                            {
                                Debug.LogWarning("Invalid light preset: " + key);
                            }
                        }
                        sectionPreset.AllowedLightPresets = allowedLightPresets;
                        break;
                    case "allowed_postprocs":
                        List<PostProcessingType> allowedPostProcs = new List<PostProcessingType>();
                        foreach (string key in (JArray)parameter.Value)
                        {
                            var keyTrim = key.Trim();
                            if (VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData) && eventData.type == 1)
                            {
                                allowedPostProcs.Add(PostProcessLookup[eventData.text]);
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
        
        private CameraPacing StringToCameraPacing(string cameraPacing)
        {
            switch (cameraPacing.ToLower().Trim())
            {
                case "minimal": 
                    return CameraPacing.Minimal;
                case "slow":
                    return CameraPacing.Slow;
                case "medium":
                    return CameraPacing.Medium;
                case "fast":
                    return CameraPacing.Fast;
                case "crazy":
                    return CameraPacing.Crazy;
                default:
                    Debug.LogWarning("Invalid camera pacing in auto-gen preset: " + cameraPacing);
                    return CameraPacing.Medium;
            }
        }

        #region Lookups (TODO: FIND MORE ELEGANT SOLUTION)

        private static readonly Dictionary<string, string> VENUE_LIGHTING_CONVERSION_LOOKUP = new()
        {
            // { string.Empty,  VENUE_LIGHTING_DEFAULT }, // Handled by the default case
            { "chorus",      VENUE_LIGHTING_CHORUS },
            { "dischord",    VENUE_LIGHTING_DISCHORD },
            { "manual_cool", VENUE_LIGHTING_COOL_MANUAL },
            { "manual_warm", VENUE_LIGHTING_WARM_MANUAL },
            { "stomp",       VENUE_LIGHTING_STOMP },
            { "verse",       VENUE_LIGHTING_VERSE },

            { "blackout_fast",    VENUE_LIGHTING_BLACKOUT_FAST },
            { "blackout_slow",    VENUE_LIGHTING_BLACKOUT_SLOW },
            { "blackout_spot",    VENUE_LIGHTING_BLACKOUT_SPOTLIGHT },
            { "bre",              VENUE_LIGHTING_BIG_ROCK_ENDING },
            { "flare_fast",       VENUE_LIGHTING_FLARE_FAST },
            { "flare_slow",       VENUE_LIGHTING_FLARE_SLOW },
            { "frenzy",           VENUE_LIGHTING_FRENZY },
            { "harmony",          VENUE_LIGHTING_HARMONY },
            { "intro",            VENUE_LIGHTING_INTRO },
            { "loop_cool",        VENUE_LIGHTING_COOL_AUTOMATIC },
            { "loop_warm",        VENUE_LIGHTING_WARM_AUTOMATIC },
            { "searchlights",     VENUE_LIGHTING_SEARCHLIGHTS },
            { "silhouettes",      VENUE_LIGHTING_SILHOUETTES },
            { "silhouettes_spot", VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT },
            { "strobe_fast",      VENUE_LIGHTING_STROBE_FAST },
            { "strobe_slow",      VENUE_LIGHTING_STROBE_SLOW },
            { "sweep",            VENUE_LIGHTING_SWEEP },
        };

        public static readonly Dictionary<string, (int type, string text)> VENUE_TEXT_CONVERSION_LOOKUP = new()
        {
            // Keyframe events
            { "first", (0, VENUE_LIGHTING_FIRST) },
            { "next",  (0, VENUE_LIGHTING_NEXT) },
            { "prev",  (0, VENUE_LIGHTING_PREVIOUS) },

            // RBN1 equivalents for `lighting (chorus)` and `lighting (verse)`
            { "verse",  (0, VENUE_LIGHTING_VERSE) },
            { "chorus", (0, VENUE_LIGHTING_CHORUS) },

            { "bloom.pp",                        (1, VENUE_POSTPROCESS_BLOOM) },
            { "bright.pp",                       (1, VENUE_POSTPROCESS_BRIGHT) },
            { "clean_trails.pp",                 (1, VENUE_POSTPROCESS_TRAILS) },
            { "contrast_a.pp",                   (1, VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE) },
            { "desat_blue.pp",                   (1, VENUE_POSTPROCESS_DESATURATED_BLUE) },
            { "desat_posterize_trails.pp",       (1, VENUE_POSTPROCESS_TRAILS_DESATURATED) },
            { "film_contrast.pp",                (1, VENUE_POSTPROCESS_CONTRAST) },
            { "film_b+w.pp",                     (1, VENUE_POSTPROCESS_BLACK_WHITE) },
            { "film_sepia_ink.pp",               (1, VENUE_POSTPROCESS_SEPIATONE) },
            { "film_silvertone.pp",              (1, VENUE_POSTPROCESS_SILVERTONE) },
            { "film_contrast_red.pp",            (1, VENUE_POSTPROCESS_CONTRAST_RED) },
            { "film_contrast_green.pp",          (1, VENUE_POSTPROCESS_CONTRAST_GREEN) },
            { "film_contrast_blue.pp",           (1, VENUE_POSTPROCESS_CONTRAST_BLUE) },
            { "film_16mm.pp",                    (1, VENUE_POSTPROCESS_GRAINY_FILM) },
            { "film_blue_filter.pp",             (1, VENUE_POSTPROCESS_SCANLINES_BLUE) },
            { "flicker_trails.pp",               (1, VENUE_POSTPROCESS_TRAILS_FLICKERY) },
            { "horror_movie_special.pp",         (1, VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK) },
            { "photocopy.pp",                    (1, VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE) },
            { "photo_negative.pp",               (1, VENUE_POSTPROCESS_PHOTONEGATIVE) },
            { "posterize.pp",                    (1, VENUE_POSTPROCESS_POSTERIZE) },
            { "ProFilm_a.pp",                    (1, VENUE_POSTPROCESS_DEFAULT) },
            { "ProFilm_b.pp",                    (1, VENUE_POSTPROCESS_DESATURATED_RED) },
            { "ProFilm_mirror_a.pp",             (1, VENUE_POSTPROCESS_MIRROR) },
            { "ProFilm_psychedelic_blue_red.pp", (1, VENUE_POSTPROCESS_POLARIZED_RED_BLUE) },
            { "shitty_tv.pp",                    (1, VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION) },
            { "space_woosh.pp",                  (1, VENUE_POSTPROCESS_TRAILS_SPACEY) },
            { "video_a.pp",                      (1, VENUE_POSTPROCESS_SCANLINES) },
            { "video_bw.pp",                     (1, VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE) },
            { "video_security.pp",               (1, VENUE_POSTPROCESS_SCANLINES_SECURITY) },
            { "video_trails.pp",                 (1, VENUE_POSTPROCESS_TRAILS_LONG) },

            { "bonusfx",          (2, VENUE_STAGE_BONUS_FX) },
            { "FogOn",            (2, VENUE_STAGE_FOG_ON) },
            { "FogOff",           (2, VENUE_STAGE_FOG_OFF) },
        };

        private static readonly Dictionary<string, LightingType> LightingLookup = new()
        {
            // Keyframed
            { VENUE_LIGHTING_DEFAULT,     LightingType.Default },
            { VENUE_LIGHTING_DISCHORD,    LightingType.Dischord },
            { VENUE_LIGHTING_CHORUS,      LightingType.Chorus },
            { VENUE_LIGHTING_COOL_MANUAL, LightingType.Cool_Manual },
            { VENUE_LIGHTING_STOMP,       LightingType.Stomp },
            { VENUE_LIGHTING_VERSE,       LightingType.Verse },
            { VENUE_LIGHTING_WARM_MANUAL, LightingType.Warm_Manual },

            // Automatic
            { VENUE_LIGHTING_BIG_ROCK_ENDING,        LightingType.BigRockEnding },
            { VENUE_LIGHTING_BLACKOUT_FAST,          LightingType.Blackout_Fast },
            { VENUE_LIGHTING_BLACKOUT_SLOW,          LightingType.Blackout_Slow },
            { VENUE_LIGHTING_BLACKOUT_SPOTLIGHT,     LightingType.Blackout_Spotlight },
            { VENUE_LIGHTING_COOL_AUTOMATIC,         LightingType.Cool_Automatic },
            { VENUE_LIGHTING_FLARE_FAST,             LightingType.Flare_Fast },
            { VENUE_LIGHTING_FLARE_SLOW,             LightingType.Flare_Slow },
            { VENUE_LIGHTING_FRENZY,                 LightingType.Frenzy },
            { VENUE_LIGHTING_INTRO,                  LightingType.Intro },
            { VENUE_LIGHTING_HARMONY,                LightingType.Harmony },
            { VENUE_LIGHTING_SILHOUETTES,            LightingType.Silhouettes },
            { VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT,  LightingType.Silhouettes_Spotlight },
            { VENUE_LIGHTING_SEARCHLIGHTS,           LightingType.Searchlights },
            { VENUE_LIGHTING_STROBE_FAST,            LightingType.Strobe_Fast },
            { VENUE_LIGHTING_STROBE_SLOW,            LightingType.Strobe_Slow },
            { VENUE_LIGHTING_SWEEP,                  LightingType.Sweep },
            { VENUE_LIGHTING_WARM_AUTOMATIC,         LightingType.Warm_Automatic },

            // Keyframes
            { VENUE_LIGHTING_FIRST,    LightingType.Keyframe_First },
            { VENUE_LIGHTING_NEXT,     LightingType.Keyframe_Next },
            { VENUE_LIGHTING_PREVIOUS, LightingType.Keyframe_Previous },
        };

        private static readonly Dictionary<string, PostProcessingType> PostProcessLookup = new()
        {
            { VENUE_POSTPROCESS_DEFAULT, PostProcessingType.Default },

            // Basic effects
            { VENUE_POSTPROCESS_BLOOM,         PostProcessingType.Bloom },
            { VENUE_POSTPROCESS_BRIGHT,        PostProcessingType.Bright },
            { VENUE_POSTPROCESS_CONTRAST,      PostProcessingType.Contrast },
            { VENUE_POSTPROCESS_MIRROR,        PostProcessingType.Mirror },
            { VENUE_POSTPROCESS_PHOTONEGATIVE, PostProcessingType.PhotoNegative },
            { VENUE_POSTPROCESS_POSTERIZE,     PostProcessingType.Posterize },

            // Color filters/effects
            { VENUE_POSTPROCESS_BLACK_WHITE,             PostProcessingType.BlackAndWhite },
            { VENUE_POSTPROCESS_SEPIATONE,               PostProcessingType.SepiaTone },
            { VENUE_POSTPROCESS_SILVERTONE,              PostProcessingType.SilverTone },
            { VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE,      PostProcessingType.Choppy_BlackAndWhite },
            { VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK, PostProcessingType.PhotoNegative_RedAndBlack },
            { VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE,   PostProcessingType.Polarized_BlackAndWhite },
            { VENUE_POSTPROCESS_POLARIZED_RED_BLUE,      PostProcessingType.Polarized_RedAndBlue },
            { VENUE_POSTPROCESS_DESATURATED_RED,         PostProcessingType.Desaturated_Red },
            { VENUE_POSTPROCESS_DESATURATED_BLUE,        PostProcessingType.Desaturated_Blue },
            { VENUE_POSTPROCESS_CONTRAST_RED,            PostProcessingType.Contrast_Red },
            { VENUE_POSTPROCESS_CONTRAST_GREEN,          PostProcessingType.Contrast_Green },
            { VENUE_POSTPROCESS_CONTRAST_BLUE,           PostProcessingType.Contrast_Blue },

            // Grainy
            { VENUE_POSTPROCESS_GRAINY_FILM,                 PostProcessingType.Grainy_Film },
            { VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION, PostProcessingType.Grainy_ChromaticAbberation },

            // Scanlines
            { VENUE_POSTPROCESS_SCANLINES,             PostProcessingType.Scanlines },
            { VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE, PostProcessingType.Scanlines_BlackAndWhite },
            { VENUE_POSTPROCESS_SCANLINES_BLUE,        PostProcessingType.Scanlines_Blue },
            { VENUE_POSTPROCESS_SCANLINES_SECURITY,    PostProcessingType.Scanlines_Security },

            // Trails
            { VENUE_POSTPROCESS_TRAILS,             PostProcessingType.Trails },
            { VENUE_POSTPROCESS_TRAILS_LONG,        PostProcessingType.Trails_Long },
            { VENUE_POSTPROCESS_TRAILS_DESATURATED, PostProcessingType.Trails_Desaturated },
            { VENUE_POSTPROCESS_TRAILS_FLICKERY,    PostProcessingType.Trails_Flickery },
            { VENUE_POSTPROCESS_TRAILS_SPACEY,      PostProcessingType.Trails_Spacey },
        };

        private static readonly Dictionary<string, StageEffect> StageEffectLookup = new()
        {
            { VENUE_STAGE_BONUS_FX, StageEffect.BonusFx },
            { VENUE_STAGE_FOG_ON,   StageEffect.FogOn },
            { VENUE_STAGE_FOG_OFF,  StageEffect.FogOff },
        };
        // Keyframed
        public const string
        VENUE_LIGHTING_DEFAULT = "default",
        VENUE_LIGHTING_DISCHORD = "dischord",
        VENUE_LIGHTING_CHORUS = "chorus",
        VENUE_LIGHTING_COOL_MANUAL = "cool_manual", // manual_cool
        VENUE_LIGHTING_STOMP = "stomp",
        VENUE_LIGHTING_VERSE = "verse",
        VENUE_LIGHTING_WARM_MANUAL = "warm_manual", // manual_warm

        // Automatic
        VENUE_LIGHTING_BIG_ROCK_ENDING = "big_rock_ending", // bre
        VENUE_LIGHTING_BLACKOUT_FAST = "blackout_fast",
        VENUE_LIGHTING_BLACKOUT_SLOW = "blackout_slow",
        VENUE_LIGHTING_BLACKOUT_SPOTLIGHT = "blackout_spotlight", // blackout_spot
        VENUE_LIGHTING_COOL_AUTOMATIC = "cool_automatic", // loop_cool
        VENUE_LIGHTING_FLARE_FAST = "flare_fast",
        VENUE_LIGHTING_FLARE_SLOW = "flare_slow",
        VENUE_LIGHTING_FRENZY = "frenzy",
        VENUE_LIGHTING_INTRO = "intro",
        VENUE_LIGHTING_HARMONY = "harmony",
        VENUE_LIGHTING_SILHOUETTES = "silhouettes",
        VENUE_LIGHTING_SILHOUETTES_SPOTLIGHT = "silhouettes_spotlight", // silhouettes_spot
        VENUE_LIGHTING_SEARCHLIGHTS = "searchlights",
        VENUE_LIGHTING_STROBE_FAST = "strobe_fast",
        VENUE_LIGHTING_STROBE_SLOW = "strobe_slow",
        VENUE_LIGHTING_SWEEP = "sweep",
        VENUE_LIGHTING_WARM_AUTOMATIC = "warm_automatic", // loop_warm

        // Keyframe events
        VENUE_LIGHTING_FIRST = "first",
        VENUE_LIGHTING_NEXT = "next",
        VENUE_LIGHTING_PREVIOUS = "previous";

        public const string
        VENUE_POSTPROCESS_DEFAULT = "default", // ProFilm_a.pp

        // Basic effects
        VENUE_POSTPROCESS_BLOOM = "bloom", // bloom.pp
        VENUE_POSTPROCESS_BRIGHT = "bright", // bright.pp
        VENUE_POSTPROCESS_CONTRAST = "contrast", // film_contrast.pp
        VENUE_POSTPROCESS_MIRROR = "mirror", // ProFilm_mirror_a.pp
        VENUE_POSTPROCESS_PHOTONEGATIVE = "photonegative", // photo_negative.pp
        VENUE_POSTPROCESS_POSTERIZE = "posterize", // posterize.pp

        // Color filters/effects
        VENUE_POSTPROCESS_BLACK_WHITE = "black_white", // film_b+w.pp
        VENUE_POSTPROCESS_SEPIATONE = "sepiatone", // film_sepia_ink.pp
        VENUE_POSTPROCESS_SILVERTONE = "silvertone", // film_silvertone.pp

        VENUE_POSTPROCESS_CHOPPY_BLACK_WHITE = "choppy_black_white", // photocopy.pp
        VENUE_POSTPROCESS_PHOTONEGATIVE_RED_BLACK = "photonegative_red_black", // horror_movie_special.pp
        VENUE_POSTPROCESS_POLARIZED_BLACK_WHITE = "polarized_black_white", // contrast_a.pp
        VENUE_POSTPROCESS_POLARIZED_RED_BLUE = "polarized_red_blue", // ProFilm_psychedelic_blue_red.pp

        VENUE_POSTPROCESS_DESATURATED_RED = "desaturated_red", // ProFilm_b.pp
        VENUE_POSTPROCESS_DESATURATED_BLUE = "desaturated_blue", // desat_blue.pp

        VENUE_POSTPROCESS_CONTRAST_RED = "contrast_red", // film_contrast_red.pp
        VENUE_POSTPROCESS_CONTRAST_GREEN = "contrast_green", // film_contrast_green.pp
        VENUE_POSTPROCESS_CONTRAST_BLUE = "contrast_blue", // film_contrast_blue.pp

        // Grainy
        VENUE_POSTPROCESS_GRAINY_FILM = "grainy_film", // film_16mm.pp
        VENUE_POSTPROCESS_GRAINY_CHROMATIC_ABBERATION = "grainy_chromatic_abberation", // shitty_tv.pp

        // Scanlines
        VENUE_POSTPROCESS_SCANLINES = "scanlines", // video_a.pp
        VENUE_POSTPROCESS_SCANLINES_BLACK_WHITE = "scanlines_black_white", // video_bw.pp
        VENUE_POSTPROCESS_SCANLINES_BLUE = "scanlines_blue", // film_blue_filter.pp
        VENUE_POSTPROCESS_SCANLINES_SECURITY = "scanlines_security", // video_security.pp

        // Trails (video feed delay, a "visual echo")
        VENUE_POSTPROCESS_TRAILS = "trails", // clean_trails.pp
        VENUE_POSTPROCESS_TRAILS_LONG = "trails_long", // video_trails.pp
        VENUE_POSTPROCESS_TRAILS_DESATURATED = "trails_desaturated", // desat_posterize_trails.pp
        VENUE_POSTPROCESS_TRAILS_FLICKERY = "trails_flickery", // flicker_trails.pp
        VENUE_POSTPROCESS_TRAILS_SPACEY = "trails_spacey"; // space_woosh.pp

        public const string
        VENUE_STAGE_BONUS_FX = "bonus_fx",
        VENUE_STAGE_FOG_ON = "fog_on",
        VENUE_STAGE_FOG_OFF = "fog_off";
        #endregion
    }

    public class AutogenerationSectionPreset
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
        public CameraPacing? CameraPacingOverride;

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
    public enum CameraPacing
    {
        Minimal,
        Slow,
        Medium,
        Fast,
        Crazy
    }
}