using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using AnimationEvent = YARG.Core.Chart.AnimationEvent;

namespace YARG.Venue
{
    /// <summary>
    /// Computed once on Awake. Every field is a pre-resolved Animator parameter hash.
    /// Add new fields here as new parameters are introduced in the animator.
    /// </summary>
    public sealed class VenueHashLibrary
    {
        // ── Fixed params ─────────────────────────────────────────────────────────
        public readonly int BPMAdjust      = Animator.StringToHash("BPMAdjust");
        public readonly int RNG            = Animator.StringToHash("RNG");
        public readonly int Fog            = Animator.StringToHash("Fog");
        public readonly int BonusFx        = Animator.StringToHash("BonusFx");
        public readonly int CrowdClap      = Animator.StringToHash("CrowdClap");
        public readonly int Happiness      = Animator.StringToHash("Happiness");
        public readonly int HappyHigh      = Animator.StringToHash("HappyHigh");
        public readonly int HappyMed       = Animator.StringToHash("HappyMed");
        public readonly int HappyLow       = Animator.StringToHash("HappyLow");
        public readonly int VocalNote      = Animator.StringToHash("VocalNote");
        public readonly int Har1Note       = Animator.StringToHash("Har1Note");
        public readonly int Har2Note       = Animator.StringToHash("Har2Note");
        public readonly int VocalUnpitched = Animator.StringToHash("VocalUnpitched");
        public readonly int Har1Unpitched  = Animator.StringToHash("Har1Unpitched");
        public readonly int Har2Unpitched  = Animator.StringToHash("Har2Unpitched");
        public readonly int VocalPitch     = Animator.StringToHash("VocalPitch");
        public readonly int Har1Pitch      = Animator.StringToHash("Har1Pitch");
        public readonly int Har2Pitch      = Animator.StringToHash("Har2Pitch");

        // ── Enum-indexed arrays ───────────────────────────────────────────────────
        // Index directly: LightingHashes[(int)LightingType.Frenzy]
        public readonly int[] LightingHashes;
        public readonly int[] PostProcessingHashes;
        public readonly int[] BeatlineHashes;
        public readonly int[] DrumAnimHashes;
        public readonly int[] CameraSubjectHashes;
        public readonly int[] CrowdStateHashes;

        // ── Blend state hashes (LayerName.StateName)  ─────────────────────────────
        // Indexed same as LightingHashes, PostProcessingHashes
        public readonly int[] LightingBlendHashes;
        public readonly int[] PostProcessingBlendHashes;

        // ── List-indexed arrays ───────────────────────────────────────────────────
        // Index matches the existing note name lists
        public readonly int[] GuitarNoteHashes;
        public readonly int[] BassNoteHashes;
        public readonly int[] DrumNoteHashes;
        public readonly int[] KeysNoteHashes;

        // ── 2D composite arrays ───────────────────────────────────────────────────
        // [stringIndex * FretCount + fret]
        public const    int   FretCount = 23;
        public readonly int[] ProGuitarHashes; // "pg{string}{fret}"
        public readonly int[] ProBassHashes;   // "pb{string}{fret}"

        // [noteIndex] → "pk{noteName}"
        public readonly int[] ProKeysHashes;

        // [inst * noteCount + noteIndex] → "{prefix}{noteName}"
        // inst: 0=Vocal, 1=Har1, 2=Har2
        public readonly int[] VocalNoteHashes;
        public readonly int   VocalNoteCount;
        public const    int   VocalInstrumentCount = 3;

        // IDs that vary between venues
        public int LightingLayerHash = -1;
        public int PostProcessingLayerHash = -1;

        public int LightDefault = Animator.StringToHash("LightDefault");
        public int PPDefault = Animator.StringToHash("PPDefault");
        public int LightBlendDefault;
        public int PPBlendDefault;

