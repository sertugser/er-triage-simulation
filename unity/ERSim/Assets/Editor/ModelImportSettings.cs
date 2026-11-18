using UnityEditor;
using UnityEngine;

namespace ERSim.EditorTools
{
    /// <summary>
    /// Import settings for the third-party models under Assets/Resources/Models,
    /// applied from code so no settings have to be clicked in the Inspector.
    /// Characters use legacy animation clips, which can be played by name without
    /// an Animator Controller asset.
    /// </summary>
    class ModelImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;

            if (assetPath.StartsWith(Root + "Characters/"))
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                importer.importAnimation = true;
                importer.animationWrapMode = WrapMode.Loop;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;   // palette texture: no bleeding between swatches
            importer.mipmapEnabled = false;
        }
    }
}
