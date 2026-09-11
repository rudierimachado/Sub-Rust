using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tela de morte: o fundo escurece e "VOCE SE FODEU" cresce no meio da tela antes
/// do jogo voltar ao inicio.
///
/// POR QUE UMA PAUSA ANTES DE RECARREGAR
/// Antes a morte recarregava a cena depois de 2 segundos sem dizer NADA: o jogador via
/// o personagem parar e a fase piscar de volta, sem nunca receber a informacao de que
/// tinha morrido. Uma tela de morte nao e' enfeite - e' o unico momento em que o jogo
/// confirma o fracasso, e e' o que transforma "buguei" em "eu errei".
///
/// O tempo aqui e' todo NAO ESCALADO: a morte costuma acontecer logo depois de um
/// hitstop (timeScale 0,05), e em tempo escalado a tela levaria quase um minuto real
/// pra aparecer.
///
/// Construida por codigo, sem prefab - mesmo padrao do HudBarraDoChefe e do
/// NumeroFlutuante neste projeto. Assim ela funciona em qualquer fase sem ninguem
/// precisar lembrar de arrastar nada no Inspector.
///
/// Uso: TelaDeMorte.Mostrar(aoTerminar);
/// </summary>
public class TelaDeMorte : MonoBehaviour
{
    private const string Frase = "VOCÊ SE FODEU";

    [Tooltip("Segundos ate' o fundo escuro chegar no maximo.")]
    private const float TempoDoEscuro = 0.9f;
    [Tooltip("Segundos que a frase leva pra aparecer.")]
    private const float TempoDaFrase = 1.1f;
    [Tooltip("Quanto tempo a frase fica parada na tela antes de recarregar.")]
    private const float TempoParado = 1.6f;

    private static TelaDeMorte instancia;

    private CanvasGroup grupoDoFundo;
    private CanvasGroup grupoDaFrase;
    private TextMeshProUGUI texto;
    private RectTransform caixaDaFrase;

    /// <summary>Mostra a tela e chama <paramref name="aoTerminar"/> quando ela acaba.
    /// Ignora chamadas repetidas: morrer duas vezes no mesmo frame (dano + queda no
    /// abismo, por exemplo) nao pode empilhar duas telas nem dois recarregamentos.</summary>
    public static void Mostrar(System.Action aoTerminar)
    {
        if (instancia != null) return;

        var go = new GameObject("TelaDeMorte");
        instancia = go.AddComponent<TelaDeMorte>();
        instancia.Construir();
        instancia.StartCoroutine(instancia.Rodar(aoTerminar));
    }

    private void Construir()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Bem acima da HUD: a tela de morte tem que cobrir barra de vida, contador de
        // almas e a barra do chefe.
        canvas.sortingOrder = 1000;

        var escala = canvasGo.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        // --- fundo escuro ---
        var fundoGo = new GameObject("Fundo", typeof(Image), typeof(CanvasGroup));
        fundoGo.transform.SetParent(canvasGo.transform, false);
        var fundoRt = fundoGo.GetComponent<RectTransform>();
        fundoRt.anchorMin = Vector2.zero;
        fundoRt.anchorMax = Vector2.one;
        fundoRt.offsetMin = Vector2.zero;
        fundoRt.offsetMax = Vector2.zero;
        var img = fundoGo.GetComponent<Image>();
        // Nao e' preto puro: um preto levemente quente deixa a cena ainda insinuada por
        // baixo, o que le' melhor que um apagao total.
        img.color = new Color(0.03f, 0.01f, 0.01f, 1f);
        img.raycastTarget = false;
        grupoDoFundo = fundoGo.GetComponent<CanvasGroup>();
        grupoDoFundo.alpha = 0f;
        grupoDoFundo.blocksRaycasts = false;

        // --- frase ---
        var fraseGo = new GameObject("Frase", typeof(TextMeshProUGUI), typeof(CanvasGroup));
        fraseGo.transform.SetParent(canvasGo.transform, false);
        caixaDaFrase = fraseGo.GetComponent<RectTransform>();
        caixaDaFrase.anchorMin = new Vector2(0.5f, 0.5f);
        caixaDaFrase.anchorMax = new Vector2(0.5f, 0.5f);
        caixaDaFrase.pivot = new Vector2(0.5f, 0.5f);
        caixaDaFrase.sizeDelta = new Vector2(1600f, 300f);
        caixaDaFrase.anchoredPosition = Vector2.zero;

        texto = fraseGo.GetComponent<TextMeshProUGUI>();
        texto.text = Frase;
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontSize = 150f;
        texto.fontStyle = FontStyles.Bold;
        // Vermelho escuro e sujo, nao vermelho puro: puro vibra na tela e parece aviso
        // de erro de sistema, nao morte.
        texto.color = new Color(0.62f, 0.06f, 0.05f);
        texto.characterSpacing = 12f;
        texto.raycastTarget = false;
        texto.enableWordWrapping = false;

        grupoDaFrase = fraseGo.GetComponent<CanvasGroup>();
        grupoDaFrase.alpha = 0f;
        grupoDaFrase.blocksRaycasts = false;
    }

    private IEnumerator Rodar(System.Action aoTerminar)
    {
        // 1. escurece
        for (float t = 0f; t < TempoDoEscuro; t += Time.unscaledDeltaTime)
        {
            grupoDoFundo.alpha = Mathf.Clamp01(t / TempoDoEscuro) * 0.88f;
            yield return null;
        }
        grupoDoFundo.alpha = 0.88f;

        // 2. a frase entra crescendo devagar - o crescimento lento e' o que da' o peso;
        //    aparecer pronta le' como pop-up.
        for (float t = 0f; t < TempoDaFrase; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / TempoDaFrase);
            float suave = 1f - Mathf.Pow(1f - k, 3f);          // desacelera no fim
            grupoDaFrase.alpha = suave;
            caixaDaFrase.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, suave);
            yield return null;
        }
        grupoDaFrase.alpha = 1f;
        caixaDaFrase.localScale = Vector3.one;

        // 3. deixa ler
        yield return new WaitForSecondsRealtime(TempoParado);

        // 4. apaga de vez antes de recarregar, pra troca de cena nao dar um flash
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            grupoDoFundo.alpha = Mathf.Lerp(0.88f, 1f, t / 0.45f);
            yield return null;
        }
        grupoDoFundo.alpha = 1f;

        aoTerminar?.Invoke();
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }
}
