using System;

namespace DtxCS.DataTypes
{

  public abstract class DataDirective : DataNode
  {
    public override string Name { get; }
    public override string ToString() => Name + " " + Constant;

    public override string ToString(int depth)
    {
      depth = depth <= 0 ? 1 : depth;
      return Environment.NewLine + Name + " " + Constant + Environment.NewLine + new string(' ',--depth*3);
    }
    public override DataType Type { get; }

    public override DataNode Evaluate() => this;

    public string Constant;

    internal DataDirective(string name, DataType type, string constant)
    {
      this.Name = name;
      this.Type = type;
      this.Constant = constant;
    }
  }

  public class DataIfDef : DataDirective
  {
    public DataIfDef(string constant) : base("#ifdef", DataType.IFDEF, constant) { }
  }
  public class DataDefine : DataDirective
  {
    public DataNode Definition;

    public DataDefine(string constant, DataNode definition) : base("#define", DataType.DEFINE, constant)
    {
      this.Definition = definition;
    }

    public override string ToString(int depth)
    {
      depth = depth <= 0 ? 1 : depth;
      return Environment.NewLine + Name + " " + Constant + " " + Definition.ToString() + Environment.NewLine + new string(' ', --depth * 3);
    }
  }
  public class DataIfNDef : DataDirective
  {
    public DataIfNDef(string constant) : base("#ifndef", DataType.IFNDEF, constant) { }
  }
  public class DataInclude : DataDirective
  {
    public DataInclude(string constant) : base("#include", DataType.INCLUDE, constant) { }
  }
  public class DataMerge : DataDirective
  {
    public DataMerge(string constant) : base("#merge", DataType.MERGE, constant) { }
  }
  public class DataElse : DataDirective
  {
    public DataElse() : base("#else", DataType.ELSE, null) { }
  }
  public class DataEndIf : DataDirective
  {
    public DataEndIf() : base("#endif", DataType.ENDIF, null) { }
  }
  public class DataAutorun : DataDirective
  {
    public DataAutorun() : base("#autorun", DataType.AUTORUN, null) { }
  }
  public class DataUndef : DataDirective
  {
    public DataUndef(string constant) : base("#undef", DataType.UNDEF, constant) { }
  }
}
