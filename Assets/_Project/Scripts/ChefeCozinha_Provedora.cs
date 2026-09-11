using UnityEngine;

/// <summary>
/// A Provedora - chefe secreto da Cozinha 10. Fica a distancia arremessando caldo
/// fervendo; depois de N arremessos ela esgota o folego e PRECISA parar pra
/// recuperar - essa e a janela de contra-ataque, nao um numero escondido.
///
/// POR QUE NAO USA EnemyAI
/// EnemyAI e uma maquina de estados de combate CORPO A CORPO (aproxima ate
/// distanciaDeGolpe, mira, golpe, recua) - nada nela lanca projetil ou mantem
/// distancia de proposito. Encaixar comportamento a distancia ali dentro mudaria um
/// script usado por TODO inimigo comum do jogo (Esqueleto.prefab incluido) so para
/// servir a um chefe - exatamente o "dar ruim" que se pediu para evitar. Por isso a
/// Provedora tem sua PROPRIA maquina de estados, so dela, igual o principio ja
/// usado por ChefeCapela (fica ao lado dos componentes genericos, nao reescreve).
///
/// O QUE E REAPROVEITADO (porque e genuinamente generico)
/// - EnemyHealth: vida/dano/morte, identico a qualquer inimigo.
/// - EnemyStamina: JA FAZ o "gasta folego por acao, precisa esperar pra
///   recuperar" que este chefe pede. Nao foi escrito pra ela - existia pro combate
///   corpo a corpo comum ficar legivel (ver comentario no proprio arquivo) e serve
///   aqui sem alterar uma linha. EstaExausta simplesmente le folego.Exausto.
/// - EnemyDamageFeedback / SeparacaoDeCorpos: nao dependem de EnemyAI (conferido),
///   funcionam do mesmo jeito.
///
/// Anexar na RAIZ do inimigo, junto com EnemyHealth, EnemyStamina,
/// CharacterController. NAO anexar EnemyAI nela.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyStamina))]
[RequireComponent(typeof(CharacterController))]
public class ChefeCozinha_Provedora : MonoBehaviour
{
    private enum Estado { Adormecida, Reposicionar, Arremessando, Panelada, Cansada, Morta }

    // Deteccao: NAO tem campo nenhum aqui, de proposito. Quem faz ela aparecer e
    // comecar a lutar e' o SpawnDoChefe (um por fornalha), que posiciona ela no chao
    // e chama IniciarLuta(). Este script so' cuida do COMBATE em si.

    [Header("Ancoragem (fica perto de onde comecou a lutar - nao persegue pela sala)")]
    [Tooltip("O quanto ela pode se afastar do ponto onde a luta comecou (a fornalha) " +
             "antes de andar de volta. Isso e' so' CORRECAO de deriva (recuo de dano, " +
             "empurrao de SeparacaoDeCorpos) - ela nao persegue o jogador.")]
    [SerializeField] private float raioDeAncoragem = 6.0f;
    [SerializeField] private float velocidadeDeReposicao = 3.2f;
    [Tooltip("De quanto em quanto tempo ela escolhe um ponto NOVO pra onde ir. " +
             "Faixa, nao valor fixo: com periodo fixo o vai-e-vem vira metronomo e o " +
             "jogador decora o ritmo em duas manobras.")]
    [SerializeField] private Vector2 intervaloParaTrocarDeX = new Vector2(0.6f, 1.5f);
    [Tooltip("Ela nunca escolhe um destino mais perto do jogador que isto - e' o que " +
             "impede a chefe de virar inimigo corpo a corpo colado em voce.")]
    [SerializeField] private float distanciaMinima = 2.5f;
    [Tooltip("Alcance maximo pra comecar a arremessar. Perto disso ela arremessa " +
             "de qualquer distancia, ate' bem de perto - ela fica no fogao, nao foge.")]
    [SerializeField] private float distanciaMaxima = 9f;

