using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 플레이 스토어용 AAB 빌드 (10-24, 서바이벌 BuildAab 를 가져옴). 배치: bash tools/unity_batch.sh KachiGun.EditorTools.BuildAab.Build
    /// 메뉴 카치건 > AAB 빌드... = 비밀번호 입력 창 — 넣은 값은 빌드에만 쓰고 저장 안 함, 굽기 전에 keytool 로 먼저 확인.
    /// 서명 비밀번호 = 키 보관/비번.txt (git 제외, 화면에 출력 금지) — 1줄이면 키스토어·키 같은 비밀번호, 2줄이면 1줄 키스토어 · 2줄 키.
    /// 결과 = Kachi_Gun/Builds/kachizombie-{버전}-{번호}.aab (git 제외). 버전 번호는 자동으로 안 올림 — 이미 올린 번호면 [AAB 버전 번호 +1]
    /// APK: 메뉴 카치건 > APK 빌드... (개발용 키면 비밀번호 없이). 배치 BuildApk(업로드 키) · BuildApkDebug(개발용 키)
    /// </summary>
    public static class BuildAab
    {
        const string PasswordFile = "D:/00 Final_Game/키 보관/비번.txt";
        // 10-24 결정: 패키지 이름 p-pkg · 버전 1.0 (1) · 최소 API 24 · 대상 API 36 · IL2CPP ARM64 · 세로 고정
        public const string PackageName = "com.zkfpf.kachizombie";
        public const string KeystorePath = "D:/00 Final_Game/키 보관/upload.keystore";
        public const string KeyAlias = "kachizombie";
        const string FilePrefix = "kachizombie";

        [MenuItem("카치건/AAB 빌드 (플레이 스토어)...", priority = 30)]
        public static void BuildMenu() => AabBuildWindow.Open(false);

        [MenuItem("카치건/APK 빌드 (폰에 바로 설치)...", priority = 31)]
        public static void BuildApkMenu() => AabBuildWindow.Open(true);

        /// <summary>배치용 — 비밀번호는 Unity 화면 값 또는 비밀번호 파일</summary>
        public static void Build() => ExitAfter(() => DoBuild(null, null, false, false));

        /// <summary>배치용 APK — 업로드 키로 서명 (비밀번호는 Build 와 같은 곳에서)</summary>
        public static void BuildApk() => ExitAfter(() => DoBuild(null, null, true, false));

        /// <summary>배치용 APK — 개발용 키로 서명 (비밀번호 필요 없음)</summary>
        public static void BuildApkDebug() => ExitAfter(() => DoBuild(null, null, true, true));

        static void ExitAfter(Func<bool> build)
        {
            int code = 1;
            try { code = build() ? 0 : 2; }
            catch (Exception e) { Debug.LogError("[BuildAab] " + e); code = 3; }
            EditorApplication.Exit(code);
        }

        /// <summary>빌드 창에서 부름 — 넣은 비밀번호로 확인 후 빌드 (apk + debugKey 면 비밀번호 없이)</summary>
        public static bool BuildWith(string storePass, string keyPass, bool apk = false, bool debugKey = false)
        {
            try { return DoBuild(storePass, keyPass, apk, debugKey); }
            catch (Exception e) { Debug.LogError("[BuildAab] " + e); return false; }
        }

        /// <summary>마지막으로 만든 파일 (창의 [폴더 열기] 용)</summary>
        public static string LastOutput;

        /// <summary>안드로이드 설정을 결정값대로 맞춤 (버전 코드는 건드리지 않음). 빌드 때마다 부름 · 배치 KachiGun.EditorTools.BuildAab.Setup</summary>
        [MenuItem("카치건/안드로이드 설정 맞추기", priority = 33)]
        public static void Setup()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            if (File.Exists(KeystorePath))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = KeystorePath;
                PlayerSettings.Android.keyaliasName = KeyAlias;
            }
            AppIconSetup.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildAab] 설정 — {PackageName} · 버전 {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}) · API 24~36 · IL2CPP ARM64 · 세로 · 키 {(PlayerSettings.Android.useCustomKeystore ? KeyAlias : "없음")}");
        }

        /// <summary>배치용: 설정만 맞추고 끝냄</summary>
        public static void SetupBatch() => ExitAfter(() => { Setup(); return true; });

        [MenuItem("카치건/AAB 버전 번호 +1", priority = 32)]
        public static void BumpVersionCode()
        {
            PlayerSettings.Android.bundleVersionCode += 1;
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildAab] versionCode → {PlayerSettings.Android.bundleVersionCode}");
        }

        static bool DoBuild(string storePass, string keyPass, bool apk, bool debugKey)
        {
            Setup();
            // 개발용 키 APK (10-23): 비밀번호 없이 Unity 기본 디버그 키로 서명 — 키스토어 설정은 빌드 뒤 되돌림
            if (apk && debugKey)
            {
                bool wasCustom = PlayerSettings.Android.useCustomKeystore;
                try
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                    Debug.Log("[BuildAab] 서명 = 개발용 키 (비밀번호 없음)");
                    return BuildPlayer(true, "-debug");
                }
                finally { PlayerSettings.Android.useCustomKeystore = wasCustom; }
            }

            string ks = PlayerSettings.Android.keystoreName;
            if (!PlayerSettings.Android.useCustomKeystore || string.IsNullOrEmpty(ks) || !File.Exists(ks))
            {
                Debug.LogError($"[BuildAab] 키스토어가 없음 — Project Settings > Player > Publishing Settings 확인 (지금: '{ks}')");
                return false;
            }
            bool fromWindow = !string.IsNullOrEmpty(storePass);
            if (fromWindow)
            {
                PlayerSettings.Android.keystorePass = storePass;
                PlayerSettings.Android.keyaliasPass = string.IsNullOrEmpty(keyPass) ? storePass : keyPass;
            }
            // Unity 화면(Publishing Settings)에 이번에 비밀번호를 넣었으면 그것을 씀 — 없으면 비밀번호 파일 (10-12)
            bool typed = fromWindow || (!string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass));
            if (!typed)
            {
                if (!File.Exists(PasswordFile))
                {
                    Debug.LogError("[BuildAab] 비밀번호 파일이 없음: " + PasswordFile + " — Unity 에서 Keystore password · Key password 를 넣고 메뉴로 빌드하세요");
                    return false;
                }
                var lines = File.ReadAllLines(PasswordFile).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                if (lines.Length == 0) { Debug.LogError("[BuildAab] 비밀번호 파일이 비어 있음"); return false; }
                PlayerSettings.Android.keystorePass = lines[0];
                PlayerSettings.Android.keyaliasPass = lines.Length > 1 ? lines[1] : lines[0];
            }
            Debug.Log("[BuildAab] 비밀번호 = " + (fromWindow ? "빌드 창에 넣은 것" : typed ? "Unity 화면에 넣은 것" : "비밀번호 파일"));

            // 굽기 전에 키스토어를 열어 봄 (10-13) — 틀리면 10분 넘게 구운 뒤 마지막 서명 단계에서야 실패했음
            if (!CheckPasswords(PlayerSettings.Android.keystorePass, PlayerSettings.Android.keyaliasPass, out string problem))
            {
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                Debug.LogError("[BuildAab] " + problem);
                return false;
            }

            try { return BuildPlayer(apk, ""); }
            finally
            {
                // 비밀번호는 세션에서 지움 (프로젝트 파일에는 원래 저장 안 됨)
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
        }

        static bool BuildPlayer(bool apk, string suffix)
        {
            // 10-30 광고: 개발용 키 APK(-debug) = 구글 테스트 광고(KACHI_TEST_ADS) · AAB·업로드 키 APK = 실제 광고
            bool testAds = suffix == "-debug";
            Debug.Log("[BuildAab] 광고 = " + (testAds ? "구글 테스트 광고 (눌러도 됨)" : "실제 광고 — 내 광고는 누르지 말 것"));
            IapTools.Check();
            string kind = apk ? "APK" : "AAB";
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[BuildAab] 빌드 대상을 Android 로 바꿈 (처음엔 오래 걸림)");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            EditorUserBuildSettings.buildAppBundle = !apk;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string ver = PlayerSettings.bundleVersion;
            int vc = PlayerSettings.Android.bundleVersionCode;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds"));
            Directory.CreateDirectory(dir);
            string outPath = Path.Combine(dir, $"{FilePrefix}-{ver}-{vc}{suffix}.{(apk ? "apk" : "aab")}");
            string ks = PlayerSettings.Android.useCustomKeystore
                ? Path.GetFileName(PlayerSettings.Android.keystoreName) + " / " + PlayerSettings.Android.keyaliasName
                : "개발용 키";
            Debug.Log($"[BuildAab] {kind} 시작 — {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)} · 버전 {ver} ({vc}) · 씬 {string.Join(", ", scenes)} · 키 {ks}");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outPath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = testAds ? new[] { "KACHI_TEST_ADS" } : null,
                });
            }
            finally { EditorUserBuildSettings.buildAppBundle = true; }   // 평소(스토어용)는 AAB 그대로

            var s = report.summary;
            if (s.result != BuildResult.Succeeded)
            {
                foreach (var step in report.steps)
                    foreach (var m in step.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) Debug.Log("[BuildAab] 오류: " + m.content);
                Debug.LogError($"[BuildAab] {kind} 실패 — {s.result} · 오류 {s.totalErrors}개");
                return false;
            }
            LastOutput = outPath;
            Debug.Log($"[BuildAab] {kind} 성공 — {outPath} · {s.totalSize / 1024f / 1024f:0.0} MB · {s.totalTime.TotalMinutes:0.0}분");
            return true;
        }

        // ------------------------------------------------------------------ 비밀번호 확인 (10-13)

        /// <summary>keytool 로 키스토어와 키를 열어 봄 (몇 초). 비밀번호는 명령줄이 아니라 환경 변수로 넘김.</summary>
        public static bool CheckPasswords(string storePass, string keyPass, out string problem)
        {
            problem = null;
            string ks = PlayerSettings.Android.keystoreName;
            string alias = PlayerSettings.Android.keyaliasName;
            if (string.IsNullOrEmpty(storePass)) { problem = "키스토어 비밀번호가 비어 있음"; return false; }
            string keytool = FindKeytool();
            if (keytool == null) { Debug.LogWarning("[BuildAab] keytool 을 못 찾아 비밀번호 미리 확인을 건너뜀"); return true; }

            if (RunKeytool(keytool, $"-list -keystore \"{ks}\" -storepass:env KG_STORE_PASS", storePass, keyPass) != 0)
            {
                problem = "키스토어 비밀번호가 틀림 — 빌드하지 않았습니다 (한/영 키 · Caps Lock 확인)\n  키스토어: " + ks;
                return false;
            }
            if (RunKeytool(keytool, $"-certreq -keystore \"{ks}\" -alias \"{alias}\" -storepass:env KG_STORE_PASS -keypass:env KG_KEY_PASS", storePass, keyPass) != 0)
            {
                problem = $"키(별칭 {alias}) 비밀번호가 틀리거나 별칭이 없음 — 빌드하지 않았습니다. 키 비밀번호에 키스토어 비밀번호와 같은 값을 넣어 보세요";
                return false;
            }
            return true;
        }

        static int RunKeytool(string keytool, string args, string storePass, string keyPass)
        {
            var info = new ProcessStartInfo(keytool, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            };
            info.EnvironmentVariables["KG_STORE_PASS"] = storePass ?? "";
            info.EnvironmentVariables["KG_KEY_PASS"] = string.IsNullOrEmpty(keyPass) ? (storePass ?? "") : keyPass;
            try
            {
                using (var p = Process.Start(info))
                {
                    p.StandardInput.Close();   // 비밀번호를 다시 묻더라도 멈추지 않게
                    var o = p.StandardOutput.ReadToEndAsync();
                    var e = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return -1; }
                    o.Wait(1000); e.Wait(1000);
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BuildAab] keytool 실행 실패 — 미리 확인을 건너뜀: " + ex.Message);
                return 0;
            }
        }

        static string FindKeytool()
        {
            string bundled = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe");
            if (File.Exists(bundled)) return bundled;
            string jdk = EditorPrefs.GetString("JdkPath", "");
            if (!string.IsNullOrEmpty(jdk) && File.Exists(Path.Combine(jdk, "bin/keytool.exe"))) return Path.Combine(jdk, "bin/keytool.exe");
            return null;
        }
    }

    /// <summary>AAB · APK 빌드 창 (10-13, APK 10-23) — 비밀번호는 이 창에서만 쓰이고 저장되지 않음</summary>
    public class AabBuildWindow : EditorWindow
    {
        string storePass = "";
        string keyPass = "";
        bool sameKeyPass = true;
        bool apk;
        bool debugKey = true;   // APK 기본 = 개발용 키 (비밀번호 없이 바로)
        string message = "";
        MessageType messageType = MessageType.Info;

        public static void Open(bool apk)
        {
            var w = GetWindow<AabBuildWindow>(true, apk ? "APK 빌드 (폰에 바로 설치)" : "AAB 빌드 (플레이 스토어)", true);
            w.apk = apk;
            w.message = "";
            w.minSize = new Vector2(500f, 460f);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("만들 파일", EditorStyles.boldLabel);
            int pick = GUILayout.Toolbar(apk ? 1 : 0, new[] { "AAB (플레이 스토어 올리기)", "APK (폰에 바로 설치)" });
            if ((pick == 1) != apk) { apk = pick == 1; message = ""; titleContent = new GUIContent(apk ? "APK 빌드 (폰에 바로 설치)" : "AAB 빌드 (플레이 스토어)"); }
            EditorGUILayout.Space(6f);

            if (apk)
            {
                debugKey = EditorGUILayout.ToggleLeft("개발용 키로 서명 (비밀번호 필요 없음)", debugKey);
                EditorGUILayout.LabelField(debugKey
                    ? "폰에 설치해 보기만 할 때. 플레이 스토어에서 받은 앱이 폰에 있으면 서명이 달라 먼저 지워야 설치됩니다."
                    : "업로드 키로 서명. 플레이 스토어 앱과도 서명이 다르니(스토어는 구글 앱 서명 키) 마찬가지로 먼저 지워야 합니다.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(4f);
            }

            bool needPass = !(apk && debugKey);
            string ks = PlayerSettings.Android.keystoreName;
            if (needPass)
            {
                EditorGUILayout.LabelField("서명 키", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("키스토어 : " + (string.IsNullOrEmpty(ks) ? "(없음 — Publishing Settings 에서 고르세요)" : ks), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("별칭 : " + PlayerSettings.Android.keyaliasName, EditorStyles.miniLabel);

                EditorGUILayout.Space(4f);
                storePass = EditorGUILayout.PasswordField("키스토어 비밀번호", storePass);
                sameKeyPass = EditorGUILayout.ToggleLeft("키 비밀번호도 같음", sameKeyPass);
                if (!sameKeyPass) keyPass = EditorGUILayout.PasswordField("키 비밀번호", keyPass);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("버전", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{PlayerSettings.bundleVersion}  (버전 코드 {PlayerSettings.Android.bundleVersionCode})");
                if (GUILayout.Button("버전 코드 +1", GUILayout.Width(110f))) BuildAab.BumpVersionCode();
            }
            EditorGUILayout.LabelField(apk
                ? "APK 는 폰 시험용이라 버전 코드를 안 올려도 됩니다 (같은 코드로 덮어 설치 가능)."
                : "플레이 스토어에 이미 올린 코드면 +1 하고 빌드하세요.", EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(8f);
            string kp = sameKeyPass ? storePass : keyPass;
            string kind = apk ? "APK" : "AAB";
            using (new EditorGUI.DisabledScope(needPass && string.IsNullOrEmpty(storePass)))
            {
                if (needPass && GUILayout.Button("비밀번호만 확인"))
                {
                    bool ok = BuildAab.CheckPasswords(storePass, kp, out string problem);
                    message = ok ? $"비밀번호가 맞습니다. [{kind} 빌드] 를 누르세요." : problem;
                    messageType = ok ? MessageType.Info : MessageType.Error;
                }
                if (GUILayout.Button($"{kind} 빌드 (10~20분)", GUILayout.Height(30f)))
                {
                    bool ok = needPass ? BuildAab.BuildWith(storePass, kp, apk, false) : BuildAab.BuildWith(null, null, true, true);
                    storePass = keyPass = "";
                    message = ok ? "빌드 끝 — " + Path.GetFileName(BuildAab.LastOutput) + " (Kachi_Gun/Builds 폴더)"
                                 : "빌드하지 못했습니다 — Console 의 [BuildAab] 빨간 줄을 보세요";
                    messageType = ok ? MessageType.Info : MessageType.Error;
                    GUIUtility.ExitGUI();
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.Space(6f);
                EditorGUILayout.HelpBox(message, messageType);
            }
            if (!string.IsNullOrEmpty(BuildAab.LastOutput) && File.Exists(BuildAab.LastOutput) && GUILayout.Button("폴더 열기"))
                EditorUtility.RevealInFinder(BuildAab.LastOutput);
        }

        void OnDestroy() { storePass = keyPass = ""; }
    }
}
