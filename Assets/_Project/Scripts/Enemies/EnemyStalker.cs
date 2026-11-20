using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaSurvivor.Enemies
{
    // Inimigo Stalker - Persegue o jogador de forma furtiva, teleporta-se atrás dele
    // Tem animações de idle, walk, run, attack, teleport, death
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class EnemyStalker : EnemyBase
    {
        [Header("Stalker Settings")]
        [SerializeField] private float teleportCooldown = 8f;
        [SerializeField] private float teleportRange = 15f;
        [SerializeField] private float minTeleportDistance = 3f;
        [SerializeField] private float stalkSpeed = 2f;
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float detectionRange = 20f;
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private int attackDamage = 15;
        [SerializeField] private float attackCooldown = 1.5f;

        [Header("Visual Effects")]
        [SerializeField] private GameObject teleportEffectPrefab;
        [SerializeField] private GameObject teleportWarningPrefab;
        [SerializeField] private float teleportWarningTime = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioClip idleSound;
        [SerializeField] private AudioClip walkSound;
        [SerializeField] private AudioClip teleportSound;
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip deathSound;
        [SerializeField] private AudioClip hitSound;

        private NavMeshAgent agent;
        private Animator animator;
        private AudioSource audioSource;

        private float teleportTimer;
        private float stalkerAttackTimer;
        private float idleSoundTimer;
        private bool isTeleporting;
        private bool isStalking = true;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int TeleportHash = Animator.StringToHash("Teleport");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        protected override void Awake()
        {
            base.Awake();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();
            
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            
            audioSource.spatialBlend = 1f; // 3D sound
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = 20f;
        }

        public override void Initialize(int waveNumber)
        {
            base.Initialize(waveNumber);

            if (agent != null)
            {
                agent.speed = stalkSpeed * waveSpeedFactor;
                agent.stoppingDistance = attackRange;
                agent.enabled = true;
            }

            teleportTimer = Random.Range(3f, teleportCooldown);
            stalkerAttackTimer = 0f;
            idleSoundTimer = Random.Range(5f, 15f);
            isTeleporting = false;
            isStalking = true;

            // Garante que o renderer esteja ativo
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                r.enabled = true;

            // Garante que o collider esteja ativo
            var colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
                c.enabled = true;

            // Play idle sound
            PlayRandomIdleSound();
        }

        protected override void Update()
        {
            base.Update();
            
            if (isDead || playerTransform == null || isTeleporting) return;

            float dist = Vector3.Distance(transform.position, playerTransform.position);

            // Update animator speed
            if (animator != null && animator.runtimeAnimatorController != null && agent != null)
            {
                animator.SetFloat(SpeedHash, agent.velocity.magnitude);
            }

            // Handle teleport cooldown
            teleportTimer -= Time.deltaTime;

            // Handle attack cooldown
            stalkerAttackTimer -= Time.deltaTime;

            // Handle idle sounds
            idleSoundTimer -= Time.deltaTime;
            if (idleSoundTimer <= 0f && isStalking)
            {
                PlayRandomIdleSound();
                idleSoundTimer = Random.Range(8f, 20f);
            }

            // Behavior: Stalk -> Chase -> Attack -> Teleport
            if (dist <= attackRange && stalkerAttackTimer <= 0f)
            {
                Attack();
            }
            else if (dist <= detectionRange)
            {
                if (isStalking)
                {
                    isStalking = false;
                    if (agent != null) agent.speed = chaseSpeed * waveSpeedFactor;
                }

                // Chase player
                if (agent.enabled && !agent.pathPending)
                {
                    agent.SetDestination(playerTransform.position);
                }

                // Teleport behind player occasionally
                if (teleportTimer <= 0f && dist > minTeleportDistance && dist < teleportRange)
                {
                    StartCoroutine(TeleportBehindPlayer());
                }
            }
            else
            {
                // Patrol/wander when player is far
                if (!isStalking)
                {
                    isStalking = true;
                    if (agent != null) agent.speed = stalkSpeed * waveSpeedFactor;
                }

                if (agent.enabled && agent.remainingDistance < 1f)
                {
                    Vector3 randomPos = GetRandomNavMeshPosition(10f);
                    if (randomPos != Vector3.zero)
                        agent.SetDestination(randomPos);
                }
            }
        }

        private void Attack()
        {
            stalkerAttackTimer = attackCooldown;
            
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(AttackHash);

            PlaySound(attackSound);

            var health = playerTransform.GetComponent<Player.PlayerHealth>();
            health?.TakeDamage(attackDamage);
        }

        private IEnumerator TeleportBehindPlayer()
        {
            isTeleporting = true;
            teleportTimer = teleportCooldown;

            if (agent != null) agent.enabled = false;

            // Show warning at target position
            Vector3 targetPos = GetTeleportPositionBehindPlayer();
            
            GameObject warning = null;
            if (teleportWarningPrefab != null)
            {
                warning = Instantiate(teleportWarningPrefab, targetPos, Quaternion.identity);
            }

            // Play teleport start effect
            if (teleportEffectPrefab != null)
            {
                Instantiate(teleportEffectPrefab, transform.position, Quaternion.identity);
            }
            PlaySound(teleportSound);

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(TeleportHash);

            yield return new WaitForSeconds(teleportWarningTime);

            // Teleport
            transform.position = targetPos;
            transform.LookAt(playerTransform);

            if (teleportEffectPrefab != null)
            {
                Instantiate(teleportEffectPrefab, transform.position, Quaternion.identity);
            }
            PlaySound(teleportSound);

            if (warning != null)
                Destroy(warning);

            if (agent != null)
            {
                agent.enabled = true;
                agent.Warp(transform.position);
                agent.SetDestination(playerTransform.position);
            }

            isTeleporting = false;
        }

        private Vector3 GetTeleportPositionBehindPlayer()
        {
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 behindPlayer = playerTransform.position - toPlayer * (attackRange + 1f);
            
            // Find valid NavMesh position
            if (NavMesh.SamplePosition(behindPlayer, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                return hit.position;
            }

            // Fallback: random position around player
            for (int i = 0; i < 10; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 pos = playerTransform.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * Random.Range(minTeleportDistance, attackRange + 2f);
                
                if (NavMesh.SamplePosition(pos, out hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return transform.position;
        }

        private Vector3 GetRandomNavMeshPosition(float radius)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 randomDir = Random.insideUnitSphere * radius;
                randomDir.y = 0f;
                Vector3 pos = transform.position + randomDir;
                
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            return Vector3.zero;
        }

        protected override void Die()
        {
            if (isDead) return;
            
            // Play death sound before base Die
            PlaySound(deathSound);
            
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(DeathHash);

            base.Die();
        }

        public override void TakeDamage(int amount)
        {
            if (isDead) return;

            base.TakeDamage(amount);

            if (!isDead)
            {
                PlaySound(hitSound);
                
                if (animator != null && animator.runtimeAnimatorController != null)
                    animator.SetTrigger(HitHash);
            }
        }

        private void PlayRandomIdleSound()
        {
            if (isStalking)
            {
                PlaySound(idleSound);
            }
            else
            {
                PlaySound(walkSound);
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = 1f + Random.Range(-0.1f, 0.1f);
                audioSource.PlayOneShot(clip);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (agent != null && agent.isActiveAndEnabled)
                agent.ResetPath();
        }
    }
}