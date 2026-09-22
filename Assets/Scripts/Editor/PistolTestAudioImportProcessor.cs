using System;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    sealed class PistolTestAudioImportProcessor : AssetPostprocessor
    {
        const string PistolAssetPrefix =
            "Assets/Resources/Audio/Weapons/Pistol/TestClose/pistol_close_";
        const string PistolCasingAssetPrefix =
            "Assets/Resources/Audio/Weapons/Pistol/Casing/pistol_casing_";
        const string RifleAssetPrefix =
            "Assets/Resources/Audio/Weapons/Rifle/Selected/rifle_close_";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(PistolAssetPrefix, StringComparison.Ordinal) &&
                !assetPath.StartsWith(PistolCasingAssetPrefix, StringComparison.Ordinal) &&
                !assetPath.StartsWith(RifleAssetPrefix, StringComparison.Ordinal)) return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
        }
    }
}
