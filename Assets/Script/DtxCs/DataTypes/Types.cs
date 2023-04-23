namespace DtxCS.DataTypes
{
  /// <summary>
  /// Represents the possible types of values in a DataArray.
  /// </summary>
  public enum DataType : int
  {
    /// <summary>
    /// Integral value.
    /// </summary>
    INT = 0x00,
    /// <summary>
    /// Floating point value.
    /// </summary>
    FLOAT = 0x01,
    /// <summary>
    /// $-prefixed variable type
    /// </summary>
    VARIABLE = 0x02,
    /// <summary>
    /// Symbol value.
    /// </summary>
    SYMBOL = 0x05,
    /// <summary>
    /// '()
    /// </summary>
    EMPTY = 0x06,
    /// <summary>
    /// #ifdef directive
    /// </summary>
    IFDEF = 0x07,
    /// <summary>
    /// #else directive
    /// </summary>
    ELSE = 0x08,
    /// <summary>
    /// #endif directive
    /// </summary>
    ENDIF = 0x09,
    /// <summary>
    /// Array.
    /// </summary>
    ARRAY = 0x10,
    /// <summary>
    /// {}-enclosed function call
    /// </summary>
    COMMAND = 0x11,
    /// <summary>
    /// ""-enclosed String value.
    /// </summary>
    STRING = 0x12,
    /// <summary>
    /// []-enclosed macro definition
    /// </summary>
    MACRO = 0x13,
    /// <summary>
    /// #define directive
    /// </summary>
    DEFINE = 0x20,
    /// <summary>
    /// #include directive
    /// </summary>
    INCLUDE = 0x21,
    /// <summary>
    /// #merge directive
    /// </summary>
    MERGE = 0x22,
    /// <summary>
    /// #ifndef directive
    /// </summary>
    IFNDEF = 0x23,
    /// <summary>
    /// #autorun directive
    /// </summary>
    AUTORUN = 0x24,
    /// <summary>
    /// #undef directive
    /// </summary>
    UNDEF = 0x25
  };

  /// <summary>
  /// Represents the basic element of DTA, which could be an Atom or an Array.
  /// </summary>
  public abstract class DataNode
  {
    /// <summary>
    /// The parent of this data node. If null, this is a root node.
    /// </summary>
    public DataArray Parent { get; set; }

    /// <summary>
    /// The name of this data node.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The type of this data node.
    /// </summary>
    public abstract DataType Type { get; }

    /// <summary>
    /// Evaluate this node to get a value.
    /// </summary>
    /// <returns></returns>
    public abstract DataNode Evaluate();

    public override bool Equals(object obj)
    {
      if (!(obj is DataNode)) return false;
      if ((obj as DataNode).Type != this.Type) return false;
      if (obj.ToString() != this.ToString()) return false;
      return true;
    }

    public virtual string ToString(int depth) => ToString();

    public override int GetHashCode()
    {
      return ToString().GetHashCode();
    }
  }
}
