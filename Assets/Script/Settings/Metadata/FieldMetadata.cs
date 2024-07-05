namespace YARG.Settings.Metadata
{
    public sealed class FieldMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames { get; }

        public string FieldName { get; private set; }

        public FieldMetadata(string fieldName)
        {
            UnlocalizedSearchNames = new[] { $"Setting.{fieldName}.Name" };
            FieldName = fieldName;
        }
    }
}