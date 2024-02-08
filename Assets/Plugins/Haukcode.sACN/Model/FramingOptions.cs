
namespace Haukcode.sACN.Model
{
    public class FramingOptions
    {
        public const byte FORCE_SYNCHRONIZATION = 0b0000_1000;
        public const byte STREAM_TERMINATED = 0b0000_0100;
        public const byte PREVIEW_DATA = 0b0000_0010;

        public bool PreviewData { get; set; }

        public bool StreamTerminated { get; set; }

        public bool ForceSynchronization { get; set; }

        public FramingOptions()
        {
        }

        public static FramingOptions Parse(byte optionsByte)
        {
            var options = new FramingOptions();

            if ((optionsByte & FORCE_SYNCHRONIZATION) != 0)
            {
                options.ForceSynchronization = true;
            }
            if ((optionsByte & STREAM_TERMINATED) != 0)
            {
                options.StreamTerminated = true;
            }
            if ((optionsByte & PREVIEW_DATA) != 0)
            {
                options.PreviewData = true;
            }

            return options;
        }

        public byte ToByte()
        {
            byte returnVal = 0;

            if (PreviewData)
            {
                returnVal = (byte)(returnVal | PREVIEW_DATA);
            }
            if (StreamTerminated)
            {
                returnVal = (byte)(returnVal | STREAM_TERMINATED);
            }
            if (ForceSynchronization)
            {
                returnVal = (byte)(returnVal | FORCE_SYNCHRONIZATION);
            }

            return returnVal;
        }
    }
}
