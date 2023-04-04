using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Model;
using UnityEngine;
using YARG.Data;

namespace YARG.Serialization {
    public static class SongsDta {
		static Regex dtaMatchRegex = new Regex(@"\(.*\b\n");
    }
}