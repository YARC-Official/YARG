namespace YARG.Settings.Metadata
{
    public abstract class AbstractMetadata
    {
        public abstract string[] UnlocalizedSearchNames { get; }

        public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
    }
}