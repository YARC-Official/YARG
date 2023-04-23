using System;
using System.Globalization;
using System.Text;
using DtxCS.DataTypes;

namespace DtxCS
{
  /// <summary>
  /// Represents a .dta/.dtx file.
  /// </summary>
  public static class DTX
  {
    /// <summary>
    /// Parses a plaintext DTA given its bytes in a byte array. If an encoding tag is set, tries to use the correct encoding.
    /// </summary>
    /// <param name="dtaBytes">A byte array containing binary DTB data</param>
    /// <returns>DataArray with parsed contents of the given data</returns>
    public static DataArray FromPlainTextBytes(byte[] dtaBytes)
    {
      DataArray dta;
      try
      {
        dta = FromDtaString(Encoding.GetEncoding(1252).GetString(dtaBytes));
      }
      catch (Exception)
      {
        return FromDtaString(Encoding.UTF8.GetString(dtaBytes));
      }
      bool utf8 = false;
      foreach (DataNode d in dta.Children)
      {
        if (d != null && d is DataArray && ((DataArray)d).Array("encoding") != null && ((DataArray)d).Array("encoding").Children[1].Name == "utf8")
        {
          utf8 = true;
          break;
        }
      }
      if (utf8)
      {
        dta = FromDtaString(Encoding.UTF8.GetString(dtaBytes));
      }
      return dta;
    } //FromPlainTextBytes

    /// <summary>
    /// Parses the entirety of a .dta file in plain text into a DataArray.
    /// </summary>
    /// <param name="data"></param>
    public static DataArray FromDtaString(string data)
    {
      DataArray root = new DataArray();
      ParseString(data, root);
      return root;
    }

    /// <summary>
    /// Parses the entirety of a .dta file in a stream to a DataArray.
    /// </summary>
    /// <param name="data">Stream containing DTA data</param>
    /// <returns>DataArray with parsed contents of the given data</returns>
    public static DataArray FromDtaStream(System.IO.Stream data)
      => FromDtaString(new UTF8Encoding(false).GetString(data.ReadBytes((int)data.Length)));

    enum ParseState
    {
      whitespace,
      in_string,
      in_literal,
      in_symbol,
      in_comment,
      in_directive,
      in_constant
    }

    // Helper function for parsing #define directives.
    private static int ParseDefine(string data, DataArray root)
    {
      int parsedCharacters = 0;
      int layersDeep = 0;
      int start = 0;
      for (parsedCharacters = 0; parsedCharacters < data.Length; parsedCharacters++)
      {
        switch (data[parsedCharacters])
        {
          case '(':
            if (layersDeep == 0) start = parsedCharacters + 1;
            layersDeep++;
            break;
          case ')':
            layersDeep--;
            if (layersDeep == 0) goto DoneParsing;
            break;
        }
      }
      DoneParsing:
      if (layersDeep != 0)
      {
        throw new Exception("Mismatching brackets in parsing #define directive.");
      }
      ParseString(data.Substring(start, parsedCharacters - start), root);
      return parsedCharacters;
    }

