using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace AlipiriAR.Profile
{
    /// <summary>Plain settable properties + a parameterless constructor, so Newtonsoft's
    /// default deserialization path (not its constructor-parameter-matching fallback) handles
    /// logins.json round-trips unambiguously.</summary>
    public class LoginEntry
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Language { get; set; }

        public LoginEntry() { }

        public LoginEntry(string name, int age, string language)
        {
            Name = name;
            Age = age;
            Language = language;
        }
    }

    /// <summary>
    /// Writes login rows into the user's own login.xlsx template (Assets/login.xlsx —
    /// 3 columns: Name, Age, Language; 1000 pre-styled rows) rather than generating a workbook
    /// from scratch. Renaming a CSV to .xlsx corrupts under Excel's own re-save, and the usual
    /// libraries (EPPlus's licence, ClosedXML's dependency chain under IL2CPP) are poor fits —
    /// ZipArchive + XDocument on the template's own parts has zero dependencies and cannot
    /// drift from a format Excel already accepts, because it *is* that exact file. PLAN.md §12.
    /// </summary>
    public static class XlsxWriter
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const int HeaderRow = 1;
        private const int FirstDataRow = 2;
        private const int LastStyledRow = 1000; // rows the template pre-formats

        /// <summary>Rewrites the sheet inside templateBytes with every entry starting at row 2,
        /// preserving every other part (styles, theme, drawing) byte-for-byte. Entries are the
        /// full authoritative list each time — see ExcelLoginStore for why (a ZIP can't be
        /// appended to row-by-row; logins.json is the real source of truth, this is the export).</summary>
        public static byte[] BuildWorkbook(byte[] templateBytes, IReadOnlyList<LoginEntry> entries)
        {
            using var input = new MemoryStream(templateBytes);
            using var output = new MemoryStream();
            input.CopyTo(output);
            output.Position = 0;

            using (var archive = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
            {
                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null) throw new FileNotFoundException("Template is missing xl/worksheets/sheet1.xml");

                XDocument doc;
                using (var s = sheetEntry.Open())
                    doc = XDocument.Load(s);

                var sheetData = doc.Root!.Element(Main + "sheetData");
                var rowsByIndex = sheetData!.Elements(Main + "row")
                    .ToDictionary(r => (int)r.Attribute("r"));

                int lastStyleUsed = 2; // the template's data-row style index (s="2")
                if (rowsByIndex.TryGetValue(FirstDataRow, out var sampleRow))
                {
                    var sampleCell = sampleRow.Elements(Main + "c").FirstOrDefault();
                    if (sampleCell?.Attribute("s") != null)
                        lastStyleUsed = (int)sampleCell.Attribute("s");
                }

                for (int i = 0; i < entries.Count; i++)
                {
                    int rowIndex = FirstDataRow + i;
                    var row = rowsByIndex.TryGetValue(rowIndex, out var existing)
                        ? existing
                        : CreateOverflowRow(sheetData, rowIndex);

                    // Template header (row 1) is exactly A=Name, B=Age, C=Language — 3 columns,
                    // confirmed against the actual file, not the 4-column layout first assumed.
                    WriteCell(row, "A", rowIndex, lastStyleUsed, inlineString: entries[i].Name);
                    WriteCell(row, "B", rowIndex, lastStyleUsed, numeric: entries[i].Age);
                    WriteCell(row, "C", rowIndex, lastStyleUsed, inlineString: entries[i].Language);
                }

                using var writeStream = sheetEntry.Open();
                writeStream.SetLength(0);
                doc.Save(writeStream);
            }

            return output.ToArray();
        }

        private static XElement CreateOverflowRow(XElement sheetData, int rowIndex)
        {
            var row = new XElement(Main + "row", new XAttribute("r", rowIndex));
            sheetData.Add(row);
            return row;
        }

        private static void WriteCell(XElement row, string column, int rowIndex, int style, string inlineString = null, int? numeric = null)
        {
            string cellRef = $"{column}{rowIndex}";
            var cell = row.Elements(Main + "c").FirstOrDefault(c => (string)c.Attribute("r") == cellRef);

            if (cell == null)
            {
                cell = new XElement(Main + "c", new XAttribute("r", cellRef), new XAttribute("s", style));
                InsertInColumnOrder(row, cell);
            }

            cell.RemoveAttributes();
            cell.SetAttributeValue("r", cellRef);
            cell.SetAttributeValue("s", style);
            cell.RemoveNodes();

            if (numeric.HasValue)
            {
                cell.Add(new XElement(Main + "v", numeric.Value));
            }
            else
            {
                cell.SetAttributeValue("t", "inlineStr");
                cell.Add(new XElement(Main + "is", new XElement(Main + "t", inlineString ?? string.Empty)));
            }
        }

        private static void InsertInColumnOrder(XElement row, XElement newCell)
        {
            string newCol = new string(((string)newCell.Attribute("r")).TakeWhile(char.IsLetter).ToArray());
            foreach (var existing in row.Elements(Main + "c"))
            {
                string col = new string(((string)existing.Attribute("r")).TakeWhile(char.IsLetter).ToArray());
                if (string.CompareOrdinal(col, newCol) > 0)
                {
                    existing.AddBeforeSelf(newCell);
                    return;
                }
            }
            row.Add(newCell);
        }
    }
}
