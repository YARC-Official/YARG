using System;
using System.Collections.Generic;

namespace DtxCS.DataTypes
{
  /// <summary>
  /// The most basic element of dta.
  /// </summary>
  public class DataAtom : DataNode
  {
    DataType type;
    string sData;
    int iData;
    float fData;

    /// <summary>
    /// The type of this Atom.
    /// </summary>
    public override DataType Type => type;

    public override DataNode Evaluate() => this;

    /// <summary>
    /// The value of this Atom.
    /// </summary>
    public int Int
    {
      get
      {
        if (type == DataType.INT)
          return iData;
        else throw new Exception("Data is not int");
      }
    }

    /// <summary>
    /// The value of this Atom.
    /// </summary>
    public float Float
    {
      get
      {
        if (type == DataType.FLOAT)
          return fData;
        else throw new Exception("Data is not float");
      }
    }

    /// <summary>
    /// The value of this Atom.
    /// </summary>
    public string String
    {
      get
      {
        if (type == DataType.STRING)
          return sData;
        else throw new Exception("Data is not string");
      }
    }

    /// <summary>
    /// Construct an Atom whose value is a string or symbol.
    /// </summary>
    /// <param name="data">The value assigned to this atom.</param>
    public DataAtom(string data)
    {
      type = DataType.STRING;
      sData = data.Replace("\\q", "\"");
    }

    /// <summary>
    /// Construct an Atom whose value is an integer.
    /// </summary>
    /// <param name="data"></param>
    public DataAtom(int data)
    {
      type = DataType.INT;
      iData = data;
    }

    /// <summary>
    /// Construct an Atom whose value is a floating-point value.
    /// </summary>
    /// <param name="data"></param>
    public DataAtom(float data)
    {
      type = DataType.FLOAT;
      fData = data;
    }

    /// <summary>
    /// The string representation of this Atom.
    /// </summary>
    public override string Name => ToString(true);

    private string ToString(bool name)
    {
      string ret = "";
      switch (type)
      {
        case DataType.STRING:
          ret += name ? sData : "\"" + sData + "\"";
          break;
        case DataType.INT:
          ret += iData.ToString();
          break;
        case DataType.FLOAT:
          // Even though the format string uses a dot, it gets changed to a comma on some locales
          // unless you give ToString the invariant culture.
          ret += fData.ToString("0.0#", System.Globalization.NumberFormatInfo.InvariantInfo);
          break;
      }
      return ret;
    }

    /// <summary>
    /// Returns the string representation of this Atom.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => ToString(false);
  }

  public class DataVariable : DataNode
  {
    static Dictionary<string, DataVariable> vars = new Dictionary<string, DataVariable>();

    public override string Name { get; }
    public override string ToString() => Name;
    
    public override DataType Type => DataType.VARIABLE;

    public DataNode Value { get; set; }

    public override DataNode Evaluate() => Value;

    public static DataVariable Var(string name)
    {
      DataVariable ret;
      if (!vars.TryGetValue(name, out ret))
        vars.Add(name, ret = new DataVariable(name, new DataAtom(0)));
      return ret;
    }

    /// <summary>
    /// Makes a data variable. Scoping not implemented because we're not an interpreter.
    /// Don't give this the $.
    /// </summary>
    /// <param name="name"></param>
    private DataVariable(string name, DataNode value)
    {
      Name = "$" + name;
      Value = value;
    }
  }

  public class DataSymbol : DataNode
  {
    static Dictionary<string, DataSymbol> symbols = new Dictionary<string, DataSymbol>();

    public static DataSymbol Symbol(string value)
    {
      DataSymbol ret;
      if (!symbols.TryGetValue(value, out ret))
        symbols.Add(value, ret = new DataSymbol(value));
      return ret;
    }

    public override string Name => value;
    public override DataNode Evaluate() => this;

    public override DataType Type => DataType.SYMBOL;

    private string value;
    private bool quote;

    private DataSymbol(string value)
    {
      this.value = value;
      foreach(var c in value)
      {
        // TODO: Is this right?
        if(c == ' ' || c == '\r' || c == '\n' || c == '\t'
          || c == '(' || c == ')' || c == '{' || c == '}'
          || c == '[' || c == ']')
        {
          quote = true;
          break;
        }
      }
    }

    public override string ToString() => quote ? $"'{Name}'" : Name;
  }
}
