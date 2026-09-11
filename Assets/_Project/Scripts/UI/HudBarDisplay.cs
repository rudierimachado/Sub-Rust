using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Barra de vida ou stamina do jogador.
///
/// O QUE MUDOU E POR QUE
/// A versao anterior escrevia fillAmount direto no evento: a barra PULAVA de um valor
/// pro outro num frame. Isso tem tres problemas de leitura, e os tres sao resolvidos
/// aqui:
///
///  1. NAO DAVA PRA VER QUANTO SE PERDEU. Um corte de 20% e um de 40% tem exatamente
///     a mesma aparencia quando os dois sao instantaneos - o olho nao consegue medir
///     um salto. A BARRA DE RASTRO (ver rastro) resolve: ela fica pra tras e escorre
///     ate' o valor novo, entao o tamanho do pedaco claro E' o tamanho da pancada.
///  2. PULO SECO NAO TEM PESO. O preenchimento agora persegue o valor real, mais
///     rapido quando ganha e mais lento quando perde.
///  3. A BARRA CHEIA OCUPAVA A TELA A TODA HORA. Fora de combate e com tudo cheio ela
///     some sozinha (ver tempoParaSumir) e volta na hora em que algo muda.
///
/// O texto "1250 / 1250" saiu: numero grande e permanente e' ruido, e a fracao ja'
/// esta' desenhada na propria barra. Ele volta so' enquanto o valor esta' mudando.
///
/// Anexar no objeto que tem a Image "Fill" como filho.
/// </summary>
public class HudBarDisplay : MonoBehaviour
{
    private enum TipoBarra { Vida, Stamina }
    [SerializeField] private TipoBarra tipo = TipoBarra.Vida;
    [SerializeField] private Image fill;
    [SerializeField] private TextMeshProUGUI texto;

    [Header("Rastro de dano")]
    [Tooltip("Cor do pedaco que fica pra tras quando voce perde - e' ele que mostra o " +
             "TAMANHO da pancada que acabou de levar.")]
    [SerializeField] private Color corDoRastro = new Color(0.94f, 0.86f, 0.72f, 0.85f);
    [Tooltip("Segundos que o rastro fica parado antes de comecar a escorrer. Sem esta " +
             "pausa ele acompanha a perda e nao chega a ser visto.")]
    [SerializeField] private float esperaDoRastro = 0.35f;
    [Tooltip("Fracao da barra por segundo que o rastro escorre.")]
    [SerializeField] private float velocidadeDoRastro = 0.55f;

    [Header("Preenchimento")]
    [Tooltip("Fracao por segundo ao PERDER. Mais lento que o ganho: perder e' o que " +
             "precisa ser sentido.")]
    [SerializeField] private float velocidadeAoPerder = 1.6f;
    [Tooltip("Fracao por segundo ao GANHAR. Rapido, pra regeneracao de stamina nao " +
             "parecer travada.")]
    [SerializeField] private float velocidadeAoGanhar = 2.6f;

    [Header("Sumir quando nao importa")]
    [Tooltip("Segundos cheio e sem mudanca antes de desaparecer. Zero = nunca some.")]
    [SerializeField] private float tempoParaSumir = 4f;
    [Tooltip("Opacidade quando escondida. Nao vai a zero de proposito: a barra some " +
             "de vista mas o lugar dela continua sendo aprendido pelo olho.")]
    [Range(0f, 1f)] [SerializeField] private float opacidadeEscondida = 0.12f;
    [SerializeField] private float velocidadeDoFade = 3f;

    [Header("Exaustao (so' na stamina)")]
    [Tooltip("Cor da barra enquanto o jogador esta' sem folego e travado.")]
    [SerializeField] private Color corExausta = new Color(0.85f, 0.22f, 0.16f);
    [Tooltip("Piscadas por segundo durante a exaustao.")]
    [SerializeField] private float piscadasPorSegundo = 5f;

    private Image rastro;
    private CanvasGroup grupo;

    private float alvo = 1f;        // fracao real, vinda do evento
    private float exibido = 1f;     // a que a barra desenha
    private float valorDoRastro = 1f;
    private float rastroEscorreEm;
    private float ultimaMudanca;

    private Color corOriginal;
    private PlayerHealth vida;

    private void Awake()
    {
        if (fill == null) fill = GetComponentInChildren<Image>();
        if (texto == null)
        {
            var t = transform.Find("Text");
            if (t != null) texto = t.GetComponent<TextMeshProUGUI>();
        }

        if (fill != null) corOriginal = fill.color;

        grupo = GetComponent<CanvasGroup>();
        if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
        // A HUD nao pode roubar clique do jogo.
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        CriarRastro();
    }

