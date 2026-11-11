using UnityEditor;
using UnityEngine;

namespace KachiGun.EditorTools
{
    /// <summary>
    /// Resources/Art 그림 가져오기 설정: 압축 없음 · 밉맵 없음 · 최대 4096(맵 2448px) · 부드럽게.
    /// 스프라이트는 코드(Art.cs)가 Sprite.Create 로 잘라 쓰므로 여기서는 텍스처 설정만.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 4096;
            ti.wrapMode = TextureWrapMode.Clamp;
            // 모두 부드럽게
            ti.filterMode = FilterMode.Bilinear;   // 11-05 낙서풍(매끈한 벡터 그림) — 전엔 Gen 도트 아이콘만 Point
            ti.npotScale = TextureImporterNPOTScale.None;
            // 11-11 좀비가 12 → 44종: 좀비 시트(2800x380)는 압축 없이 장당 4.2MB → 안드로이드만 ASTC 4x4(1/4, 선 그림이라 거의 그대로).
            //   맵(배경 판 4장)도 같이. 에디터·자동 시험은 압축 없음 그대로.
            bool astc = p.Contains("/Resources/Art/Zombie/") || p.Contains("/Resources/Art/Map/");
            if (!astc) return;      // 다른 그림은 안드로이드 설정을 건드리지 않음
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 4096;
            android.format = TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(android);
        }

        // 설정을 바꾸면 숫자를 올림 → Unity 가 Resources/Art 그림을 다시 가져옴
        public override uint GetVersion() => 2;
    }
}
