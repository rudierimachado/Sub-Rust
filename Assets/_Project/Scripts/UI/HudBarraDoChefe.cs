using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de chefe GRANDE, no topo da tela - separada da BarraDoInimigo (que e' a
/// barrinha pequena flutuando sobre a cabeca de inimigo comum). Mora na HUD (cena
/// Core), construida por codigo no Awake, mesmo padrao da BarraDoInimigo.
///
/// POR QUE SINGLETON ESTATICO E NAO REFERENCIA SERIALIZADA
/// A HUD vive na cena Core, carregada ADITIVAMENTE; o chefe vive na cena da fase.
/// Referencia entre cenas nao serializa (objeto de uma cena nao pode apontar pra
/// objeto de outra no Inspector) - ver PROJETO.md. Instancia estatica, resolvida em
/// runtime, e' o mesmo padrao que RedeSessao.Instancia ja usa neste projeto.
///
/// Fica escondida ate' algum chefe chamar Mostrar(). Esconde sozinha quando o alvo
/// morre/e' destruido (Health<=0 chama Destroy no EnemyHealth) - poll em LateUpdate
/// contra "alvo == null" pega isso (Unity "fake null" de objeto destruido).
/// </summary>
public class HudBarraDoChefe : MonoBehaviour
{
    public static HudBarraDoChefe Instancia { get; private set; }

    [Header("Posicao/tamanho")]
    [SerializeField] private float largura = 620f;
    [SerializeField] private float alturaBarra = 34f;
    [Tooltip("Distancia do topo da tela. O PainelStatus do jogador (vida/stamina/" +
             "calor) ocupa de Y=-24 a Y=-174 na resolucao de referencia 1920x1080; " +
             "195 poe a barra do chefe LOGO ABAIXO dele, sem disputar espaco. " +
             "Tentar encaixar as duas na mesma faixa do topo so' deu sobreposicao.")]
    [SerializeField] private float margemDoTopo = 195f;

    [Header("Cores")]
    [SerializeField] private Color corFundo = new Color(0.04f, 0.03f, 0.03f, 0.90f);
    [SerializeField] private Color corVida = new Color(0.62f, 0.06f, 0.10f, 1f);
    [SerializeField] private Color corBorda = new Color(0.55f, 0.16f, 0.06f, 1f);

    private RectTransform raiz;
    private RectTransform fillVida;
    private TextMeshProUGUI txtNome;
    private EnemyHealth alvo;

    private void Awake()
    {
        Instancia = this;
        Construir();
        raiz.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void Construir()
    {
        var canvas = GetComponentInParent<Canvas>();

        var go = new GameObject("BarraDoChefe", typeof(RectTransform));
        raiz = (RectTransform)go.transform;
        raiz.SetParent(canvas != null ? canvas.transform : transform, false);
        raiz.anchorMin = new Vector2(0.5f, 1f);
        raiz.anchorMax = new Vector2(0.5f, 1f);
        raiz.pivot = new Vector2(0.5f, 1f);
        raiz.anchoredPosition = new Vector2(0f, -margemDoTopo);
        raiz.sizeDelta = new Vector2(largura, alturaBarra + 26f);

        // nome do chefe, acima da barra
        var nomeGo = new GameObject("Nome", typeof(RectTransform), typeof(TextMeshProUGUI));
        var nomeRT = (RectTransform)nomeGo.transform;
        nomeRT.SetParent(raiz, false);
        nomeRT.anchorMin = new Vector2(0f, 1f);
        nomeRT.anchorMax = new Vector2(1f, 1f);
        nomeRT.pivot = new Vector2(0.5f, 1f);
        nomeRT.anchoredPosition = Vector2.zero;
        nomeRT.sizeDelta = new Vector2(0f, 22f);
        txtNome = nomeGo.GetComponent<TextMeshProUGUI>();
        txtNome.alignment = TextAlignmentOptions.Center;
        txtNome.fontSize = 19f;
        txtNome.fontStyle = FontStyles.Bold;
        txtNome.color = new Color(0.92f, 0.85f, 0.78f);
        txtNome.text = "";

        // fundo da barra
        var fundoGo = new GameObject("Fundo", typeof(RectTransform), typeof(Image));
        var fundoRT = (RectTransform)fundoGo.transform;
        fundoRT.SetParent(raiz, false);
        fundoRT.anchorMin = new Vector2(0f, 0f);
        fundoRT.anchorMax = new Vector2(1f, 0f);
        fundoRT.pivot = new Vector2(0.5f, 0f);
        fundoRT.anchoredPosition = Vector2.zero;
        fundoRT.sizeDelta = new Vector2(0f, alturaBarra);
        var fundoImg = fundoGo.GetComponent<Image>();
        fundoImg.color = corFundo;
        fundoImg.raycastTarget = false;

        // borda fina (moldura), so' pra nao parecer um retangulo solto sem contexto
        var bordaGo = new GameObject("Borda", typeof(RectTransform), typeof(Image));
        var bordaRT = (RectTransform)bordaGo.transform;
        bordaRT.SetParent(fundoRT, false);
        bordaRT.anchorMin = Vector2.zero; bordaRT.anchorMax = Vector2.one;
        bordaRT.offsetMin = new Vector2(-2f, -2f); bordaRT.offsetMax = new Vector2(2f, 2f);
        var bordaImg = bordaGo.GetComponent<Image>();
        bordaImg.color = corBorda;
        bordaImg.raycastTarget = false;
        bordaGo.transform.SetAsFirstSibling(); // atras do fundo

        // preenchimento (encolhe da direita pra esquerda, ancorado a esquerda)
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillVida = (RectTransform)fillGo.transform;
        fillVida.SetParent(fundoRT, false);
        fillVida.anchorMin = new Vector2(0f, 0f);
        fillVida.anchorMax = new Vector2(1f, 1f);
        fillVida.offsetMin = Vector2.zero;
        fillVida.offsetMax = Vector2.zero;
        fillVida.pivot = new Vector2(0f, 0.5f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = corVida;
        fillImg.raycastTarget = false;
    }

    /// <summary>Chamado pelo chefe ao comecar a luta. Se ja' houver outro chefe
    /// mostrado, troca (nao ha' dois chefes ativos ao mesmo tempo neste jogo).</summary>
    public void Mostrar(EnemyHealth vidaDoChefe, string nome)
    {
        alvo = vidaDoChefe;
        txtNome.text = nome;
        raiz.gameObject.SetActive(true);
        AtualizarFill();
    }

    public void Esconder()
    {
        alvo = null;
        if (raiz != null) raiz.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (alvo == null)
        {
            if (raiz != null && raiz.gameObject.activeSelf) Esconder();
            return;
        }
        AtualizarFill();
    }

    private void AtualizarFill()
    {
        float fracao = alvo.MaxHealth > 0f ? Mathf.Clamp01(alvo.Health / alvo.MaxHealth) : 0f;
        var max = fillVida.anchorMax;
        max.x = fracao;
        fillVida.anchorMax = max;
    }
}
