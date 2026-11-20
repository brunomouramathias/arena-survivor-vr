using UnityEngine;

namespace ArenaSurvivor.Enemies
{
    // Animação procedural dos bruxos (Fire_Wizard / Air_Wizard).
    // Os FBX do pack Wizard_Free vêm em T-pose e sem rig humanoide compatível,
    // então as poses (idle, andar, conjurar, atacar) são geradas por código sobre
    // os ossos do próprio modelo: previsível, natural e sem retarget/Animator.
    [DisallowMultipleComponent]
    public class WizardAnim : MonoBehaviour
    {
        [Header("Referências (auto se vazio)")]
        public Transform modelRoot;

        [Header("Estado")]
        [Range(0f, 12f)] public float speed;
        [Range(0f, 1f)]  public float cast;
        [Range(0f, 1f)]  public float attack;

        [Header("Pose base (sai da T-pose)")]
        [Tooltip("Ângulo que abaixa os braços da T-pose para a posição natural ao lado do corpo.")]
        public float armDown = 62f;
        [Tooltip("Rotação do antebraço na pose base (leve flexão).")]
        public float elbowBend = 14f;
        [Tooltip("Inclinação lateral da coluna na pose base.")]
        public float spineTilt = 0f;

        [Header("Amplitudes")]
        public float walkLegAmplitude = 26f;
        public float walkArmAmplitude = 20f;
        public float bobHeight = 0.05f;

        [Header("Conjuração (calibrado)")]
        public float castLiftX = -90f;
        public float castOpenZ = -90f;
        public float castElbow = -55f;

        [Header("Efeito de poder")]
        public Color glowColor = new Color(1f, 0.45f, 0.08f, 1f);
        public float glowSize = 0.22f;
        [Tooltip("Deslocamento do orbe a partir da mão direita (para frente da mão).")]
        public Vector3 glowLocalOffset = Vector3.zero;

        Transform spine, chest, neck, head;
        Transform shoulderL, shoulderR, armL, armR, foreL, foreR, handL, handR;
        Transform thighL, thighR, shinL, shinR, footL, footR;

        Quaternion rSpine, rChest, rNeck, rHead, rShoulderL, rShoulderR, rArmL, rArmR, rForeL, rForeR, rHandL, rHandR;
        Quaternion rThighL, rThighR, rShinL, rShinR, rFootL, rFootR;
        Vector3 restSpinePos;
        float phase;
        float castTimer, attackTimer;
        bool ready;

        void Awake() { Build(); }

        GameObject glow;
        GameObject glowLight;
        Transform glowParent;

        // Orbe de energia que aparece nas mãos durante conjuração/ataque.
        void EnsureGlow()
        {
            if (glow != null || handR == null) return;
            glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "WizardPowerGlow";
            var col = glow.GetComponent<Collider>();
            if (col != null) Destroy(col);
            glow.transform.SetParent(handR, false);
            glow.transform.localPosition = glowLocalOffset;
            glow.transform.localRotation = Quaternion.identity;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = "Mat_WizardGlow" };
            mat.SetColor("_BaseColor", glowColor * 3.5f);
            mat.SetColor("_Color", glowColor * 3.5f);
            glow.GetComponent<Renderer>().material = mat;

            // luz pontual para o poder iluminar a cena
            var lightGo = new GameObject("WizardPowerLight");
            lightGo.transform.SetParent(handR, false);
            lightGo.transform.localPosition = glowLocalOffset;
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = glowColor;
            pl.range = 2.4f;
            pl.intensity = 1.6f;
            pl.shadows = LightShadows.None;
            lightGo.SetActive(false);
            glowLight = lightGo;

            glow.SetActive(false);
            glowParent = handR;
        }

