#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using ArenaSurvivor.Player;

namespace ArenaSurvivor.Utils
{
    // Mede em Play Mode quanto o pé de apoio escorrega (m/s) em cada direção.
    public class FootSlipRunner : MonoBehaviour
    {
        public static bool AutoRun => SessionState.GetBool("FootSlip.AutoRun", false);
        GameObject player;
        Animator anim;
        Transform footL, footR;
        readonly StringBuilder sb = new StringBuilder();

        private IEnumerator Start()
        {
            if (!AutoRun) { enabled = false; yield break; }

            sb.AppendLine("=== [FOOT SLIP] " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
            player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) { Finish(); yield break; }
            anim = player.GetComponent<Animator>();
            var pc = player.GetComponent<PlayerController>();
            var ps = player.GetComponent<PlayerShooter>();
            var pa = player.GetComponent<PlayerAnimation>();
            if (pc) pc.enabled = false;
            if (ps) ps.enabled = false;
            if (pa) pa.enabled = false;

            footL = FindDeep(player.transform, "Foot.L");
            footR = FindDeep(player.transform, "Foot.R");
            sb.AppendLine($"pés: L={footL != null} R={footR != null}");

            yield return new WaitForSeconds(0.4f);

            yield return Measure(Vector3.forward, 6f, "frente 6 m/s");
            yield return Measure(Vector3.back, 6f, "trás 6 m/s");
            yield return Measure(Vector3.right, 6f, "direita 6 m/s");
            yield return Measure(Vector3.left, 6f, "esquerda 6 m/s");
            yield return Measure(Vector3.back, 2f, "trás 2 m/s");
            yield return Measure(Vector3.forward, 2f, "frente 2 m/s");

            Finish();
        }

        private IEnumerator Measure(Vector3 worldDir, float speed, string label)
        {
            player.transform.rotation = Quaternion.identity;
            Vector3 localDir = player.transform.InverseTransformDirection(worldDir);

            anim.SetFloat("MoveX", localDir.x);
            anim.SetFloat("MoveZ", localDir.z);
            anim.SetFloat("Speed", 1f);
            anim.SetFloat("LegSpeed", Mathf.Clamp(speed / 3.28f, 0.2f, 2.4f));

            // deixa o blend assentar
            yield return new WaitForSeconds(0.7f);

            float dt = Time.deltaTime;
            Vector3 prevL = footL.position, prevR = footR.position;
            Vector3 prevLL = player.transform.InverseTransformPoint(footL.position);
            Vector3 prevRL = player.transform.InverseTransformPoint(footR.position);

            float localSum = 0f; int localN = 0;
            float slipSum = 0f; int slipN = 0;

            float t = 0f;
            while (t < 1.2f)
            {
                dt = Time.deltaTime;
                player.transform.position += worldDir * speed * dt;

                Vector3 ll = player.transform.InverseTransformPoint(footL.position);
                Vector3 rl = player.transform.InverseTransformPoint(footR.position);
                float vL = new Vector2(ll.x - prevLL.x, ll.z - prevLL.z).magnitude / Mathf.Max(0.0001f, dt);
                float vR = new Vector2(rl.x - prevRL.x, rl.z - prevRL.z).magnitude / Mathf.Max(0.0001f, dt);

                // pé plantado = o mais baixo (com folga de 6 cm do mínimo)
                float yL = footL.position.y, yR = footR.position.y;
                bool planted = Mathf.Abs(yL - yR) > 0.001f;
                int stance = yL <= yR ? 0 : 1;
                float groundGap = Mathf.Abs((yL <= yR ? yL : yR));
                if (!planted) { prevL = footL.position; prevR = footR.position; prevLL = ll; prevRL = rl; t += dt; yield return null; continue; }
                Vector3 curW = stance == 0 ? footL.position : footR.position;
                Vector3 prevW = stance == 0 ? prevL : prevR;
                float localV = stance == 0 ? vL : vR;

                Vector3 d = curW - prevW; d.y = 0f;
                localSum += localV; localN++;
                slipSum += d.magnitude / Mathf.Max(0.0001f, dt); slipN++;

                prevL = footL.position; prevR = footR.position;
                prevLL = ll; prevRL = rl;
                t += dt;
                yield return null;
            }

            var si = anim.GetCurrentAnimatorStateInfo(0);
            float stateSpeed = si.speed;
            float localSpeed = localN > 0 ? localSum / localN : -1f;
            float slip = slipN > 0 ? slipSum / slipN : -1f;
            float pct = speed > 0.01f ? slip / speed * 100f : 0f;
            sb.AppendLine($"{label,-16} pe(local)={localSpeed:F2} m/s | mundo={slip:F2} m/s ({pct:F0}%) | anim.speed={stateSpeed:F2}");
        }

        private void Finish()
        {
            File.WriteAllText("docs/foot_slip.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            SessionState.SetBool("FootSlip.AutoRun", false);
            EditorApplication.Exit(0);
        }

        private Transform FindDeep(Transform p, string n)
        {
            if (p.name == n) return p;
            for (int i = 0; i < p.childCount; i++)
            {
                var f = FindDeep(p.GetChild(i), n);
                if (f != null) return f;
            }
            return null;
        }
    }
}
#endif