    /// <summary>
    /// Parses the string as DTA elements, adding each one to the given root array.
    /// </summary>
    /// <param name="data">string of DTA info</param>
    /// <param name="root">top-level array to add the string to</param>
    private static void ParseString(string data, DataArray root)
    {
      ParseState state = ParseState.whitespace;
      data += " "; // this ensures we parse the whole string...
      DataArray current = root;
      string tmp_literal = "";
      string tmp_directive = "";
      string tmp_constant = "";
      int line = 1;
      for (int i = 0; i < data.Length; i++)
      {
        if (data[i] == '\uFEFF') continue;
        if (data[i] == '\n') line++;
        switch (state)
        {
          case ParseState.whitespace:
            switch (data[i])
            {
              case '\'':
                tmp_literal = "";
                state = ParseState.in_symbol;
                break;
              case '"':
                tmp_literal = "";
                state = ParseState.in_string;
                break;
              case ';':
                tmp_literal = "";
                state = ParseState.in_comment;
                break;
              case ' ':
              case '\r':
              case '\n':
              case '\t':
                continue;
              case '}':
              case ')':
              case ']':
                if (data[i] != current.ClosingChar || current.Parent == null)
                {
                  throw new Exception($"Mismatched closing brace encountered at line {line}.");
                }
                current = current.Parent;
                break;
              case '(':
                current = (DataArray)current.AddNode(new DataArray());
                break;
              case '{':
                current = (DataArray)current.AddNode(new DataCommand());
                break;
              case '[':
                current = (DataArray)current.AddNode(new DataMacroDefinition());
                break;
              case '#':
                state = ParseState.in_directive;
                tmp_directive = "";
                break;
              default:
                state = ParseState.in_literal;
                tmp_literal = new string(data[i], 1);
                continue;
            }
            break;
          case ParseState.in_directive:
            switch (data[i])
            {
              case ' ':
              case '\t':
              case '\r':
              case '\n':
                switch(tmp_directive)
                {
                  case "else":
                    current.AddNode(new DataElse());
                    state = ParseState.whitespace;
                    break;
                  case "endif":
                    current.AddNode(new DataEndIf());
                    state = ParseState.whitespace;
                    break;
                  case "autorun":
                    current.AddNode(new DataAutorun());
                    state = ParseState.whitespace;
                    break;
                  default:
                    state = ParseState.in_constant;
                    tmp_constant = "";
                    break;
                }
                break;
              default:
                tmp_directive += data[i];
                continue;
            }
            break;
          case ParseState.in_constant:
            switch (data[i])
            {
              case ' ':
              case '\t':
              case '\r':
              case '\n':
              case ')':
              case '}':
              case ']':
                //Console.WriteLine($"{tmp_directive} {tmp_constant}");
                switch (tmp_directive)
                {
                  case "define":
                    DataArray def = new DataArray();
                    i += ParseDefine(data.Substring(i), def);
                    current.AddNode(new DataDefine(tmp_constant, def));
                    break;
                  case "ifdef":
                    current.AddNode(new DataIfDef(tmp_constant));
                    break;
                  case "ifndef":
                    current.AddNode(new DataIfNDef(tmp_constant));
                    break;
                  case "include":
                    current.AddNode(new DataInclude(tmp_constant));
                    break;
                  case "merge":
                    current.AddNode(new DataMerge(tmp_constant));
                    break;
                  case "undef":
                    current.AddNode(new DataUndef(tmp_constant));
                    break;
                  default:
                    Console.WriteLine($"Unknown directive {tmp_directive}, constant {tmp_constant}");
                    AddLiteral(current, tmp_directive);
                    AddLiteral(current, tmp_constant);
                    break;
                }
                state = ParseState.whitespace;
                break;
              default:
                tmp_constant += data[i];
                continue;
            }
            break;
          case ParseState.in_string:
            switch (data[i])
            {
              case '"':
                current.AddNode(new DataAtom(tmp_literal));
                state = ParseState.whitespace;
                break;
              default:
                tmp_literal += data[i];
                continue;
            }
            break;
          case ParseState.in_literal:
            switch (data[i])
            {
              case ' ':
              case '\r':
              case '\n':
              case '\t':
                AddLiteral(current, tmp_literal);
                state = ParseState.whitespace;
                break;
              case '}':
              case ')':
              case ']':
                AddLiteral(current, tmp_literal);
                if (data[i] != current.ClosingChar)
                {
                  throw new Exception("Mismatched brace types encountered.");
                }
                current = current.Parent;
                state = ParseState.whitespace;
                break;
              case '(':
                AddLiteral(current, tmp_literal);
                current = (DataArray)current.AddNode(new DataArray());
                break;
              case '{':
                AddLiteral(current, tmp_literal);
                current = (DataArray)current.AddNode(new DataCommand());
                break;
              case '[':
                AddLiteral(current, tmp_literal);
                current = (DataArray)current.AddNode(new DataMacroDefinition());
                break;
              default:
                tmp_literal += data[i];
                continue;
            }
            break;
          case ParseState.in_symbol:
            switch (data[i])
            {
              case '\r':
              case '\n':
              case '\t':
                throw new Exception("Whitespace encountered in symbol.");
              case '}':
              case ')':
              case ']':
                current.AddNode(DataSymbol.Symbol(tmp_literal));
                if (data[i] != current.ClosingChar)
                {
                  throw new Exception("Mismatched brace types encountered.");
                }
                current = current.Parent;
                state = ParseState.whitespace;
                break;
              case '\'':
                current.AddNode(DataSymbol.Symbol(tmp_literal));
                state = ParseState.whitespace;
                break;
              default:
                tmp_literal += data[i];
                continue;
            }
            break;
          case ParseState.in_comment:
            switch (data[i])
            {
              case '\r':
              case '\n':
                state = ParseState.whitespace;
                break;
              default:
                continue;
            }
            break;
        }
      }
    }

