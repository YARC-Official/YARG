namespace YARG.Metadata {
	public abstract class AbstractMetadata {
		public static implicit operator AbstractMetadata(string name) => new FieldMetadata(name);
	}
}