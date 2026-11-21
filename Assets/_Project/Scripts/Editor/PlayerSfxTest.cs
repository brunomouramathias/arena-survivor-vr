#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Play mode: valida os sons de dano e morte do jogador (clipes, volume e quando tocam)
    public static class PlayerSfxTest
    {
        [MenuItem("Tools/Arena/Teste Sons de Dano e Morte")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            var runner = new GameObject("PlayerSfxProbeRunner");
            runner.AddComponent<PlayerSfxProbeRunner>();

            SessionState.SetBool("PlayerSfxProbe.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch() => Run();
    }
}
#endif
