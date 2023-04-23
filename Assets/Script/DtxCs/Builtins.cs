using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DtxCS.DataTypes;
namespace DtxCS
{
  /// <summary>
  /// Builtin functions, in case I end up writing an interpreter.
  /// </summary>
  static class Builtins
  {
    public static Dictionary<DataSymbol, Func<DataCommand, DataNode>> Funcs { get; }

    static Builtins()
    {
      Funcs = new Dictionary<DataSymbol, Func<DataCommand, DataNode>>();
      Funcs.Add(DataSymbol.Symbol("abs"),        Abs);
      Funcs.Add(DataSymbol.Symbol("+"),          Add);
      Funcs.Add(DataSymbol.Symbol("+="),         AddEq);
      Funcs.Add(DataSymbol.Symbol("&"),          BitAnd);
      Funcs.Add(DataSymbol.Symbol("append_str"), AppendStr);
      Funcs.Add(DataSymbol.Symbol("assign"),     Assign);
      Funcs.Add(DataSymbol.Symbol("clamp"),      Clamp);
      Funcs.Add(DataSymbol.Symbol("--"),         Dec);
      Funcs.Add(DataSymbol.Symbol("/"),          Divide);
      Funcs.Add(DataSymbol.Symbol("="),          Eq);
      Funcs.Add(DataSymbol.Symbol("++"),         Inc);
      Funcs.Add(DataSymbol.Symbol(">"),          Gt);
      Funcs.Add(DataSymbol.Symbol("<"),          Lt);
      Funcs.Add(DataSymbol.Symbol("-"),          Subtract);
      Funcs.Add(DataSymbol.Symbol("if"),         If);
    }
    static DataNode Abs(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(Math.Abs(args.Number(1)));
    }
    static DataNode Add(DataCommand input)
    {
      var args = input.EvalAll();
      if(args.Node(1).Type == DataType.INT && args.Node(2).Type == DataType.INT)
      {
        return new DataAtom(args.Int(1) + args.Int(2));
      }
      return new DataAtom(args.Number(1) + args.Number(2));
    }
    static DataNode AddEq(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Number(1) + args.Number(2));
    }
    static DataNode BitAnd(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Int(1) & args.Int(2));
    }
    static DataNode AppendStr(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.String(1) + args.String(2));
    }
    static DataNode Assign(DataCommand input)
    {
      input.Var(1).Value = input.Node(2).Evaluate();
      return input.Var(1).Value;
    }
    static DataNode Clamp(DataCommand input)
    {
      var args = input.EvalAll();
      float f1 = args.Number(1), f2 = args.Number(2), f3 = args.Number(3);
      return new DataAtom(f1 > f3 ? f3 : f1 < f2 ? f1 : f2);
    }
    static DataNode Dec(DataCommand input)
    {
      DataAtom a = input.Var(1).Value as DataAtom;
      input.Var(1).Value = new DataAtom(a.Int - 1);
      return input.Var(1).Value;
    }
    static DataNode Divide(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Number(1) / args.Number(2));
    }
    static DataNode Eq(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Node(1) == args.Node(2) ? 1 : 0);
    }
    static DataNode Gt(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Number(1) > args.Number(2) ? 1 : 0);
    }
    static DataNode Inc(DataCommand input)
    {
      DataAtom a = input.Var(1).Value as DataAtom;
      input.Var(1).Value = new DataAtom(a.Int + 1);
      return input.Var(1).Value;
    }
    static DataNode Lt(DataCommand input)
    {
      var args = input.EvalAll();
      return new DataAtom(args.Number(1) < args.Number(2) ? 1 : 0);
    }
    static DataNode Subtract(DataCommand input)
    {
      var args = input.EvalAll();
      if (args.Node(1).Type == DataType.INT && args.Node(2).Type == DataType.INT)
      {
        return new DataAtom(args.Int(1) - args.Int(2));
      }
      return new DataAtom(args.Number(1) - args.Number(2));
    }

    static DataNode If(DataCommand input)
    {
      if ((input.Children[1].Evaluate() as DataAtom).Int == 0)
      {
        return input.Children[3].Evaluate();
      }
      return input.Children[2].Evaluate();
    }
  }
}
