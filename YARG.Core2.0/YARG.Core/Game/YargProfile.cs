using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Chart;
using YARG.Core.Utility;
using YARG.Core.Extensions;
using System.Linq;

namespace YARG.Core.Game
{
    public class YargProfile
    {
        private const int PROFILE_VERSION = 2;

        public Guid Id;
        public string Name;

        public bool IsBot;

        public GameMode GameMode;

        public float NoteSpeed;
        public float HighwayLength;

        public bool LeftyFlip;

        public bool AutoConnect;

        public long InputCalibrationMilliseconds;
        public double InputCalibrationSeconds
        {
            get => InputCalibrationMilliseconds / 1000.0;
            set => InputCalibrationMilliseconds = (long) (value * 1000);
        }

        public bool HasValidInstrument => GameMode.PossibleInstruments().Contains(CurrentInstrument);

        public Guid EnginePreset;

        public Guid ThemePreset;
        public Guid ColorProfile;
        public Guid CameraPreset;
        public Guid HighwayPreset;

        /// <summary>
        /// The selected instrument.
        /// </summary>
        public Instrument CurrentInstrument;

        /// <summary>
        /// The selected difficulty.
        /// </summary>
        public Difficulty CurrentDifficulty;

        /// <summary>
        /// The difficulty to be saved in the profile.
        /// 
        /// If a song does not contain this difficulty, so long as the player
        /// does not *explicitly* and *manually* change the difficulty, this value
        /// should remain unchanged.
        /// </summary>
        public Difficulty DifficultyFallback;

        /// <summary>
        /// The harmony index, used for determining what harmony part the player selected.
        /// Does nothing if <see cref="CurrentInstrument"/> is not a harmony.
        /// </summary>
        public byte HarmonyIndex;

        /// <summary>
        /// The currently selected modifiers as a flag.
        /// Use <see cref="AddSingleModifier"/> and <see cref="RemoveModifiers"/> to modify.
        /// </summary>
        [JsonProperty]
        public Modifier CurrentModifiers { get; private set; }

        public YargProfile()
        {
            Id = Guid.NewGuid();
            Name = "Default";
            GameMode = GameMode.FiveFretGuitar;
            NoteSpeed = 6;
            HighwayLength = 1;
            LeftyFlip = false;
            AutoConnect = false;

            // Set preset IDs to default
            ColorProfile = Game.ColorProfile.Default.Id;
            CameraPreset = Game.CameraPreset.Default.Id;
            HighwayPreset = Game.HighwayPreset.Default.Id;

            CurrentModifiers = Modifier.None;
        }

        public YargProfile(Guid id) : this()
        {
            Id = id;
        }

        public YargProfile(Stream stream)
        {
            int version = stream.Read<int>(Endianness.Little);

            Name = stream.ReadString();

            EnginePreset = stream.ReadGuid();

            ThemePreset = stream.ReadGuid();
            ColorProfile = stream.ReadGuid();
            CameraPreset = stream.ReadGuid();

            if (version >= 2)
            {
                HighwayPreset = stream.ReadGuid();
            }
            CurrentInstrument = (Instrument) stream.ReadByte();
            CurrentDifficulty = (Difficulty) stream.ReadByte();
            CurrentModifiers = (Modifier) stream.Read<ulong>(Endianness.Little);
            HarmonyIndex = (byte)stream.ReadByte();

            NoteSpeed = stream.Read<float>(Endianness.Little);
            HighwayLength = stream.Read<float>(Endianness.Little);
            LeftyFlip = stream.ReadBoolean();

            GameMode = CurrentInstrument.ToGameMode();
        }

        public void AddSingleModifier(Modifier modifier)
        {
            // Remove conflicting modifiers first
            RemoveModifiers(ModifierConflicts.FromSingleModifier(modifier));
            CurrentModifiers |= modifier;
        }

        public void RemoveModifiers(Modifier modifier)
        {
            CurrentModifiers &= ~modifier;
        }

        public bool IsModifierActive(Modifier modifier)
        {
            return (CurrentModifiers & modifier) == modifier;
        }

        public void CopyModifiers(YargProfile profile)
        {
            // The modifiers of the other profile are guaranteed to be correct
            CurrentModifiers = profile.CurrentModifiers;
        }

        public void ApplyModifiers<TNote>(InstrumentDifficulty<TNote> track) where TNote : Note<TNote>
        {
            switch (CurrentInstrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                    if (track is not InstrumentDifficulty<GuitarNote> guitarTrack)
                    {
                        throw new InvalidOperationException("Cannot apply guitar modifiers to non-guitar track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.AllStrums))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Strum);
                    }
                    else if (IsModifierActive(Modifier.AllHopos))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Hopo);
                    }
                    else if (IsModifierActive(Modifier.AllTaps))
                    {
                        guitarTrack.ConvertToGuitarType(GuitarNoteType.Tap);
                    }
                    else if (IsModifierActive(Modifier.HoposToTaps))
                    {
                        guitarTrack.ConvertFromTypeToType(GuitarNoteType.Hopo, GuitarNoteType.Tap);
                    }
                    else if (IsModifierActive(Modifier.TapsToHopos))
                    {
                        guitarTrack.ConvertFromTypeToType(GuitarNoteType.Tap, GuitarNoteType.Hopo);
                    }

                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    if (track is not InstrumentDifficulty<DrumNote> drumsTrack)
                    {
                        throw new InvalidOperationException("Cannot apply drum modifiers to non-drums track " +
                            $"with notes of {typeof(TNote)}!");
                    }

                    if (IsModifierActive(Modifier.NoKicks))
                    {
                        drumsTrack.RemoveKickDrumNotes();
                    }

                    if (IsModifierActive(Modifier.NoDynamics))
                    {
                        drumsTrack.RemoveDynamics();
                    }

                    break;
                case GameMode.Vocals:
                    throw new InvalidOperationException("For vocals, use ApplyVocalModifiers instead!");
            }
        }

        public void ApplyVocalModifiers(VocalsPart vocalsPart)
        {
            if (IsModifierActive(Modifier.UnpitchedOnly))
            {
                vocalsPart.ConvertAllToUnpitched();
            }
        }

        public void EnsureValidInstrument()
        {

            if (!HasValidInstrument)
            {
                CurrentInstrument = GameMode.PossibleInstruments()[0];
            }
        }

        // For replay serialization
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PROFILE_VERSION);

            writer.Write(Name);

            writer.Write(EnginePreset);

            writer.Write(ThemePreset);
            writer.Write(ColorProfile);
            writer.Write(CameraPreset);
            writer.Write(HighwayPreset);

            writer.Write((byte) CurrentInstrument);
            writer.Write((byte) CurrentDifficulty);
            writer.Write((ulong) CurrentModifiers);
            writer.Write(HarmonyIndex);

            writer.Write(NoteSpeed);
            writer.Write(HighwayLength);
            writer.Write(LeftyFlip);
        }
    }
}