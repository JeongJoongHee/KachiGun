using System.Collections.Generic;
using UnityEngine;

namespace KachiGun
{
    /// <summary>
    /// 다국어 (한국어 · English). 글자 표 = Kachi_Gun/Table/String.xlsx → Tables/String.csv
    ///   칸: Key(한국어 글자 그대로) · EN(영어). 영어 칸이 비어 있으면 한국어로 보임.
    /// 코드에서는 한국어 글자를 그대로 L.T("상점") · L.F("챕터 {0} 에서 열립니다", n) 로 감싸고,
    /// 표의 이름·설명도 화면에 쓸 때 L.T(r.name). 새 글자는 python tools/strings_collect.py 가 String.xlsx 에 줄을 더함.
    /// 언어를 바꾸면(설정 창) 화면 전체를 다시 만든다(씬 다시 불러오기).
    /// </summary>
    public static class L
    {
        public const string Ko = "ko", En = "en";
        static Dictionary<string, string> en;

        public static bool IsEn => SaveData.Cur.lang == En;

        /// <summary>지금 언어의 글자 (영어 칸이 없으면 한국어 그대로)</summary>
        public static string T(string ko)
        {
            if (string.IsNullOrEmpty(ko) || !IsEn) return ko;
            Load();
            return en.TryGetValue(ko, out var e) && !string.IsNullOrEmpty(e) ? e : ko;
        }

        /// <summary>{0} {1} 자리에 값을 넣는 글자</summary>
        public static string F(string ko, params object[] args)
        {
            string f = T(ko);
            try { return string.Format(f, args); }
            catch (System.FormatException) { return string.Format(ko, args); }   // 영어 칸의 {} 가 잘못되면 한국어로
        }

        static void Load()
        {
            if (en != null) return;
            en = new Dictionary<string, string>();
            foreach (var r in Tables.ReadTable("String"))
            {
                string k = Unescape(r.S("Key"));
                if (k.Length > 0 && !en.ContainsKey(k)) en[k] = Unescape(r.S("EN"));
            }
        }

        /// <summary>표 안에서는 줄바꿈을 \n 두 글자로 적음</summary>
        static string Unescape(string s) => s.Replace("\\n", "\n");

        /// <summary>언어 바꾸기 → 저장하고 화면을 다시 만든다</summary>
        public static void Set(string lang)
        {
            if (SaveData.Cur.lang == lang) return;
            SaveData.Cur.lang = lang;
            SaveData.Cur.Save();
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
