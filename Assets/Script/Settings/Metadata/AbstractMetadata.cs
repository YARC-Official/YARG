namespace YARG.Settings.Metadata
{
    public abstract class AbstractMetadata
    {
        public abstract string[] UnlocalizedSearchNames { get; }

        public bool IsAdvanced { get; }

        protected AbstractMetadata(bool isAdvanced = false)
        {
            IsAdvanced = isAdvanced;
        }

        public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
    }
}