using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.DTB
{
    public static class DTAParsing
    {
        public static string ExportToDTA(DTBFile dtbFile)
        {
            StringBuilder sb = new StringBuilder();

            foreach (DTBItem item in dtbFile.Items)
            {
                WriteItemToDTA4(sb, item, 0);
            }

            return sb.ToString();
        }

        private static void WriteItemToDTA2(StringBuilder sb, DTBItem item, int depth, bool newLine)
        {
            if (item is IntegerItem)
            {
                sb.Append(((IntegerItem)item).Integer);
            }
            else if (item is FloatItem)
            {
                sb.Append(((FloatItem)item).Float);
            }
            else if (item is StringItem)
            {
                StringItem str = item as StringItem;
                sb.Append(GetStringValue(str));

                // Appends new line if it's a directive
                switch (str.StringType)
                {
                    case StringType.IfDef:
                    case StringType.Else:
                    case StringType.EndIf:
                    case StringType.Include:
                    case StringType.Merge:
                    case StringType.IfNDef:
                        //sb.Append("\n");
                        newLine = true;
                        break;
                }
            }
            else if (item is ParentItem)
            {
                ParentItem parent = item as ParentItem;

                sb.Append(GetBracket(parent, true));

                if (parent.Count < 3 || parent.ContainsSameItems)
                {
                    for (int i = 0; i < parent.Count; i++)
                    {
                        if (i != 0) sb.Append(" ");

                        if (i != parent.Count - 1) WriteItemToDTA(sb, parent[i], depth, false);
                        else WriteItemToDTA(sb, parent[i], depth, true);
                    }
                }
                else
                {
                    depth++;
                    
                    for (int i = 0; i < parent.Count; i++)
                    {
                        if (i != 0) for (int ii = 0; ii < depth; ii++) sb.Append("\t");

                        WriteItemToDTA(sb, parent[i], depth, true);
                    }

                }

                sb.Append(GetBracket(parent, false));
                //if (depth == 0) newLine = true;

            }
            else throw new Exception("Invalid DTB item type!");

            if (newLine) sb.Append("\n");
        }

        private static void WriteItemToDTA(StringBuilder sb, DTBItem item, int depth, bool root)
        {
            bool newLine = false;

            if (item is IntegerItem)
            {
                sb.Append(((IntegerItem)item).Integer);
            }
            else if (item is FloatItem)
            {
                sb.Append(((FloatItem)item).Float);
            }
            else if (item is StringItem)
            {
                StringItem str = item as StringItem;
                sb.Append(GetStringValue(str));

                // Appends new line
                switch (str.StringType)
                {
                    case StringType.IfDef:
                    case StringType.Else:
                    case StringType.EndIf:
                    case StringType.Include:
                    case StringType.Merge:
                    case StringType.IfNDef:
                        //sb.Append("\n");
                        newLine = true;
                        break;
                }
            }
            else if (item is ParentItem)
            {
                ParentItem parent = item as ParentItem;

                sb.Append(GetBracket(parent, true));

                bool firstItem = true;
                if (parent.Count < 3 || parent.ContainsSameItems)
                {
                    foreach (DTBItem sub in parent)
                    {
                        if (!firstItem) sb.Append(" ");
                        WriteItemToDTA(sb, sub, depth, false);
                        firstItem = false;
                    }

                    if (depth == 0 && root) newLine = true;

                    //for (int i = 0; i < parent.Count; i++)
                    //{
                    //    if (i != 0) sb.Append(" ");
                    //    WriteItemToDTA(sb, parent[i], depth);

                    //    if (i != parent.Count - 1) sb.Append("\n");
                    //}
                }
                else
                {
                    newLine = true;
                    depth++;

                    //foreach (DTBItem sub in parent)
                    //{
                    //    if (!firstItem) for (int i = 0; i < depth; i++) sb.Append("\t");
                    //    WriteItemToDTA(sb, sub, depth);

                    //    sb.Append("\n");
                    //    firstItem = false;
                    //}

                    for (int i = 0; i < parent.Count; i++)
                    {
                        if (i != 0) for (int ii = 0; ii < depth; ii++) sb.Append("\t");
                        WriteItemToDTA(sb, parent[i], depth, false);

                        if (i != parent.Count - 1) sb.Append("\n");
                        else
                        {
                            int y = 9;
                        }
                    }


                }

                sb.Append(GetBracket(parent, false));
                //if (depth == 0) newLine = true;

            }
            else throw new Exception("Invalid DTB item type!");

            if (newLine) sb.Append("\n");
        }

        private static void WriteItemToDTA3(StringBuilder sb, DTBItem item, int depth, bool root)
        {
            bool newLine = false;

            if (item is IntegerItem)
            {
                sb.Append(((IntegerItem)item).Integer);
            }
            else if (item is FloatItem)
            {
                sb.Append(((FloatItem)item).Float);
            }
            else if (item is StringItem)
            {
                StringItem str = item as StringItem;
                sb.Append(GetStringValue(str));

                // Appends new line
                switch (str.StringType)
                {
                    case StringType.IfDef:
                    case StringType.Else:
                    case StringType.EndIf:
                    case StringType.Include:
                    case StringType.Merge:
                    case StringType.IfNDef:
                        //sb.Append("\n");
                        newLine = true;
                        break;
                }

                if (newLine) sb.Append("\r\n");
            }
            else if (item is ParentItem)
            {
                ParentItem parent = item as ParentItem;

                sb.Append(GetBracket(parent, true));

                bool firstItem = true;
                if (parent.Count < 3)
                {
                    foreach (DTBItem sub in parent)
                    {
                        if (!firstItem) sb.Append(" ");
                        WriteItemToDTA(sb, sub, depth, false);
                        firstItem = false;
                    }

                    if (depth == 0 && root) newLine = true;

                    //for (int i = 0; i < parent.Count; i++)
                    //{
                    //    if (i != 0) sb.Append(" ");
                    //    WriteItemToDTA(sb, parent[i], depth);

                    //    if (i != parent.Count - 1) sb.Append("\n");
                    //}
                }
                else
                {
                    newLine = true;
                    depth++;

                    //foreach (DTBItem sub in parent)
                    //{
                    //    if (!firstItem) for (int i = 0; i < depth; i++) sb.Append("\t");
                    //    WriteItemToDTA(sb, sub, depth);

                    //    sb.Append("\n");
                    //    firstItem = false;
                    //}

                    for (int i = 0; i < parent.Count; i++)
                    {
                        //  Adds tabs
                        if (i != 0) for (int ii = 0; ii < depth; ii++) sb.Append("\t");
                        WriteItemToDTA(sb, parent[i], depth, false);
                        
                        if (i != parent.Count - 1) sb.Append("\r\n");
                        else
                        {
                            int y = 9;
                        }
                    }


                }

                sb.Append(GetBracket(parent, false));
                //if (depth == 0) newLine = true;

            }
            else throw new Exception("Invalid DTB item type!");

            if (newLine) sb.Append("\r\n");
        }

        private static void WriteItemToDTA4(StringBuilder sb, DTBItem item, int depth)
        {
            if (item is IntegerItem)
            {
                sb.Append(((IntegerItem)item).Integer);
            }
            else if (item is FloatItem)
            {
                sb.Append(((FloatItem)item).Float);
            }
            else if (item is StringItem)
            {
                StringItem str = item as StringItem;

                // Appends new line
                switch (str.StringType)
                {
                    case StringType.IfDef:
                    case StringType.Else:
                    case StringType.EndIf:
                    case StringType.Include:
                    case StringType.Merge:
                    case StringType.IfNDef:
                        sb.Append("\r\n");
                        break;
                }

                sb.Append(GetStringValue(str));

                // Appends new line
                switch (str.StringType)
                {
                    case StringType.IfDef:
                    case StringType.Else:
                    case StringType.EndIf:
                    case StringType.Include:
                    case StringType.Merge:
                    case StringType.IfNDef:
                        sb.Append("\r\n");
                        break;
                }
            }
            else if (item is ParentItem)
            {
                ParentItem parent = item as ParentItem;

                sb.Append(GetBracket(parent, true));
                bool newLine = (parent.Count > 2)
                    || (parent.Count > 0 && parent.ContainsSameItems && parent[0] is ParentItem);
                
                for (int i = 0; i < parent.Count; i++)
                {
                    if (newLine && i != 0)
                    {
                        sb.Append("\r\n");
                        for (int ii = 0; ii < depth + 1; ii++) sb.Append("\t");
                    }
                    else if (i != 0)
                    {
                        sb.Append(" ");
                    }

                    // Writes items values
                    WriteItemToDTA4(sb, parent[i], depth + 1);
                }
                
                sb.Append(GetBracket(parent, false));
                if (depth == 0) sb.Append("\r\n");
            }
            else throw new Exception("Invalid DTB item type!");
        }


        private static string GetStringValue(StringItem str)
        {
            switch (str.StringType)
            {
                case StringType.Default:
                    return "\"" + str.String.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r") + "\"";
                case StringType.Define:
                    return "#define " + str.String;
                case StringType.Else:
                    return "#else";
                case StringType.EndIf:
                    return "#endif";
                case StringType.IfDef:
                    return "#ifdef " + str.String;
                case StringType.IfNDef:
                    return "#ifndef " + str.String;
                case StringType.Include:
                    return "#include " + str.String;
                case StringType.KDataUnhandled:
                    return "kDataUnhandled";
                case StringType.Keyword:
                    return str.String;
                case StringType.Merge:
                    return "#merge " + str.String;
                case StringType.MiloEmbedded:
                    return "#0x04 ";
                case StringType.Mysterious24:
                    return "#0x24 ";
                case StringType.Variable:
                    return "$" + str.String;
                default:
                    break;
            }

            return "";
        }

        private static string GetBracket(ParentItem parent, bool start)
        {
            switch (parent.ParentType)
            {
                case ParentType.Default:
                    return start ? "(" : ")";
                case ParentType.Script:
                    return start ? "{" : "}";
                case ParentType.Property:
                    return start ? "[" : "]";
                default:
                    return "";
            }
        }
    }
}