    /// <summary>O rastro e' uma copia da Image de preenchimento, inserida ATRAS dela na
    /// hierarquia (SetSiblingIndex antes do fill), pra aparecer so' na parte que o
    /// preenchimento ja' desocupou.
    ///
    /// Criado por codigo em vez de exigir um objeto novo no prefab: assim vale pras
    /// duas barras sem ninguem ter que lembrar de montar a mesma coisa duas vezes.</summary>
    private void CriarRastro()
    {
        if (fill == null) return;

        var go = new GameObject("Rastro", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        var origem = fill.rectTransform;

        rt.SetParent(origem.parent, false);
        rt.anchorMin = origem.anchorMin;
        rt.anchorMax = origem.anchorMax;
        rt.pivot = origem.pivot;
        rt.anchoredPosition = origem.anchoredPosition;
        rt.sizeDelta = origem.sizeDelta;
        rt.offsetMin = origem.offsetMin;
        rt.offsetMax = origem.offsetMax;

        rastro = go.AddComponent<Image>();
        rastro.sprite = fill.sprite;
        rastro.type = Image.Type.Filled;
        rastro.fillMethod = fill.fillMethod;
        rastro.fillOrigin = fill.fillOrigin;
        rastro.color = corDoRastro;
        rastro.raycastTarget = false;

        // ATRAS do preenchimento: so' assim ele fica visivel apenas onde o fill saiu.
        rt.SetSiblingIndex(origem.GetSiblingIndex());
    }

    private void OnEnable()
    {
        if (tipo == TipoBarra.Vida) PlayerHealth.OnHealthChanged += Atualizar;
        else PlayerHealth.OnStaminaChanged += Atualizar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= Atualizar;
        PlayerHealth.OnStaminaChanged -= Atualizar;
    }

    private void Atualizar(float atual, float max)
    {
        float novo = max > 0f ? Mathf.Clamp01(atual / max) : 0f;
        if (!Mathf.Approximately(novo, alvo)) ultimaMudanca = Time.unscaledTime;

        // Perdeu: o rastro fica parado no valor ANTIGO e so' comeca a escorrer depois
        // da espera. Ganhou: ele acompanha na hora, senao sobraria um pedaco claro
        // pendurado a frente do preenchimento.
        if (novo < alvo) rastroEscorreEm = Time.unscaledTime + esperaDoRastro;
        else valorDoRastro = Mathf.Max(valorDoRastro, novo);

        alvo = novo;

        if (texto != null)
        {
            // Ceil: com 0,4 de vida restante o certo e' mostrar 1, nao 0 - "0" so'
            // quando estiver realmente morto.
            int atualExibido = atual > 0f ? Mathf.CeilToInt(atual) : 0;
            texto.text = atualExibido.ToString();
        }
    }

    private void Update()
    {
        // Tempo NAO ESCALADO: o hitstop do impacto (ver HitStop) derruba o timeScale
        // pra 0,05, e a barra animando nele levaria segundos pra reagir justamente no
        // momento em que o jogador quer ver o quanto perdeu.
        float dt = Time.unscaledDeltaTime;

        AnimarPreenchimento(dt);
        AnimarRastro(dt);
        AnimarVisibilidade(dt);
        AnimarExaustao();
    }

    private void AnimarPreenchimento(float dt)
    {
        if (fill == null) return;
        float v = exibido > alvo ? velocidadeAoPerder : velocidadeAoGanhar;
        exibido = Mathf.MoveTowards(exibido, alvo, v * dt);
        fill.fillAmount = exibido;
    }

    private void AnimarRastro(float dt)
    {
        if (rastro == null) return;

        if (valorDoRastro > exibido && Time.unscaledTime >= rastroEscorreEm)
            valorDoRastro = Mathf.MoveTowards(valorDoRastro, exibido, velocidadeDoRastro * dt);
        else if (valorDoRastro < exibido)
            valorDoRastro = exibido;   // ganhou: nunca fica atras do preenchimento

        rastro.fillAmount = valorDoRastro;
        // Some junto quando alcanca: rastro do tamanho do fill nao tem o que mostrar.
        rastro.enabled = valorDoRastro > exibido + 0.001f;
    }

    private void AnimarVisibilidade(float dt)
    {
        if (grupo == null) return;

        bool cheia = alvo >= 0.999f && Mathf.Approximately(exibido, alvo);
        bool quieta = Time.unscaledTime - ultimaMudanca > tempoParaSumir;
        bool esconder = tempoParaSumir > 0f && cheia && quieta;

        float destino = esconder ? opacidadeEscondida : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, destino, velocidadeDoFade * dt);
    }

    /// <summary>Barra de stamina pisca em vermelho enquanto o jogador esta' travado sem
    /// folego. E' o que liga o "apertei e nao aconteceu nada" a uma CAUSA na tela.</summary>
    private void AnimarExaustao()
    {
        if (tipo != TipoBarra.Stamina || fill == null) return;

        if (vida == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) vida = go.GetComponent<PlayerHealth>();
            if (vida == null) return;
        }

        if (!vida.Exausto)
        {
            fill.color = corOriginal;
            return;
        }

        float pisca = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * piscadasPorSegundo * Mathf.PI * 2f);
        fill.color = Color.Lerp(corOriginal, corExausta, pisca);
        if (grupo != null) grupo.alpha = 1f;   // nunca escondida enquanto pune
    }
}
