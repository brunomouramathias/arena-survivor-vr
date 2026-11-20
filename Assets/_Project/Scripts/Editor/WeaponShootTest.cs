#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Utils;

namespace ArenaSurvivor.Editor
{
    public static class WeaponShootTest
    {
        [MenuItem("Tools/Arena/Teste Tiro das Armas")]
        public static void Run() { Prepare(); }

        public static void RunBatch() { Prepare(); }

        private static void Prepare()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var runner = new GameObject("WeaponShootRunner");
            runner.AddComponent<WeaponShootRunner>();
            SessionState.SetBool("WeaponShoot.AutoRun", true);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
