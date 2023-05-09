using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace YARG.Serialization {
    public static class XboxCONInnerFileRetriever {
        public static byte[] RetrieveFile(string CONname, string filename, uint filesize, uint[] fileOffsets){

            byte[] f = new byte[filesize];
            uint lastSize = filesize % 0x1000;

            Parallel.For(0, fileOffsets.Length, i => {
                uint ReadLen = (i == fileOffsets.Length - 1) ? lastSize : 0x1000;
                using var fs = new FileStream(CONname, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs, new ASCIIEncoding());
                fs.Seek(fileOffsets[i], SeekOrigin.Begin);
                Array.Copy(br.ReadBytes((int)ReadLen), 0, f, i*0x1000, (int)ReadLen);
            });

            return f;
        }
    }
}