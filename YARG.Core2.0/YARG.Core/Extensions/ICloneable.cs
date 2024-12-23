using System.Diagnostics.CodeAnalysis;

// Since this is a generic version of an existing interface
// in the System namespace
namespace System
{
    /// <summary>
    /// Supports cloning in a generic fashion.
    /// </summary>
    public interface ICloneable<T> : ICloneable
    {
        /// <summary>
        /// Creates a copy of this object with the same set of values.
        /// </summary>
        [return: NotNull] // thanks Roslyn
        new T Clone();

        object ICloneable.Clone() => Clone();
    }
}