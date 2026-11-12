using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// Play 게임즈(클라우드 저장) App ID 넣기 (11-12, 불도깨비 GpgsTools 를 줄여 가져옴).
    ///  - App ID = Play Console › Play Games 서비스 › 설정 맨 위 "프로젝트 ID" (kachigoogoo 계정 · Google Cloud 프로젝트 Kachizombie). 비밀 아님
    ///  - 배치: SetupBatch = Window > Google Play Games > Setup > Android setup 과 같은 일
    ///    (PlayGamesSettings.asset mAppId · GooglePlayGamesManifest.androidlib APP_ID · ProjectSettings/GooglePlayGameSettings.txt · GPGSIds.cs)
    ///  - 메뉴 카치건 > Play 게임즈 점검 : App ID · 정의 KACHI_GPGS · 패키지 · 업로드 키
    /// </summary>
    public static class GpgsTools
    {
        public const string AppId = "667458878000";
        const string Package = "com.zkfpf.kachizombie";
        const string Define = "KACHI_GPGS";

        /// <summary>Play Console [리소스 보기] 의 Android XML 과 같은 모양 (업적 · 리더보드 없음)</summary>
        static string ResourceXml =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<!--Google Play game services IDs. Save this file as res/values/games-ids.xml in your project.-->\n" +
            "<resources>\n" +
            "  <!--app_id-->\n" +
            $"  <string name=\"app_id\" translatable=\"false\">{AppId}</string>\n" +
            "  <!--package_name-->\n" +
            $"  <string name=\"package_name\" translatable=\"false\">{Package}</string>\n" +
            "</resources>\n";

        public static void SetupBatch()
        {
            int code = 1;
            try
            {
                var t = System.Type.GetType("GooglePlayGames.Editor.GPGSAndroidSetupUI, Google.Play.Games.Editor");
                var m = t?.GetMethod("PerformSetup", new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (m == null) Debug.LogError("[GPGS] GPGSAndroidSetupUI.PerformSetup 을 못 찾음");
                else
                {
                    bool ok = (bool)m.Invoke(null, new object[] { "", "Assets/Scripts", "GPGSIds", ResourceXml, null });
                    AssetDatabase.SaveAssets();
                    Debug.Log("[GPGS] Android setup " + (ok ? "성공" : "실패") + " · App ID = " + ReadAppId());
                    Check();
                    code = ok && ReadAppId() == AppId ? 0 : 2;
                }
            }
            catch (System.Exception e) { Debug.LogError("[GPGS] " + e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        [MenuItem("카치건/Play 게임즈 점검", false, 42)]
        public static void Check()
        {
            string id = ReadAppId();
            PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android, out var defines);
            bool def = System.Array.IndexOf(defines, Define) >= 0;
            string pkg = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string manifest = "";
            var mf = "Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml";
            if (File.Exists(mf)) manifest = File.ReadAllText(mf);
            bool mfOk = manifest.Contains("\\u003" + AppId);   // 플러그인이 숫자 앞에 \u003 를 붙여 글자로 읽히게 함
            bool keystore = PlayerSettings.Android.useCustomKeystore && !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName);
            string msg = string.Join("\n", new[]
            {
                (id == AppId ? "[OK] " : "[  ] ") + "PlayGamesSettings App ID: " + (id == "" ? "(없음)" : id),
                (mfOk ? "[OK] " : "[  ] ") + "매니페스트 APP_ID (GooglePlayGamesManifest.androidlib)",
                (def ? "[OK] " : "[  ] ") + "Android 정의 " + Define,
                (pkg == Package ? "[OK] " : "[!!] ") + "패키지 " + pkg,
                (keystore ? "[OK] " : "[  ] ") + "업로드 키 " + (keystore ? Path.GetFileName(PlayerSettings.Android.keystoreName) : "(없음)"),
            });
            Debug.Log("[GPGS 점검]\n" + msg);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Play 게임즈 점검", msg, "확인");
        }

        static string ReadAppId()
        {
            foreach (var guid in AssetDatabase.FindAssets("PlayGamesSettings"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".asset")) continue;
                foreach (var line in File.ReadAllLines(path))
                {
                    string s = line.Trim();
                    if (s.StartsWith("mAppId:")) return s.Substring(7).Trim();
                }
            }
            return "";
        }
    }
}
