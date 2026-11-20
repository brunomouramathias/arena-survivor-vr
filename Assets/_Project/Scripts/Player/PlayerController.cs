using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float gravity   = -20f;

        [Header("Dodge")]
        [SerializeField] private float dodgeSpeed    = 16f;
        [SerializeField] private float dodgeDuration = 0.18f;
        [SerializeField] private float dodgeCooldown = 1.2f;

        [Header("VR Waypoint Movement")]
        [SerializeField] private bool useWaypointMovement = true;
        [SerializeField] private float waypointMoveSpeed = 8f;

        private CharacterController cc;
        private Animator animator;
        private PlayerHealth health;
        private Camera mainCamera;
        private InputService inputService;
        private WaypointManager waypointManager;

        private Vector3 verticalVelocity;
        private Vector3 aimPoint;
        private Vector3 velocityHorizontal;

        private bool  isDodging;
        private float dodgeCooldownTimer;
        private float dodgeTimer;
        private Vector3 dodgeDirection;

        private bool isMovingToWaypoint;
        private Vector3 targetWaypointPosition;

        public bool IsDodging => isDodging;
        public float DodgeCooldownNormalized => Mathf.Clamp01(dodgeCooldownTimer / dodgeCooldown);
        public Vector3 AimPoint => aimPoint;
        public bool IsMovingToWaypoint => isMovingToWaypoint;
        public bool IsMovingNow => velocityHorizontal.sqrMagnitude > 0.04f;

        // Velocidade efetiva = base x multiplicador de poderes/loja
        public float EffectiveMoveSpeed =>
            moveSpeed * (PlayerStats.Instance != null ? Mathf.Max(0.2f, PlayerStats.Instance.SpeedMultiplier) : 1f);

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
        private static readonly int LegSpeedHash = Animator.StringToHash("LegSpeed");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");

        [Header("Facing / Aim")]
        [SerializeField] private float turnSpeed = 14f;

        private void Awake()
        {
            cc       = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();
            health   = GetComponent<PlayerHealth>();
            mainCamera = Camera.main;
            waypointManager = WaypointManager.Instance;
        }

        private void Start()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();
            waypointManager = WaypointManager.Instance;

            if (waypointManager != null)
            {
                waypointManager.OnPlayerTeleported += OnPlayerTeleported;
            }
        }

        private void OnDestroy()
        {
            if (waypointManager != null)
            {
                waypointManager.OnPlayerTeleported -= OnPlayerTeleported;
            }
        }

        private void OnPlayerTeleported(Vector3 position)
        {
            verticalVelocity = Vector3.zero;
            if (cc != null && cc.enabled)
            {
                cc.enabled = false;
                cc.enabled = true;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            {
                if (SceneManager.GetActiveScene().name == "Arena" && GameManager.Instance.CurrentState == GameState.Menu)
                    GameManager.Instance.SetState(GameState.Playing);
                else
                    return;
            }

            if (inputService == null)
                inputService = InputService.Instance ?? FindAnyObjectByType<InputService>();

            if (waypointManager == null)
                waypointManager = WaypointManager.Instance;

            HandleAim();
            HandleDodgeCooldown();

            Vector3 moveMotion = Vector3.zero;

            if (isDodging)
            {
                dodgeTimer -= Time.deltaTime;
                moveMotion = dodgeDirection * dodgeSpeed;

                if (dodgeTimer <= 0f)
                {
                    isDodging = false;
                    if (health != null) health.SetInvincible(false);
                }
            }
            else if (isMovingToWaypoint)
            {
                Vector3 toTarget = targetWaypointPosition - transform.position;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;

                if (distance < 0.3f)
                {
                    isMovingToWaypoint = false;
                    moveMotion = Vector3.zero;
                }
                else
                {
                    moveMotion = toTarget.normalized * waypointMoveSpeed;
                }
            }
            else if (useWaypointMovement && waypointManager != null)
            {
                bool moveForward = inputService != null && inputService.MoveInput.y > 0.5f;
                bool moveBackward = inputService != null && inputService.MoveInput.y < -0.5f;

                if (moveForward && waypointManager.CurrentWaypointIndex < waypointManager.WaypointCount - 1)
                {
                    StartMoveToWaypoint(waypointManager.CurrentWaypointIndex + 1);
                }
                else if (moveBackward && waypointManager.CurrentWaypointIndex > 0)
                {
                    StartMoveToWaypoint(waypointManager.CurrentWaypointIndex - 1);
                }
            }
            else
            {
                Vector2 input = inputService != null ? inputService.MoveInput : Vector2.zero;
                Vector3 moveDir = new Vector3(input.x, 0f, input.y);
                if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
                moveMotion = moveDir * EffectiveMoveSpeed;

                if (inputService != null && inputService.DodgeDown && dodgeCooldownTimer <= 0f)
                {
                    Vector3 dashDir = moveDir.sqrMagnitude > 0.01f ? moveDir : transform.forward;
                    StartDodge(dashDir);
                }
            }

            HandleFacing(moveMotion);

            if (cc.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;
            else
                verticalVelocity.y += gravity * Time.deltaTime;

            Vector3 finalMove = moveMotion + verticalVelocity;
            velocityHorizontal = new Vector3(moveMotion.x, 0f, moveMotion.z);
            cc.Move(finalMove * Time.deltaTime);

            UpdateAnimator();
        }

        private void StartMoveToWaypoint(int index)
        {
            if (waypointManager == null) return;
            var target = waypointManager.GetWaypointPosition(index);
            if (target == Vector3.zero) return;

            targetWaypointPosition = target;
            targetWaypointPosition.y = transform.position.y;
            isMovingToWaypoint = true;

            Vector3 dir = (targetWaypointPosition - transform.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void HandleAim()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector2 mousePos = inputService != null ? inputService.MouseScreenPosition : (Vector2)Input.mousePosition;
            var plane = new Plane(Vector3.up, transform.position);
            var ray   = mainCamera.ScreenPointToRay(mousePos);

            if (plane.Raycast(ray, out float enter))
            {
                aimPoint = ray.GetPoint(enter);
            }
        }

        // Mira quando parado ou atirando; ao andar sem atirar o corpo vira para a direção do movimento.
        private void HandleFacing(Vector3 moveMotion)
        {
            Vector3 planar   = new Vector3(moveMotion.x, 0f, moveMotion.z);
            bool    moving   = planar.sqrMagnitude > 0.05f;
            Vector3 desired  = transform.forward;

            // Andando: o corpo SEMPRE encara a direção do movimento (passada sempre "para frente").
            // Parado: encara a mira do mouse.
            if (moving)
                desired = planar.normalized;
            else
            {
                desired = aimPoint - transform.position;
                desired.y = 0f;
            }

            if (desired.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(desired.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void StartDodge(Vector3 dir)
        {
            isDodging          = true;
            dodgeTimer         = dodgeDuration;
            dodgeCooldownTimer = dodgeCooldown;
            dodgeDirection     = dir.normalized;

            if (health != null) health.SetInvincible(true);
            if (animator != null) animator.SetTrigger(DodgeHash);
        }

        private void HandleDodgeCooldown()
        {
            if (dodgeCooldownTimer > 0f) dodgeCooldownTimer -= Time.deltaTime;
        }

        private void UpdateAnimator()
        {
            if (animator == null) return;
            // Velocidade horizontal real, convertida para o espaço local do corpo.
            Vector3 local = transform.InverseTransformDirection(velocityHorizontal);
            float planar  = new Vector2(local.x, local.z).magnitude;
            float norm    = Mathf.Clamp01(planar / Mathf.Max(0.01f, EffectiveMoveSpeed));

            // IMPORTANTE: o blend 2D usa a DIREÇÃO normalizada (não a velocidade).
            // Usar velocidade diluía a passada com o Idle em velocidades baixas => pé deslizando.
            float ramp = Mathf.Clamp01(planar / 0.35f);
            Vector3 dir = planar > 0.01f ? local.normalized : Vector3.zero;

            animator.SetFloat(SpeedHash, norm, 0.08f, Time.deltaTime);
            animator.SetFloat(MoveXHash, dir.x * ramp, 0.08f, Time.deltaTime);
            animator.SetFloat(MoveZHash, dir.z * ramp, 0.08f, Time.deltaTime);
            // Cadência da passada casada com a velocidade real (clipes normalizados em 4 m/s)
            animator.SetFloat(LegSpeedHash, Mathf.Clamp(planar / 3.28f, 0.2f, 2.4f), 0.1f, Time.deltaTime);
        }
    }
}
