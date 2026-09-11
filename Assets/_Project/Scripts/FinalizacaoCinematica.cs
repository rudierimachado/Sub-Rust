using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Finalizacao: um golpe encenado no inimigo com a GUARDA QUEBRADA, com camera
/// propria, tempo desacelerado e os dois corpos posicionados um pro outro.
///
/// QUANDO APARECE
/// Enquanto o inimigo focado esta' exausto (ver EnemyStamina.GuardaQuebrada) e voce
/// esta' perto, um "[F]" flutua sobre a cabeca dele (ver AvisoDeTecla). A janela e' a
/// mesma da exaustao: quando ele se recupera, o aviso some e o F deixa de valer.
///
/// Repare que o golpe forte automatico CONTINUA existindo no clique (ver
/// PlayerCombat): sao duas respostas pra mesma abertura - o clique e' a punicao
/// rapida, o F e' a execucao. Os dois nunca colidem porque a cena desliga o combate
/// enquanto roda.
///
/// O QUE FAZ ISTO SER "CINEMATOGRAFICO" E NAO SO' UM GOLPE FORTE
/// Nao e' a animacao - e' CAMERA, TEMPO e ENCENACAO. Um clipe de execucao caro visto
/// de 14 m de lado, em velocidade normal, com a vitima tocando a morte generica,
/// continua parecendo um golpe qualquer. Os quatro pilares:
///  1. ENCENACAO - os dois deslizam pra uma distancia exata antes do golpe. Sem isso a
///     lamina passa longe ou atravessa o corpo.
///  2. TEMPO - desacelera na investida, quase congela no impacto, volta na queda.
///  3. CAMERA - plano fechado de baixo pra cima, com aproximacao (CameraDeFinalizacao).
///  4. REACAO - a vitima responde a ESTE golpe (ColapsoDeFinalizacao).
///
/// A sequencia roda em tempo NAO ESCALADO, senao desacelerar o jogo desaceleraria
/// tambem o relogio que controla a desaceleracao.
///
/// Anexar na RAIZ do Player.
/// </summary>
public class FinalizacaoCinematica : MonoBehaviour
{
    [Header("Quando fica disponivel")]
    [Tooltip("Distancia maxima ate' o alvo pra oferecer a finalizacao.")]
    [SerializeField] private float alcance = 2.6f;
    [SerializeField] private Key tecla = Key.F;

    [Header("Diagnostico (desligar depois)")]
    [Tooltip("Escreve no console QUAL condicao esta' impedindo o aviso de aparecer. " +
             "Use pra descobrir onde a corrente quebra; desligue depois.")]
    [SerializeField] private bool logDaFinalizacao = true;

    [Header("Encenacao")]
    [Tooltip("Distancia entre os dois corpos durante o golpe. Este numero decide se a " +
             "lamina encosta, atravessa ou passa longe.")]
    [SerializeField] private float distanciaDoGolpe = 1.05f;
    [Tooltip("Tempo pros dois deslizarem pra posicao. Curto, mas nao zero: teleporte no " +
             "frame do comando le' como falha, nao como corte.")]
    [SerializeField] private float tempoDeEncenar = 0.16f;

    [Header("Tempo")]
    [Tooltip("Escala de tempo na investida. Nao pode ser muito baixa: o clipe do golpe " +
             "avanca em tempo ESCALADO, entao desacelerar demais deixa a lamina quase " +
             "parada no ar durante segundos.")]
    [SerializeField] private float lentidaoDaInvestida = 0.55f;
    [SerializeField] private float lentidaoDoImpacto = 0.05f;
    [SerializeField] private float duracaoDoImpacto = 0.30f;

    [Tooltip("Em que ponto do clipe do golpe forte a lamina ATINGE, de 0 a 1. Vem da " +
             "janela de dano do PlayerCombat (0,30 a 0,70): o meio dela. A cena espera " +
             "a ANIMACAO chegar aqui em vez de contar segundos - trocar o clipe ou a " +
             "lentidao dessincronizaria o sangue do golpe.")]
    [Range(0.1f, 0.9f)] [SerializeField] private float momentoDoGolpe = 0.5f;
    [Tooltip("Teto de seguranca pra espera do golpe. Sem ele, um estado que nao entra " +
             "travaria a cena - e o jogo inteiro - em camera lenta, sem saida.")]
    [SerializeField] private float esperaMaximaDoGolpe = 3f;
    [Tooltip("Duracao efetiva do clipe do golpe forte. Medida: 2,33 s a 1,85x = 1,26 s. " +
             "So' da o andamento da camera; o corte no impacto e' medido do Animator.")]
    [SerializeField] private float duracaoDoGolpe = 1.26f;

