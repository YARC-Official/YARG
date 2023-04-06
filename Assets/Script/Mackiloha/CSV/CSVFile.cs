using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mackiloha.CSV
{
    public class CSVFile
    {
        private readonly string[] Header;
        private readonly string[][] Rows;

        private CSVFile(string[] header, string[][] rows)
        {
            Header = header;
            Rows = rows;
        }

        public static CSVFile FromForgeCSVFile(string path)
        {
            using var fs = File.OpenRead(path);
            return FromForgeCSVStream(fs);
        }

        public static CSVFile FromForgeCSVStream(Stream stream)
        {
            var ar = new AwesomeReader(stream);
            ar.BaseStream.Seek(9, SeekOrigin.Begin);

            var textBlockSize = ar.ReadInt32();
            var indexOffset = ar.BaseStream.Position + textBlockSize;

            var strings = GetStringStream(ar, textBlockSize);
            ar.BaseStream.Seek(indexOffset, SeekOrigin.Begin); // Should already be here

            var header = ParseRow(ar, strings);

            var rowCount = ar.ReadInt32();
            var rows = Enumerable.Range(0, rowCount)
                .Select(x => ParseRow(ar, strings))
                .ToArray();

            strings.BaseStream.Dispose();

            return new CSVFile(header, rows);
        }

        private static AwesomeReader GetStringStream(AwesomeReader ar, int size)
        {
            var bytes = ar.ReadBytes(size);
            return new AwesomeReader(new MemoryStream(bytes));
        }

        private static string GetStringValue(AwesomeReader ar, int offset)
        {
            ar.BaseStream.Seek(offset, SeekOrigin.Begin);
            return ar.ReadNullString();
        }

        private static string[] ParseRow(AwesomeReader ar, AwesomeReader strStream)
        {
            var count = ar.ReadInt32();

            return Enumerable.Range(0, count)
                .Select(x => GetStringValue(strStream, ar.ReadInt32()))
                .ToArray();
        }

        public void SaveToFileAsCSV(string path)
        {
            var dirPath = Path.GetDirectoryName(path);

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            using var fs = File.Create(path);
            SaveToStreamAsCSV(fs);
        }

        public void SaveToStreamAsCSV(Stream stream)
        {
            var sw = new StreamWriter(stream, Encoding.UTF8);
            WriteRow(sw, Header, Header.Length);

            foreach (var row in Rows)
            {
                WriteRow(sw, row, Header.Length);
            }
        }

        private static void WriteRow(StreamWriter sw, string[] row, int headerSize)
        {
            var sanitizedRow = row
                .Concat(new string[headerSize - row.Length])
                .Select(x => !(x is null) && x.Contains(',')
                    ? $"\"{x}\""
                    : x ?? "");
            
            var line = string.Join(",", sanitizedRow);
            sw.WriteLine(line);
        }
    }
}
