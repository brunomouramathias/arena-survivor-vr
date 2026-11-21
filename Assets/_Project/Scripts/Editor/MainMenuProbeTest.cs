#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Play test da tela de abertura (MainMenu): layout, modos, "Como jogar" e início de partida.
    public static class MainMenuProbeTest
    {
        [MenuItem("Tools/Arena/Teste Tela de Abertura")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Single);
            var runner = new GameObject("MainMenuProbeRunner");
            runner.AddComponent<MainMenuProbeRunner>();
            SessionState.SetBool("MainMenuProbe.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
