using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ArenaSurvivor.Core;
using ArenaSurvivor.Items;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Enemies
{
    // Classe base para todos os inimigos
    public class EnemyBase : MonoBehaviour
    {
        // A vida por onda agora vem de Waves.WaveTuning (2 tiros na onda 1, 3 na 2, ...).

        [SerializeField] protected EnemyData data;

        // A barra e SEMPRE criada no root do inimigo (nao usamos a referencia antiga
        // do prefab, que apontava para um Canvas/Slider filho e era destruida).
        private EnemyHealthBar healthBar;

        protected int   maxHealth;
        protected int   currentHealth;
        protected bool  isDead;
        protected float attackTimer;

        protected Transform playerTransform;

        // Referência ao pool para retornar
        public event System.Action<EnemyBase> OnDied;

        private Renderer[] renderers;
        private MaterialPropertyBlock mpb;
        private NavMeshAgent navMeshAgent;
        private EnemyVisibilityMonitor visibilityMonitor;

        protected virtual void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            mpb       = new MaterialPropertyBlock();
            navMeshAgent = GetComponent<NavMeshAgent>();
            visibilityMonitor = GetComponent<EnemyVisibilityMonitor>();
            
            if (visibilityMonitor == null)
            {
                visibilityMonitor = gameObject.AddComponent<EnemyVisibilityMonitor>();
            }

            EnsureHealthBar();
        }

        private EnemyHealthBar EnsureHealthBar()
        {
            if (healthBar != null) return healthBar;
            healthBar = GetComponent<EnemyHealthBar>();
            if (healthBar == null) healthBar = gameObject.AddComponent<EnemyHealthBar>();
            return healthBar;
        }

        public int   MaxHealth        => maxHealth;
        public int   CurrentHealth    => currentHealth;
        public float HealthNormalized => maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;

        // Velocidade já escalada pela onda (inimigos lentos na 1ª, rápidos depois)
        protected float waveSpeedFactor = 1f;
        public float MoveSpeed => (data != null ? data.moveSpeed : 3.2f) * waveSpeedFactor;

        public virtual void Initialize(int waveNumber)
        {
            if (data == null) return;

            waveSpeedFactor = Waves.WaveTuning.SpeedFactor(waveNumber);

            if (data.useWaveShotRule)
            {
                // Regra das ondas: 2 tiros na onda 1, 3 na onda 2, 4 na onda 3...
                maxHealth = Waves.WaveTuning.HealthForWave(waveNumber);
            }
            else
            {
                // Chefe e unidades especiais: vida própria, crescendo com a onda
                float scale = 1f + data.hpScalePerWave * waveNumber;
                maxHealth   = Mathf.RoundToInt(data.baseHealth * scale);
            }

            currentHealth = maxHealth;
            isDead       = false;
            attackTimer  = 0f;

            // Encontra o jogador
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null) playerTransform = playerGO.transform;

            EnsureHealthBar().SetVisible(true);
            EnsureHealthBar().SetHealth(currentHealth, maxHealth);

            EventBus.Subscribe<PlayerTeleportedEvent>(OnPlayerTeleported);

            // Garante visibilidade total
            visibilityMonitor?.EnsureVisibility();
        }

        private void OnPlayerTeleported(PlayerTeleportedEvent e)
        {
            if (navMeshAgent != null && navMeshAgent.enabled && playerTransform != null)
            {
                navMeshAgent.SetDestination(playerTransform.position);
            }
        }

        protected virtual void OnDisable()
        {
            EventBus.Unsubscribe<PlayerTeleportedEvent>(OnPlayerTeleported);
        }

        public virtual void TakeDamage(int amount)
        {
            if (isDead) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);
            EnsureHealthBar().SetHealth(currentHealth, maxHealth);
            if (Application.isPlaying) StartCoroutine(HitFlash());   // em edit mode (prefab/teste) não roda

            // Texto flutuante de dano
            PoolService.Instance?.ShowFloatingText(
                transform.position + Vector3.up * 2f,
                amount.ToString(),
                Color.yellow
            );

            if (currentHealth <= 0) Die();
        }

        protected virtual void Die()
        {
            if (isDead) return;
            isDead = true;

            healthBar?.SetVisible(false);

            // Publica evento de morte
            EventBus.Publish(new EnemyDiedEvent
            {
                Score    = data?.scoreValue ?? 10,
                Position = transform.position
            });

            // Adiciona pontuação
            ScoreService.Instance?.AddScore(data?.scoreValue ?? 10);

            // Drop de itens
            TryDrop();

            // Partículas de morte (se houver)
            if (Application.isPlaying) StartCoroutine(DieEffect());
        }

        private void TryDrop()
        {
            if (data == null) return;

            // Health drop
            if (Random.value < data.healthDropChance)
                DropManager.Instance?.DropHealth(transform.position);

            // Ammo drop
            if (Random.value < data.ammoDropChance)
                DropManager.Instance?.DropAmmo(transform.position);
        }

        private IEnumerator DieEffect()
        {
            // Desativa colliders imediatamente
            foreach (var col in GetComponentsInChildren<Collider>())
                col.enabled = false;

            yield return new WaitForSeconds(0.3f);

            OnDied?.Invoke(this);
            gameObject.SetActive(false);
        }

        private IEnumerator HitFlash()
        {
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", new Color(1f, 0.45f, 0.4f));
                r.SetPropertyBlock(mpb);
            }
            yield return new WaitForSecondsRealtime(0.08f);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);   // devolve a cor original
            }
        }

        protected virtual void Update()
        {
            if (isDead) return;
            attackTimer -= Time.deltaTime;
        }

        // Propriedade pública para verificar se está morto
        public bool IsDead => isDead;
    }
}
