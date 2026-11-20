using UnityEngine;

namespace ArenaSurvivor.Enemies
{
    // Barra de vida flutuante acima do inimigo.
    // Construida em runtime (quad + URP Unlit) para funcionar em QUALQUER prefab,
    // sem depender de Canvas/Slider configurados na mao.
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("Tamanho (metros)")]
        [SerializeField] private float width      = 0.95f;
        [SerializeField] private float thickness  = 0.12f;
        [SerializeField] private float headOffset = 0.32f;   // acima do osso da cabeca
        [SerializeField] private float boundsMargin = 0.15f; // fallback: acima do bounding box

        private static Material bgMaterial;
        private static Material fillMaterial;

        // Posicao da barra no mundo (para testes/diagnostico)
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform root;
        private Transform fill;
        private Renderer  fillRenderer;
        private Renderer  bgRenderer;
        private MaterialPropertyBlock mpb;   // criado em Awake (objeto Unity não pode nascer no construtor)
        private Renderer[] bodyRenderers;
        private Transform  headAnchor;
        private Camera cam;

        private int   current = 1;
        private int   maximum = 1;
        private bool  built;
        private float nextBoundsTime;

        public bool  IsBuilt    => built;
        public bool  IsVisible  => root != null && root.gameObject.activeSelf;
        public Vector3 BarWorldPosition => root != null ? root.position : transform.position;
        public float Normalized => maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;

        private void Awake()
        {
            cam = Camera.main;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            RemoveLegacyUi();
            headAnchor = FindHeadAnchor();
        }

        // Acha o osso da cabeca do rig (funciona com os bruxos e com os Mixamo)
        private Transform FindHeadAnchor()
        {
            Transform fallback = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == transform) continue;
                if (t.GetComponent<Renderer>() != null) continue;  // ignora malhas (Head/Body/Skirt...)

                string n = t.name.ToLowerInvariant();
                if (n == "spine.005") return t;                    // topo do rig dos bruxos = cabeca
                if (fallback == null && (n == "head" || n.EndsWith(":head") || n.EndsWith("_head")))
                    fallback = t;
            }
            return fallback;
        }

        private void Start()
        {
            if (!built) Build();
            Reposition();
        }

        // Remove barras antigas baseadas em Canvas/Slider, se o prefab tiver
        private void RemoveLegacyUi()
        {
            var canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c == null || c.transform == transform) continue;
                Destroy(c.gameObject);
            }
        }

        public void SetHealth(int curr, int max)
        {
            current = Mathf.Max(0, curr);
            maximum = Mathf.Max(1, max);
            if (!built) Build();
            if (root != null && !root.gameObject.activeSelf) root.gameObject.SetActive(true);
            UpdateFill();
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (!built) return;

            if (root == null) return;
            if (cam == null) cam = Camera.main;

            // Vira para a camera (billboard) e mantem acima da cabeca
            if (cam != null) root.rotation = cam.transform.rotation;

            if (Time.time >= nextBoundsTime)
            {
                nextBoundsTime = Time.time + 0.25f;
                Reposition();
            }
        }

        private void Reposition()
        {
            if (root == null) return;

            // Preferencia: posicao do osso da cabeca (barra sempre colada no inimigo)
            if (headAnchor != null)
            {
                root.position = headAnchor.position + Vector3.up * headOffset;
                return;
            }

            if (!TryGetHeadPoint(out Vector3 head))
                head = transform.position + Vector3.up * 2f;

            root.position = head + Vector3.up * boundsMargin;
        }

        private bool TryGetHeadPoint(out Vector3 head)
        {
            head = transform.position;
            if (bodyRenderers == null || bodyRenderers.Length == 0)
                bodyRenderers = GetComponentsInChildren<Renderer>(true);

            bool found = false;
            Bounds b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in bodyRenderers)
            {
                if (r == null || !r.enabled) continue;
                if (root != null && r.transform.IsChildOf(root)) continue;   // ignora a propria barra
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }

            if (!found) return false;
            head = new Vector3(b.center.x, b.max.y, b.center.z);
            return true;
        }

        // ------------------------------------------------------------------
        private void Build()
        {
            if (built) return;

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Color");
            if (bgMaterial == null)
            {
                bgMaterial = new Material(unlit) { name = "EnemyBarBG" };
                bgMaterial.SetColor(BaseColorId, new Color(0.04f, 0.04f, 0.06f, 0.85f));
            }
            if (fillMaterial == null)
            {
                fillMaterial = new Material(unlit) { name = "EnemyBarFill" };
                fillMaterial.SetColor(BaseColorId, Color.green);
            }

            root = new GameObject("EnemyHealthBar").transform;
            root.SetParent(transform, false);

            // Compensa a escala do prefab para a barra ter sempre o mesmo tamanho em metros
            Vector3 ls = transform.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.001f, ls.x),
                                          1f / Mathf.Max(0.001f, ls.y),
                                          1f / Mathf.Max(0.001f, ls.z));

            // Fundo
            var bgGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bgGO.name = "BG";
            var bgCol = bgGO.GetComponent<Collider>();
            if (bgCol != null) Destroy(bgCol);
            bgGO.transform.SetParent(root, false);
            bgGO.transform.localScale = new Vector3(width, thickness, 1f);
            bgRenderer = bgGO.GetComponent<Renderer>();
            bgRenderer.sharedMaterial = bgMaterial;
            bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;

            // Preenchimento
            var fillGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillGO.name = "Fill";
            var fillCol = fillGO.GetComponent<Collider>();
            if (fillCol != null) Destroy(fillCol);
            fillGO.transform.SetParent(root, false);
            fillGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            fillGO.transform.localScale = new Vector3(width * 0.92f, thickness * 0.62f, 1f);
            fillRenderer = fillGO.GetComponent<Renderer>();
            fillRenderer.sharedMaterial = fillMaterial;
            fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fillRenderer.receiveShadows = false;
            fill = fillGO.transform;

            built = true;
            UpdateFill();
            Reposition();
        }

        private void UpdateFill()
        {
            if (fill == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();   // Instantiate não roda inicializador de campo

            float n = Normalized;
            float w = width * 0.92f;
            float h = thickness * 0.62f;

            fill.localScale    = new Vector3(Mathf.Max(0.0001f, w * n), h, 1f);
            fill.localPosition = new Vector3(-w * 0.5f * (1f - n), 0f, -0.01f);

            if (fillRenderer != null)
            {
                Color c = Color.Lerp(new Color(0.9f, 0.18f, 0.12f), new Color(0.25f, 0.95f, 0.35f), n);
                fillRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                fillRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
