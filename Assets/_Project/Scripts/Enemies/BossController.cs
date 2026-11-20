using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ArenaSurvivor.Audio;

namespace ArenaSurvivor.Enemies
{
    // Chefe — fases por % de vida, ataques telegrafados, invoca adds
    [RequireComponent(typeof(NavMeshAgent))]
    public class BossController : EnemyBase
    {
        [Header("Fases")]
        [SerializeField] private float phase2Threshold = 0.6f; // 60% de vida
        [SerializeField] private float phase3Threshold = 0.3f; // 30% de vida

        [Header("Ataque em Área")]
        [SerializeField] private GameObject aoeWarningPrefab;  // Círculo de aviso
        [SerializeField] private float      aoeDamage   = 1f; // 1 coração
        [SerializeField] private float      aoeRadius   = 4f;
        [SerializeField] private float      aoeDelay    = 1.2f;

        [Header("Tiro em Leque")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform  shootPoint;
        [SerializeField] private int        fanBullets  = 7;
        [SerializeField] private float      fanAngle    = 90f;
        [SerializeField] private float      fanDamage   = 1f; // 1 coração

        [Header("Invocação")]
        [SerializeField] private GameObject minionPrefab;
        [SerializeField] private int        minionCount = 3;

        private NavMeshAgent agent;
        private Animator     animator;
        private WizardAnim   wizardAnim;
        private EnemyAudio   enemyAudio;
        private int          currentPhase = 1;
        private float        phaseAttackTimer;

        protected override void Awake()
        {
            base.Awake();
            agent      = GetComponent<NavMeshAgent>();
            animator   = GetComponent<Animator>();
            wizardAnim = GetComponent<WizardAnim>();
            enemyAudio = GetComponent<EnemyAudio>();
        }

        public override void Initialize(int waveNumber)
        {
            base.Initialize(waveNumber);
            currentPhase    = 1;
            phaseAttackTimer = 3f;
            if (agent != null)
            {
                agent.speed   = 2.5f * waveSpeedFactor;
                agent.enabled = true;
            }

            // Garante que o inimigo esteja visível e com colliders ativos
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = true;

            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders) c.enabled = true;
        }

        protected override void Update()
        {
            base.Update();
            if (playerTransform == null) return;

            // Verifica transição de fase
            float hpPct = (float)currentHealth / maxHealth;
            if (currentPhase == 1 && hpPct <= phase2Threshold) EnterPhase(2);
            else if (currentPhase == 2 && hpPct <= phase3Threshold) EnterPhase(3);

            // Persegue o jogador
            if (agent.enabled) agent.SetDestination(playerTransform.position);

            if (wizardAnim != null && agent != null)
                wizardAnim.speed = agent.velocity.magnitude;
            else if (animator != null && animator.runtimeAnimatorController != null && agent != null)
                animator.SetFloat("Speed", agent.velocity.magnitude);

            // Ataque de fase
            phaseAttackTimer -= Time.deltaTime;
            if (phaseAttackTimer <= 0f)
            {
                ExecutePhaseAttack();
                phaseAttackTimer = GetAttackInterval();
            }
        }

        private void EnterPhase(int phase)
        {
            currentPhase = phase;
            // Fase 2: mais rápido
            if (phase == 2 && agent != null) agent.speed = 3.5f * waveSpeedFactor;
            // Fase 3: invoca adds e fica mais agressivo
            if (phase == 3) StartCoroutine(SummonMinions());
        }

        private void ExecutePhaseAttack()
        {
            if (attackTimer > 0f) return;
            attackTimer = data?.attackCooldown ?? 2f;

            switch (currentPhase)
            {
                case 1: StartCoroutine(AoeAttack()); break;
                case 2: FanAttack(); break;
                case 3:
                    StartCoroutine(AoeAttack());
                    FanAttack();
                    break;
            }
        }

        private IEnumerator AoeAttack()
        {
            wizardAnim?.PlayCast(1.1f);
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("CastSpell");

            enemyAudio?.PlayAttackSound();

            if (aoeWarningPrefab != null)
            {
                var warn = Instantiate(aoeWarningPrefab, transform.position, Quaternion.identity);
                yield return new WaitForSeconds(aoeDelay);
                Destroy(warn);
            }
            else
            {
                yield return new WaitForSeconds(aoeDelay);
            }

            // Aplica dano em raio
            var cols = Physics.OverlapSphere(transform.position, aoeRadius, LayerMask.GetMask("Player"));
            foreach (var c in cols)
            {
                var hp = c.GetComponentInParent<Player.PlayerHealth>();
                hp?.TakeDamage((int)aoeDamage);
            }
        }

        private void FanAttack()
        {
            wizardAnim?.PlayAttack(0.7f);
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger("Attack");

            enemyAudio?.PlayAttackSound();

            if (projectilePrefab == null || shootPoint == null) return;

            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float startAngle = -fanAngle * 0.5f;
            float step       = fanAngle / (fanBullets - 1);

            for (int i = 0; i < fanBullets; i++)
            {
                float   angle = startAngle + step * i;
                Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * toPlayer;
                var     proj  = Instantiate(projectilePrefab, shootPoint.position, Quaternion.LookRotation(dir));
                var     pc    = proj.GetComponent<Weapons.Projectile>();
                pc?.Initialize(12f, fanDamage, 20f, false, true);
            }
        }

        private IEnumerator SummonMinions()
        {
            if (minionPrefab == null) yield break;

            for (int i = 0; i < minionCount; i++)
            {
                float angle = i * (360f / minionCount);
                Vector3 spawnPos = transform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 5f;
                var minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);
                minion.GetComponent<EnemyBase>()?.Initialize(0);
                yield return new WaitForSeconds(0.3f);
            }
        }

        private float GetAttackInterval()
        {
            return currentPhase switch
            {
                1 => 4f,
                2 => 2.5f,
                3 => 1.8f,
                _ => 3f
            };
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
    }
}
