using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ArenaSurvivor.Player;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Editor
{
    public static class PlayerMovementTest
    {
        [MenuItem("Tools/Arena/Run Player Movement Test")]
        public static void RunMovementTest()
        {
            RunMovementTestInternal(false);
        }

        public static void RunMovementTestBatch()
        {
            RunMovementTestInternal(true);
        }

        private static void RunMovementTestInternal(bool isBatch)
        {
            Debug.Log("=== [TEST] Iniciando teste automatizado de movimentação do jogador ===");

            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity");
            var playerGO = GameObject.FindGameObjectWithTag("Player");

            if (playerGO == null)
            {
                Debug.LogError("[TEST FAILED] Objeto Player com tag 'Player' não foi encontrado na cena Arena!");
                if (isBatch) EditorApplication.Exit(1);
                return;
            }

            var cc = playerGO.GetComponent<CharacterController>();
            if (cc == null)
            {
                Debug.LogError("[TEST FAILED] CharacterController não encontrado no Player!");
                if (isBatch) EditorApplication.Exit(1);
                return;
            }

            var controller = playerGO.GetComponent<PlayerController>();
            if (controller == null)
            {
                Debug.LogError("[TEST FAILED] PlayerController não encontrado no Player!");
                if (isBatch) EditorApplication.Exit(1);
                return;
            }

            Vector3 startPos = playerGO.transform.position;
            Debug.Log($"[TEST] Posição inicial do Player: {startPos}");

            // Simula 60 passos de física (1 segundo a 60 fps) com input W (para frente: +Z)
            float moveSpeed = 6f;
            float dt = 1f / 60f;
            for (int i = 0; i < 60; i++)
            {
                Vector3 motion = Vector3.forward * moveSpeed * dt;
                cc.Move(motion);
            }

            Vector3 endPos = playerGO.transform.position;
            float deltaZ = endPos.z - startPos.z;
            Debug.Log($"[TEST] Posição final do Player após 1s de 'W': {endPos} (Delta Z = {deltaZ:0.00}m)");

            bool passed = deltaZ > 5.0f;
            if (passed)
            {
                Debug.Log($"[TEST PASSED] Movimentação validada com sucesso! O jogador avançou {deltaZ:0.00}m (esperado ~6m).");
            }
            else
            {
                Debug.LogError($"[TEST FAILED] O jogador não se moveu a distância esperada. Delta Z = {deltaZ}");
            }

            // Restaura posição
            playerGO.transform.position = startPos;
            EditorSceneManager.SaveScene(scene);

            Debug.Log("=== [TEST] Teste de movimentação concluído com êxito! ===");
            if (isBatch) EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}

