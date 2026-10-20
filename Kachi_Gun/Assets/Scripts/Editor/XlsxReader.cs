using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// xlsx 를 직접 읽는다 (따로 설치할 것 없음 — xlsx = xml 묶음 zip).
    /// 수식 칸은 엑셀이 마지막으로 저장한 계산 결과를 읽는다 (엑셀에서 저장만 하면 됨).
    /// </summary>
    public static class XlsxReader
    {
        public class Sheet
        {
            public string name;
            public List<List<string>> rows = new();   // 빈 줄은 빼고, 칸은 A 부터
        }

        public static List<Sheet> Read(string path)
        {
            // 엑셀로 열어 둔 파일도 읽을 수 있게 공유 모드로 연다
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

            var shared = ReadShared(zip);
            var rels = new Dictionary<string, string>();
            var relDoc = Load(zip, "xl/_rels/workbook.xml.rels");
            if (relDoc != null)
                foreach (XmlElement r in relDoc.GetElementsByTagName("Relationship"))
                    rels[r.GetAttribute("Id")] = r.GetAttribute("Target");

            var list = new List<Sheet>();
            var wb = Load(zip, "xl/workbook.xml") ?? throw new Exception("workbook.xml 없음");
            foreach (XmlElement s in wb.GetElementsByTagName("sheet", "*"))
            {
                string name = s.GetAttribute("name");
                string rid = null;
                foreach (XmlAttribute a in s.Attributes) if (a.LocalName == "id") rid = a.Value;
                if (rid == null || !rels.TryGetValue(rid, out var target)) continue;
                string entry = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                var doc = Load(zip, entry);
                if (doc == null) continue;
                list.Add(new Sheet { name = name, rows = ReadRows(doc, shared) });
            }
            return list;
        }

        static XmlDocument Load(ZipArchive zip, string entry)
        {
            var e = zip.GetEntry(entry);
            if (e == null) return null;
            var doc = new XmlDocument();
            using var st = e.Open();
            doc.Load(st);
            return doc;
        }

        static List<string> ReadShared(ZipArchive zip)
        {
            var l = new List<string>();
            var doc = Load(zip, "xl/sharedStrings.xml");
            if (doc == null) return l;
            foreach (XmlElement si in doc.GetElementsByTagName("si", "*")) l.Add(Text(si));
            return l;
        }

        /// <summary>글자 칸: &lt;t&gt; 를 모두 이음 (서식이 섞인 글자 포함, 일본어 읽기 표시 rPh 는 뺌)</summary>
        static string Text(XmlElement e)
        {
            var sb = new StringBuilder();
            foreach (XmlElement t in e.GetElementsByTagName("t", "*"))
            {
                if (t.ParentNode is XmlElement p && p.LocalName == "rPh") continue;
                sb.Append(t.InnerText);
            }
            return sb.ToString();
        }

        static List<List<string>> ReadRows(XmlDocument doc, List<string> shared)
        {
            var rows = new List<List<string>>();
            foreach (XmlElement row in doc.GetElementsByTagName("row", "*"))
            {
                var cells = new List<string>();
                int next = 0;
                foreach (XmlNode n in row.ChildNodes)
                {
                    if (!(n is XmlElement c) || c.LocalName != "c") continue;
                    string r = c.GetAttribute("r");
                    int col = string.IsNullOrEmpty(r) ? next : ColIndex(r);
                    next = col + 1;
                    while (cells.Count <= col) cells.Add("");
                    cells[col] = Value(c, shared);
                }
                // 뒤쪽 빈칸 정리, 완전히 빈 줄은 뺌
                while (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
                if (cells.Count > 0) rows.Add(cells);
            }
            return rows;
        }

        static string Value(XmlElement c, List<string> shared)
        {
            string t = c.GetAttribute("t");
            string v = null;
            foreach (XmlNode n in c.ChildNodes)
                if (n is XmlElement e && e.LocalName == "v") v = e.InnerText;
            switch (t)
            {
                case "s":
                    return int.TryParse(v, out int i) && i >= 0 && i < shared.Count ? shared[i] : "";
                case "inlineStr":
                    foreach (XmlNode n in c.ChildNodes)
                        if (n is XmlElement e && e.LocalName == "is") return Text(e);
                    return "";
                case "str": return v ?? "";
                case "b": return v == "1" ? "TRUE" : "FALSE";
                case "e": return "";        // #DIV/0! 같은 오류 칸
                default: return Number(v);
            }
        }

        /// <summary>숫자는 짧게 (0.69999999999999996 → 0.7), 값이 바뀌지 않는 한에서</summary>
        static string Number(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return v;
            string s = d.ToString("G15", CultureInfo.InvariantCulture);
            if (double.Parse(s, CultureInfo.InvariantCulture) != d) s = d.ToString("G17", CultureInfo.InvariantCulture);
            return s;
        }

        static int ColIndex(string r)
        {
            int n = 0;
            foreach (char ch in r)
            {
                if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') n = n * 26 + (ch - 'a' + 1);
                else break;
            }
            return n - 1;
        }

        // ---------- CSV ----------
        public static string ToCsv(List<List<string>> rows)
        {
            var sb = new StringBuilder();
            int width = rows.Count > 0 ? rows[0].Count : 0;   // 첫 줄(칸 이름) 너비에 맞춤
            foreach (var r in rows)
            {
                for (int i = 0; i < width; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Escape(i < r.Count ? r[i] : ""));
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static string Escape(string s)
        {
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