    [Header("Ritmo (segundos reais)")]
    [SerializeField] private float tempoCravado = 0.34f;
    [SerializeField] private float tempoDeSacar = 0.34f;
    [SerializeField] private float tempoDeQueda = 0.70f;

    [Header("Recompensa")]
    [Tooltip("Multiplicador de almas por finalizar em vez de so' matar - o que faz " +
             "valer a pena gastar a janela nisso em vez de so' bater.")]
    [SerializeField] private float bonusDeAlmas = 2f;

    [Header("Sangue")]
    [SerializeField] private float sangueNoImpacto = 3.2f;
    [SerializeField] private float sangueNoSaque = 2.4f;

    private static readonly int HeavyTrigger = Animator.StringToHash("AttackHeavy");
    private static readonly int EstadoHeavy = Animator.StringToHash("AttackHeavy");

    private FocoDeAlvo foco;
    private PlayerMovement2_5D movimento;
    private PlayerCombat combate;
    private PlayerBloqueio bloqueio;
    private PlayerHealth vida;
    private Animator animator;
    private CharacterController controller;
    private CameraDeFinalizacao camera;
    private AvisoDeTecla aviso;
    private int camadaTronco = -1;

    /// <summary>True enquanto a cena roda.</summary>
    public bool EmCena { get; private set; }

    /// <summary>O alvo que pode ser finalizado agora, ou null.</summary>
    public Transform AlvoFinalizavel { get; private set; }

    private void Awake()
    {
        foco = GetComponent<FocoDeAlvo>();
        movimento = GetComponent<PlayerMovement2_5D>();
        vida = GetComponent<PlayerHealth>();
        controller = GetComponent<CharacterController>();

        var visual = transform.Find("Visual");
        animator = visual != null ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
        combate = GetComponentInChildren<PlayerCombat>();
        bloqueio = GetComponentInChildren<PlayerBloqueio>();

        if (animator != null) camadaTronco = animator.GetLayerIndex("TroncoAtaque");

        camera = gameObject.AddComponent<CameraDeFinalizacao>();
        aviso = AvisoDeTecla.Criar(tecla.ToString());
    }

    private void OnDestroy()
    {
        if (aviso != null) Destroy(aviso.gameObject);
    }

