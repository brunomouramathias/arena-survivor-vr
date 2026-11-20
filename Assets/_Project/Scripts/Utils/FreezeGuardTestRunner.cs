#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Core;
using ArenaSurvivor.UI;

namespace ArenaSurvivor.Utils
{
    // Prova de que os estados Pausa/Loja/GameOver não travam mais o jogo (timeScale 0 eterno).
    public class FreezeGuardTestRunner : MonoBehaviour
    {
        private readonly StringBuilder sb = new StringBuilder();

        private IEnumerator Start()
        {
            sb.AppendLine("=== TESTE ANTI-TRAVAMENTO ===");
            sb.AppendLine($"Data: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var gm = GameManager.Instance;
            var ui = FindAnyObjectByType<UIManager>();
            sb.AppendLine($"GameManager={(gm != null)} | UIManager={(ui != null)}");
            if (gm == null) { Finish(); yield break; }

            yield return new WaitForSecondsRealtime(0.3f);

            // 1) Loja (painel vazio) deve retomar sozinha
            gm.SetState(GameState.Shop);
            sb.AppendLine($"1. SetState(Shop): state={gm.CurrentState} timeScale={Time.timeScale}");
            yield return new WaitForSecondsRealtime(2.6f);
            bool shopOk = gm.CurrentState == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f);
            sb.AppendLine($"1. Após 2,6s: state={gm.CurrentState} timeScale={Time.timeScale} => {(shopOk ? "PASS" : "FAIL")}");

            // 2) Pausa sem painel utilizável deve retomar (Esc)
            gm.SetState(GameState.Paused);
            sb.AppendLine($"2. SetState(Paused): state={gm.CurrentState} timeScale={Time.timeScale}");
            yield return new WaitForSecondsRealtime(2.6f);
            bool pauseOk = gm.CurrentState == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f);
            sb.AppendLine($"2. Após 2,6s: state={gm.CurrentState} timeScale={Time.timeScale} => {(pauseOk ? "PASS" : "FAIL")}");

            // 3) Trava de segurança do GameManager (Playing nunca com timeScale 0)
            gm.SetState(GameState.Playing);
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            bool tsOk = Mathf.Approximately(Time.timeScale, 1f);
            sb.AppendLine($"3. Playing com timeScale forçado a 0: timeScale={Time.timeScale} => {(tsOk ? "PASS" : "FAIL")}");

            sb.AppendLine($"RESULTADO: {(shopOk && pauseOk && tsOk ? "PASS" : "FAIL")}");
            Finish();
        }

        private void Finish()
        {
            File.WriteAllText("docs/teste_travamento.txt", sb.ToString(), Encoding.UTF8);
            Debug.Log(sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
#endif
