using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 휴대폰 홈 화면의 앱 이름 (10-24 결정 p-enname): 한국어 폰 = "카피바라vs좀비" · 그 밖의 언어 = "Capybara vs Zombies".
    /// 안드로이드 빌드 때 만들어지는 Gradle 프로젝트에서 values/strings.xml 의 app_name 을 영어로,
    /// values-ko/strings.xml 을 새로 만들어 한국어로 넣음 (서바이벌 AndroidAppName 방식 + 언어별).
    /// </summary>
    public class AndroidAppName : IPostGenerateGradleAndroidProject
    {
        public const string NameKo = ProjectInfo.Name;          // 카피바라vs좀비
        public const string NameEn = "Capybara vs Zombies";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path = …/unityLibrary → 그 위 = Gradle 프로젝트 루트
            string root = Path.GetFullPath(Path.Combine(path, ".."));
            int changed = 0;
            foreach (var module in new[] { "launcher", "unityLibrary" })
            {
                string res = Path.Combine(root, module, "src", "main", "res");
                string file = Path.Combine(res, "values", "strings.xml");
                if (!File.Exists(file)) continue;
                string text = File.ReadAllText(file);
                if (!Regex.IsMatch(text, "<string name=\"app_name\">")) continue;
                File.WriteAllText(file, Regex.Replace(text, "<string name=\"app_name\">[^<]*</string>", $"<string name=\"app_name\">{NameEn}</string>"), new System.Text.UTF8Encoding(false));
                string ko = Path.Combine(res, "values-ko");
                Directory.CreateDirectory(ko);
                File.WriteAllText(Path.Combine(ko, "strings.xml"),
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n  <string name=\"app_name\">{NameKo}</string>\n</resources>\n",
                    new System.Text.UTF8Encoding(false));
                changed++;
            }
            if (changed > 0) Debug.Log($"[AndroidAppName] 앱 이름 = {NameEn} / 한국어 {NameKo} (모듈 {changed}개)");
            else Debug.LogWarning("[AndroidAppName] strings.xml 에서 app_name 을 못 찾음 — 앱 이름이 productName 그대로 들어감");
        }
    }
}
