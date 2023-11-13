namespace YARG.Settings.Metadata
{
    public class TextMetadata : AbstractMetadata
    {
        public string TextName { get; private set; }

        public TextMetadata(string textName)
        {
            TextName = textName;
        }
    }
}