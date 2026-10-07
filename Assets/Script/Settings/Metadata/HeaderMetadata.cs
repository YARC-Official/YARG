namespace YARG.Settings.Metadata
{
    public sealed class HeaderMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames => null;

        public string HeaderName { get; private set; }
        public bool ShowPreview { get; }

        public HeaderMetadata(string headerName, bool isAdvanced = false, bool showPreview = false)
            : base(isAdvanced)
        {
            HeaderName = headerName;
            ShowPreview = showPreview;
        }
    }
}