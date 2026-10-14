using System;

namespace YARG.Settings.Metadata
{
    public abstract class AbstractMetadata
    {
        public abstract string[] UnlocalizedSearchNames { get; }

        public bool IsVisible => VisibleWhen?.Invoke() ?? true;

        private Func<bool> VisibleWhen { get; }

        protected AbstractMetadata(Func<bool> visibleWhen = null)
        {
            VisibleWhen = visibleWhen;
        }

        public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
    }
}
