using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// 앱 아이콘 넣기 (10-24, 서바이벌 것을 가져옴 · BuildAab.Setup 이 빌드 때마다 부름). 그림은 tools/app_icon_gen.py 가 Art/AppIcon 에 만든다.
    /// 기본 아이콘 = app_icon · 안드로이드 적응형 = app_icon_bg + app_icon_fg · 둥근 = app_icon_round · 옛 아이콘 = app_icon
    /// </summary>
    public static class AppIconSetup
    {
        const string Dir = "Assets/Art/AppIcon/";

        [MenuItem("카치건/앱 아이콘 넣기", priority = 34)]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var full = Load("app_icon");
            var round = Load("app_icon_round");
            var bg = Load("app_icon_bg");
            var fg = Load("app_icon_fg");
            if (full == null || round == null || bg == null || fg == null)
            {
                Debug.LogError("[AppIcon] Art/AppIcon 그림이 없습니다 — tools/app_icon_gen.py 먼저");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Application);

            var android = NamedBuildTarget.Android;
            Set(android, AndroidPlatformIconKind.Adaptive, bg, fg);
            Set(android, AndroidPlatformIconKind.Round, round);
            Set(android, AndroidPlatformIconKind.Legacy, full);

            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcon] 넣음: 기본 · 적응형 · 둥근 · 옛 아이콘");
        }

        public static void ApplyBatch()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        static void Set(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
            Debug.Log($"[AppIcon] {kind} {icons.Length}칸");
        }

        static Texture2D Load(string name)
        {
            var path = Dir + name + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.mipmapEnabled || imp.textureCompression != TextureImporterCompression.Uncompressed || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.mipmapEnabled = false;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
