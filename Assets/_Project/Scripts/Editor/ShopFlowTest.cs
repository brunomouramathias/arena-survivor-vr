#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    public static class ShopFlowTest
    {
        [MenuItem("Tools/Arena/Teste Loja (ícone -> loja -> onda 2)")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var runner = new GameObject("ShopFlowRunner");
            runner.AddComponent<ShopFlowRunner>();
            SessionState.SetBool("ShopFlow.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
