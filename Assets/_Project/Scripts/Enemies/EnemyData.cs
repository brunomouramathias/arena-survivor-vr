using UnityEngine;

namespace ArenaSurvivor.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "ArenaSurvivor/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identificação")]
        public string enemyName = "Inimigo Comum";

        [Header("Atributos Base")]
        public int   baseHealth    = 40;
        public float moveSpeed     = 3.2f;
        public int   contactDamage = 1; // 1 coração por hit
        public float attackCooldown = 0.9f;

        [Header("Pontuação")]
        public int scoreValue = 10;

        [Header("Drops")]
        [Range(0f, 1f)] public float healthDropChance = 0.15f;
        [Range(0f, 1f)] public float ammoDropChance   = 0.20f;

        [Header("Escalonamento por Onda")]
        [Tooltip("Inimigos comuns: vida = tiros para matar (2 na onda 1, 3 na 2, 4 na 3...) x dano da pistola")]
        public bool useWaveShotRule = true;
        [Tooltip("Multiplicador de vida por onda: vida = base * (1 + hpScalePerWave * waveNum)")]
        public float hpScalePerWave     = 0.12f;
        public float damageScalePerWave = 0.08f;
    }
}
