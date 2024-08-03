namespace YARG.Settings.Metadata
{
    public sealed class FieldMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames { get; }

        public string FieldName { get; }
        public bool HasDescription { get; } = true;

        public FieldMetadata(string fieldName)
        {
            UnlocalizedSearchNames = new[] { $"Setting.{fieldName}.Name" };
            FieldName = fieldName;
        }

        public FieldMetadata(string fieldName, bool hasDescription)
            : this(fieldName)
        {
            HasDescription = hasDescription;
        }
    }
}