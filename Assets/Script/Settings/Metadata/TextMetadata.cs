namespace YARG.Settings.Metadata
{
    public sealed class TextMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames => null;

        public string TextName { get; private set; }

        public TextMetadata(string textName, bool advanced = false)
            : base(advanced)
        {
            TextName = textName;
        }
    }
}