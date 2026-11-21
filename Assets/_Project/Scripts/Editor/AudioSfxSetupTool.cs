using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Audio;

namespace ArenaSurvivor.Editor
{
    // Instala os dois SFX de combate (dano e morte do jogador) na cena Arena:
    // ajusta as opções de importação dos WAVs e preenche os campos do AudioManager.
    public static class AudioSfxSetupTool
    {
        const string ArenaPath  = "Assets/_Project/Scenes/Arena.unity";
        const string DamagePath = "Assets/_Project/Audio/SFX/Sfx_PlayerDamage.wav";
        const string DeathPath  = "Assets/_Project/Audio/SFX/Sfx_PlayerDeath.wav";

        [MenuItem("Tools/Arena/Instalar Sons de Dano e Morte")]
        public static void Run()
        {
            ApplyImportSettings(DamagePath);
            ApplyImportSettings(DeathPath);

            var damage = AssetDatabase.LoadAssetAtPath<AudioClip>(DamagePath);
            var death  = AssetDatabase.LoadAssetAtPath<AudioClip>(DeathPath);

            if (damage == null || death == null)
            {
                Debug.LogError($"[AUDIO] clipes não encontrados ({DamagePath} / {DeathPath})");
                return;
            }

            var scene   = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Single);
            var manager = Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                Debug.LogError("[AUDIO] AudioManager não encontrado na cena Arena.");
                return;
            }

            var so = new SerializedObject(manager);
            so.FindProperty("playerDamageSound").objectReferenceValue = damage;
            so.FindProperty("playerDeathSound").objectReferenceValue  = death;
            so.ApplyModifiedPropertiesWithoutUndo();

            // volumes baixos por padrão (o jogador sente o som sem ele dominar a música)
            var dmgVol = so.FindProperty("playerDamageVolume");
            var dthVol = so.FindProperty("playerDeathVolume");
            if (dmgVol != null && dmgVol.floatValue > 0.5f) dmgVol.floatValue = 0.28f;
            if (dthVol != null && dthVol.floatValue > 0.5f) dthVol.floatValue = 0.32f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            Debug.Log($"[AUDIO] sons instalados: dano='{damage.name}' ({damage.length:F2}s) " +
                      $"volume={dmgVol?.floatValue:F2} | morte='{death.name}' ({death.length:F2}s) " +
                      $"volume={dthVol?.floatValue:F2}");
        }

        static void ApplyImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as AudioImporter;
            }
            if (importer == null)
            {
                Debug.LogError($"[AUDIO] não consegui importar {path}");
                return;
            }

            // SFX curtinho: memória descomprimida (sem latência) e mono
            var settings = importer.defaultSampleSettings;
            settings.loadType          = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.quality           = 1f;
            settings.preloadAudioData  = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono       = true;
            importer.SaveAndReimport();
        }
    }
}
