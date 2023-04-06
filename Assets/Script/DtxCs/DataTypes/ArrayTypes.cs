using System;
using System.Collections.Generic;

namespace DtxCS.DataTypes
{
  /// <summary>
  /// Represents an array of DataNodes.
  /// </summary>
  public class DataArray : DataNode
  {
    public virtual char ClosingChar => ')';

    /// <summary>
    /// Returns DataType.ARRAY
    /// </summary>
    public override DataType Type => DataType.ARRAY;

    /// <summary>
    /// The children of this array.
    /// </summary>
    public List<DataNode> Children { get; }

    /// <summary>
    /// Default constructor for a Data Array.
    /// </summary>
    public DataArray()
    {
      this.Children = new List<DataNode>();
    }

    /// <summary>
    /// Add a node to this Array.
    /// </summary>
    /// <param name="node">Node to add</param>
    /// <returns>The added node.</returns>
    public T AddNode<T>(T node) where T : DataNode
    {
      Children.Add(node);
      node.Parent = this;
      return node;
    }

    /// <summary>
    /// Get or set the child of this array at the given index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public DataNode this[int index]
    {
      get { return Children[index]; }
      set { Children[index] = value; }
    }

    /// <summary>
    /// Find the array in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public DataArray Array(int idx)
    {
      if (Children[idx].Type == DataType.ARRAY)
      {
        return (DataArray)Children[idx];
      }
      throw new Exception("Element at index " + idx + " is not an Array. It is "+Children[idx].GetType().Name);
    }

    /// <summary>
    /// Find the integer in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public int Int(int idx)
    {
      if (Children[idx].Type == DataType.INT)
      {
        return ((DataAtom)Children[idx]).Int;
      }
      throw new Exception("Element at index " + idx + " is not an integer.");
    }

    /// <summary>
    /// Find the float in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public float Float(int idx)
    {
      if (Children[idx].Type == DataType.FLOAT)
      {
        return ((DataAtom)Children[idx]).Float;
      }
      throw new Exception("Element at index " + idx + " is not a float.");
    }

    public float Number(int idx)
    {
      if (Children[idx].Type == DataType.FLOAT)
      {
        return ((DataAtom)Children[idx]).Float;
      }
      else if (Children[idx].Type == DataType.INT)
      {
        return ((DataAtom)Children[idx]).Int;
      }
      throw new Exception("Element at index " + idx + " is not a number.");
    }

    public DataNode Node(int idx)
    {
      return Children[idx];
    }

    /// <summary>
    /// Find the string in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public string String(int idx)
    {
      if (Children[idx].Type == DataType.STRING)
      {
        return ((DataAtom)Children[idx]).String;
      }
      throw new Exception("Element at index " + idx + " is not a string.");
    }

    /// <summary>
    /// Find the symbol in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public DataSymbol Symbol(int idx)
    {
      if(Children[idx].Type == DataType.SYMBOL)
      {
        return (DataSymbol)Children[idx];
      }
      throw new Exception("Element at index " + idx + " is not a symbol.");
    }

    /// <summary>
    /// Find the variable in this array's children at the given index.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public DataVariable Var(int idx)
    {
      if (Children[idx].Type == DataType.VARIABLE)
      {
        return (DataVariable)Children[idx];
      }
      throw new Exception("Element at index " + idx + " is not a variable.");
    }

    /// <summary>
    /// Finds any node at the given index and returns it as a string.
    /// </summary>
    /// <param name="idx"></param>
    /// <returns></returns>
    public string Any(int idx)
    {
      return Children[idx].Name;
    }

    /// <summary>
    /// Find the first array in this array's children whose name matches.
    /// If none is found, returns null.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public DataArray Array(string name)
    {
      for (int i = 0; i < Children.Count; i++)
      {
        if (!(Children[i] is DataArray)) continue;
        if (Children[i].Name == name)
        {
          return ((DataArray)Children[i]);
        }
      }
      return null;
    }

    public override DataNode Evaluate()
    {
      var returnArray = new DataArray();
      foreach(var node in Children)
      {
        returnArray.AddNode(node.Evaluate());
      }
      return returnArray;
    }

    /// <summary>
    /// The string representation of the first element of the array, unless
    /// that element is another array, which would result in an empty string.
    /// </summary>
    public override string Name => Children[0].Type == DataType.ARRAY ?
                             "" : Children[0].Name;

    /// <summary>
    /// The number of elements in this array.
    /// </summary>
    public int Count => Children.Count;

    /// <summary>
    /// The string representation of this Array, suitable for putting right
    /// back into a .dta file.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
      string ret = new string(' ', depth*3) + "(";
      for (int i = 0; i < Children.Count; i++)
      {
        var n = Children[i];
        ret += n is DataArray ? Environment.NewLine + n.ToString(depth + 1) : n.ToString(depth + 1);
        if (i + 1 != Children.Count) ret += " ";
      }
      ret += ")";
      return ret;
    }
  }

  public class DataCommand : DataArray
  {
    public override DataType Type => DataType.COMMAND;
    public override char ClosingChar => '}';

    public DataCommand() : base()
    {
    }

    public override string ToString() => ToString(0);

    public override string ToString(int depth)
    {
      string ret = new string(' ', depth*3)+"{";
      foreach (DataNode n in Children)
      {
        ret += (n is DataArray ? Environment.NewLine + n.ToString(depth + 1) : n.ToString(depth + 1) + " ");
      }
      ret += "}";
      return ret;
    }

    /// <summary>
    /// Returns a copy of this datacommand with all children evaulated.
    /// </summary>
    public DataCommand EvalAll()
    {
      var ret = new DataCommand();
      for(var i = 0; i < Children.Count; i++)
      {
        ret.AddNode(Children[i].Evaluate());
      }
      return ret;
    }

    public override DataNode Evaluate()
    {
      Func<DataCommand, DataNode> f;
      if (Builtins.Funcs.TryGetValue(Symbol(0), out f))
      {
        return f.Invoke(this);
      }
      throw new Exception($"Func '{Any(0)}' is not defined.");
    }
  }

  // e.g., [RED 1 0 0] in a file, then (color RED) -> (color 1 0 0)
  // or, [TRUE 1] -> (happy TRUE) -> (happy 1)
  public class DataMacroDefinition : DataArray
  {
    public override DataType Type => DataType.MACRO;
    public override char ClosingChar => ']';
    public DataMacroDefinition() : base()
    {

    }
    public override string ToString() => ToString(0);

    public override DataNode Evaluate() => this;
    public override string ToString(int depth)
    {
      string ret = new string(' ', depth*3) + "[";
      foreach (DataNode n in Children)
      {
        ret += (n is DataArray ? Environment.NewLine + n.ToString(depth + 1) : n.ToString(depth + 1) + " ");
      }
      ret += "]";
      return ret;
    }
  }

}
