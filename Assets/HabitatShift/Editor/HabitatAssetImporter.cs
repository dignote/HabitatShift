using UnityEditor;
using UnityEngine;
namespace HabitatShift.Editor {
public sealed class HabitatAssetImporter:AssetPostprocessor {
 void OnPreprocessTexture(){if(!assetPath.Contains("/Resources/HabitatShift/"))return;var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.maxTextureSize=1024;t.textureCompression=TextureImporterCompression.Compressed;if(assetPath.Contains("/Board/")){t.spritePixelsPerUnit=192;var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);t.spriteBorder=assetPath.EndsWith("board_card.png")?new Vector4(36,36,36,36):Vector4.zero;var android=new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=1024,format=TextureImporterFormat.ASTC_6x6};t.SetPlatformTextureSettings(android);}}
}
}

