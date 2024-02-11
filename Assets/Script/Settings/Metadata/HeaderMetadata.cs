namespace YARG.Settings.Metadata
{
    public sealed class HeaderMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames { get; }

        public string HeaderName { get; private set; }

        public HeaderMetadata(string headerName)
        {
            UnlocalizedSearchNames = new[] { $"Header.{headerName}" };
            HeaderName = headerName;
        }
    }
}