    private void Update()
    {
        if (EmCena) { aviso?.Esconder(); return; }

        AlvoFinalizavel = Procurar();

        if (AlvoFinalizavel == null) { aviso?.Esconder(); return; }
        aviso?.Mostrar(AlvoFinalizavel);

        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame)
            StartCoroutine(Executar(AlvoFinalizavel));
    }

    /// <summary>O alvo ja' focado, se estiver com a guarda quebrada e perto o bastante.
    /// Reusa o foco em vez de varrer de novo: assim a finalizacao acontece SEMPRE em
    /// quem a bolinha esta' marcando, e nunca num terceiro que por acaso esta' exausto
    /// atras do jogador.</summary>
    private Transform Procurar()
    {
        if (foco == null) return Recusar("sem FocoDeAlvo no Player");
        if (foco.Alvo == null) return Recusar("foco sem alvo");

        // A EXAUSTAO DO JOGADOR NAO BLOQUEIA MAIS. Quebrar a guarda do inimigo custa
        // tres bloqueios, 96 dos seus 100 de stamina: exigir folego pra executar seria
        // cobrar de novo por uma abertura que o jogador acabou de PAGAR, e a janela
        // passaria inteira enquanto ele recupera. A execucao e' o premio, nao mais um
        // custo.
        if (vida != null && vida.Morto) return Recusar("jogador morto");

        var alvo = foco.Alvo;
        var folego = alvo.GetComponent<EnemyStamina>();
        if (folego == null) return Recusar("alvo '" + alvo.name + "' nao tem EnemyStamina");
        if (!folego.GuardaQuebrada) return Recusar("alvo '" + alvo.name + "' com guarda inteira (stamina " + folego.Stamina.ToString("F0") + ")");
        var saudeDoAlvo = alvo.GetComponent<EnemyHealth>();
        if (saudeDoAlvo == null) return Recusar("alvo sem EnemyHealth");
        // Chefe nao morre por atalho - ver EnemyHealth.podeSerFinalizado.
        if (!saudeDoAlvo.PodeSerFinalizado) return Recusar("'" + alvo.name + "' nao aceita finalizacao (chefe)");

        Vector3 d = alvo.position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > alcance) return Recusar("longe demais: " + dist.ToString("F2") + "m (limite " + alcance + ")");

        if (logDaFinalizacao && !avisouDisponivel)
        {
            avisouDisponivel = true;
            Debug.Log("[finalizacao] DISPONIVEL em '" + alvo.name + "' a " + dist.ToString("F2") + "m - aperte " + tecla);
        }
        return alvo;
    }

    private string ultimaRecusa;
    private bool avisouDisponivel;

    /// <summary>Recusa com log, mas so' quando o MOTIVO muda - senao um Update a 60 fps
    /// enche o console com a mesma linha e esconde o que importa.</summary>
    private Transform Recusar(string motivo)
    {
        avisouDisponivel = false;
        if (logDaFinalizacao && motivo != ultimaRecusa)
        {
            ultimaRecusa = motivo;
            Debug.Log("[finalizacao] sem aviso: " + motivo);
        }
        return null;
    }

    private IEnumerator Executar(Transform alvo)
    {
        var saude = alvo.GetComponent<EnemyHealth>();
        if (saude == null) yield break;

        EmCena = true;
        aviso?.Esconder();

        // Trava a vitima: ela nao morre por outra fonte no meio da cena, o que
        // destruiria o objeto e deixaria a camera olhando pro vazio.
        saude.PrepararFinalizacao();

        float lado = Mathf.Sign(alvo.position.x - transform.position.x);
        if (Mathf.Approximately(lado, 0f)) lado = 1f;

        bool tinhaMovimento = movimento != null && movimento.enabled;
        bool tinhaCombate = combate != null && combate.enabled;
        bool tinhaBloqueio = bloqueio != null && bloqueio.enabled;
        float escalaOriginal = Time.timeScale;

        if (movimento != null) { movimento.Virar(lado); movimento.enabled = false; }
        if (combate != null) combate.enabled = false;
        if (bloqueio != null) bloqueio.enabled = false;
        if (vida != null) vida.Invulneravel = true;

        var colapso = alvo.gameObject.AddComponent<ColapsoDeFinalizacao>();
        colapso.Preparar(new Vector3(lado, 0f, 0f));

        Vector3 destinoJogador = new Vector3(alvo.position.x - lado * distanciaDoGolpe,
                                             transform.position.y, alvo.position.z);
        camera.Assumir(destinoJogador, alvo.position, lado);

        float total = tempoDeEncenar + PrevisaoAteOGolpe() + tempoCravado + tempoDeSacar + tempoDeQueda;
        float decorrido = 0f;

        // --- 1. ENCENACAO ---
        Vector3 origem = transform.position;
        for (float t = 0f; t < tempoDeEncenar; t += Time.unscaledDeltaTime)
        {
            float k = t / tempoDeEncenar;
            Mover(Vector3.Lerp(origem, destinoJogador, k * k * (3f - 2f * k)));
            Time.timeScale = Mathf.Lerp(escalaOriginal, lentidaoDaInvestida, k);
            decorrido += Time.unscaledDeltaTime;
            camera.Posicionar(decorrido / total);
            yield return null;
        }
        Mover(destinoJogador);
        Time.timeScale = lentidaoDaInvestida;

        // --- 2. INVESTIDA ---
        if (animator != null && camadaTronco >= 0)
        {
            // CrossFade e nao SetTrigger: com o PlayerCombat desligado ninguem consome
            // o trigger, e um trigger pendurado dispararia um golpe fantasma assim que
            // o combate voltasse.
            animator.ResetTrigger(HeavyTrigger);
            animator.CrossFade(EstadoHeavy, 0.05f, camadaTronco, 0f);
            // A camada normalmente e' pesada pelo PlayerCombat, que acabou de ser
            // desligado: sem levantar o peso aqui o estado entra e nao move osso nenhum.
            animator.SetLayerWeight(camadaTronco, 1f);
        }

        foreach (var passo in EsperarOGolpe())
        {
            decorrido += Time.unscaledDeltaTime;
            camera.Posicionar(decorrido / total);
            yield return passo;
        }

        // --- 3. IMPACTO ---
        Vector3 peito = alvo.position + Vector3.up * 1.15f;
        colapso.Cravar();
        ImpactoDeGolpe.Tocar(peito, new Vector3(lado, 0.25f, 0f), sangueNoImpacto);
        Time.timeScale = lentidaoDoImpacto;

        for (float t = 0f; t < tempoCravado; t += Time.unscaledDeltaTime)
        {
            decorrido += Time.unscaledDeltaTime;
            camera.Posicionar(decorrido / total);
            float k = 1f - Mathf.Clamp01(t / duracaoDoImpacto);
            if (k > 0f) camera.Tremer(0.035f * k);
            if (t >= duracaoDoImpacto) Time.timeScale = lentidaoDaInvestida;
            yield return null;
        }

        // --- 4. SAQUE ---
        ImpactoDeGolpe.Tocar(peito, new Vector3(-lado, 0.5f, 0f), sangueNoSaque);
        Vector3 recuo = destinoJogador - new Vector3(lado * 0.35f, 0f, 0f);
        for (float t = 0f; t < tempoDeSacar; t += Time.unscaledDeltaTime)
        {
            float k = t / tempoDeSacar;
            Mover(Vector3.Lerp(destinoJogador, recuo, k));
            Time.timeScale = Mathf.Lerp(lentidaoDaInvestida, 0.65f, k);
            decorrido += Time.unscaledDeltaTime;
            camera.Posicionar(decorrido / total);
            yield return null;
        }

        // --- 5. QUEDA ---
        for (float t = 0f; t < tempoDeQueda; t += Time.unscaledDeltaTime)
        {
            float k = t / tempoDeQueda;
            colapso.Tombar(k);
            Time.timeScale = Mathf.Lerp(0.65f, escalaOriginal, k);
            decorrido += Time.unscaledDeltaTime;
            camera.Posicionar(decorrido / total);
            yield return null;
        }
        colapso.Tombar(1f);

        ImpactoDeGolpe.Tocar(alvo.position + Vector3.up * 0.25f + new Vector3(lado * 0.6f, 0f, 0f),
                             new Vector3(lado, 0.15f, 0f), 1.8f);

        // --- FIM ---
        Time.timeScale = escalaOriginal;
        camera.Devolver();

        if (movimento != null) movimento.enabled = tinhaMovimento;
        if (combate != null) combate.enabled = tinhaCombate;
        if (bloqueio != null) bloqueio.enabled = tinhaBloqueio;
        if (vida != null) vida.Invulneravel = false;
        if (animator != null && camadaTronco >= 0) animator.SetLayerWeight(camadaTronco, 0f);

        EmCena = false;
        saude.ConcluirFinalizacao(bonusDeAlmas);
    }

    /// <summary>Segura a cena ate' o clipe do golpe passar de momentoDoGolpe.
    ///
    /// POR QUE MEDIR EM VEZ DE CONTAR SEGUNDOS
    /// O Animator avanca em tempo ESCALADO e esta cena roda em camera lenta. Com um
    /// numero fixo de segundos, mudar a lentidao dessincronizaria o sangue do momento
    /// em que a lamina encosta - o impacto sairia com a espada ainda no ar.</summary>
    private System.Collections.Generic.IEnumerable<object> EsperarOGolpe()
    {
        if (animator == null || camadaTronco < 0) yield break;

        float limite = 0f;
        while (limite < esperaMaximaDoGolpe)
        {
            var info = animator.GetCurrentAnimatorStateInfo(camadaTronco);
            if (info.shortNameHash == EstadoHeavy && info.normalizedTime >= momentoDoGolpe)
                yield break;

            limite += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>Duracao prevista da investida, so' pro andamento da camera; o corte de
    /// fato e' feito por EsperarOGolpe.</summary>
    private float PrevisaoAteOGolpe() =>
        Mathf.Clamp(duracaoDoGolpe * momentoDoGolpe / Mathf.Max(0.05f, lentidaoDaInvestida), 0.2f, 3f);

    /// <summary>Posiciona pelo CharacterController em vez de escrever position direto:
    /// escrever a posicao atravessa parede e chao, e a encenacao acontece exatamente
    /// onde ha' mais chance de ter um deles perto.</summary>
    private void Mover(Vector3 destino)
    {
        if (controller != null && controller.enabled)
            controller.Move(destino - transform.position);
        else
            transform.position = destino;
    }
}
