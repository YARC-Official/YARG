namespace YARG.Settings.Metadata
{
    public class HeaderMetadata : AbstractMetadata
    {
        public string HeaderName { get; private set; }

        public HeaderMetadata(string headerName)
        {
            HeaderName = headerName;
        }
    }
}