        public void Build()
        {
            if (modelRoot == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Model_")) { modelRoot = t; break; }
            }
            if (modelRoot == null) modelRoot = transform;

            var map = new System.Collections.Generic.Dictionary<string, Transform>();
            Collect(modelRoot, map);

            spine  = Find(map, "spine");
            chest  = Find(map, "spine.002") ?? Find(map, "spine.001");
            neck   = Find(map, "spine.004");
            head   = Find(map, "spine.005");
            shoulderL = Find(map, "shoulder.L"); shoulderR = Find(map, "shoulder.R");
            armL   = Find(map, "upper_arm.L");   armR   = Find(map, "upper_arm.R");
            foreL  = Find(map, "forearm.L");     foreR  = Find(map, "forearm.R");
            handL  = Find(map, "hand.L");        handR  = Find(map, "hand.R");
            thighL = Find(map, "thigh.L");       thighR = Find(map, "thigh.R");
            shinL  = Find(map, "shin.L");        shinR  = Find(map, "shin.R");
            footL  = Find(map, "foot.L");        footR  = Find(map, "foot.R");

            rSpine = Rot(spine); rChest = Rot(chest); rNeck = Rot(neck); rHead = Rot(head);
            rShoulderL = Rot(shoulderL); rShoulderR = Rot(shoulderR);
            rArmL = Rot(armL); rArmR = Rot(armR);
            rForeL = Rot(foreL); rForeR = Rot(foreR);
            rHandL = Rot(handL); rHandR = Rot(handR);
            rThighL = Rot(thighL); rThighR = Rot(thighR);
            rShinL = Rot(shinL); rShinR = Rot(shinR);
            rFootL = Rot(footL); rFootR = Rot(footR);
            restSpinePos = spine != null ? spine.localPosition : Vector3.zero;
            phase = 0.7f;
            ready = spine != null;
        }