    [Header("Profundidade (ela nao fica presa num plano so')")]
    [Tooltip("Faixa de Z em que ela pode andar. Medido no piso da cozinha, que vai de " +
             "Z -4,0 a 2,9 com a parede do fundo em Z 2,30 - a faixa fica dentro disso " +
             "com folga pro corpo dela nao raspar na parede. O jogador anda em Z 0 e " +
             "tem passo lateral de -0,5 a 1,2, entao ela cruza a faixa dele.")]
    [SerializeField] private float zMinimo = -2.5f;
    [SerializeField] private float zMaximo = 1.2f;
    [SerializeField] private float velocidadeEmZ = 2.2f;
    [Tooltip("A cada quanto tempo ela escolhe uma profundidade nova. Faixa, nao valor " +
             "fixo: com periodo fixo o vai-e-vem fica metronomico e previsivel.")]
    [SerializeField] private Vector2 intervaloParaTrocarDeZ = new Vector2(0.8f, 1.9f);

    [Header("Arremesso")]
    [SerializeField] private GameObject prefabProjetil;
    [SerializeField] private Transform pontoDeArremesso;
    [SerializeField] private float danoDoArremesso = 24f;
    [SerializeField] private float alturaDoArco = 1.3f;
    [Tooltip("OBSOLETO como tempo fixo - mantido so' como TETO do voo, pra um arremesso " +
             "muito longo nao virar um lob eterno. O tempo real vem da distancia.")]
    [SerializeField] private float tempoDeVoo = 0.9f;

    [Tooltip("Velocidade com que a panela SAI da mao, em m/s - constante, como a forca " +
             "de um braco. O tempo de voo passa a ser distancia/velocidade. " +
             "Antes o tempo era FIXO em 0,5 s pra qualquer distancia, o que fazia a " +
             "mesma panela sair a 17 m/s de longe (rapida demais pra ler) e a 5 m/s de " +
             "perto (lenta demais pra ameacar) - exatamente ao contrario de um " +
             "arremesso real.")]
    [SerializeField] private float velocidadeDeLancamento = 11f;
    [Tooltip("Quanto ela ANTECIPA o movimento do jogador, de 0 a 1. Em 1 ela mira onde " +
             "voce VAI estar e acerta sempre; em 0 mira onde voce esta' e erra sempre " +
             "(era o comportamento antigo). 0,7 acerta quem corre em linha reta e erra " +
             "quem muda de direcao - que e' a decisao que a luta deve cobrar.")]
    [Range(0f, 1f)] [SerializeField] private float antecipacao = 0.7f;
    [Tooltip("Arco extra por metro de distancia. Arremesso longo sobe mais - com arco " +
             "fixo, o caldo de longe vem quase reto e nao da' pra ler a trajetoria.")]
    [SerializeField] private float arcoPorMetro = 0.10f;

    [Header("Peso do corpo")]
    [Tooltip("Metros por segundo que ela avanca ao soltar o arremesso. Arremessar " +
             "plantada no chao le' como boneco - o corpo inteiro entra no lancamento.")]
    [SerializeField] private float avancoNoArremesso = 1.6f;
    [Tooltip("Segundos de avanco, a partir do lancamento.")]
    [SerializeField] private float duracaoDoAvanco = 0.22f;

    [Header("Panelada (corpo a corpo)")]
    [Tooltip("Distancia em X dentro da qual ela para de arremessar e PARTE PRA CIMA. " +
             "Antes ela nao tinha ataque nenhum de perto: colar nela era 100% seguro, " +
             "e essa era a maior quebra de credibilidade da luta - da' pra ficar em " +
             "cima dela a luta inteira sem risco.")]
    [SerializeField] private float distanciaDaPanelada = 2.8f;
    [Tooltip("Segundos de telegrafo antes da panela descer. Generoso de proposito: e' " +
             "um golpe que mata rapido, entao precisa ser desviavel por leitura.")]
    [SerializeField] private float telegrafoDaPanelada = 0.42f;
    [Tooltip("Alcance do golpe no instante em que conecta.")]
    [SerializeField] private float alcanceDaPanelada = 2.4f;
    [Tooltip("Dano da panelada. Quase o dobro do arremesso: e' o preco de escolher " +
             "ficar colado nela em vez de administrar distancia.")]
    [SerializeField] private float danoDaPanelada = 22f;
    [Tooltip("Espera entre paneladas, pra ela nao virar uma moedora de carne quando " +
             "voce estiver sem espaco pra recuar.")]
    [SerializeField] private float cooldownDaPanelada = 2.2f;
    [Tooltip("Avanco em m/s durante a descida do golpe.")]
    [SerializeField] private float avancoDaPanelada = 2.6f;

