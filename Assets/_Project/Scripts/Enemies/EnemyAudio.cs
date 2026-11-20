using UnityEngine;
using UnityEngine.AI;
using ArenaSurvivor.Audio;

namespace ArenaSurvivor.Enemies
{
    [RequireComponent(typeof(AudioSource))]
    public class EnemyAudio : MonoBehaviour
    {
        [Header("Clipes de Áudio")]
        [SerializeField] private AudioClip[] idleSounds;
        [SerializeField] private AudioClip[] walkSounds;
        [SerializeField] private AudioClip[] attackSounds;
        [SerializeField] private AudioClip[] hitSounds;
        [SerializeField] private AudioClip[] deathSounds;
        [SerializeField] private AudioClip[] teleportSounds;

        [Header("Configurações")]
        [SerializeField] private float idleSoundInterval = 5f;
        [SerializeField] private float walkSoundInterval = 0.5f;
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;
        [SerializeField] private float volume = 1f;
        [SerializeField] private float maxDistance = 20f;

        private AudioSource audioSource;
        private NavMeshAgent agent;
        private EnemyBase enemyBase;
        private float idleTimer;
        private float walkTimer;
        private bool wasMoving;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            agent = GetComponent<NavMeshAgent>();
            enemyBase = GetComponent<EnemyBase>();

            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.maxDistance = maxDistance;
            audioSource.volume = volume;
        }

        private void Start()
        {
            idleTimer = Random.Range(0f, idleSoundInterval);
            walkTimer = 0f;
        }

        private void Update()
        {
            if (enemyBase != null && enemyBase.GetType().GetField("isDead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(enemyBase) as bool? == true)
                return;

            bool isMoving = agent != null && agent.velocity.magnitude > 0.5f;

            // Sons de movimento (passos)
            if (isMoving)
            {
                walkTimer -= Time.deltaTime;
                if (walkTimer <= 0f && walkSounds.Length > 0)
                {
                    PlayRandomSound(walkSounds, 0.8f);
                    walkTimer = walkSoundInterval;
                }
            }
            else
            {
                walkTimer = 0f;
            }

            // Sons de idle (ambiente)
            if (!isMoving)
            {
                idleTimer -= Time.deltaTime;
                if (idleTimer <= 0f && idleSounds.Length > 0)
                {
                    PlayRandomSound(idleSounds, 0.5f);
                    idleTimer = Random.Range(idleSoundInterval * 0.5f, idleSoundInterval * 1.5f);
                }
            }
            else
            {
                idleTimer = Mathf.Max(idleTimer, 0f);
            }

            wasMoving = isMoving;
        }

        public void PlayAttackSound()
        {
            PlayRandomSound(attackSounds, 1f);
        }

        public void PlayHitSound()
        {
            PlayRandomSound(hitSounds, 1f);
        }

        public void PlayDeathSound()
        {
            PlayRandomSound(deathSounds, 1f);
        }

        public void PlayTeleportSound()
        {
            PlayRandomSound(teleportSounds, 1f);
        }

        private void PlayRandomSound(AudioClip[] clips, float volumeScale = 1f)
        {
            if (clips == null || clips.Length == 0 || audioSource == null) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;

            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.volume = volume * volumeScale;
            audioSource.PlayOneShot(clip);
        }

        // Métodos estáticos para uso sem componente
        public static void PlaySoundAt(AudioClip clip, Vector3 position, float volume = 1f, float maxDistance = 20f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        public static void PlayRandomSoundAt(AudioClip[] clips, Vector3 position, float volume = 1f, float maxDistance = 20f, float minPitch = 0.9f, float maxPitch = 1.1f)
        {
            if (clips == null || clips.Length == 0) return;
            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == null) return;
            
            var tempSource = new GameObject("TempAudio").AddComponent<AudioSource>();
            tempSource.transform.position = position;
            tempSource.spatialBlend = 1f;
            tempSource.rolloffMode = AudioRolloffMode.Linear;
            tempSource.maxDistance = maxDistance;
            tempSource.volume = volume;
            tempSource.pitch = Random.Range(minPitch, maxPitch);
            tempSource.PlayOneShot(clip);
            Destroy(tempSource.gameObject, clip.length + 0.1f);
        }
    }
}