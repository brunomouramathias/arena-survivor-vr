using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using ArenaSurvivor.Core;
using ArenaSurvivor.Enemies;

namespace ArenaSurvivor.Audio
{
    // Gerenciador de áudio com mixer, pooling de AudioSources e música em camadas
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Música")]
        [SerializeField] private AudioClip[] musicTracks;   // [0]=normal [1]=boss [2]=intenso
        [SerializeField] private float       crossfadeTime  = 2f;

        [Header("SFX Pool")]
        [SerializeField] private int sfxPoolSize = 20;

        [Header("Volume dos SFX globais (0-1)")]
        [Range(0f, 1f)] [SerializeField] private float playerDamageVolume = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float playerDeathVolume  = 0.50f;
        [Tooltip("Tempo mínimo entre dois sons de dano, para não virar metralhadora")]
        [SerializeField] private float damageSoundCooldown = 0.12f;

        [Header("Global SFX")]
        [SerializeField] private AudioClip playerDamageSound;
        [SerializeField] private AudioClip playerDeathSound;
        [SerializeField] private AudioClip playerHealSound;
        [SerializeField] private AudioClip weaponFireSound;
        [SerializeField] private AudioClip weaponReloadSound;
        [SerializeField] private AudioClip pickupSound;
        [SerializeField] private AudioClip waveStartSound;
        [SerializeField] private AudioClip gameOverSound;
        [SerializeField] private AudioClip victorySound;

        private AudioSource musicSource;
        private AudioSource musicSourceB;   // Para crossfade
        private bool        usingSourceA = true;

        private List<AudioSource> sfxPool = new();

        // controle do som de dano: toca só no golpe, nunca ao curar/nascer
        private float lastDamageSoundTime = -10f;

        // último SFX disparado (usado pelos testes automatizados)
        public AudioClip LastUISound       { get; private set; }
        public float     LastUISoundVolume { get; private set; }
        public AudioClip PlayerDamageClip => playerDamageSound;
        public AudioClip PlayerDeathClip  => playerDeathSound;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Cria AudioSources para música
            musicSource  = gameObject.AddComponent<AudioSource>();
            musicSourceB = gameObject.AddComponent<AudioSource>();
            musicSource.loop  = true;
            musicSourceB.loop = true;
            if (audioMixer != null)
            {
                var group = audioMixer.FindMatchingGroups("Music");
                if (group.Length > 0)
                {
                    musicSource.outputAudioMixerGroup  = group[0];
                    musicSourceB.outputAudioMixerGroup = group[0];
                }
            }

            // Pool de SFX
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                if (audioMixer != null)
                {
                    var group = audioMixer.FindMatchingGroups("SFX");
                    if (group.Length > 0) src.outputAudioMixerGroup = group[0];
                }
                src.spatialBlend = 0f; // 2D para UI sounds
                sfxPool.Add(src);
            }

            // Inscreve no barramento
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
            EventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        }

        private void OnWaveStarted(WaveStartedEvent e)
        {
            if (musicTracks == null || musicTracks.Length == 0) return;

            // Músicas se intensificam nas ondas finais e chefe
            if (e.IsBossWave && musicTracks.Length > 1)
                CrossfadeTo(musicTracks[1]);
            else if (e.WaveNumber >= 15 && musicTracks.Length > 2)
                CrossfadeTo(musicTracks[2]);
            else if (musicTracks.Length > 0)
                CrossfadeTo(musicTracks[0]);

            PlayUISound(waveStartSound);
        }

        // Chamados pelo PlayerHealth: o golpe sabe que houve dano (o evento de vida também
        // dispara ao curar e ao renascer, então não serve para decidir o som).
        public void PlayPlayerDamageSound()
        {
            if (Time.unscaledTime - lastDamageSoundTime < damageSoundCooldown) return;
            lastDamageSoundTime = Time.unscaledTime;
            PlayUISound(playerDamageSound, 0.06f, playerDamageVolume);
        }

        public void PlayPlayerDeathSound()
        {
            PlayUISound(playerDeathSound, 0.03f, playerDeathVolume);
        }

        public void ClearLastUISound() => LastUISound = null;

        private void OnEnemyDied(EnemyDiedEvent e)
        {
            // O som de morte do inimigo é tocado pelo próprio inimigo via EnemyAudio
            // Apenas adicionamos pontos aqui se necessário
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            AudioSource active = usingSourceA ? musicSource : musicSourceB;
            active.clip = clip;
            active.Play();
        }

        public void CrossfadeTo(AudioClip clip)
        {
            if (clip == null) return;
            StartCoroutine(DoCrossfade(clip));
        }

        private IEnumerator DoCrossfade(AudioClip newClip)
        {
            AudioSource fadeOut = usingSourceA ? musicSource  : musicSourceB;
            AudioSource fadeIn  = usingSourceA ? musicSourceB : musicSource;

            fadeIn.clip   = newClip;
            fadeIn.volume = 0f;
            fadeIn.Play();

            float elapsed = 0f;
            float startVol = fadeOut.volume;

            while (elapsed < crossfadeTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / crossfadeTime;
                fadeOut.volume = Mathf.Lerp(startVol, 0f, t);
                fadeIn.volume  = Mathf.Lerp(0f, 1f, t);
                yield return null;
            }

            fadeOut.Stop();
            usingSourceA = !usingSourceA;
        }

        // Reproduz um SFX 2D (UI) com variação de pitch
        public void PlayUISound(AudioClip clip, float pitchVariation = 0.05f, float volume = 1f)
        {
            if (clip == null) return;

            var src = GetFreeSFXSource();
            if (src == null) return;

            LastUISound       = clip;
            LastUISoundVolume = Mathf.Clamp01(volume);

            src.spatialBlend = 0f;
            src.volume       = LastUISoundVolume;
            src.pitch        = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.PlayOneShot(clip);
        }

        // Reproduz um SFX 3D no mundo
        public void PlayWorldSound(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = 0.1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        // Reproduz um SFX 3D no mundo usando pool (para sons frequentes)
        public void PlayPooledWorldSound(AudioClip clip, Vector3 position, float volume = 1f, float pitchVariation = 0.1f, float maxDistance = 20f)
        {
            if (clip == null) return;

            var src = GetFreeSFXSource();
            if (src == null) return;

            src.transform.position = position;
            src.spatialBlend = 1f;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            src.maxDistance = maxDistance;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.PlayOneShot(clip);
        }

        private AudioSource GetFreeSFXSource()
        {
            foreach (var src in sfxPool)
                if (!src.isPlaying) return src;
            return sfxPool[0]; // Reutiliza o primeiro se todos estiverem ocupados
        }

        // Controles de volume para sliders de UI
        public void SetMasterVolume(float v) => SetMixerVolume("MasterVolume", v);
        public void SetMusicVolume(float v)  => SetMixerVolume("MusicVolume", v);
        public void SetSFXVolume(float v)    => SetMixerVolume("SFXVolume", v);

        private void SetMixerVolume(string param, float linear)
        {
            if (audioMixer == null) return;
            float db = linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f;
            audioMixer.SetFloat(param, db);
        }
    }
}
