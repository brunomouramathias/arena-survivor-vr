#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Play mode: captura a tela de fim de jogo com o tempo de sobrevivência
    public static class GameOverShotTest
    {
        [MenuItem("Tools/Arena/Capturar Tela de Fim de Jogo")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            var runner = new GameObject("GameOverShotProbeRunner");
            runner.AddComponent<GameOverShotProbeRunner>();

            SessionState.SetBool("GameOverShot.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch() => Run();
    }
}
#endif