    private static void AddLiteral(DataArray current, string tmp_literal)
    {
      int tmp_int;
      float tmp_float;
      if (int.TryParse(tmp_literal, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out tmp_int))
      {
        current.AddNode(new DataAtom(tmp_int));
      }
      else if (float.TryParse(tmp_literal, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out tmp_float))
      {
        current.AddNode(new DataAtom(tmp_float));
      }
      else if (tmp_literal[0] == '$')
      {
        current.AddNode(DataVariable.Var(tmp_literal.Substring(1)));
      }
      else
      {
        current.AddNode(DataSymbol.Symbol(tmp_literal));
      }
    }

    /// <summary>
    /// Parses a binary format (dtb) file.
    /// </summary>
    /// <param name="dtb">A stream of binary DTB data</param>
    /// <returns>DataArray with parsed contents of the given data</returns>
    public static DataArray FromDtb(System.IO.Stream dtb)
    {
      DataArray root;
      dtb.Position = 0;
      if (dtb.ReadUInt8() != 0x01)
      {
        dtb.Position = 0;
        dtb = new CryptStream(dtb);
        if (dtb.ReadUInt8() != 0x01)
        {
          throw new Exception("DTB contained unrecognized header.");
        }
      }
      uint rootNodes = dtb.ReadUInt16LE();
      dtb.Position = 1;
      uint rootNodes2 = dtb.ReadUInt32LE();
      dtb.Position = 3;
      if (rootNodes == 0)
      {
        dtb.Position = 5;
        rootNodes = dtb.ReadUInt32LE();
        uint unk = dtb.ReadUInt16LE();
        root = parse_children(dtb, rootNodes, DataType.ARRAY, 2);
      }
      else if (rootNodes == rootNodes2)
      {
        dtb.Position = 5;
        rootNodes = dtb.ReadUInt16LE();
        uint unk = dtb.ReadUInt16LE();
        root = parse_children(dtb, rootNodes, DataType.ARRAY, 3);
      }
      else
      {
        dtb.ReadInt32LE(); // unknown, always = 1
        root = parse_children(dtb, rootNodes, DataType.ARRAY, 1);
      }

      return root;
    }

    /// <summary>
    /// Parses a binary format (dtb) file and returns the version and encryption state.
    /// </summary>
    /// <param name="dtb">A stream of binary DTB data</param>
    /// <param name="encrypted">A reference to a boolean to return encryption state of the passed DTB</param>
    /// <returns>The version of the passed DTB</returns>
    public static int DtbVersion(System.IO.Stream dtb, ref bool encrypted)
    {
      dtb.Position = 0;
      encrypted = false;
      if (dtb.ReadUInt8() != 0x01)
      {
        dtb.Position = 0;
        dtb = new CryptStream(dtb);
        if (dtb.ReadUInt8() != 0x01)
          throw new Exception("DTB contained unrecognized header.");
        encrypted = true;
      }
      uint rootNodes = dtb.ReadUInt16LE();
      dtb.Position = 1;
      uint rootNodes2 = dtb.ReadUInt32LE();
      dtb.Position = 3;
      if (rootNodes == 0)
        return 2;
      else if (rootNodes == rootNodes2)
        return 3;
      else
        return 1;
    }

