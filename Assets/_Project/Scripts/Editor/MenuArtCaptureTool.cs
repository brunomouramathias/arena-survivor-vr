#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Captura a arte de fundo da tela de abertura a partir da Arena em jogo.
    public static class MenuArtCaptureTool
    {
        [MenuItem("Tools/Arena/Capturar Arte do Menu")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var runner = new GameObject("MenuArtCaptureRunner");
            runner.AddComponent<MenuArtCaptureRunner>();
            SessionState.SetBool("MenuArt.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
