using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;
using ArenaSurvivor.Items;

namespace ArenaSurvivor.Waves
{
    public enum WaveState { Preparing, Spawning, InCombat, Cleared, Rest }

    // Gerencia o ciclo de ondas: preparar, spawnar, combater, limpar, descansar, loja
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }

        [Header("Configuração")]
        [SerializeField] private WaveConfig config;
        [SerializeField] private WaveSpawner spawner;

        [Header("Vitória")]
        [SerializeField] private int victoryWave = 20;

        private int   currentWave  = 0;
        private int   aliveEnemies = 0;
        private Transform playerTransform;
        private WaveState state    = WaveState.Preparing;

        // Onde o último inimigo da onda morreu (o poder brota ali)
        private Vector3 lastDeathPos = Vector3.zero;
        private bool    hasDeathPos;

        public int  CurrentWave  => currentWave;
        public bool IsBossWave   => config != null && config.IsBossWave(currentWave);
        public WaveState State   => state;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerTransform = player.transform;
            PowerUpPickup.ResetProgression();
            StartCoroutine(WaveLoop());
        }

        private IEnumerator WaveLoop()
        {
            while (true)
            {
                // Aguarda o jogo estar em estado Playing (não na loja/pausa)
                yield return new WaitUntil(() =>
                    GameManager.Instance != null &&
                    GameManager.Instance.CurrentState == GameState.Playing);

                currentWave++;
                bool bossWave = IsBossWave;

                // Aviso de nova onda
                SetState(WaveState.Preparing);
                EventBus.Publish(new WaveStartedEvent { WaveNumber = currentWave, IsBossWave = bossWave });

                yield return new WaitForSeconds(3f);  // Tempo de aviso

                // Spawn da onda
                SetState(WaveState.Spawning);
                // quantidade da onda ajustada pela dificuldade (mínimo 3 para a arena não ficar vazia)
                float countMul = Difficulty.Profile.EnemyCount;
                int total = Mathf.Max(3, Mathf.RoundToInt(config.GetEnemyCount(currentWave) * countMul));
                aliveEnemies = 0;

                yield return spawner.SpawnWave(currentWave, total, bossWave, OnEnemySpawned, OnEnemyDied);

                SetState(WaveState.InCombat);

                // Aguarda todos os inimigos morrerem
                yield return new WaitUntil(() => aliveEnemies <= 0 && state == WaveState.InCombat);

                // Onda concluída
                SetState(WaveState.Cleared);
                int bonus = currentWave * 25;
                ScoreService.Instance?.AddScore(bonus);
                EventBus.Publish(new WaveClearedEvent { WaveNumber = currentWave, BonusScore = bonus });

                // Brota o ícone de poder onde o último inimigo caiu e espera o jogador pegar
                if (currentWave < victoryWave)
                    yield return SpawnAndWaitPowerUp();

                // Verificação de vitória
                if (currentWave >= victoryWave)
                {
                    GameManager.Instance?.SetState(GameState.Victory);
                    yield break;
                }

                // Descanso + Loja
                SetState(WaveState.Rest);
                GameManager.Instance?.SetState(GameState.Shop);

                // Aguarda o jogador sair da loja (Shop -> Playing)
                yield return new WaitUntil(() =>
                    GameManager.Instance != null &&
                    GameManager.Instance.CurrentState == GameState.Playing);

                // Descanso curto após fechar a loja
                yield return new WaitForSeconds(config?.restDuration * 0.3f ?? 2f);
            }
        }

        private void OnEnemySpawned()
        {
            aliveEnemies++;
        }

        private void OnEnemyDied(EnemyBase enemy)
        {
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            if (enemy != null)
            {
                lastDeathPos = enemy.transform.position;
                hasDeathPos  = true;
            }
        }

        // Cria o poder no local do último inimigo e espera a coleta (ou 14s, aí aplica sozinho)
        private IEnumerator SpawnAndWaitPowerUp()
        {
            Vector3 pos = hasDeathPos
                ? lastDeathPos
                : (playerTransform != null ? playerTransform.position + playerTransform.forward * 2f : Vector3.zero);

            var pu = PowerUpPickup.SpawnNext(pos);
            hasDeathPos = false;

            float waited = 0f;
            while (pu != null && !pu.Collected && waited < 14f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // Se o jogador não buscou, aplica automaticamente para a onda não travar
            if (pu != null) pu.Collect();
        }

        private void SetState(WaveState newState)
        {
            state = newState;
        }

        // Atalho de debug: pula para a onda N (apenas editor)
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugSkipToWave(int waveNumber)
        {
            StopAllCoroutines();
            currentWave  = waveNumber - 1;
            aliveEnemies = 0;
            StartCoroutine(WaveLoop());
        }
    }
}