    /// <summary>
    /// Converts a DataArray to a binary format (DTB) file.
    /// </summary>
    /// <param name="arr">The DataArray to convert to DTB.</param>
    /// <param name="dtb">The stream to write the DTB to.</param>
    /// <param name="version">The version of DTB to write.</param>
    /// <param name="crypt">Whether to encrypt the DTB.</param>
    /// <returns>Long integer of bytes written to the stream.</returns>
    public static long ToDtb(DataArray arr, System.IO.Stream dtb, int version = 3, bool crypt = false)
    {
      if (crypt) dtb = new CryptStream(dtb, true);
      dtb.Position = 0;
      dtb.WriteUInt8(0x01);
      // skip to the correct position to write nodes
      if (version == 2)
        dtb.Position = 11;
      else if (version == 3)
        dtb.Position = 9;
      else if (version == 1)
        dtb.Position = 7;
      else
        throw new Exception($"Invalid DTB version specified (got {version}, expected 1, 2 or 3)");
      // write the root nodes to the stream
      int rootnodesToAdd = write_children(arr, dtb, arr.Count, version);
      long length = dtb.Position;
      // write the header with correct root node count
      dtb.Position = 1;
      if (version == 2)
      {
        dtb.WriteInt32LE(0); //unknown
        dtb.WriteInt32LE(arr.Count + rootnodesToAdd); //rootNodes
        dtb.WriteInt16LE(0); //unknown
      }
      else if (version == 3)
      {
        dtb.WriteInt32LE(1); //unknown
        dtb.WriteInt16LE((short)(arr.Count + rootnodesToAdd)); //rootNodes
        dtb.WriteInt16LE(1); //unknown
      }
      else if (version == 1)
      {
        dtb.WriteInt16LE((short)(arr.Count + rootnodesToAdd)); //rootNodes
        dtb.WriteInt32LE(1); //unknown
      }
      return length;
    }

    static int write_children(DataArray dta, System.IO.Stream dtb, int numChildren, int version = 1)
    {
      int rootnodesToAdd = 0;
      for (int i = 0; i < numChildren; i++)
      {
        DataType t = dta.Children[i].Type;
        dtb.WriteInt32LE((int)t);
        switch (t)
        {
          case DataType.INT:
            dtb.WriteInt32LE(((DataAtom)dta.Children[i]).Int);
            break;
          case DataType.FLOAT:
            dtb.WriteFloat(((DataAtom)dta.Children[i]).Float);
            break;
          case DataType.VARIABLE:
            dtb.WriteLengthUTF8(((DataVariable)dta.Children[i]).Name.Substring(1));
            break;
          case DataType.SYMBOL:
            dtb.WriteLengthUTF8(((DataSymbol)dta.Children[i]).Name);
            break;
          case DataType.ARRAY:
          case DataType.COMMAND:
          case DataType.MACRO:
            if (version == 2)
            {
              dtb.WriteInt32LE(0); //unknown
              dtb.WriteInt32LE(((DataArray)dta.Children[i]).Count); //rootNodes
              dtb.WriteInt16LE(0); //unknown
            }
            else if (version == 3)
            {
              dtb.WriteInt32LE(1); //unknown
              dtb.WriteInt16LE((short)((DataArray)dta.Children[i]).Count); //rootNodes
              dtb.WriteInt16LE(1); //unknown
            }
            else if (version == 1)
            {
              dtb.WriteInt16LE((short)((DataArray)dta.Children[i]).Count); //rootNodes
              dtb.WriteInt32LE(1); //unknown
            }
            write_children((DataArray)(dta.Children[i]), dtb, ((DataArray)dta.Children[i]).Count, version);
            break;
          case DataType.STRING:
            dtb.WriteLengthUTF8(((DataAtom)dta.Children[i]).String);
            break;
          case DataType.EMPTY:
            dtb.WriteUInt32LE(0);
            break;
          case DataType.IFDEF:
          case DataType.IFNDEF:
          case DataType.ELSE:
          case DataType.ENDIF:
          case DataType.INCLUDE:
          case DataType.MERGE:
          case DataType.AUTORUN:
          case DataType.UNDEF:
            // directives
            if (((DataDirective)dta.Children[i]).Constant != null)
              dtb.WriteLengthUTF8(((DataDirective)dta.Children[i]).Constant);
            else
              dtb.WriteUInt32LE(0);
            break;
          case DataType.DEFINE:
            dtb.WriteLengthUTF8(((DataDefine)dta.Children[i]).Constant);
            DataArray parent = new DataArray();
            parent.AddNode(((DataDefine)dta.Children[i]).Definition);
            write_children(parent, dtb, 1, version);
            rootnodesToAdd++;
            break;
          default:
            throw new Exception($"Unhandled DTB DataType {Enum.GetName(typeof(DataType), t)} ({(uint)t}) in write_children");
        }
      }
      return rootnodesToAdd;
    }