        static Quaternion Rot(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        static void Collect(Transform t, System.Collections.Generic.Dictionary<string, Transform> map)
        {
            if (!map.ContainsKey(t.name)) map[t.name] = t;
            foreach (Transform c in t) Collect(c, map);
        }

        static Transform Find(System.Collections.Generic.Dictionary<string, Transform> map, string key)
            => map.TryGetValue(key, out var t) ? t : null;

        public void PlayCast(float duration = 0.9f) { castTimer = Mathf.Max(castTimer, duration); }
        public void PlayAttack(float duration = 0.6f) { attackTimer = Mathf.Max(attackTimer, duration); }

        /// <summary>Liga/desliga o orbe de poder na mão (usado também pela prova em edit mode).</summary>
        public void PreviewPower(float amount)
        {
            EnsureGlow();
            if (glow == null) return;
            bool on = amount > 0.02f;
            if (glow.activeSelf != on) glow.SetActive(on);
            if (glowLight != null && glowLight.activeSelf != on) glowLight.SetActive(on);
            if (on)
            {
                glow.transform.localPosition = glowLocalOffset;
                glow.transform.localScale = Vector3.one * glowSize * (0.6f + Mathf.Clamp01(amount));
                if (glowLight != null) glowLight.transform.localPosition = glowLocalOffset;
            }
        }

        void LateUpdate()
        {
            if (castTimer > 0f)   castTimer   = Mathf.Max(0f, castTimer - Time.deltaTime);
            if (attackTimer > 0f) attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
            float cw = castTimer   > 0f ? Mathf.Sin(Mathf.Clamp01(castTimer)   * Mathf.PI) : 0f;
            float aw = attackTimer > 0f ? Mathf.Sin(Mathf.Clamp01(attackTimer) * Mathf.PI) : 0f;
            cast = cw; attack = aw;

            float moveAmt = Mathf.Clamp01(speed / 1.2f);
            phase += Time.deltaTime * Mathf.Lerp(1.7f, 7.0f, Mathf.SmoothStep(0f, 1f, moveAmt));
            Pose(phase, moveAmt, Mathf.Clamp01(cw + aw));

            // orbe de poder
            float power = Mathf.Max(cw, aw);
            PreviewPower(power);
            if (glow != null && glow.activeSelf)
            {
                float pulse = 0.9f + Mathf.Sin(Time.time * 18f) * 0.1f;
                glow.transform.localScale *= pulse;
            }
        }

        // Aplica a pose. Público para permitir prova/render em edit mode.
        public void Pose(float ph, float move, float castBlend)
        {
            if (!ready) { Build(); if (!ready) return; }

            float breathe = Mathf.Sin(ph * 0.45f) * 1.4f;
            float step    = Mathf.Sin(ph);
            float bob     = Mathf.Abs(Mathf.Cos(ph)) * bobHeight * move;

            // tronco
            if (spine != null)
            {
                var lp = restSpinePos;
                lp.y += bob;
                spine.localPosition = lp;
                spine.localRotation = rSpine * Quaternion.Euler(
                    breathe * 0.6f + move * 3f - castBlend * 5f, step * 2.2f * move + spineTilt, 0f);
            }
            if (chest != null)
                chest.localRotation = rChest * Quaternion.Euler(breathe * 0.5f - castBlend * 3f, -step * 1.6f * move, 0f);
            if (neck != null)
                neck.localRotation = rNeck * Quaternion.Euler(-breathe * 0.4f + castBlend * 2f, 0f, 0f);
            if (head != null)
                head.localRotation = rHead * Quaternion.Euler(-castBlend * 3f, Mathf.Sin(ph * 0.19f) * 3f, 0f);

            // braços: preparadas acima
            // Braços: Z abaixa da T-pose (armDown) e também leva à frente;
            // X abre/fecha lateralmente; o balanço da caminhada usa Z (frente/trás)
            // e sai em oposição de fase porque os lados são espelhados na base.
            float lift   = castLiftX * castBlend;
            float open   = castOpenZ * castBlend;
            float swing  = step * walkArmAmplitude * move;
            float baseZL = armDown - open;
            float baseZR = -armDown + open;

            if (armL != null) armL.localRotation = rArmL * Quaternion.Euler(lift, 0f, baseZL - swing);
            if (armR != null) armR.localRotation = rArmR * Quaternion.Euler(lift, 0f, baseZR - swing);
            if (foreL != null) foreL.localRotation = rForeL * Quaternion.Euler(-elbowBend - 8f * move + castElbow * castBlend, 0f, 0f);
            if (foreR != null) foreR.localRotation = rForeR * Quaternion.Euler(-elbowBend - 8f * move + castElbow * castBlend, 0f, 0f);
            if (handL != null) handL.localRotation = rHandL * Quaternion.Euler(-8f * castBlend, 0f, 0f);
            if (handR != null) handR.localRotation = rHandR * Quaternion.Euler(-8f * castBlend, 0f, 0f);
            if (shoulderL != null) shoulderL.localRotation = rShoulderL * Quaternion.Euler(0f, 0f, -4f * castBlend);
            if (shoulderR != null) shoulderR.localRotation = rShoulderR * Quaternion.Euler(0f, 0f, 4f * castBlend);

            // pernas
            float legAmp = walkLegAmplitude * move;
            if (thighL != null) thighL.localRotation = rThighL * Quaternion.Euler(step * legAmp, 0f, 0f);
            if (thighR != null) thighR.localRotation = rThighR * Quaternion.Euler(-step * legAmp, 0f, 0f);
            if (shinL != null)  shinL.localRotation  = rShinL * Quaternion.Euler(Mathf.Max(0f, -step) * legAmp * 1.5f, 0f, 0f);
            if (shinR != null)  shinR.localRotation  = rShinR * Quaternion.Euler(Mathf.Max(0f, step) * legAmp * 1.5f, 0f, 0f);
            if (footL != null)  footL.localRotation  = rFootL * Quaternion.Euler(-step * legAmp * 0.2f, 0f, 0f);
            if (footR != null)  footR.localRotation  = rFootR * Quaternion.Euler(step * legAmp * 0.2f, 0f, 0f);
        }

        /// <summary>Faz o modelo olhar na direção XZ informada (giro suave).</summary>
        public void FaceDirection(Vector3 dir)
        {
            if (modelRoot == null || dir.sqrMagnitude < 0.001f) return;
            var target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 8f * Time.deltaTime);
        }
    }
}