    [Header("Reagir a apanhar")]
    [Tooltip("Dano que INTERROMPE o arremesso no meio. Abaixo disso ela cambaleia mas " +
             "termina o gesto. Seu golpe forte faz ~49 e o leve ~34: assim o pesado " +
             "interrompe e o leve nao, que e' a diferenca que da' sentido a escolher.")]
    [SerializeField] private float danoQueInterrompe = 45f;
    [Tooltip("Segundos que ela leva pra se recompor depois de um golpe que interrompe.")]
    [SerializeField] private float atrasoAposInterromper = 0.55f;
    [SerializeField] private float intervaloEntreArremessos = 0.45f;

    [Header("Cansaco")]
    [Tooltip("Quanto tempo ela fica indefesa depois de esgotar a barragem. E' a " +
             "janela de contra-ataque inteira do combate, entao e' um tempo FIXO daqui " +
             "e nao o 'ate' a stamina dar pra mais um golpe' do EnemyStamina - aquele " +
             "criterio devolve o controle em ~1s e ela volta a atacar antes de voce " +
             "chegar perto.")]
    [SerializeField] private float duracaoDoCansaco = 3.4f;

    [Header("Fase 2 (abaixo desta fracao de vida, fica mais dura)")]
    [Range(0f, 1f)] [SerializeField] private float vidaFase2 = 0.4f;
    [SerializeField] private float intervaloEntreArremessosFase2 = 0.25f;

    // Arena: os blocos que travavam a passagem foram REMOVIDOS a pedido - a luta
    // acontece na sala aberta, da' pra sair andando ou subir a escada a qualquer
    // momento. Se um dia quiser conter de novo, o lugar e' aqui.

    [Header("Animator (parametros - deixe em branco se nao existirem ainda)")]
    [SerializeField] private string paramCansada = "Cansada";
    [Tooltip("Trigger disparado a CADA arremesso (nao um bool continuo - senao a " +
             "barragem inteira tocaria como uma pose so').")]
    [SerializeField] private string paramArremessoTrigger = "Arremessar";

    [Header("Efeitos (todos opcionais - deixe vazio pra pular)")]
    [Tooltip("Toca uma vez na mao dela a cada arremesso (respingo/vapor de saida).")]
    [SerializeField] private ParticleSystem efeitoDeArremesso;
    [Tooltip("Fica ligado o tempo inteiro que ela estiver Cansada (folego, poeira).")]
    [SerializeField] private ParticleSystem efeitoDeCansaco;
    [Tooltip("Poeira dos passos - emite em pulsos enquanto ela anda reposicionando.")]
    [SerializeField] private ParticleSystem efeitoDePasso;
    [SerializeField] private float intervaloEntrePassos = 0.35f;

    private EnemyHealth vida;
    private EnemyStamina folego;
    private CharacterController controller;
    private Animator animator;
    private PoseProceduralProvedora pose;
    private Transform visual;
    private Transform jogador;

    private Estado estado = Estado.Adormecida;
    private float facing = 1f;
    private float velocidadeVertical;
    private float proximoArremessoEm;
    private float soltarEm = -1f;
    private float zAlvo;
    private float proximaTrocaDeZ;
    private float xAlvo;
    private float proximaTrocaDeX;
    private float cansadaAte;
    private bool fase2Ativa;
    private float proximoPassoEm;
    private float ancoraX;
    private Vector3 posAnteriorDoJogador;
    private Vector3 velocidadeDoJogador;
    private float avancoAte;
    private float paneladaEm = -1f;
    private float proximaPaneladaLiberadaEm;
    private bool danoDaPaneladaAplicado;


