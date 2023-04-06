using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Mackiloha.DTB
{
    public class DTBFile
    {
        public DTBFile()
        {
            Items = new ParentItem(ParentType.Default);
            Embedded = false;
            Encoding = DTBEncoding.Classic;
        }

        public static DTBFile FromFile(string path, DTBEncoding encoding)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                using (AwesomeReader ar = new AwesomeReader(fs))
                {
                    return FromStream(ar, encoding);
                }
            }
        }

        public void SaveToFile(string path)
        {
            using (FileStream fs = File.OpenWrite(path))
            {
                using (AwesomeWriter aw = new AwesomeWriter(fs))
                {
                    SaveToStream(aw);
                }
            }
        }

        public void SaveToStream(AwesomeWriter aw)
        {
            var version = Embedded ? 0 : 1;

            aw.Write((byte)1); // Always 1

            if (Encoding == DTBEncoding.FME)
                aw.Write((int)0); // Always 0

            if (Encoding != DTBEncoding.RBVR)
            {
                aw.Write((short)Items.Count);
                aw.Write((int)version);
            }
            else
            {
                aw.Write((int)version);
                aw.Write((short)Items.Count);
                aw.Write((short)1); // Always 1?
            }

            int lineNumber = 1;
            foreach (DTBItem item in Items)
            {
                WriteItem(aw, item, ref lineNumber);
            }
        }

        private void WriteItem(AwesomeWriter aw, DTBItem item, ref int lineNumber)
        {
            if (item is IntegerItem)
            {
                aw.Write((int)item.NumericValue);
                aw.Write((int)((IntegerItem)item).Integer);
            }
            else if (item is FloatItem)
            {
                aw.Write((int)item.NumericValue);
                aw.Write((float)((FloatItem)item).Float);
            }
            else if (item is StringItem)
            {
                aw.Write((int)item.NumericValue);
                aw.Write((string)((StringItem)item).String);
            }
            else if (item is ParentItem)
            {
                aw.Write((int)item.NumericValue);

                ParentItem parent = item as ParentItem;
                
                switch (Encoding)
                {
                    default:
                    case DTBEncoding.Classic:
                        // 6 bytes (2 count, 4 id)
                        aw.Write((short)parent.Count);
                        aw.Write((int)lineNumber);
                        break;
                    case DTBEncoding.FME:
                        // TODO: Verify unknown value is constant

                        // 10 bytes (4 unk, 4 count, 2 id)
                        aw.Write((int)1);
                        aw.Write((int)parent.Count);
                        aw.Write((short)lineNumber);
                        break;
                    case DTBEncoding.RBVR:
                        // 8 bytes (4 unk, 2 count, 2 id)
                        aw.Write((int)1);
                        aw.Write((short)parent.Count);
                        aw.Write((short)lineNumber);
                        break;
                }

                lineNumber++;
                foreach (DTBItem sub in parent)
                {
                    WriteItem(aw, sub, ref lineNumber);
                }
            }
            else throw new Exception("Invalid DTB item type!");

        }

        public static DTBFile FromStream(AwesomeReader ar, DTBEncoding encoding)
        {
            byte firstByte = ar.ReadByte();
            if (firstByte != 1) throw new Exception("Invalid first DTB byte. Expected 1.");

            short itemCount;
            int version;

            DTBFile dtb = new DTBFile();
            dtb.Encoding = encoding;

            if (encoding == DTBEncoding.FME)
                ar.ReadInt32(); // Always 0

            if (encoding != DTBEncoding.RBVR)
            {
                itemCount = ar.ReadInt16();
                version = ar.ReadInt32(); // Reads version. Should be either 0 or 1
            }
            else
            {
                version = ar.ReadInt32(); // Reads version. Should be either 0 or 1
                itemCount = ar.ReadInt16();
                ar.ReadInt16(); // Unknown, always 1?
            }
            
            if (version == 0) dtb.Embedded = true;
            else if (version == 1) dtb.Embedded = false; // Much more common
            //else throw new Exception("Invalid DTB version. Expected 0 or 1.");

            for (int i = 0; i < itemCount; i++)
            {
                ParseItem(ar, dtb.Items, encoding);
            }

            return dtb;
        }

        private static void ParseItem(AwesomeReader ar, ParentItem parent, DTBEncoding encoding)
        {
            int itemID = ar.ReadInt32();

            DTBItem item;
            switch (itemID)
            {
                // Integer
                case 0x00:
                    item = new IntegerItem(ar.ReadInt32());
                    break;
                // Float
                case 0x01:
                    item = new FloatItem(ar.ReadSingle());
                    break;
                // String
                case 0x02: // Variable
                case 0x04: // MiloEmbedded
                case 0x05: // Keyword
                case 0x06: // KDataUnhandled
                case 0x07: // IfNDef
                case 0x08: // Else
                case 0x09: // EndIf
                case 0x12: // Default
                case 0x20: // Define
                case 0x21: // Include
                case 0x22: // Merge
                case 0x23: // IfNDef
                case 0x24: // Mysterious24
                    item = new StringItem((StringType)itemID, ar.ReadString());
                    break;
                case 0x10: // Default
                case 0x11: // Script
                case 0x13: // Property
                    item = new ParentItem((ParentType)itemID);

                    short itemCount;

                    if (encoding == DTBEncoding.Classic)
                    {
                        itemCount = ar.ReadInt16();
                        ar.ReadInt32(); // Reads line number
                    }
                    else // Fantasia / RBVR
                    {
                        ar.ReadInt32(); // Unknown (RBVR - Always 1)
                        itemCount = (encoding == DTBEncoding.RBVR) ? ar.ReadInt16() : (short)ar.ReadInt32();
                        ar.ReadInt16();
                    }

                    for (int i = 0; i < itemCount; i++)
                    {
                        // Recursion: Like a boss
                        ParseItem(ar, item as ParentItem, encoding);
                    }

                    break;
                default:
                    throw new Exception(string.Format("Unknown chunk: {0} at {1}", itemID, ar.BaseStream.Position));
            }

            parent.Add(item);
        }

        public bool Embedded { get; set; }

        public ParentItem Items { get; set; }
        public DTBEncoding Encoding { get; set; }

        public override string ToString()
        {
            return DTAParsing.ExportToDTA(this);
        }
    }
}
