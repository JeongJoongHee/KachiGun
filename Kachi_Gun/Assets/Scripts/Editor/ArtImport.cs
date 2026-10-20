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
            // 작은 도트 아이콘(Gen)·UI 팩 부품(Kit)은 또렷하게, 나머지는 크기를 줄여 쓰므로 부드럽게
            ti.filterMode = p.Contains("/Art/Gen/") && !p.EndsWith("/arrow.png") ? FilterMode.Point : FilterMode.Bilinear;   // 화살표는 매끈하게 그린 그림   // Kit 는 ui_kit.py 가 매끈하게 키워 둠 → 부드럽게
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
