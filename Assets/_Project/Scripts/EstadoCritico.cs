using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Vida baixa: as bordas da tela escurecem no ritmo do batimento cardiaco e a cor
/// perde saturacao, como quem esta' prestes a apagar.
///
/// POR QUE ESCURECER E DESSATURAR, E NAO O VERMELHO DE SEMPRE
/// O cabecalho do PlayerDamageFeedback registra, de proposito, que este jogo NAO usa
/// vinheta vermelha gigante: num side-scroller a leitura do combate depende de
/// enxergar o inimigo, e um overlay colorido atrapalha exatamente no momento em que
/// mais importa ver. Escurecer a BORDA e tirar cor faz o oposto - fecha a periferia e
/// empurra a atencao pro centro, que e' onde a luta esta'.
///
/// O batimento e' DUPLO (tum-tum), nao uma senoide: uma pulsacao regular le' como
/// efeito de tela, e duas batidas em sequencia leem como coracao. A frequencia sobe
/// conforme a vida cai.
///
/// VOLUME PROPRIO, NUNCA O DA CENA
/// Este script cria o proprio Volume e o proprio perfil EM MEMORIA. Escrever no
/// perfil da cena (SampleSceneProfile) seria mexer num ScriptableObject durante o
/// Play - e isso GRAVA no asset: o efeito sobreviveria ao stop e a tela ficaria
/// escura pra sempre, inclusive nas outras fases.
///
/// Anexar na RAIZ do Player.
/// </summary>
public class EstadoCritico : MonoBehaviour
{
    [Header("Quando comeca")]
    [Tooltip("Fracao de vida abaixo da qual o efeito aparece. Acima disto, nada.")]
    [Range(0.05f, 0.6f)] [SerializeField] private float limiarCritico = 0.30f;

    [Header("Vinheta")]
    [Tooltip("Escuridao das bordas no pior caso (vida quase zero), de 0 a 1.")]
    [Range(0f, 1f)] [SerializeField] private float vinhetaMaxima = 0.46f;
    [Tooltip("Quanto o batimento faz a vinheta pulsar, somado ao valor de base.")]
    [Range(0f, 0.5f)] [SerializeField] private float pulsoDaVinheta = 0.12f;
    [SerializeField] private Color corDaVinheta = new Color(0.05f, 0.02f, 0.03f);

    [Header("Cor")]
    [Tooltip("Quanto a cor se apaga no pior caso. -100 seria preto e branco total; " +
             "-45 tira o sangue da imagem sem transformar o jogo noutra coisa.")]
    [Range(-100f, 0f)] [SerializeField] private float dessaturacaoMaxima = -45f;

    [Header("Batimento")]
    [Tooltip("Batidas por minuto quando a vida esta' no limiar (acabou de ficar ruim).")]
    [SerializeField] private float bpmNoLimiar = 72f;
    [Tooltip("Batidas por minuto com a vida quase zerada. E' a aceleracao entre os " +
             "dois que comunica 'esta' piorando' sem nenhum numero na tela.")]
    [SerializeField] private float bpmNoFim = 150f;

    [Header("Transicao")]
    [SerializeField] private float velocidadeDeEntrada = 2.5f;

    private Volume volume;
    private Vignette vinheta;
    private ColorAdjustments cor;

    private float gravidade;    // 0 = saudavel, 1 = quase morto
    private float fase;

    private PlayerHealth vida;

    private void Awake()
    {
        vida = GetComponent<PlayerHealth>();
        Construir();
    }

    private void Construir()
    {
        var go = new GameObject("FX_EstadoCritico");
        go.transform.SetParent(transform, false);

        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        // Acima do Volume da cena (prioridade 0 por padrao) pra este efeito vencer,
        // sem apagar o que o perfil da fase ja' faz.
        volume.priority = 100f;
        volume.weight = 0f;

        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        perfil.name = "PerfilEstadoCritico(memoria)";
        volume.profile = perfil;

        vinheta = perfil.Add<Vignette>(false);
        vinheta.active = true;
        vinheta.intensity.overrideState = true;
        vinheta.color.overrideState = true;
        vinheta.smoothness.overrideState = true;
        vinheta.color.value = corDaVinheta;
        vinheta.smoothness.value = 0.65f;

        cor = perfil.Add<ColorAdjustments>(false);
        cor.active = true;
        cor.saturation.overrideState = true;
    }

    private void OnDestroy()
    {
        // O perfil foi criado em memoria: sem destruir explicitamente ele vaza a cada
        // troca de fase (o Player e' reinstanciado).
        if (volume != null && volume.profile != null) Destroy(volume.profile);
    }

    private void Update()
    {
        if (vida == null || volume == null) return;

        float fracao = vida.MaxHealth > 0f ? vida.Health / vida.MaxHealth : 1f;

        // 0 no limiar, 1 na morte. Fora do limiar o efeito recolhe sozinho.
        float destino = fracao >= limiarCritico
            ? 0f
            : Mathf.Clamp01(1f - fracao / Mathf.Max(0.001f, limiarCritico));

        gravidade = Mathf.MoveTowards(gravidade, destino, velocidadeDeEntrada * Time.deltaTime);

        if (gravidade <= 0.001f)
        {
            volume.weight = 0f;
            return;
        }

        volume.weight = 1f;

        float bpm = Mathf.Lerp(bpmNoLimiar, bpmNoFim, gravidade);
        fase += Time.deltaTime * (bpm / 60f);
        float batida = Batimento(fase % 1f);

        vinheta.intensity.value = vinhetaMaxima * gravidade + pulsoDaVinheta * batida * gravidade;
        cor.saturation.value = dessaturacaoMaxima * gravidade;
    }

    /// <summary>Envelope de UM ciclo cardiaco, de 0 a 1 no tempo: uma batida forte
    /// (sistole), uma pausa curta, uma batida menor (diastole) e um silencio longo.
    ///
    /// E' o silencio no fim que faz isto soar como coracao. Uma senoide passa o mesmo
    /// tempo subindo e descendo e le' como respiracao de tela - o coracao bate rapido
    /// e ESPERA.</summary>
    private static float Batimento(float t)
    {
        if (t < 0.10f) return Mathf.Sin(t / 0.10f * Mathf.PI);            // tum
        if (t < 0.22f) return 0f;                                          // pausa
        if (t < 0.34f) return Mathf.Sin((t - 0.22f) / 0.12f * Mathf.PI) * 0.55f;  // tum menor
        return 0f;                                                         // silencio
    }
}
