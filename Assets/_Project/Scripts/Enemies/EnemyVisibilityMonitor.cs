using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace ArenaSurvivor.Enemies
{
    // Monitora a visibilidade dos inimigos para evitar inimigos "fantasma" 
    // (invisíveis mas que ainda causam dano)
    public class EnemyVisibilityMonitor : MonoBehaviour
    {
        [Header("Configuração")]
        [SerializeField] private float checkInterval = 1f;
        [SerializeField] private bool autoFix = true;

        private EnemyBase enemyBase;
        private Renderer[] renderers;
        private Collider[] colliders;
        private NavMeshAgent agent;

        private void Awake()
        {
            enemyBase = GetComponent<EnemyBase>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            agent = GetComponent<NavMeshAgent>();
        }

        private void Start()
        {
            StartCoroutine(VisibilityCheckLoop());
        }

        private IEnumerator VisibilityCheckLoop()
        {
            yield return new WaitForSeconds(0.5f); // Espera inicialização

            while (true)
            {
                yield return new WaitForSeconds(checkInterval);
                
                if (enemyBase != null)
                {
                    CheckAndFixVisibility();
                }
            }
        }

        private void CheckAndFixVisibility()
        {
            bool hasIssues = false;

            // Verifica renderers
            foreach (var renderer in renderers)
            {
                if (renderer != null && !renderer.enabled)
                {
                    hasIssues = true;
                    if (autoFix)
                    {
                        renderer.enabled = true;
                        Debug.LogWarning($"[EnemyVisibilityMonitor] Renderer reativado em {gameObject.name}");
                    }
                }
            }

            // Verifica colliders (exceto triggers que podem ser desativados propositalmente)
            foreach (var collider in colliders)
            {
                if (collider != null && !collider.enabled && !collider.isTrigger)
                {
                    hasIssues = true;
                    if (autoFix)
                    {
                        collider.enabled = true;
                        Debug.LogWarning($"[EnemyVisibilityMonitor] Collider reativado em {gameObject.name}");
                    }
                }
            }

            // Verifica NavMeshAgent
            if (agent != null && !agent.enabled && enemyBase != null)
            {
                // Verifica se o inimigo não está morto via reflection
                var isDeadField = enemyBase.GetType().GetField("isDead", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (isDeadField != null)
                {
                    bool isDead = (bool)isDeadField.GetValue(enemyBase);
                    if (!isDead)
                    {
                        hasIssues = true;
                        if (autoFix)
                        {
                            agent.enabled = true;
                            Debug.LogWarning($"[EnemyVisibilityMonitor] NavMeshAgent reativado em {gameObject.name}");
                        }
                    }
                }
            }

            // Verifica se o GameObject está ativo
            if (!gameObject.activeInHierarchy)
            {
                hasIssues = true;
                if (autoFix)
                {
                    gameObject.SetActive(true);
                    Debug.LogWarning($"[EnemyVisibilityMonitor] GameObject reativado: {gameObject.name}");
                }
            }

            if (hasIssues && !autoFix)
            {
                Debug.LogError($"[EnemyVisibilityMonitor] Inimigo com problemas de visibilidade detectado: {gameObject.name}");
            }
        }

        // Método público para forçar verificação
        public void ForceCheck()
        {
            CheckAndFixVisibility();
        }

        // Garante que o inimigo esteja totalmente visível e funcional
        public void EnsureVisibility()
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.enabled = true;
            }

            foreach (var collider in colliders)
            {
                if (collider != null && !collider.isTrigger) collider.enabled = true;
            }

            if (agent != null) agent.enabled = true;

            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);
        }
    }
}