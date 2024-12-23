using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public class CONModification
    {
        public bool Processed = false;
        public AbridgedFileInfo? Midi;
        public AbridgedFileInfo? Mogg;
        public AbridgedFileInfo? Milo;
        public AbridgedFileInfo? Image;
        public DTAEntry? UpdateDTA;
        public DTAEntry? UpgradeDTA;
        public RBProUpgrade? UpgradeNode;
    }
}