        public VenueHashLibrary(
            IReadOnlyList<string> guitarNoteNames,
            IReadOnlyList<string> bassNoteNames,
            IReadOnlyList<string> drumNoteNames,
            IReadOnlyList<string> keysNoteNames,
            IReadOnlyList<string> proGuitarStringNames,
            IReadOnlyList<string> proKeysNoteNames,
            IReadOnlyList<string> vocalNoteNames,
            IReadOnlyList<string> crowdStateNames,
            string lightingLayerName,
            string postProcessingLayerName)
        {
            LightBlendDefault = Animator.StringToHash($"{lightingLayerName}.LightDefault");
            PPBlendDefault = Animator.StringToHash($"{postProcessingLayerName}.PPDefault");

            LightingHashes = HashEnum<LightingType>();
            PostProcessingHashes = HashEnum<PostProcessingType>();
            LightingBlendHashes = HashEnumQualified<LightingType>(lightingLayerName, LightBlendDefault);
            PostProcessingBlendHashes = HashEnumQualified<PostProcessingType>(postProcessingLayerName, PPBlendDefault);
            BeatlineHashes = HashEnum<BeatlineType>();
            DrumAnimHashes = HashEnum<AnimationEvent.AnimationType>();
            CameraSubjectHashes = HashEnum<CameraCutEvent.CameraCutSubject>();

            GuitarNoteHashes = HashList(guitarNoteNames);
            BassNoteHashes = HashList(bassNoteNames);
            DrumNoteHashes = HashList(drumNoteNames);
            KeysNoteHashes = HashList(keysNoteNames);

            CrowdStateHashes = HashList(crowdStateNames);

            // Pro guitar / bass 2D: "pg{string}{fret}", "pb{string}{fret}"
            ProGuitarHashes = new int[proGuitarStringNames.Count * FretCount];
            ProBassHashes = new int[proGuitarStringNames.Count * FretCount];
            for (int s = 0; s < proGuitarStringNames.Count; s++)
            {
                for (int fret = 0; fret < FretCount; fret++)
                {
                    ProGuitarHashes[s * FretCount + fret] =
                        Animator.StringToHash($"pg{proGuitarStringNames[s]}{fret}");
                    ProBassHashes[s * FretCount + fret] =
                        Animator.StringToHash($"pb{proGuitarStringNames[s]}{fret}");
                }
            }

            // Pro keys: "pk{noteName}"
            ProKeysHashes = new int[proKeysNoteNames.Count];
            for (int i = 0; i < proKeysNoteNames.Count; i++)
                ProKeysHashes[i] = Animator.StringToHash($"pk{proKeysNoteNames[i]}");

            // Vocals: "{prefix}{noteName}"
            var prefixes = new[]
            {
                "Vocal",
                "Har1",
                "Har2"
            };
            VocalNoteCount = vocalNoteNames.Count;
            VocalNoteHashes = new int[VocalInstrumentCount * VocalNoteCount];
            for (int inst = 0; inst < VocalInstrumentCount; inst++)
            for (int n = 0; n < VocalNoteCount; n++)
                VocalNoteHashes[inst * VocalNoteCount + n] =
                    Animator.StringToHash(prefixes[inst] + vocalNoteNames[n]);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static int[] HashEnumQualified<TEnum>(string layerName, int defaultHash = -1) where TEnum : Enum
        {
            var values = (int[]) Enum.GetValues(typeof(TEnum));
            int max = 0;
            foreach (int v in values)
                if (v > max)
                    max = v;
            var result = new int[max + 1];
            foreach (TEnum val in Enum.GetValues(typeof(TEnum)))
                result[(int) (object) val] = Animator.StringToHash($"{layerName}.{val.ToString()}");
            if (defaultHash != -1)
                result[0] = defaultHash;
            return result;
        }

        private static int[] HashEnum<TEnum>() where TEnum : Enum
        {
            var values = (int[]) Enum.GetValues(typeof(TEnum));
            int max = 0;
            foreach (int v in values)
                if (v > max)
                    max = v;
            var result = new int[max + 1];
            foreach (TEnum val in Enum.GetValues(typeof(TEnum)))
                result[(int) (object) val] = Animator.StringToHash(val.ToString());
            return result;
        }

        private static int[] HashList(IReadOnlyList<string> names)
        {
            var result = new int[names.Count];
            for (int i = 0; i < names.Count; i++) result[i] = Animator.StringToHash(names[i]);
            return result;
        }

        public void Randomize(Animator animator)
        {
            float value = UnityEngine.Random.Range(100f, 1f);
            value = MathF.Round(value);
            animator.SafeSetFloat(RNG, value);
        }
    }
}