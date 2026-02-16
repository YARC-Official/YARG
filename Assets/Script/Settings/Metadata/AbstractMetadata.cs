namespace YARG.Settings.Metadata
{
    public abstract class AbstractMetadata
    {
        public abstract string[] UnlocalizedSearchNames { get; }

        public bool Advanced { get; }

        protected AbstractMetadata(bool advanced = false)
        {
            Advanced = advanced;
        }

        public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
    }
}