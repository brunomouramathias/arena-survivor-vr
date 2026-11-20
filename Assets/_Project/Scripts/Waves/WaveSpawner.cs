using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Waves
{
    // Controla o spawning de inimigos ao redor da arena com telegrafia visual
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject chaserPrefab;
        [SerializeField] private GameObject shooterPrefab;
        [SerializeField] private GameObject stalkerPrefab;
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private GameObject spawnWarningPrefab;  // Círculo que aparece antes do spawn

        [Header("Configuração")]
        [SerializeField] private float arenaRadius       = 20f;
        [SerializeField] private float minPlayerDist     = 8f;
        [SerializeField] private float spawnWarningTime  = 1.0f;
        [SerializeField] private WaveConfig config;

        private Transform playerTransform;
        private List<EnemyBase> activeEnemies = new();

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        // Corrotina principal de spawn — retorna quando todos foram spawnados
        public IEnumerator SpawnWave(
            int waveNumber,
            int total,
            bool spawnBoss,
            Action onEnemySpawned,
            Action<EnemyBase> onEnemyDied)
        {
            float shooterRatio = config?.GetShooterRatio(waveNumber) ?? 0f;
            float stalkerRatio = config?.GetStalkerRatio(waveNumber) ?? 0f;
            
            int   shooterCount = Mathf.RoundToInt(total * shooterRatio);
            int   stalkerCount = Mathf.RoundToInt(total * stalkerRatio);

            // Os bruxos (Enemy_Sorcerer) aparecem desde a primeira onda:
            // minimo de 2 por onda, limitado a 1/3 da onda para nao dominar o combate.
            if (shooterPrefab != null)
            {
                int minShooters = waveNumber >= 1 ? 2 : 0;
                int maxShooters = Mathf.Max(minShooters, total / 3);
                shooterCount = Mathf.Clamp(shooterCount, minShooters, maxShooters);
            }

            int   chaserCount  = total - shooterCount - stalkerCount;
            chaserCount = Mathf.Max(0, chaserCount);

            // Spawn do chefe (separado) — sempre que for onda de chefe
            if (spawnBoss && bossPrefab != null)
            {
                yield return SpawnSingle(bossPrefab, waveNumber, onEnemySpawned, onEnemyDied);
            }

            // Spawn dos stalkers (aparecem a partir da onda 3)
            for (int i = 0; i < stalkerCount; i++)
            {
                yield return SpawnSingle(stalkerPrefab, waveNumber, onEnemySpawned, onEnemyDied);
                yield return new WaitForSeconds(config?.spawnInterval ?? 0.8f);
            }

            // Spawn dos atiradores
            for (int i = 0; i < shooterCount; i++)
            {
                yield return SpawnSingle(shooterPrefab, waveNumber, onEnemySpawned, onEnemyDied);
                yield return new WaitForSeconds(config?.spawnInterval ?? 0.8f);
            }

            // Spawn dos perseguidores (comuns)
            for (int i = 0; i < chaserCount; i++)
            {
                yield return SpawnSingle(chaserPrefab, waveNumber, onEnemySpawned, onEnemyDied);
                yield return new WaitForSeconds(config?.spawnInterval ?? 0.8f);
            }
        }

        private IEnumerator SpawnSingle(
            GameObject prefab,
            int waveNumber,
            Action onEnemySpawned,
            Action<EnemyBase> onEnemyDied)
        {
            if (prefab == null) yield break;

            Vector3 pos = GetSpawnPosition();

            // Aviso visual (círculo no chão)
            if (spawnWarningPrefab != null)
            {
                var warn = Instantiate(spawnWarningPrefab, pos, Quaternion.identity);
                yield return new WaitForSeconds(spawnWarningTime);
                Destroy(warn);
            }
            else
            {
                yield return new WaitForSeconds(0.3f);
            }

            // Instancia o inimigo
            var go    = Instantiate(prefab, pos, Quaternion.identity);
            var enemy = go.GetComponent<EnemyBase>();

            if (enemy != null)
            {
                enemy.Initialize(waveNumber);
                enemy.OnDied += onEnemyDied;
                activeEnemies.Add(enemy);
                onEnemySpawned?.Invoke();
            }
        }

        private Vector3 GetSpawnPosition()
        {
            // Tenta encontrar posição válida ao redor da arena
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float   angle = UnityEngine.Random.Range(0f, 360f);
                float   r     = UnityEngine.Random.Range(arenaRadius - 1.6f, arenaRadius - 0.4f); // junto às paredes
                Vector3 pos   = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * r,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * r
                );

                // Verifica distância mínima do jogador
                if (playerTransform == null) return pos;
                if (Vector3.Distance(pos, playerTransform.position) >= minPlayerDist)
                    return pos;
            }

            // Fallback: posição oposta ao jogador
            if (playerTransform != null)
            {
                Vector3 opp = -playerTransform.position.normalized * arenaRadius * 0.8f;
                opp.y = 0f;
                return opp;
            }

            return Vector3.zero;
        }
    }
}
