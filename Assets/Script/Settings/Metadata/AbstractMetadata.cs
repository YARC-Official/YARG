using System;

namespace YARG.Settings.Metadata
{
    public abstract class AbstractMetadata
    {
        public abstract string[] UnlocalizedSearchNames { get; }

        public bool IsAdvanced { get; }
        public bool IsVisible => VisibleWhen?.Invoke() ?? true;

        private Func<bool> VisibleWhen { get; }

        protected AbstractMetadata(bool isAdvanced = false, Func<bool> visibleWhen = null)
        {
            IsAdvanced = isAdvanced;
            VisibleWhen = visibleWhen;
        }

        public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
    }
}
