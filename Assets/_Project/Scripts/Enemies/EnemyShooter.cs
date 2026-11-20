using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using ArenaSurvivor.Weapons;
using ArenaSurvivor.Audio;

namespace ArenaSurvivor.Enemies
{
    // Inimigo atirador — mantém distância e dispara projéteis
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyShooter : EnemyBase
    {
        [Header("Configuração do Atirador")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform  shootPoint;
        [SerializeField] private float      preferredDistance = 9f;
        [SerializeField] private float      minDistance       = 6f;
        [SerializeField] private float      shootDamage       = 1f; // 1 coração
        [SerializeField] private float      shootCooldown     = 1.6f;
        [SerializeField] private float      projectileSpeed   = 10f;
        [SerializeField] private float      strafeRadius      = 3f;

        private NavMeshAgent agent;
        private Animator     animator;
        private WizardAnim   wizardAnim;
        private EnemyAudio   enemyAudio;
        private float        strafeTimer;

        private static readonly int SpeedHash  = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

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
            if (agent != null)
            {
                agent.speed = MoveSpeed;                // já com o fator da onda
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

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Mantém distância ideal: recua se muito perto, avança se muito longe
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;

            if (dist < minDistance)
            {
                // Recua
                Vector3 retreatPos = transform.position - toPlayer * 3f;
                if (agent.enabled) agent.SetDestination(retreatPos);
            }
            else if (dist > preferredDistance + 2f)
            {
                // Avança
                if (agent.enabled) agent.SetDestination(playerTransform.position);
            }
            else
            {
                // Strafe lateral
                strafeTimer -= Time.deltaTime;
                if (strafeTimer <= 0f)
                {
                    Vector3 lateral = Vector3.Cross(toPlayer, Vector3.up) * (Random.value > 0.5f ? 1f : -1f);
                    Vector3 strafePos = transform.position + lateral * strafeRadius;
                    if (agent.enabled) agent.SetDestination(strafePos);
                    strafeTimer = Random.Range(1f, 2.5f);
                }
            }

            // Vira para o jogador
            if (dist < preferredDistance + 5f)
            {
                Vector3 dir = toPlayer;
                dir.y = 0f;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }

            if (wizardAnim != null)
                wizardAnim.speed = agent.velocity.magnitude;
            else if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(SpeedHash, agent.velocity.magnitude);

            // Disparo
            if (attackTimer <= 0f && dist <= preferredDistance + 3f)
            {
                Shoot();
            }
        }

        private void Shoot()
        {
            attackTimer = shootCooldown;
            wizardAnim?.PlayCast(0.85f);
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(AttackHash);

            enemyAudio?.PlayAttackSound();

            if (projectilePrefab == null || shootPoint == null) return;

            var proj = Instantiate(projectilePrefab, shootPoint.position, shootPoint.rotation);
            var projComp = proj.GetComponent<Projectile>();
            projComp?.Initialize(projectileSpeed, shootDamage, 20f, false, true);
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
