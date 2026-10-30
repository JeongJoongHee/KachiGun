using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
using UnityEngine.Purchasing.Security;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 결제·광고 준비 도구 (10-30, 불도깨비 IapTools 를 가져옴).
    ///  - 카치건 > 결제 라이선스 키 넣기... : Play Console 의 라이선스 키(Base64 한 줄) → GooglePlayTangle.cs (영수증 서명 확인용)
    ///    라이선스 키 = Play Console > 앱 > 수익 창출 설정 > 라이선스. 공개 키라 비밀번호는 아님. 사본 = 키 보관/google_play_license_key.txt
    ///    ※ 새 개발자 계정(kachigoogoo)으로 앱을 옮긴 뒤 그 계정에서 보이는 키를 넣을 것
    ///  - 카치건 > 결제·광고 점검 : 키 · 상품 ID · AdMob 앱 ID
    ///  - 배치: SetKeyFromFileBatch (키 파일에서) · CompileAndroidBatch (폰 전용 코드 = 실제 광고·결제 경로를 컴파일해 봄) · CheckBatch
    /// </summary>
    public class IapTools : EditorWindow
    {
        const string KeyFile = "../키 보관/google_play_license_key.txt";   // 프로젝트 폴더(Kachi_Gun) 기준
        string key = "";

        [MenuItem("카치건/결제 라이선스 키 넣기...", false, 40)]
        static void Open()
        {
            var w = GetWindow<IapTools>(true, "결제 라이선스 키", true);
            w.minSize = new Vector2(520, 260);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Play Console > 앱 > 수익 창출 설정 > 라이선스 의 키(긴 영문·숫자 한 줄)를 복사해 붙여 넣으세요.\n" +
                                    "이 키로 결제 영수증이 진짜 구글 것인지 앱 안에서 확인합니다. 키가 없으면 휴대폰에서 결제가 꺼져 있습니다.", MessageType.Info);
            EditorGUILayout.LabelField("지금 상태", GooglePlayTangle.IsPopulated ? "키 들어 있음" : "키 없음 (결제 꺼짐)");
            key = EditorGUILayout.TextArea(key, GUILayout.Height(90));
            if (GUILayout.Button("넣기", GUILayout.Height(32)))
            {
                string err = Apply(key);
                if (err == null) { SaveKeyFile(key); ShowNotification(new GUIContent("넣었습니다 — 컴파일 후 적용")); }
                else EditorUtility.DisplayDialog("결제 라이선스 키", err, "확인");
            }
        }

        /// <returns>null = 성공, 아니면 안내 글</returns>
        static string Apply(string raw)
        {
            string k = new string((raw ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
            if (k.Length < 200) return "키가 너무 짧습니다. 라이선스 키 전체(보통 390자 정도)를 복사했는지 확인해 주세요.";
            try { Convert.FromBase64String(k); }
            catch { return "키 모양이 아닙니다 (Base64). 라이선스 키를 그대로 복사해 주세요."; }

            var gen = Type.GetType("UnityEditor.Purchasing.ObfuscationGenerator, UnityEditor.Purchasing");
            var m = gen?.GetMethod("ObfuscateGoogleSecrets", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null) return "In-App Purchasing 패키지에서 키 만드는 기능을 찾지 못했습니다 (Services > In-App Purchasing > Receipt Validation Obfuscator 로 직접 넣어 주세요).";
            var result = m.Invoke(null, new object[] { k }) as string;
            if (!string.IsNullOrEmpty(result)) return result.Trim();
            Debug.Log($"[IapTools] 라이선스 키 넣음 ({k.Length}자) → Assets/Scripts/UnityPurchasing/generated/GooglePlayTangle.cs");
            return null;
        }

        static void SaveKeyFile(string raw)
        {
            try
            {
                var path = Path.GetFullPath(KeyFile);
                if (Directory.Exists(Path.GetDirectoryName(path))) File.WriteAllText(path, raw.Trim());
            }
            catch (Exception e) { Debug.LogWarning("[IapTools] 키 파일 저장 못 함: " + e.Message); }
        }

        /// <summary>배치: 키 보관/google_play_license_key.txt → GooglePlayTangle</summary>
        public static void SetKeyFromFileBatch()
        {
            var path = Path.GetFullPath(KeyFile);
            if (!File.Exists(path)) { Debug.LogError("[IapTools] 키 파일 없음: " + path); EditorApplication.Exit(1); return; }
            var err = Apply(File.ReadAllText(path));
            if (err != null) { Debug.LogError("[IapTools] " + err); EditorApplication.Exit(1); return; }
            AssetDatabase.Refresh();
            EditorApplication.Exit(0);
        }

        [MenuItem("카치건/결제·광고 점검", false, 41)]
        public static void Check()
        {
            Tables.Load();
            var cash = Tables.Shop.Where(Iap.IsCash).ToList();
            string pluginAppId = AdMobPluginAppId();
            var lines = new System.Collections.Generic.List<string>
            {
                (GooglePlayTangle.IsPopulated ? "[OK] " : "[  ] ") + "결제 라이선스 키 (GooglePlayTangle) — 없으면 휴대폰에서 결제 꺼짐 → 카치건 > 결제 라이선스 키 넣기",
                (cash.Count > 0 ? "[OK] " : "[  ] ") + "원화 상품: " + string.Join(" · ", cash.Select(p => $"{p.id}({(Iap.Permanent(p) ? "영구" : "소모")}) {p.priceValue}원")),
                (pluginAppId == AdIds.AppId ? "[OK] " : "[!!] ") + $"AdMob 앱 ID — 플러그인 설정 '{pluginAppId}' · 코드 '{AdIds.AppId}'",
                "[참고] 보상형 광고 단위 " + AdIds.Rewarded + " (개발용 키 APK 는 구글 테스트 광고)",
            };
            Debug.Log("[IapTools] 결제·광고 점검\n" + string.Join("\n", lines));
        }

        public static void CheckBatch() { Check(); EditorApplication.Exit(0); }

        /// <summary>광고 플러그인 설정 파일의 안드로이드 앱 ID (칸 이름으로 읽음 — 플러그인 설정 클래스가 internal 이라서)</summary>
        public static string AdMobPluginAppId()
        {
            var type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            if (type == null) return "(플러그인 없음)";
            var guid = AssetDatabase.FindAssets("GoogleMobileAdsSettings t:ScriptableObject").FirstOrDefault();
            var asset = guid == null ? null : AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) return "(설정 파일 없음)";
            return new SerializedObject(asset).FindProperty("adMobAndroidAppId")?.stringValue ?? "(칸 없음)";
        }

        /// <summary>폰 전용 코드(#if UNITY_ANDROID && !UNITY_EDITOR — 실제 광고·결제)를 안드로이드 기준으로 컴파일만 해 봄. 실패하면 종료 코드 1</summary>
        public static void CompileAndroidBatch()
        {
            bool ok = true;
            foreach (var defs in new[] { new string[0], new[] { "KACHI_TEST_ADS" } })
            {
                string outDir = Path.Combine(Path.GetTempPath(), "kachigun_android_compile" + defs.Length);
                var result = PlayerBuildInterface.CompilePlayerScripts(new ScriptCompilationSettings
                {
                    target = BuildTarget.Android,
                    group = BuildTargetGroup.Android,
                    options = ScriptCompilationOptions.None,
                    extraScriptingDefines = defs,
                }, outDir);
                bool one = result.assemblies != null && result.assemblies.Contains("Assembly-CSharp.dll");
                Debug.Log($"[IapTools] 안드로이드 컴파일 {(defs.Length > 0 ? "(테스트 광고)" : "(실제 광고)")} {(one ? "성공" : "실패")} — {(result.assemblies == null ? "" : string.Join(", ", result.assemblies))}");
                ok &= one;
            }
            Check();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
