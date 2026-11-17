using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace DiplomPrintGenerate.Services
{
    public class CsvImportExportService
    {
        public static List<Dictionary<string, string>> ImportFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<Dictionary<string, string>>();

            Encoding encoding = DetectEncoding(filePath);
            string content = File.ReadAllText(filePath, encoding);
            return ParseDelimitedText(content);
        }

        public static List<Dictionary<string, string>> ImportFromClipboard()
        {
            if (!Clipboard.ContainsText())
                return new List<Dictionary<string, string>>();

            string text = Clipboard.GetText();
            return ParseDelimitedText(text);
        }

        public static List<Dictionary<string, string>> ParseDelimitedText(string content)
        {
            var result = new List<Dictionary<string, string>>();
            if (string.IsNullOrWhiteSpace(content))
                return result;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return result;

            // Detect delimiter from the first line: tab (\t), semicolon (;), or comma (,)
            string firstLine = lines[0];
            char delimiter = DetectDelimiter(firstLine);

            var headers = SplitCsvLine(firstLine, delimiter)
                .Select(h => h.Trim().Trim('"', '\''))
                .ToList();

            for (int i = 1; i < lines.Length; i++)
            {
                var rowValues = SplitCsvLine(lines[i], delimiter);
                if (rowValues.Count == 0 || (rowValues.Count == 1 && string.IsNullOrWhiteSpace(rowValues[0])))
                    continue;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Count; c++)
                {
                    string header = headers[c];
                    string val = c < rowValues.Count ? rowValues[c].Trim().Trim('"', '\'') : "";
                    dict[header] = val;
                }
                result.Add(dict);
            }

            return result;
        }

        private static char DetectDelimiter(string line)
        {
            int tabCount = line.Count(c => c == '\t');
            int semiCount = line.Count(c => c == ';');
            int commaCount = line.Count(c => c == ',');

            if (tabCount >= semiCount && tabCount >= commaCount && tabCount > 0)
                return '\t';
            if (semiCount >= commaCount && semiCount > 0)
                return ';';
            return ',';
        }

        private static List<string> SplitCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var sb = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        public static void ExportToCsv(string filePath, DataTable table)
        {
            var sb = new StringBuilder();
            char delimiter = ';';

            // Headers
            var colNames = table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(c.ColumnName, delimiter));
            sb.AppendLine(string.Join(delimiter.ToString(), colNames));

            // Rows
            foreach (DataRow row in table.Rows)
            {
                var rowVals = table.Columns.Cast<DataColumn>().Select(c => EscapeCsv(row[c]?.ToString() ?? "", delimiter));
                sb.AppendLine(string.Join(delimiter.ToString(), rowVals));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string EscapeCsv(string val, char delimiter)
        {
            if (val.Contains(delimiter.ToString()) || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
            {
                return "\"" + val.Replace("\"", "\"\"") + "\"";
            }
            return val;
        }

        private static Encoding DetectEncoding(string filename)
        {
            byte[] bom = new byte[4];
            using (var file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode;
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode;
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;

            // Check if valid UTF-8
            try
            {
                string text = File.ReadAllText(filename, new UTF8Encoding(false, true));
                return Encoding.UTF8;
            }
            catch
            {
                // Fallback to Windows-1251 (Cyrillic ANSI) common in Russian Excel CSVs
                try
                {
                    return Encoding.GetEncoding(1251);
                }
                catch
                {
                    return Encoding.Default;
                }
            }
        }
    }
}
