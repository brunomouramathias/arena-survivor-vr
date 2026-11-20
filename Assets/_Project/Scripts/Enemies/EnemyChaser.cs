using UnityEngine;
using UnityEngine.AI;
using ArenaSurvivor.Player;
using ArenaSurvivor.Audio;
using ArenaSurvivor.World;

namespace ArenaSurvivor.Enemies
{
    // Inimigo comum — corre em direção ao jogador e ataca corpo a corpo
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyChaser : EnemyBase
    {
        private NavMeshAgent agent;
        private Animator     animator;
        private EnemyAudio   enemyAudio;
        private TowerHealth  targetTower;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        protected override void Awake()
        {
            base.Awake();
            agent      = GetComponent<NavMeshAgent>();
            animator   = GetComponent<Animator>();
            enemyAudio = GetComponent<EnemyAudio>();
        }

        public override void Initialize(int waveNumber)
        {
            base.Initialize(waveNumber);

            if (agent == null) return;
            agent.speed           = MoveSpeed;                      // já com o fator da onda
            agent.stoppingDistance = 1.2f;
            agent.enabled         = true;

            // Garante que o inimigo esteja visível e com colliders ativos
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = true;

            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = true;
        }

        protected override void Update()
        {
            base.Update();
            if (playerTransform == null || !agent.enabled) return;

            // Escolhe alvo: torre próxima (objetivo) ou o jogador
            Transform target = PickTarget();

            agent.SetDestination(target.position);

            // Animar velocidade
            float spd = agent.velocity.magnitude;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(SpeedHash, spd);

            // Ataque corpo a corpo (torre ou jogador)
            float dist = Vector3.Distance(transform.position, target.position);
            float reach = targetTower != null ? 3.2f : agent.stoppingDistance + 0.5f;
            if (dist <= reach && attackTimer <= 0f)
            {
                Attack();
            }
        }

        private Transform PickTarget()
        {
            targetTower = null;
            if (playerTransform == null) return transform;

            float best = Vector3.Distance(transform.position, playerTransform.position);
            Transform target = playerTransform;

            // Jogador muito perto tem prioridade (pode "body-block")
            if (best <= 5f) return target;

            foreach (var t in TowerHealth.All)
            {
                if (t == null || t.Destroyed) continue;
                float d = Vector3.Distance(transform.position, t.transform.position);
                // Prioriza a torre mais próxima dentro do raio de agressão
                if (d < 13f && d < best)
                {
                    best = d;
                    target = t.transform;
                    targetTower = t;
                }
            }
            return target;
        }

        private void Attack()
        {
            attackTimer = data?.attackCooldown ?? 0.9f;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(AttackHash);

            enemyAudio?.PlayAttackSound();

            int dmg = data?.contactDamage ?? 10;
            if (targetTower != null)
            {
                targetTower.TakeDamage(dmg);
            }
            else
            {
                var health = playerTransform.GetComponent<PlayerHealth>();
                health?.TakeDamage(dmg);
            }
        }

        public override void TakeDamage(int amount)
        {
            base.TakeDamage(amount);
            enemyAudio?.PlayHitSound();
        }

        protected override void Die()
        {
            enemyAudio?.PlayDeathSound();
            base.Die();
        }

        protected override void OnDisable()
        {
            if (agent != null && agent.isActiveAndEnabled)
                agent.ResetPath();
        }
    }
}
