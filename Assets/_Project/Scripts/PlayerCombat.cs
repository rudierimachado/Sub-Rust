using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Combate corpo a corpo com a espada da mao DIREITA: clique rapido = golpe leve,
/// segurar = golpe forte. A mao esquerda carrega o escudo (ver PlayerBloqueio).
/// Anexar no mesmo objeto do Animator (Visual), nao na raiz do Player.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Tempo pra distinguir clique de segurar")]
    [SerializeField] private float holdThreshold = 0.25f;

    [Tooltip("A partir de que ponto da animacao (0-1) um novo clique CORTA o golpe " +
             "atual e emenda o proximo. So' vale depois que o dano ja' saiu, entao " +
             "cortar aqui nao rouba acerto nenhum - corta so' a recuperacao. Perto de " +
             "1 o combate fica travado esperando a animacao acabar; muito baixo e os " +
             "golpes viram um borrao sem leitura.")]
    [Range(0.3f, 1f)] [SerializeField] private float janelaParaEmendar = 0.55f;

    [Header("Combo")]
    [Tooltip("Quanto tempo depois do fim de um golpe o proximo clique ainda CONTINUA o " +
             "combo em vez de recomecar do 1. Curto demais e o combo e' impossivel de " +
             "manter; longo demais e o golpe 3 sai sozinho muito depois da luta.")]
    [SerializeField] private float memoriaDoCombo = 0.45f;
    [Tooltip("Multiplicador de dano por passo do combo. O ultimo golpe doer mais e' o " +
             "que da' motivo pra fechar a sequencia em vez de so' clicar sem ritmo.")]
    [SerializeField] private float[] danoPorPasso = { 1f, 1.15f, 1.45f };

    [Header("Janela de acerto (fracao da animacao)")]
    [SerializeField] private float hitCheckStart = 0.3f;
    [SerializeField] private float hitCheckEnd = 0.7f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float hitStopSeconds = 0.05f;

    [Header("Dano")]
    [SerializeField] private float danoEspada = 34f;

    [Header("Area de acerto (caixa a FRENTE do jogador)")]
    // MEDIDO (setembro/2026): a versao antiga usava uma esfera de raio 0,45 na
    // posicao do transform da espada - que fica EXATAMENTE na mao (pivo e osso da
    // mao na mesma coordenada). Alcance efetivo ~0,85m. So' que o inimigo para a
    // 1,15m pra atacar e tem raio 0,30, ou seja era preciso 1,45m pra encostar
    // nele: o jogador NAO ALCANCAVA quem estava batendo nele, e os ~0,68m externos
    // da lamina (1,13m de comprimento) nao tinham deteccao nenhuma.
    //
    // A caixa a frente resolve e ainda e' mais honesta num side-scroller: o alcance
    // vira uma distancia que o jogador APRENDE, em vez de depender do quadro exato
    // da animacao e da pose da malha (que em bind pose mente - ver PROJETO.md).
    [Tooltip("Distancia do centro da caixa a frente do jogador.")]
    [SerializeField] private float alcanceFrente = 0.85f;
    [Tooltip("Altura do centro da caixa a partir dos pes.")]
    [SerializeField] private float alturaDoGolpe = 1.0f;
    [SerializeField] private Vector3 meiaCaixa = new Vector3(0.80f, 0.70f, 0.50f);

    [Header("Mira automatica no alvo focado")]
    [Tooltip("Ao golpear, VIRA pro lado do inimigo focado. Sem isto o jogador acerta o " +
             "vazio quando o inimigo passa pro outro lado no meio do gesto - num " +
             "side-scroller rapido isso acontece o tempo todo.")]
    [SerializeField] private bool virarParaOAlvo = true;
    [Tooltip("A caixa de acerto acompanha a PROFUNDIDADE do alvo focado. O jogador " +
             "anda numa faixa de 5,2 m em Z, entao uma caixa presa em Z=0 erra quem " +
             "esta' meio metro pra dentro da sala mesmo com o golpe visualmente certo.")]
    [SerializeField] private bool mirarEmProfundidade = true;
    [Tooltip("Quanto a caixa pode deslocar em Z pra alcancar o alvo. Limitado pra nao " +
             "virar teleporte de hitbox: fora disto o jogador precisa se posicionar.")]
    [SerializeField] private float alcanceEmZ = 1.6f;
    [Tooltip("Terceira pessoa: so' gira sozinho pro inimigo focado se ele estiver ate' esta " +
             "distancia. Mais longe o golpe sai pra onde o jogador esta' virado - senao um " +
             "clique no vazio fazia o corpo estalar pra um inimigo do outro lado da sala.")]
    [SerializeField] private float raioDaMiraAutomatica = 3.5f;

    [Header("Efeito de impacto")]
    [SerializeField] private float shakeForce = 1.2f;
    [SerializeField] private Color sparkColor = new Color(1f, 0.75f, 0.2f);

    [Header("Audio")]
    [SerializeField] private AudioClip[] sonsGolpeLeve;
    [SerializeField] private AudioClip[] sonsGolpeForte;
    [SerializeField] private AudioClip[] sonsImpacto;
    [SerializeField, Range(0f, 1f)] private float volumeGolpe = 0.58f;
    [SerializeField, Range(0f, 1f)] private float volumeImpacto = 0.72f;
    [SerializeField, Range(0.85f, 1.15f)] private float variacaoPitch = 0.06f;

    // Os tres golpes leves. Alternar entre clipes DIFERENTES e' o ponto: com um clipe
    // so' o olho decora o padrao em dois golpes e a luta vira "vai e volta, espera".
    // Os tres foram medidos e terminam com a mao em alturas distintas (1,75 / 1,20 /
    // 1,56), entao leem como golpes diferentes de verdade, nao como repeticao.
    private static readonly int[] LightTriggers = {
        Animator.StringToHash("AttackLight"),
        Animator.StringToHash("AttackLight2"),
        Animator.StringToHash("AttackLight3"),
    };
    private static readonly int LightTrigger = Animator.StringToHash("AttackLight");
    private static readonly int HeavyTrigger = Animator.StringToHash("AttackHeavy");

    private Animator animator;
    private PlayerBloqueio bloqueio;
    private FocoDeAlvo foco;
    private VarreduraDeLamina varredura;
    private CinemachineImpulseSource impulseSource;
    private Transform rightSword;
    private PlayerMovement2_5D movimento;
    private PlayerHealth vida;
    private AudioSource audioSource;

    private bool botaoPressionado;
    private float tempoPressionado;
    private bool golpeForteDisparado;

    private enum GolpePendente { Nenhum, Leve, Forte }
    private GolpePendente golpePendente = GolpePendente.Nenhum;

    private bool isAttacking;
    private bool hitAppliedThisSwing;

    // Camada so' do tronco: as pernas seguem na locomocao, entao da' pra atacar andando
    // e correndo. Indice buscado por NOME - inserir uma camada nova reordena os indices
    // e quebraria tudo em silencio.
    private int camadaTronco;
    private float pesoTronco;
    [SerializeField] private float tempoDeSaidaDoTronco = 0.12f;
    private int lightStateHash;
    private int heavyStateHash;
    private int[] lightStateHashes;
    private int passoDoCombo;         // 0,1,2 - qual golpe leve vem agora
    private float comboExpiraEm;      // depois disto, o proximo clique recomeca do 1
    private float multiplicadorDoGolpe = 1f;
    private readonly Collider[] hitBuffer = new Collider[16];
    // Reusado a cada golpe (limpo no inicio do CheckHit): evita alocar por acerto
    // numa luta de barragem.
    private readonly System.Collections.Generic.HashSet<IDamageable> jaAtingidos = new System.Collections.Generic.HashSet<IDamageable>();

    private void Awake()
    {
        animator = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        movimento = GetComponentInParent<PlayerMovement2_5D>();
        vida = GetComponentInParent<PlayerHealth>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.55f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 18f;

        var rightHand = FindDeep(transform, "mixamorig:RightHand");
        rightSword = rightHand != null ? FindDeep(rightHand, "RealSword_Right") : null;

        lightStateHash = Animator.StringToHash("AttackLightR");
        heavyStateHash = Animator.StringToHash("AttackHeavy");
        lightStateHashes = new[] {
            lightStateHash,
            Animator.StringToHash("AttackLight2"),
            Animator.StringToHash("AttackLight3"),
        };

        bloqueio = GetComponent<PlayerBloqueio>();
        // O foco vive na RAIZ do Player (ver FocoDeAlvo); este script esta' no Visual.
        foco = GetComponentInParent<FocoDeAlvo>();
        varredura = GetComponentInChildren<VarreduraDeLamina>(true);
        if (varredura == null)
            Debug.LogError("Sem VarreduraDeLamina na espada - o golpe nao vai dar dano.", this);

        camadaTronco = animator.GetLayerIndex("TroncoAtaque");
        if (camadaTronco < 0)
            Debug.LogError("Camada 'TroncoAtaque' nao existe no Animator Controller - o ataque nao vai aparecer.");
    }

    private void Update()
    {
        LerBotaoDeAtaque();

        if (camadaTronco < 0) return;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(camadaTronco);
        bool emGolpeAgora = stateInfo.shortNameHash == heavyStateHash
                          || EhGolpeLeve(stateInfo.shortNameHash);

        // A camada so' pesa enquanto o golpe roda; some suave para os bracos voltarem
        // a balancar junto com a corrida em vez de cortar seco.
        //
        // O BLOQUEIO tambem precisa de peso: ele vive nesta mesma camada, e com peso 0
        // o estado Bloqueio ate' entra no Animator mas nao move osso nenhum - o escudo
        // simplesmente nao subia, que era o sintoma de "o escudo nao funcionou".
        // O bloqueio NAO entra mais nesta conta: ele mora na camada BracoEscudo, que
        // so' mascara o braco esquerdo (ver PlayerBloqueio). Assim o escudo sobe sem
        // roubar o braco direito, e da' pra atacar com o escudo levantado.
        pesoTronco = Mathf.MoveTowards(pesoTronco, emGolpeAgora ? 1f : 0f,
                                       Time.deltaTime / Mathf.Max(0.01f, tempoDeSaidaDoTronco));
        animator.SetLayerWeight(camadaTronco, pesoTronco);

        if (emGolpeAgora && !isAttacking)
        {
            isAttacking = true;
            hitAppliedThisSwing = false;
            // Zera a lista de atingidos e a memoria do frame anterior: golpe novo,
            // caminho novo.
            jaAtingidos.Clear();
            if (varredura != null) varredura.Comecar();
        }
        else if (!emGolpeAgora && isAttacking)
        {
            isAttacking = false;
            comboExpiraEm = Time.time + memoriaDoCombo;
            DispararPendente();
        }

        // Passou da memoria sem clicar: a sequencia zera e o proximo golpe volta a ser
        // o primeiro. Sem isto o jogador voltaria de uma exploracao de 2 minutos e o
        // proximo clique sairia direto no golpe 3.
        if (!isAttacking && passoDoCombo != 0 && Time.time > comboExpiraEm) passoDoCombo = 0;

    }

    /// <summary>A varredura da lamina roda em LateUpdate, DEPOIS do Animator escrever
    /// os ossos - se rodasse no Update leria a espada na pose do frame ANTERIOR, e a
    /// deteccao ficaria sempre um frame atras do que o jogador ve.
    ///
    /// Foi exatamente isso que fez o dano sumir quando troquei a caixa fixa (que nao
    /// dependia da pose) pela varredura (que depende).</summary>
    [Header("Diagnostico (desligar depois)")]
    [Tooltip("Escreve no console o que a varredura ve a cada golpe. Use pra descobrir " +
             "ONDE a corrente quebra quando o dano nao sai; desligue depois.")]
    [SerializeField] private bool logDoGolpe;

    private void LateUpdate()
    {
        if (!isAttacking || camadaTronco < 0)
        {
            if (logDoGolpe && isAttacking) Debug.Log("[golpe] isAttacking mas camadaTronco<0");
            return;
        }

        float t = animator.GetCurrentAnimatorStateInfo(camadaTronco).normalizedTime % 1f;
        if (logDoGolpe)
            Debug.Log($"[golpe] t={t:F2} janela={hitCheckStart:F2}-{hitCheckEnd:F2} " +
                      $"dentro={(t >= hitCheckStart && t <= hitCheckEnd)} varredura={(varredura != null)}");
        // TODO frame da janela, nao so' o primeiro: e' o que fecha o buraco entre
        // frames. Quem impede dano repetido no mesmo alvo e' o jaAtingidos.
        if (t >= hitCheckStart && t <= hitCheckEnd) CheckHit();
        else if (varredura != null) varredura.Acompanhar();
    }

    private void LerBotaoDeAtaque()
    {
        // EXAUSTO nao ataca. Junto com o escudo travado (PlayerBloqueio) e a esquiva
        // travada (PlayerDodge), e' o que faz raspar o fundo da barra virar um erro de
        // verdade - o mesmo castigo que o inimigo leva ao ter a guarda quebrada.
        if (vida != null && vida.Exausto)
        {
            botaoPressionado = false;
            golpePendente = GolpePendente.Nenhum;
            return;
        }

        bool down = (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                    (Keyboard.current != null && Keyboard.current.jKey.isPressed);

        // O golpe leve sai NO CLIQUE, nao ao soltar.
        //
        // Antes ele so' disparava no release: o atraso entre apertar e ver a espada
        // era o tempo que o DEDO do jogador levava, nao um tempo do jogo - e um clique
        // de 0,25s ainda virava golpe FORTE sem querer. Nada le' menos como hack and
        // slash que apertar e o personagem esperar pra decidir o que fazer.
        //
        // O golpe forte continua no segurar, mas agora ele EMENDA depois do leve (o
        // leve ja' saiu), que e' o comportamento normal do genero: bater rapido, e
        // segurar pra terminar mais pesado.
        // Bloquear NAO impede mais de atacar: o escudo vive numa camada propria que
        // so' move o braco esquerdo, entao o braco direito continua livre pro golpe.
        // Era a versao anterior, com tudo na mesma camada, que obrigava a escolher.

        if (down && !botaoPressionado)
        {
            botaoPressionado = true;
            tempoPressionado = 0f;
            golpeForteDisparado = false;
            EncararAlvoFocado();

            // GOLPE FORTE AUTOMATICO no inimigo com a guarda quebrada. Ele ja' esta'
            // travado e exposto (ver EnemyStamina/EnemyAI); exigir que o jogador
            // SEGURE o botao pra aproveitar transformaria a recompensa em mais uma
            // coisa pra executar sob pressao, e o momento passaria antes da mao
            // reagir. Aqui a punicao e' automatica: quebrou a guarda, o proximo
            // clique ja' sai pesado.
            if (AlvoComGuardaQuebrada())
            {
                golpeForteDisparado = true;   // nao repete o forte ao segurar
                DispararOuEnfileirar(GolpePendente.Forte);
            }
            else
            {
                DispararOuEnfileirar(GolpePendente.Leve);
            }
        }
        else if (down && botaoPressionado)
        {
            tempoPressionado += Time.deltaTime;
            if (!golpeForteDisparado && tempoPressionado >= holdThreshold)
            {
                golpeForteDisparado = true;
                DispararOuEnfileirar(GolpePendente.Forte);
            }
        }
        else if (!down && botaoPressionado)
        {
            botaoPressionado = false;
        }
    }

    /// <summary>Dispara agora se puder, senao guarda pro fim do golpe atual.
    ///
    /// O "pode agora" nao e' so' "nao esta atacando": depois que a lamina JA passou
    /// (o dano saiu, t > janelaParaEmendar) o resto da animacao e' so' recuperacao, e
    /// travar ali e' o que fazia o combate parecer lento. Cortar a recuperacao pra
    /// emendar o proximo golpe e' o que da' a cadencia de hack and slash - o jogador
    /// mantem a pressao em vez de esperar o boneco terminar de guardar a espada.
    ///
    /// O dano nao repete no golpe cortado: hitAppliedThisSwing so' volta a false
    /// quando o Animator entra no estado de novo (ver Update).</summary>
    /// <summary>Se o inimigo FOCADO agora esta' com a guarda quebrada.
    ///
    /// Usa o alvo do foco e nao uma varredura propria: assim o golpe forte automatico
    /// sai exatamente em quem a bolinha esta' marcando, que e' o inimigo que o jogador
    /// esta' vendo como alvo - e nunca num terceiro que por acaso esta' cansado atras
    /// dele.</summary>
    private bool AlvoComGuardaQuebrada()
    {
        if (foco == null || foco.Alvo == null) return false;
        var folego = foco.Alvo.GetComponent<EnemyStamina>();
        return folego != null && folego.GuardaQuebrada;
    }

    /// <summary>Vira o jogador pro inimigo focado no instante do golpe.
    ///
    /// So' vira se ele estiver do OUTRO lado - e nunca se estiver praticamente na
    /// mesma linha (a checagem de 0,25 m), senao o personagem ficaria oscilando de
    /// lado com o inimigo colado nele.</summary>
    private void EncararAlvoFocado()
    {
        if (!virarParaOAlvo || foco == null || foco.Alvo == null || movimento == null) return;

        if (movimento.TerceiraPessoa)
        {
            Vector3 d = foco.Alvo.position - movimento.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0625f || d.magnitude > raioDaMiraAutomatica) return;
            movimento.Encarar(d);
            return;
        }

        float dx = foco.Alvo.position.x - movimento.transform.position.x;
        if (Mathf.Abs(dx) < 0.25f) return;
        movimento.Virar(Mathf.Sign(dx));
    }

    private void DispararOuEnfileirar(GolpePendente golpe)
    {
        if (!isAttacking) { Disparar(golpe); return; }

        if (hitAppliedThisSwing && camadaTronco >= 0)
        {
            float t = animator.GetCurrentAnimatorStateInfo(camadaTronco).normalizedTime % 1f;
            if (t >= janelaParaEmendar) { Disparar(golpe); return; }
        }

        golpePendente = golpe;
    }

    private void DispararPendente()
    {
        if (golpePendente == GolpePendente.Nenhum) return;
        var golpe = golpePendente;
        golpePendente = GolpePendente.Nenhum;
        Disparar(golpe);
    }

    private bool EhGolpeLeve(int hash)
    {
        if (lightStateHashes == null) return hash == lightStateHash;
        for (int i = 0; i < lightStateHashes.Length; i++)
            if (lightStateHashes[i] == hash) return true;
        return false;
    }

    private void Disparar(GolpePendente golpe)
    {
        // Peso cheio JA': a transicao leva alguns frames e sem isso os primeiros
        // quadros do golpe sairiam apagados.
        if (camadaTronco >= 0)
        {
            pesoTronco = 1f;
            animator.SetLayerWeight(camadaTronco, 1f);
        }

        if (golpe == GolpePendente.Forte)
        {
            animator.SetTrigger(HeavyTrigger);
            TocarSomAleatorio(sonsGolpeForte != null && sonsGolpeForte.Length > 0 ? sonsGolpeForte : sonsGolpeLeve, volumeGolpe);
        }
        else if (golpe == GolpePendente.Leve)
        {
            // Avanca a sequencia: cada clique sai num clipe DIFERENTE, e o dano sobe
            // ate' o ultimo passo. O passo volta pro 0 sozinho depois de memoriaDoCombo
            // sem clicar (ver Update).
            int passo = Mathf.Clamp(passoDoCombo, 0, LightTriggers.Length - 1);
            animator.SetTrigger(LightTriggers[passo]);

            multiplicadorDoGolpe = (danoPorPasso != null && passo < danoPorPasso.Length)
                                 ? danoPorPasso[passo] : 1f;

            // Pitch subindo ao longo do combo: mesmo com o audio provisorio, ouvir a
            // sequencia SUBIR ja' da' a leitura de que os golpes sao diferentes.
            TocarSomAleatorio(sonsGolpeLeve, volumeGolpe, 1f + passo * 0.07f);


            passoDoCombo = (passo + 1) % LightTriggers.Length;
            comboExpiraEm = Time.time + memoriaDoCombo;
        }
    }

    /// <summary>Centro da caixa de acerto: a frente do jogador, na altura do peito.
    /// Usa a RAIZ do Player e o Facing dele, nunca o transform do Visual - o Visual
    /// e' espelhado em escala Z pra virar (ver PlayerMovement2_5D) e ler direcao
    /// dali daria o lado errado em um dos sentidos.</summary>
    /// <summary>Centro da caixa de acerto. Usa a RAIZ e o Facing do movimento, nunca o
    /// transform do Visual - o Visual e' espelhado em escala Z pra virar, e ler direcao
    /// dali daria o lado errado num dos sentidos.
    ///
    /// Quando ha' alvo focado, a caixa acompanha a PROFUNDIDADE dele (dentro de um
    /// limite): o jogador anda numa faixa de 5,2 m em Z e o inimigo tambem, entao uma
    /// caixa presa em Z=0 erra golpe que na tela parece ter acertado.</summary>
    private Vector3 CentroDoGolpe()
    {
        var raiz = movimento != null ? movimento.transform : transform;
        Vector3 frente = movimento != null ? movimento.Direcao : Vector3.right;

        // A correcao de profundidade so' existe no side-scroller: em terceira pessoa a
        // frente ja' e' 3D e o corpo gira pro alvo (EncararAlvoFocado).
        float z = 0f;
        bool tresD = movimento != null && movimento.TerceiraPessoa;
        if (!tresD && mirarEmProfundidade && foco != null && foco.Alvo != null)
            z = Mathf.Clamp(foco.Alvo.position.z - raiz.position.z, -alcanceEmZ, alcanceEmZ);

        return raiz.position + frente * alcanceFrente + new Vector3(0f, alturaDoGolpe, z);
    }

    /// <summary>Deteccao de acerto pela LAMINA, varrendo o caminho dela desde o frame
    /// anterior (ver VarreduraDeLamina). Chamada TODO frame da janela de acerto, nao
    /// uma vez so'.
    ///
    /// O que mudou e por que: a versao anterior era um OverlapBox fixo a frente do
    /// jogador, testado num instante. Isso dava alcance que nao correspondia a espada,
    /// perdia acerto quando o inimigo caia entre dois frames (a lamina cruza mais de
    /// 2 m em 0,68 s) e obrigava a encostar no inimigo. A varredura usa a posicao real
    /// do fio e cobre o caminho inteiro, sem buraco entre frames.
    ///
    /// Cada alvo leva dano UMA vez por golpe (jaAtingidos), mas o golpe continua
    /// varrendo ate' o fim da janela - entao um corte em arco pega todos que
    /// atravessarem o caminho, nao so' o primeiro.</summary>
    private void CheckHit()
    {
        if (varredura == null) return;

        varredura.Varrer(jaAtingidos, (alvo, ponto) =>
        {
            alvo.TakeHit(danoEspada * multiplicadorDoGolpe, ponto);

            Vector3 dir = movimento != null ? movimento.Direcao : Vector3.right;
            // Intensidade escalada pelo dano: 34 e' o golpe base, entao um golpe
            // pesado esguicha mais que um leve.
            ImpactoDeGolpe.Tocar(ponto, dir + Vector3.up * 0.25f,
                                 (danoEspada / 34f) * multiplicadorDoGolpe);

            TocarSomAleatorio(sonsImpacto, volumeImpacto);
            // Tremor e congelamento escalam com o passo do combo: o golpe que FECHA a
            // sequencia bate mais forte.
            impulseSource?.GenerateImpulseWithForce(shakeForce * multiplicadorDoGolpe);
            HitStop.Aplicar(this, hitStopSeconds * multiplicadorDoGolpe, 0.02f);
        });
    }

    private void OnDrawGizmosSelected()
    {
        // Deixa a area de acerto visivel no editor - sem isso, calibrar alcance de
        // golpe vira tentativa e erro.
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawCube(CentroDoGolpe(), meiaCaixa * 2f);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(CentroDoGolpe(), meiaCaixa * 2f);
    }

    private void TocarSomAleatorio(AudioClip[] clips, float volume, float pitchBase = 1f)
    {
        if (clips == null || clips.Length == 0 || audioSource == null) return;
        audioSource.pitch = pitchBase * Random.Range(1f - variacaoPitch, 1f + variacaoPitch);
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
