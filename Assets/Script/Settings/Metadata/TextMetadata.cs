namespace YARG.Settings.Metadata
{
    public sealed class TextMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames => null;

        public string TextName { get; private set; }

        public TextMetadata(string textName, bool isAdvanced = false)
            : base(isAdvanced)
        {
            TextName = textName;
        }
    }
}