using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    public static class ScenarioPlayTest
    {
        [MenuItem("Tools/Arena/Test Scenario Play")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var runner = new GameObject("ScenarioPlayRunner");
            runner.AddComponent<ScenarioPlayRunner>();
            UnityEditor.SessionState.SetBool("Scenario.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
