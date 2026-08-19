using System;

namespace YARG.Settings.Metadata
{
    public sealed class FieldMetadata : AbstractMetadata
    {
        public override string[] UnlocalizedSearchNames { get; }

        public string FieldName { get; }
        public bool HasDescription { get; } = true;

        public FieldMetadata(string fieldName, bool hasDescription = true, bool isAdvanced = false,
            Func<bool> visibleWhen = null)
            : base(isAdvanced, visibleWhen)
        {
            UnlocalizedSearchNames = new[] { $"Setting.{fieldName}.Name" };
            FieldName = fieldName;
            HasDescription = hasDescription;
        }
    }
}