    static DataArray parse_children(System.IO.Stream s, uint numChildren, DataType type = DataType.ARRAY, int version = 1)
    {
      DataArray ret = type == DataType.MACRO ? new DataMacroDefinition()
                            : type == DataType.COMMAND ? new DataCommand()
                            : new DataArray();
      while (numChildren-- > 0)
      {
        DataType t = (DataType)s.ReadInt32LE();
        switch (t)
        {
          case DataType.INT:
            ret.AddNode(new DataAtom(s.ReadInt32LE()));
            break;
          case DataType.FLOAT:
            ret.AddNode(new DataAtom(s.ReadFloat()));
            break;
          case DataType.VARIABLE:
            ret.AddNode(DataVariable.Var(s.ReadLengthUTF8()));
            break;
          case DataType.SYMBOL:
            ret.AddNode(DataSymbol.Symbol(s.ReadLengthUTF8()));
            break;
          case DataType.ARRAY:
          case DataType.COMMAND:
          case DataType.MACRO:
            if (version == 2)
            {
              s.Position += 4;
              uint nC = s.ReadUInt32LE();
              ushort unk = s.ReadUInt16LE();
              ret.AddNode(parse_children(s, nC, t, version));
            }
            else if (version == 3)
            {
              s.Position += 4;
              ushort nC = s.ReadUInt16LE();
              s.Position += 2;
              ret.AddNode(parse_children(s, nC, t, version));
            }
            else
            {
              ushort nC = s.ReadUInt16LE(); // numChildren
              s.Position += 4; // id
              ret.AddNode(parse_children(s, nC, t, version));
            }
            break;
          case DataType.STRING:
            ret.AddNode(new DataAtom(s.ReadLengthUTF8()));
            break;
          case DataType.EMPTY:
            s.Position += 4;
            break;
          case DataType.DEFINE:
            var constant = s.ReadLengthUTF8();
            numChildren--;
            var definition = parse_children(s, 1, DataType.ARRAY, version).Array(0);
            ret.AddNode(new DataDefine(constant, definition));
            break;
          case DataType.IFDEF:
            ret.AddNode(new DataIfDef(s.ReadLengthUTF8()));
            break;
          case DataType.IFNDEF:
            ret.AddNode(new DataIfNDef(s.ReadLengthUTF8()));
            break;
          case DataType.ELSE:
            s.Position += 4;
            ret.AddNode(new DataElse());
            break;
          case DataType.ENDIF:
            s.Position += 4;
            ret.AddNode(new DataEndIf());
            break;
          case DataType.INCLUDE:
            ret.AddNode(new DataInclude(s.ReadLengthUTF8()));
            break;
          case DataType.MERGE:
            ret.AddNode(new DataMerge(s.ReadLengthUTF8()));
            break;
          case DataType.AUTORUN:
            s.Position += 4;
            ret.AddNode(new DataAutorun());
            break;
          case DataType.UNDEF:
            ret.AddNode(new DataUndef(s.ReadLengthUTF8()));
            break;
          default:
            throw new Exception($"Unhandled DTB DataType {Enum.GetName(typeof(DataType), t)} ({(uint)t}) in parse_children");
        }
      }
      return ret;
    }
  }
}
