using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Game.Settings;
using YARG.Core.Utility;

namespace YARG.Core.Game
{
    public partial class ColorProfile : BasePreset, IBinarySerializable
    {
        private const int COLOR_PROFILE_VERSION = 1;

        /// <summary>
        /// Interface that has methods that allows for generic fret color retrieval.
        /// Not all instruments have frets, so it's an interface.
        /// </summary>
        public interface IFretColorProvider
        {
            public Color GetFretColor(int index);
            public Color GetFretInnerColor(int index);
            public Color GetParticleColor(int index);
        }

        [JsonIgnore]
        public int Version = COLOR_PROFILE_VERSION;

        [SettingSubSection]
        public FiveFretGuitarColors FiveFretGuitar;
        [SettingSubSection]
        public FourLaneDrumsColors FourLaneDrums;
        [SettingSubSection]
        public FiveLaneDrumsColors FiveLaneDrums;
        [SettingSubSection]
        public ProKeysColors ProKeys;

        public ColorProfile(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarColors();
            FourLaneDrums = new FourLaneDrumsColors();
            FiveLaneDrums = new FiveLaneDrumsColors();
            ProKeys = new ProKeysColors();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new ColorProfile(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                FourLaneDrums = FourLaneDrums.Copy(),
                FiveLaneDrums = FiveLaneDrums.Copy(),
                ProKeys = ProKeys.Copy(),
            };
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(Name);

            FiveFretGuitar.Serialize(writer);
            FourLaneDrums.Serialize(writer);
            FiveLaneDrums.Serialize(writer);
            ProKeys.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            version = reader.ReadInt32();
            Name = reader.ReadString();

            FiveFretGuitar.Deserialize(reader, version);
            FourLaneDrums.Deserialize(reader, version);
            FiveLaneDrums.Deserialize(reader, version);
            ProKeys.Deserialize(reader, version);
        }
    }
}