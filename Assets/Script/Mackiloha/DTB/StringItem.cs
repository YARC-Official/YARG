using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.DTB
{
    public class StringItem : DTBItem
    {
        private StringType _stringType;

        public StringItem(StringType type) : this(type, "")
        {

        }

        public StringItem(StringType type, string value)
        {
            if (!IsStringTypeTypeValid(type)) throw new Exception("Invalid string type!");

            _stringType = type;
            String = value;
        }

        private bool IsStringTypeTypeValid(StringType type)
        {
            switch (type)
            {
                case StringType.Default:
                case StringType.Define:
                case StringType.Else:
                case StringType.EndIf:
                case StringType.IfDef:
                case StringType.IfNDef:
                case StringType.Include:
                case StringType.KDataUnhandled:
                case StringType.Keyword:
                case StringType.Merge:
                case StringType.MiloEmbedded:
                case StringType.Mysterious24:
                case StringType.Variable:
                    return true;
                default:
                    return false;
            }
        }

        public string String { get; set; }

        public StringType StringType
        {
            get { return _stringType; }
            set
            {
                if (IsStringTypeTypeValid(value)) _stringType = value;
                else throw new Exception("Invalid string type!");
            }
        }

        public int NumericValue
        {
            get { return (int)_stringType; }
        }
    }

    public enum StringType : int
    {
        /// <summary>
        /// Variable "$value"
        /// </summary>
        Variable = 0x02,
        /// <summary>
        /// Found in Milo files. Unknown use.
        /// </summary>
        MiloEmbedded = 0x04,
        /// <summary>
        /// Keyword (Unquoted text)
        /// </summary>
        Keyword = 0x05,
        /// <summary>
        /// KDataUnhandled Marker "kDataUnhandled"
        /// </summary>
        KDataUnhandled = 0x06,
        /// <summary>
        /// IfDef Directive "#ifdef"
        /// </summary>
        IfDef = 0x07,
        /// <summary>
        /// Else Directive "#else"
        /// </summary>
        Else = 0x08,
        /// <summary>
        /// EndIf Directive "#endif"
        /// </summary>
        EndIf = 0x09,
        /// <summary>
        /// Default String (Quoted text)
        /// </summary>
        Default = 0x12,
        /// <summary>
        /// Define Directive "#define"
        /// </summary>
        Define = 0x20,
        /// <summary>
        /// Include Directive "#include"
        /// </summary>
        Include = 0x21,
        /// <summary>
        /// Merge Directive "#merge"
        /// </summary>
        Merge = 0x22,
        /// <summary>
        /// IfNDef Directive "#ifndef"
        /// </summary>
        IfNDef = 0x23,
        /// <summary>
        /// I've got no idea...
        /// </summary>
        Mysterious24 = 0x24
    }
}
