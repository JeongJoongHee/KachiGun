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
        }
    }
}
