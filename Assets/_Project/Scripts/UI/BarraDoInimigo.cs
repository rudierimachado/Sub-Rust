using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida + stamina flutuando sobre a cabeca do inimigo.
///
/// Construida por codigo pra nao exigir um prefab de UI
/// mantido a parte: qualquer inimigo que ganhe este componente ja nasce com barra.
///
/// A stamina aparece porque ela GOVERNA o comportamento dele (ver EnemyStamina): a
/// barra amarela esvaziando anuncia que o inimigo vai precisar recuar e respirar, e
/// e' nessa janela que voce contra-ataca. Sem mostrar isso, o recuo dele pareceria
/// aleatorio.
///
/// Preenchimento por ANCORA, nao por Image.fillAmount: fillAmount depende do sprite
/// e neste projeto as Images da HUD usam sprite nulo - redimensionar
/// pela ancora funciona em qualquer caso.
///
/// Anexar na RAIZ do inimigo, nunca no Visual: o Visual espelha em escala pra virar
/// de lado e a barra viraria junto (texto e barras invertidos).
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class BarraDoInimigo : MonoBehaviour
{
    [Header("Posicao")]
    [Tooltip("Altura acima dos pes do inimigo.")]
    [SerializeField] private float altura = 2.05f;

    [Header("Tamanho (metros)")]
    [SerializeField] private float largura = 0.85f;
    [SerializeField] private float alturaVida = 0.10f;
    [SerializeField] private float alturaStamina = 0.05f;
    [SerializeField] private float espacoEntreBarras = 0.025f;

    [Header("Cores")]
    [SerializeField] private Color corFundo = new Color(0.05f, 0.04f, 0.04f, 0.85f);
    [SerializeField] private Color corVida = new Color(0.72f, 0.10f, 0.11f, 1f);
    [SerializeField] private Color corStamina = new Color(0.95f, 0.74f, 0.25f, 1f);

    [Tooltip("Se ligado, a barra so' aparece depois do primeiro dano. Desligado = sempre visivel.")]
    [SerializeField] private bool esconderIntacto = false;

    private EnemyHealth vida;
    private EnemyStamina folego;
    private Transform raizBarra;
    private RectTransform preenchimentoVida;
    private RectTransform preenchimentoStamina;
    private Camera camera3D;
    private bool jaLevouDano;

    // 1 unidade de UI = 1 milimetro de mundo. Mantem numeros de RectTransform
    // legiveis (850 x 100) em vez de frações minusculas.
    private const float EscalaCanvas = 0.001f;

    private void Awake()
    {
        vida = GetComponent<EnemyHealth>();
        folego = GetComponent<EnemyStamina>();
        Construir();
    }

    private void Construir()
    {
        var go = new GameObject("BarraDoInimigo", typeof(RectTransform), typeof(Canvas));
        raizBarra = go.transform;
        raizBarra.SetParent(transform, false);
        raizBarra.localPosition = new Vector3(0f, altura, 0f);
        raizBarra.localScale = Vector3.one * EscalaCanvas;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        float larguraUI = largura / EscalaCanvas;
        var rt = (RectTransform)raizBarra;
        rt.sizeDelta = new Vector2(larguraUI, (alturaVida + alturaStamina + espacoEntreBarras) / EscalaCanvas);

        float yVida = (alturaStamina + espacoEntreBarras) / EscalaCanvas * 0.5f;
        float yStam = -(alturaVida + espacoEntreBarras) / EscalaCanvas * 0.5f;

        preenchimentoVida = CriarBarra("Vida", larguraUI, alturaVida / EscalaCanvas, yVida, corVida);
        if (folego != null)
            preenchimentoStamina = CriarBarra("Stamina", larguraUI, alturaStamina / EscalaCanvas, yStam, corStamina);

        if (esconderIntacto) raizBarra.gameObject.SetActive(false);
    }

    /// <summary>Cria fundo + preenchimento e devolve o RectTransform do preenchimento.</summary>
    private RectTransform CriarBarra(string nome, float larguraUI, float alturaUI, float y, Color cor)
    {
        var fundo = new GameObject(nome + "_Fundo", typeof(RectTransform), typeof(Image));
        var fundoRT = (RectTransform)fundo.transform;
        fundoRT.SetParent(raizBarra, false);
        fundoRT.sizeDelta = new Vector2(larguraUI, alturaUI);
        fundoRT.anchoredPosition = new Vector2(0f, y);
        var fundoImg = fundo.GetComponent<Image>();
        fundoImg.color = corFundo;
        fundoImg.raycastTarget = false;

        var fill = new GameObject(nome + "_Fill", typeof(RectTransform), typeof(Image));
        var fillRT = (RectTransform)fill.transform;
        fillRT.SetParent(fundoRT, false);
        // Ancorado a ESQUERDA e esticado: mexer no anchorMax.x encolhe da direita
        // pra esquerda, que e' a leitura natural de barra de vida.
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        fillRT.pivot = new Vector2(0f, 0.5f);
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = cor;
        fillImg.raycastTarget = false;

        return fillRT;
    }

    private void LateUpdate()
    {
        if (raizBarra == null || vida == null) return;

        float fracVida = vida.MaxHealth > 0f ? Mathf.Clamp01(vida.Health / vida.MaxHealth) : 0f;
        if (fracVida < 1f) jaLevouDano = true;

        bool deveAparecer = !esconderIntacto || jaLevouDano;
        if (raizBarra.gameObject.activeSelf != deveAparecer)
            raizBarra.gameObject.SetActive(deveAparecer);
        if (!deveAparecer) return;

        Encolher(preenchimentoVida, fracVida);
        if (preenchimentoStamina != null && folego != null)
            Encolher(preenchimentoStamina, folego.MaxStamina > 0f ? Mathf.Clamp01(folego.Stamina / folego.MaxStamina) : 0f);

        EncararCamera();
    }

    private static void Encolher(RectTransform fill, float fracao)
    {
        if (fill == null) return;
        var max = fill.anchorMax;
        max.x = fracao;
        fill.anchorMax = max;
        fill.offsetMax = new Vector2(0f, fill.offsetMax.y);
    }

    private void EncararCamera()
    {
        if (camera3D == null) camera3D = Camera.main;
        if (camera3D == null) return;
        // Olha na MESMA direcao da camera (nao "para" ela): numa camera lateral fixa
        // isso mantem todas as barras alinhadas entre si, sem tortas nas bordas da tela.
        raizBarra.rotation = camera3D.transform.rotation;
    }
}
