using UnityEngine;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Player
{
    /// <summary>
    /// Gerencia os parâmetros do Animator do jogador.
    /// Funciona tanto com o AnimatorController antigo (Gunplay)
    /// quanto com o novo (SWAT – BlendTree 2D com MoveX/MoveZ).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimation : MonoBehaviour
    {
        private Animator animator;
        private PlayerController controller;
        private InputService input;

        // Hashes dos parâmetros – devem existir no AnimatorController ativo
        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int MoveXHash    = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash    = Animator.StringToHash("MoveZ");
        private static readonly int LegSpeedHash = Animator.StringToHash("LegSpeed");
        private static readonly int ShootHash    = Animator.StringToHash("Shoot");
        private static readonly int DieHash      = Animator.StringToHash("Die");

        // Cache de quais parâmetros o controller atual possui
        private bool hasSpeed, hasMoveX, hasMoveZ, hasLegSpeed, hasShoot, hasDie;

        private void Awake()
        {
            animator   = GetComponent<Animator>();
            controller = GetComponent<PlayerController>();
            EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        private void Start()
        {
            input = InputService.Instance ?? FindAnyObjectByType<InputService>();
            CacheParameterList();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        }

        // ── Detecta quais parâmetros o controller atual tem ───────────────────
        private void CacheParameterList()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            hasSpeed    = HasParam("Speed");
            hasMoveX    = HasParam("MoveX");
            hasMoveZ    = HasParam("MoveZ");
            hasLegSpeed = HasParam("LegSpeed");
            hasShoot    = HasParam("Shoot");
            hasDie      = HasParam("Die");
        }

        private bool HasParam(string name)
        {
            foreach (var p in animator.parameters)
                if (p.name == name) return true;
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (controller == null) return;

            // Recacheiar se o controller mudou em tempo de execução
            if (hasSpeed == false && hasMoveX == false)
                CacheParameterList();

            // Entrada de movimento (eixo local do personagem)
            if (input == null) input = InputService.Instance ?? FindAnyObjectByType<InputService>();
            Vector2 raw = input != null ? input.MoveInput : Vector2.zero;

            float moveX = raw.x;
            float moveZ = raw.y;
            float speed = Mathf.Clamp01(new Vector2(moveX, moveZ).magnitude);
            float legSpeed = Mathf.Clamp(speed / 0.42f, 0.8f, 2.4f); // evita pé deslizando

            if (hasSpeed)    animator.SetFloat(SpeedHash,    speed,    0.08f, Time.deltaTime);
            if (hasMoveX)    animator.SetFloat(MoveXHash,    moveX,    0.08f, Time.deltaTime);
            if (hasMoveZ)    animator.SetFloat(MoveZHash,    moveZ,    0.08f, Time.deltaTime);
            if (hasLegSpeed) animator.SetFloat(LegSpeedHash, legSpeed, 0.10f, Time.deltaTime);
        }

        // ─────────────────────────────────────────────────────────────────────
        public void TriggerShoot()
        {
            if (animator != null && hasShoot)
                animator.SetTrigger(ShootHash);
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            if (animator != null && hasDie)
                animator.SetTrigger(DieHash);
        }

        // Volta o personagem para a pose de pé depois do nocaute.
        // Sem isso o Animator fica travado no clipe de morte (deitado).
        public int ReviveCount { get; private set; }

        public void Revive()
        {
            if (animator == null) return;
            ReviveCount++;

            if (hasDie) animator.ResetTrigger(DieHash);

            animator.Rebind();      // limpa triggers/estados e volta ao estado padrao (Idle)
            animator.Update(0f);

            CacheParameterList();
        }
    }
}
