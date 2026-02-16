namespace YARG.Settings.Metadata
{
    public sealed class HeaderMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames => null;

        public string HeaderName { get; private set; }

        public HeaderMetadata(string headerName, bool advanced = false)
            : base(advanced)
        {
            HeaderName = headerName;
        }
    }
}