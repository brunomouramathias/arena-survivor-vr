using UnityEngine;

namespace ArenaSurvivor.Core
{
    // Serviço de input — abstrai o sistema de entrada
    public class InputService : MonoBehaviour
    {
        public static InputService Instance { get; private set; }

        // === Movimento ===
        public Vector2 MoveInput { get; private set; }

        // === Mouse ===
        public Vector2 MouseScreenPosition { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FireDown { get; private set; }

        // === Ações ===
        public bool ReloadDown { get; private set; }
        public bool DodgeDown { get; private set; }
        public bool PauseDown { get; private set; }
        public float WeaponScrollDelta { get; private set; }
        public int WeaponSlotDown { get; private set; }  // 1, 2 ou 3; 0 = nenhum

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Update()
        {
            ReadInput();
        }

        private void ReadInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
            {
                h = 0f;
                v = 0f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    v += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  v -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  h -= 1f;
            }

            Vector2 rawMove = new Vector2(h, v);
            MoveInput = rawMove.sqrMagnitude > 0.01f ? rawMove.normalized : Vector2.zero;

            MouseScreenPosition = Input.mousePosition;
            FireHeld = Input.GetMouseButton(0);
            FireDown = Input.GetMouseButtonDown(0);

            ReloadDown = Input.GetKeyDown(KeyCode.R);
            DodgeDown  = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Space);
            PauseDown  = Input.GetKeyDown(KeyCode.Escape);

            WeaponScrollDelta = Input.GetAxis("Mouse ScrollWheel");
            WeaponSlotDown = 0;
            if (Input.GetKeyDown(KeyCode.Alpha1)) WeaponSlotDown = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) WeaponSlotDown = 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) WeaponSlotDown = 3;
        }
    }
}
