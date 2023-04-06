using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.Milo2
{
    public enum BlockStructure : uint
    {
        /// <summary>
        /// Raw data. Uncompressed.
        /// </summary>
        NONE = 0,
        /// <summary>
        /// Raw data. Compressed with GZip.
        /// <para>*FreQ</para>
        /// </summary>
        GZIP = 1,
        /// <summary>
        /// Structured as milo. No compression.
        /// <para>*RBN</para>
        /// </summary>
        MILO_A = 0xCABEDEAF,
        /// <summary>
        /// Structured as milo. Compressed with ZLib.
        /// <para>*GH1</para>
        /// <para>*GH2</para>
        /// <para>*GH80's</para>
        /// <para>*RB1</para>
        /// <para>*RBTP Vol. 1</para>
        /// <para>*RB2</para>
        /// <para>*ACDC RB</para>
        /// <para>*RBTB Vol. 2</para>
        /// <para>*RBTP Classic Rock</para>
        /// <para>*RBTP Country</para>
        /// <para>*TBRB</para>
        /// <para>*RBTP Metal</para>
        /// <para>*LRB</para>
        /// <para>*GDRB</para>
        /// <para>*RBTP Country 2</para>
        /// </summary>
        MILO_B = 0xCBBEDEAF,
        /// <summary>
        /// Structured as milo. Compressed with GZip.
        /// <para>*Amp</para>
        /// <para>*KR1</para>
        /// <para>*KR2</para>
        /// <para>*KR3</para>
        /// </summary>
        MILO_C = 0xCCBEDEAF,
        /// <summary>
        /// Structured as milo. Compressed with ZLib.
        /// <para>*RB3</para>
        /// <para>*DC1</para>
        /// <para>*DC2</para>
        /// <para>*RBB</para>
        /// <para>*DC3</para>
        /// </summary>
        MILO_D = 0xCDBEDEAF
    }
}
