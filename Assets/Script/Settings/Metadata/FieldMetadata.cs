namespace YARG.Settings.Metadata
{
    public class FieldMetadata : AbstractMetadata
    {
        public string FieldName { get; private set; }

        public FieldMetadata(string fieldName)
        {
            FieldName = fieldName;
        }
    }
}