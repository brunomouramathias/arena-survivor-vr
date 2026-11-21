#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    // Play mode: confere o aviso de torre destruída no HUD
    public static class TowerToastTest
    {
        [MenuItem("Tools/Arena/Teste Aviso de Torre Caindo")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);

            var runner = new GameObject("TowerToastProbeRunner");
            runner.AddComponent<TowerToastProbeRunner>();

            SessionState.SetBool("TowerToastProbe.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch() => Run();
    }
}
#endif