    private const float Gravidade = -25f;

    private void Awake()
    {
        vida = GetComponent<EnemyHealth>();
        folego = GetComponent<EnemyStamina>();
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        pose = GetComponent<PoseProceduralProvedora>();
        visual = transform.Find("Visual");

        // Bounds recalculados a cada frame a partir dos OSSOS REAIS.
        // Sem isso o Unity usa a caixa pre-calculada da bind pose e, quando a
        // animacao move osso pra fora dela, DESCARTA a malha inteira por culling -
        // o chefe fica invisivel em Play (mas visivel no editor, onde nao ha
        // animacao rodando) sem erro nenhum no console. Custo desprezivel: e' um
        // personagem so'. Forcado aqui em vez de marcado na cena porque reimportar
        // o FBX reseta esse override na instancia.
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        // Animator nunca para de animar por estar fora de tela - se ele congelar
        // com a pose num frame ruim, a malha some junto.
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // O baforo sai no ritmo da respiracao da pose, nao num fluxo continuo.
        if (pose != null) pose.AoExalar += Bufar;

        // Convencao 2.5D deste jogo (mesma do EnemyAI): a RAIZ olha pro +X do mundo
        // e o lado pra qual o personagem encara sai de espelhar o Z do "Visual".
        // Com a raiz em rotacao zero ela olha pro +Z - de costas pra camera, contra a
        // parede - e espelhar o Visual nao vira nada, so' inverte a malha no lugar.
        // Forcado aqui porque e' invariante do combate, nao decoracao de cena.
        transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
    }

    /// <summary>Reacao a levar dano, chamada pelo EnemyDamageFeedback (que ja' recebe
    /// o ponto e o valor do golpe do EnemyHealth). Traduz a pancada em cambaleio.
    ///
    /// Ela nao tem clipe de dano: o Animator dela so' tem "Alert" mais os parametros
    /// Cansada/Arremessar - o trigger "Hit" que o EnemyDamageFeedback dispara nao
    /// existe neste controller e cai no vazio. Sem isto ela apanha sem mexer um
    /// musculo, so' deslizando pra tras.</summary>
    public void ReagirAoDano(Vector3 origemDoGolpe, float dano)
    {
        float lado = transform.position.x >= origemDoGolpe.x ? 1f : -1f;

        if (pose != null)
        {
            // 24 = o dano do arremesso dela, referencia natural de "golpe medio" nesta luta.
            float forca = Mathf.Clamp01(dano / 24f);
            pose.LevarImpacto(forca, lado);
        }

        // GOLPE PESADO INTERROMPE. Ela arremessava impassivel enquanto voce a cortava,
        // o que fazia o corpo dela parecer sem peso nenhum. Agora o gesto MORRE no
        // meio e ela ainda leva um tempo pra se recompor - e' o premio por escolher o
        // golpe forte em vez de martelar o leve.
        //
        // Cancelar o caldo que JA saiu seria injusto ao contrario; o que morre aqui e'
        // o arremesso ainda armado (soltarEm no futuro).
        if (dano >= danoQueInterrompe && soltarEm > 0f)
        {
            soltarEm = -1f;
            avancoAte = 0f;
            proximoArremessoEm = Time.time + atrasoAposInterromper;
        }
    }

    /// <summary>Um baforo por expiracao. Emitir em rajada e' o que le' como bufar:
    /// emissao continua vira fumaca de cenario e nao diz nada sobre o folego dela.</summary>
    private void Bufar()
    {
        if (efeitoDeCansaco != null) efeitoDeCansaco.Emit(14);
    }

    private void Update()
    {
        if (jogador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) jogador = go.transform;
        }

        MedirVelocidadeDoJogador();
        AplicarGravidade();
        AplicarAvancoDoArremesso();

        // Fora do switch de proposito: o caldo tem que sair mesmo que ela mude de
        // estado no meio do gesto (levar dano e cansar, por exemplo). O golpe ja' foi
        // anunciado ao jogador - cancelar em silencio seria injusto ao contrario.
        if (soltarEm > 0f && Time.time >= soltarEm) SoltarProjetil();

