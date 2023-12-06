using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    // TODO: Move to YARG.Core

    public partial class EnginePreset : BasePreset
    {
        public FiveFretGuitarPreset FiveFretGuitar;
        public DrumsPreset          Drums;

        public EnginePreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarPreset();
            Drums = new DrumsPreset();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new EnginePreset(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                Drums = Drums.Copy(),
            };
        }
    }
}