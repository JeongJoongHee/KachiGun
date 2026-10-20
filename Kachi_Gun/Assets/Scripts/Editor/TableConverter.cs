using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 기획 표 xlsx(Kachi_Gun/Table/*.xlsx) → 게임이 읽는 csv(Assets/Resources/Tables/*.csv).
    /// 시트 하나 = csv 하나. csv 이름 = 시트 이름에서 뒤의 개수 표시(_150, _1500, _150Ch …)를 뗀 것
    ///   예) Stage.xlsx 의 'Stage_Table' → Stage_Table.csv, 'Gold_Upgrade_1500' → Gold_Upgrade.csv
    /// 언제 바뀌나: ① 메뉴 [카치건 → 표 변환] ② 재생 버튼을 누를 때 xlsx 가 바뀌어 있으면 자동 ③ 에디터를 열 때 자동
    /// </summary>
    [InitializeOnLoad]
    public static class TableConverter
    {
        static string ProjectDir => Path.GetDirectoryName(Application.dataPath);
        public static string SrcDir => Path.Combine(ProjectDir, "Table");
        public static string OutDir => Path.Combine(Application.dataPath, "Resources", "Tables");
        static string StampFile => Path.Combine(ProjectDir, "Library", "KachiGunTables.stamp");

        static TableConverter()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingEditMode) ConvertIfChanged();
            };
            EditorApplication.delayCall += ConvertIfChanged;
        }

        [MenuItem("카치건/표 변환 (xlsx → csv)", priority = 10)]
        public static void ConvertMenu()
        {
            var r = Convert(true);
            if (r != null) EditorUtility.DisplayDialog("표 변환", r, "확인");
        }

        [MenuItem("카치건/표 폴더 열기", priority = 11)]
        public static void OpenFolder() => EditorUtility.RevealInFinder(Path.Combine(SrcDir, "."));

        /// <summary>배치 모드: Unity.exe -batchmode -quit -executeMethod KachiGun.EditorTools.TableConverter.RunBatch</summary>
        public static void RunBatch()
        {
            var r = Convert(true);
            Debug.Log("[표] " + r);
        }

        static void ConvertIfChanged()
        {
            if (!Directory.Exists(SrcDir)) return;
            long newest = 0;
            foreach (var f in Xlsx()) newest = Math.Max(newest, File.GetLastWriteTimeUtc(f).Ticks);
            long stamp = 0;
            if (File.Exists(StampFile)) long.TryParse(File.ReadAllText(StampFile), out stamp);
            if (newest > stamp) Debug.Log("[표] " + Convert(false));
        }

        static IEnumerable<string> Xlsx()
        {
            if (!Directory.Exists(SrcDir)) yield break;
            foreach (var f in Directory.GetFiles(SrcDir, "*.xlsx"))
                if (!Path.GetFileName(f).StartsWith("~$")) yield return f;   // 엑셀 임시 파일 제외
        }

        /// <summary>시트 이름 → csv 이름 (뒤의 _150 · _1500 · _150Ch 같은 개수 표시를 뗌)</summary>
        public static string CsvName(string sheet) => Regex.Replace(sheet.Trim(), @"_\d+(Ch)?$", "");

        /// <summary>변환하고 결과 한 줄 요약을 돌려줌</summary>
        public static string Convert(bool force)
        {
            if (!Directory.Exists(SrcDir)) return "표 폴더가 없습니다: " + SrcDir;
            Directory.CreateDirectory(OutDir);
            int files = 0, sheets = 0, changed = 0;
            var errors = new List<string>();
            var made = new Dictionary<string, string>();   // csv 이름 → 어느 파일/시트
            long newest = 0;
            var utf8Bom = new UTF8Encoding(true);
            foreach (var f in Xlsx())
            {
                newest = Math.Max(newest, File.GetLastWriteTimeUtc(f).Ticks);
                files++;
                List<XlsxReader.Sheet> list;
                try { list = XlsxReader.Read(f); }
                catch (Exception e) { errors.Add($"{Path.GetFileName(f)} 읽기 실패: {e.Message}"); continue; }
                foreach (var sh in list)
                {
                    if (sh.rows.Count == 0) continue;
                    string name = CsvName(sh.name);
                    string from = $"{Path.GetFileName(f)} / {sh.name}";
                    if (made.TryGetValue(name, out var dup)) { errors.Add($"csv 이름이 겹칩니다: {name} ({dup} · {from})"); continue; }
                    made[name] = from;
                    sheets++;
                    string csv = XlsxReader.ToCsv(sh.rows);
                    string outPath = Path.Combine(OutDir, name + ".csv");
                    string old = File.Exists(outPath) ? File.ReadAllText(outPath, Encoding.UTF8).TrimStart('﻿') : null;
                    if (old == csv) continue;      // 그대로면 안 씀 (Unity 가 다시 읽지 않게)
                    File.WriteAllText(outPath, csv, utf8Bom);
                    changed++;
                }
            }
            if (errors.Count == 0) File.WriteAllText(StampFile, newest.ToString());
            if (changed > 0 && !Application.isBatchMode) AssetDatabase.Refresh();
            else if (changed > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 표 폴더에 없는 csv (예전 표) 알림
            var stale = new List<string>();
            foreach (var p in Directory.GetFiles(OutDir, "*.csv"))
                if (!made.ContainsKey(Path.GetFileNameWithoutExtension(p))) stale.Add(Path.GetFileName(p));

            var sb = new StringBuilder($"xlsx {files}개 · 시트 {sheets}개 → csv (바뀐 것 {changed}개)");
            if (stale.Count > 0) sb.Append($"\n표에 없는 csv: {string.Join(", ", stale)}");
            foreach (var e in errors) { sb.Append("\n오류: " + e); Debug.LogError("[표] " + e); }
            return sb.ToString();
        }
    }
}
