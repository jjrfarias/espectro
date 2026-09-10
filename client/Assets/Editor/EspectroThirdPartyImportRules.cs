using System;
using UnityEditor;
using UnityEngine;

namespace Espectro.Editor
{
    public sealed class EspectroThirdPartyImportRules : AssetPostprocessor
    {
        private const string ThirdPartyRoot = "Assets/ThirdParty/";

        private bool IsThirdParty => assetPath.StartsWith(ThirdPartyRoot, StringComparison.OrdinalIgnoreCase) ||
                                     assetPath.StartsWith("Assets/Resources/EspectroModels/", StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessTexture()
        {
            if (!IsThirdParty) return;
            var importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = assetPath.Contains("KayKit") ? 512 : 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.mipmapEnabled = true;

            if (assetPath.Contains("_Normal", StringComparison.OrdinalIgnoreCase) ||
                assetPath.Contains("/Normals ", StringComparison.OrdinalIgnoreCase))
                importer.textureType = TextureImporterType.NormalMap;

            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(android);
        }

        private void OnPreprocessModel()
        {
            if (!IsThirdParty) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;

            var isCharacterOrAnimation = assetPath.Contains("KayKit/Adventurers/Characters") ||
                                         assetPath.Contains("KayKit/Adventurers/Animations");
            importer.importAnimation = isCharacterOrAnimation;
            importer.animationType = isCharacterOrAnimation ? ModelImporterAnimationType.Human : ModelImporterAnimationType.None;
        }
    }
}
