using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Core;
using ArenaSurvivor.UI;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    public static class FreezeGuardTest
    {
        [MenuItem("Tools/Arena/Test Freeze Guard")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();

            var input = new GameObject("InputService");
            input.AddComponent<InputService>();

            var uiRoot = new GameObject("UI");
            uiRoot.AddComponent<UIManager>();

            var runner = new GameObject("Runner");
            runner.AddComponent<FreezeGuardTestRunner>();

            EditorApplication.EnterPlaymode();
        }
    }
}