        switch (estado)
        {
            case Estado.Adormecida:    Adormecida();    break;
            case Estado.Reposicionar:  Reposicionar();  break;
            case Estado.Arremessando:  Arremessando();  break;
            case Estado.Panelada:      Panelada();      break;
            case Estado.Cansada:       Cansada();       break;
            case Estado.Morta:         return;
        }

        VerificarMorte();
        VerificarFase2();
    }

    private void AplicarGravidade()
    {
        if (controller.isGrounded && velocidadeVertical < 0f) velocidadeVertical = -2f;
        else velocidadeVertical += Gravidade * Time.deltaTime;
        controller.Move(new Vector3(0f, velocidadeVertical, 0f) * Time.deltaTime);
    }

    /// <summary>Estado de espera pura - nao faz polling nenhum. Só existe pra dar
    /// nome ao "antes de IniciarLuta()" no switch do Update(); quem tira ela daqui
    /// e sempre o SpawnDoChefe, nunca ela mesma medindo distancia.</summary>
    private void Adormecida() { }

    /// <summary>Chamado pelo SpawnDoChefe quando o jogador passa na frente da
    /// fornalha. Publico de proposito - e o UNICO jeito
    /// dela comecar a lutar.</summary>
    public void IniciarLuta()
    {
        if (estado != Estado.Adormecida) return; // ja comecou (o outro gatilho ativou primeiro)

        // O SpawnDoChefe ja' posicionou ela no chao da fornalha antes de chamar isto.
        ancoraX = transform.position.x;
        zAlvo = transform.position.z;
        proximaTrocaDeZ = Time.time;
        xAlvo = transform.position.x;
        proximaTrocaDeX = Time.time;
        estado = Estado.Reposicionar;
    }

    /// <summary>NAO persegue o jogador pela sala - fica ancorada perto de onde a
    /// luta comecou (a fornalha) e so' arremessa. O unico movimento e' CORRETIVO:
    /// se alguma coisa a empurrou pra fora do raio de ancoragem (recuo de dano,
    /// SeparacaoDeCorpos), ela volta andando; do contrario fica parada.</summary>
    private void Reposicionar()
    {
        float dx = jogador.position.x - transform.position.x;
        Encarar(Mathf.Sign(dx == 0f ? facing : dx));

        Andar();

        if (Mathf.Abs(dx) <= distanciaMaxima)
        {
            estado = Estado.Arremessando;
            proximoArremessoEm = Time.time; // arremessa imediato ao entrar na faixa
        }
    }

    private void Arremessando()
    {
        float dx = jogador.position.x - transform.position.x;
        Encarar(Mathf.Sign(dx == 0f ? facing : dx));

        if (Mathf.Abs(dx) > distanciaMaxima * 1.3f)
        {
            estado = Estado.Reposicionar;
            return;
        }

        if (folego.Exausto) { EntrarEmCansada(); return; }

        // PERTO DEMAIS PRA ARREMESSAR: ela troca pro corpo a corpo em vez de recuar
        // educadamente enquanto voce a corta.
        if (Mathf.Abs(dx) <= distanciaDaPanelada && Time.time >= proximaPaneladaLiberadaEm)
        {
            EntrarNaPanelada();
            return;
        }

        Andar();

        if (Time.time < proximoArremessoEm) return;
        if (!folego.TryGastar()) { EntrarEmCansada(); return; }

        Arremessar();
        proximoArremessoEm = Time.time + (fase2Ativa ? intervaloEntreArremessosFase2 : intervaloEntreArremessos);
    }

    /// <summary>Sorteia um ponto em X dentro do raio de ancoragem que NAO fique colado
    /// no jogador nem fora do alcance de arremesso. Tenta algumas vezes e, se a sala
    /// naquele instante nao oferecer ponto bom (jogador colado na borda da ancoragem),
    /// devolve a ponta mais afastada dele - ela recua, nunca congela.
    /// Sortear em vez de calcular e' de proposito: destino previsivel vira padrao
    /// decorado, e o combate pedido e' de esquiva sob pressao.</summary>
    private float EscolherDestinoEmX()
    {
        float min = ancoraX - raioDeAncoragem;
        float max = ancoraX + raioDeAncoragem;

        for (int i = 0; i < 8; i++)
        {
            float cand = Random.Range(min, max);
            float d = Mathf.Abs(cand - jogador.position.x);
            if (d >= distanciaMinima && d <= distanciaMaxima) return cand;
        }

        return Mathf.Abs(min - jogador.position.x) > Mathf.Abs(max - jogador.position.x) ? min : max;
    }

    /// <summary>O andar dela: ela ESCOLHE um destino e vai ate' la', sempre.
    /// O modelo anterior era corretivo - so' dava um passo quando a distancia saia de
    /// uma faixa - e por isso ela passava a luta parada: dentro da zona morta nao
    /// existia motivo nenhum pra andar. Aqui sempre ha' destino, entao sempre ha'
    /// movimento, e o destino nunca sai do raio de ancoragem nem cola no jogador.</summary>
    private void Andar()
    {
        if (Time.time >= proximaTrocaDeX)
        {
            xAlvo = EscolherDestinoEmX();
            proximaTrocaDeX = Time.time + Random.Range(intervaloParaTrocarDeX.x, intervaloParaTrocarDeX.y);
        }

        float horizontal = 0f;
        float dxAlvo = xAlvo - transform.position.x;
        if (Mathf.Abs(dxAlvo) > 0.15f) horizontal = Mathf.Sign(dxAlvo) * velocidadeDeReposicao;

        // Profundidade: ela troca de plano sozinha, pra nao ficar colada na parede do
        // fundo nem virar um alvo parado numa linha so'. E' escolha propria, nao
        // perseguicao - o alvo em Z e' sorteado na faixa, nao copiado do jogador.
        if (Time.time >= proximaTrocaDeZ)
        {
            zAlvo = Random.Range(zMinimo, zMaximo);
            proximaTrocaDeZ = Time.time + Random.Range(intervaloParaTrocarDeZ.x, intervaloParaTrocarDeZ.y);
        }
        float profundidade = 0f;
        float dz = zAlvo - transform.position.z;
        if (Mathf.Abs(dz) > 0.1f) profundidade = Mathf.Sign(dz) * velocidadeEmZ;

        controller.Move(new Vector3(horizontal, 0f, profundidade) * Time.deltaTime);

        // Cadencia do passo sai da velocidade TOTAL no plano, nao so' do X - andando
        // so' em profundidade ela deslizaria de novo.
        if (pose != null) pose.DefinirVelocidade(new Vector2(horizontal, profundidade).magnitude);

        if (horizontal != 0f && efeitoDePasso != null && Time.time >= proximoPassoEm)
        {
            efeitoDePasso.Emit(1);
            proximoPassoEm = Time.time + intervaloEntrePassos;
        }
    }

    /// <summary>Arma a panelada: gesto de braco (o mesmo do arremesso - e' o mesmo
    /// movimento de ombro) e um relogio ate' o golpe conectar.</summary>
    private void EntrarNaPanelada()
    {
        estado = Estado.Panelada;
        paneladaEm = Time.time + telegrafoDaPanelada;
        danoDaPaneladaAplicado = false;

        if (pose != null) pose.Arremessar();
        if (animator != null && !string.IsNullOrEmpty(paramArremessoTrigger))
            animator.SetTrigger(paramArremessoTrigger);
        if (efeitoDeArremesso != null) efeitoDeArremesso.Play();

        // Um arremesso que ja' estivesse armado morre aqui: ela nao solta caldo e da'
        // panelada no mesmo gesto.
        soltarEm = -1f;
    }

    /// <summary>O golpe de perto. Ela AVANCA durante a descida - parada, o golpe seria
    /// desviavel so' andando pra tras um passo.</summary>
    private void Panelada()
    {
        float dx = jogador.position.x - transform.position.x;
        Encarar(Mathf.Sign(dx == 0f ? facing : dx));

        // Avanca enquanto a panela desce.
        if (Time.time < paneladaEm)
        {
            controller.Move(new Vector3(facing * avancoDaPanelada, 0f, 0f) * Time.deltaTime);
            return;
        }

        if (!danoDaPaneladaAplicado)
        {
            danoDaPaneladaAplicado = true;

            bool naFrente = dx * facing >= -0.2f;
            bool noPlano = Mathf.Abs(jogador.position.z - transform.position.z) <= 1.8f;

            if (Mathf.Abs(dx) <= alcanceDaPanelada && naFrente && noPlano)
            {
                jogador.GetComponent<IDamageable>()?.TakeHit(danoDaPanelada, transform.position);
                ImpactoDeGolpe.Tocar(jogador.position + Vector3.up * 1.0f,
                                     new Vector3(Mathf.Sign(dx), 0.3f, 0f), 1.6f);
            }
        }

        // Recuperacao curta antes de voltar a arremessar.
        if (Time.time >= paneladaEm + 0.35f)
        {
            proximaPaneladaLiberadaEm = Time.time + cooldownDaPanelada;
            proximoArremessoEm = Time.time + 0.2f;
            estado = Estado.Arremessando;
        }
    }

    private void EntrarEmCansada()
    {
        estado = Estado.Cansada;
        cansadaAte = Time.time + duracaoDoCansaco;
        SetAnimBool(paramCansada, true);
        if (pose != null) { pose.DefinirCansada(true); pose.DefinirAndando(false); }
    }

    /// <summary>Folego esgotado: ela nao ataca e mal se move ate EnemyStamina
    /// liberar de novo. E a janela de punicao - existe so porque folego.Exausto
    /// volta a false sozinho (EnemyStamina ja cuida do atraso e da regeneracao).</summary>
    private void Cansada()
    {
        if (Time.time < cansadaAte) return;

        SetAnimBool(paramCansada, false);
        if (pose != null) pose.DefinirCansada(false);
        estado = Estado.Reposicionar;
    }

    /// <summary>Comeca o gesto. O caldo NAO sai aqui: sai em SoltarProjetil(), no fim
    /// do armar. Com o voo mais rapido e o arco mais raso, o unico aviso que o jogador
    /// tem e' o braco subindo - se o projetil saisse junto com o inicio do gesto esse
    /// aviso nao existiria e a esquiva viraria sorte.</summary>
    private void Arremessar()
    {
        if (animator != null && !string.IsNullOrEmpty(paramArremessoTrigger)) animator.SetTrigger(paramArremessoTrigger);
        if (efeitoDeArremesso != null) efeitoDeArremesso.Play();
        if (pose != null) pose.Arremessar();

        soltarEm = Time.time + (pose != null ? pose.TempoDeArmar : 0.18f);
    }

    /// <summary>Chamado uma vez, quando o relogio do armar vence.</summary>
    private void SoltarProjetil()
    {
        soltarEm = -1f;
        if (prefabProjetil == null || jogador == null) return;

        var origem = pontoDeArremesso != null ? pontoDeArremesso.position : transform.position + Vector3.up * 1.2f;
        Vector3 mira = PreverPosicaoDoJogador(origem);

        float distancia = Vector3.Distance(origem, mira);

        // TEMPO VINDO DA DISTANCIA: o braco tem uma forca so', entao o que muda com a
        // distancia e' quanto o caldo demora - nao a velocidade dele.
        float t = Mathf.Min(distancia / Mathf.Max(1f, velocidadeDeLancamento), tempoDeVoo);

        // Arco cresce com a distancia: arremesso longo sobe mais, que e' o que deixa a
        // trajetoria legivel de longe em vez de vir quase reto.
        float arco = alturaDoArco + distancia * arcoPorMetro;

        var obj = Instantiate(prefabProjetil, origem, Quaternion.identity);
        var proj = obj.GetComponent<ProjetilArremessado>();
        if (proj != null) proj.Lancar(jogador, mira, arco, t, danoDoArremesso);

        // O corpo entra no lancamento (ver avancoNoArremesso).
        avancoAte = Time.time + duracaoDoAvanco;
    }

    /// <summary>Onde o jogador provavelmente vai estar quando o caldo chegar.
    ///
    /// POR QUE PREVER
    /// Mirar na posicao ATUAL com meio segundo de voo significa que qualquer jogador
    /// que continue andando nunca e' atingido - a luta inteira vira "ande e ignore".
    /// Prever devolve o peso do ataque.
    ///
    /// POR QUE NAO PREVER 100%
    /// Antecipacao total acerta sempre, e ataque inevitavel nao e' dificuldade, e'
    /// imposto. Com antecipacao parcial ela acerta quem corre em LINHA RETA e erra
    /// quem muda de direcao - a decisao passa a ser do jogador.
    ///
    /// Duas passadas: a primeira estima o tempo ate' a posicao atual, a segunda
    /// corrige usando a posicao ja' prevista. Sem a segunda, alvo rapido e longe fica
    /// sistematicamente subestimado.</summary>
    private Vector3 PreverPosicaoDoJogador(Vector3 origem)
    {
        Vector3 atual = jogador.position;
        Vector3 estimada = atual;

        for (int i = 0; i < 2; i++)
        {
            float t = Mathf.Min(Vector3.Distance(origem, estimada) / Mathf.Max(1f, velocidadeDeLancamento),
                                tempoDeVoo);
            estimada = atual + velocidadeDoJogador * t * antecipacao;
        }

        return estimada;
    }

    /// <summary>Velocidade do jogador, suavizada. Suavizar importa: a leitura crua de
    /// um frame oscila com o passo e a colisao, e miraria em saltos que nao existem no
    /// movimento real dele.</summary>
    private void MedirVelocidadeDoJogador()
    {
        if (jogador == null) return;

        if (posAnteriorDoJogador != Vector3.zero && Time.deltaTime > 0.0001f)
        {
            Vector3 crua = (jogador.position - posAnteriorDoJogador) / Time.deltaTime;
            velocidadeDoJogador = Vector3.Lerp(velocidadeDoJogador, crua, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        posAnteriorDoJogador = jogador.position;
    }

    /// <summary>O passo a frente no lancamento. Entra pelo CharacterController como
    /// todo o resto do movimento dela, entao respeita parede e chao.</summary>
    private void AplicarAvancoDoArremesso()
    {
        if (Time.time >= avancoAte) return;
        controller.Move(new Vector3(facing * avancoNoArremesso, 0f, 0f) * Time.deltaTime);
    }

    private void Encarar(float alvo)
    {
        if (alvo == 0f || alvo == facing || visual == null) return;
        facing = alvo;
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, facing * Mathf.Abs(s.z));
    }

    private void VerificarFase2()
    {
        if (fase2Ativa || vida.MaxHealth <= 0f) return;
        if (vida.Health / vida.MaxHealth <= vidaFase2) fase2Ativa = true;
    }

    private void VerificarMorte()
    {
        if (vida.Health > 0f) return;
        estado = Estado.Morta;
        EsconderBarra();
    }

    /// <summary>Some com a barra de chefe. Chamado na morte e no OnDestroy - duas
    /// vezes de proposito, porque o objeto pode sumir sem passar por VerificarMorte
    /// (o proprio EnemyHealth chama Destroy ao morrer) e a barra nao pode ficar
    /// pendurada na tela sem dono.</summary>
    private void EsconderBarra()
    {
        if (HudBarraDoChefe.Instancia != null) HudBarraDoChefe.Instancia.Esconder();
    }

    private void SetAnimBool(string nome, bool valor)
    {
        if (animator == null || string.IsNullOrEmpty(nome)) return;
        animator.SetBool(nome, valor);
    }

    private void OnDestroy()
    {
        EsconderBarra();
        // Inscricao em evento C# nao morre com o componente desativado (armadilha ja'
        // paga neste projeto) - cancelar aqui.
        if (pose != null) pose.AoExalar -= Bufar;
    }
}
