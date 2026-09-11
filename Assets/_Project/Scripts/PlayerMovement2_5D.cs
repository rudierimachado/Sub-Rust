using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento do jogador: TERCEIRA PESSOA (padrao) ou lateral 2.5D (side-scroller).
///
/// Terceira pessoa: WASD relativo a camera, o corpo GIRA de verdade (rotacao do Visual,
/// sem espelhar) pra onde o jogador quer ir, e quem mira/golpeia le' <see cref="Direcao"/>.
/// No 2.5D a Direcao vale +X ou -X, entao os mesmos consumidores servem pros dois modos.
///
/// Hierarquia esperada:
///   Player   -> CharacterController + este script   (NUNCA e' espelhado)
///     Visual -> Animator                            (espelha em Z local para virar)
///       Armature -> ossos + malha + espadas
///
/// Por que o Visual existe: o SkinnedMeshRenderer desenha a malha a partir das
/// POSICOES DOS OSSOS, ignorando o localScale do proprio renderer. Entao espelhar
/// a Armature nao virava o corpo (so' as espadas). Espelhar um pai acima dos ossos
/// funciona. E o CharacterController fica de fora do espelhamento porque ele nao
/// lida bem com escala negativa.
///
/// O eixo espelhado e' o Z LOCAL (nao o X): a raiz esta rotacionada 90 graus em Y,
/// entao quem aponta para o X do mundo - o eixo do movimento, e o unico que a camera
/// lateral le como "virar" - e' o forward local.
///
/// Velocidades: MEDIDAS nos clipes pelo recuo do pe apoiado (metodo correto para
/// clipe in-place, cujo averageSpeed e' zero). Walking = 1.57 m/s, Running = 5.20 m/s.
/// Andar mais rapido que isso faz o pe deslizar no chao.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement2_5D : MonoBehaviour
{
    [Header("Velocidade (medida dos clipes - nao chutar)")]
    [SerializeField] private float walkSpeed = 1.57f;
    [SerializeField] private float runSpeed = 5.20f;

    [Header("Resposta")]
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float deceleration = 24f;

    [Header("Gravidade")]
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Pulo")]
    [SerializeField] private float jumpSpeed = 7f;

    [Header("Terceira pessoa")]
    [Tooltip("WASD relativo a CAMERA e o corpo gira pra onde anda. Desligado volta ao " +
             "side-scroller: A/D no X do mundo, W/S na faixa de profundidade.")]
    [SerializeField] private bool terceiraPessoa = true;
    [Tooltip("Mantem a faixa de Z (profundidadeMinima/Maxima) tambem em terceira pessoa. " +
             "Existe porque as salas ainda nao tem parede do lado da camera: sem o limite " +
             "o jogador anda pra fora do piso. Desligar quando as paredes existirem.")]
    [SerializeField] private bool limitarFaixaZ = true;

    [Header("Profundidade (W/S)")]
    [Tooltip("Fracao da velocidade de X usada ao andar em profundidade. 1 = anda pra " +
             "frente/tras tao rapido quanto pro lado. Um pouco abaixo de 1 mantem o " +
             "side-scroller legivel sem parecer que o personagem esta' preso.\n\n" +
             "E' FRACAO e nao velocidade fixa de proposito: assim andar em Z respeita " +
             "andar x correr igual ao X. Com um numero fixo, correr pro lado ia a 5,20 " +
             "e correr pra frente ia a 3,20 - o corpo mudava de peso conforme a " +
             "direcao, que e' parte do que lia como 'empurrado'.")]
    [Range(0.3f, 1f)] [SerializeField] private float profundidadeFracao = 0.85f;
    [Tooltip("O personagem GIRA pra direcao em que anda, inclusive em profundidade. " +
             "Sem isto o corpo so' tem dois estados (olhando +X ou -X) porque a virada " +
             "e' um espelhamento de escala, entao andar pra frente/tras deixava ele " +
             "andando de lado. Desligue pra voltar ao side-scroller estrito.")]
    [SerializeField] private bool girarNaDirecaoDoAndar = true;
    [Tooltip("Graus por segundo do giro. Alto demais e o corpo estala de direcao; " +
             "baixo demais ele desliza pro lado antes de encarar.")]
    [SerializeField] private float velocidadeDoGiro = 720f;
    [Tooltip("Faixa de Z em que o jogador pode andar. MEDIDA com o corpo dele (raio " +
             "0,35) varrendo a sala em 5 pontos: em Z 1,95 ele ainda cabe e em 2,00 " +
             "ja' encosta na parede do fundo em TODA a extensao; o piso termina em " +
             "Z -4,00. Os limites ficam aquem dos dois.\n\n" +
             "A camera acompanha: o CinemachineThirdPersonFollow da CameraJogador " +
             "mantem distancia 14 do alvo com damping 0,5 em Z, entao recuar nao joga " +
             "o jogador pra fora de quadro.\n\n" +
             "O que ISTO custou: EnemyAI media so' X (DistanciaX), entao com faixa " +
             "grande o inimigo acertava de 3 m de distancia em profundidade e nunca " +
             "seguia quem recuava. Corrigido la' - ver alcanceEmZ.")]
    [SerializeField] private float profundidadeMinima = -3.4f;
    [SerializeField] private float profundidadeMaxima = 1.8f;

    [Header("Audio - Passos")]
    [SerializeField] private AudioClip[] passosAndando;
    [SerializeField] private AudioClip[] passosCorrendo;
    [SerializeField] private AudioClip[] sonsPulo;
    [SerializeField] private AudioClip[] sonsAterrissagem;
    [SerializeField, Range(0f, 1f)] private float volumePassos = 0.42f;
    [SerializeField, Range(0f, 1f)] private float volumePulo = 0.52f;
    [SerializeField, Range(0f, 1f)] private float volumeAterrissagem = 0.58f;
    [SerializeField] private float distanciaPorPassoAndando = 0.82f;
    [SerializeField] private float distanciaPorPassoCorrendo = 1.18f;
    [SerializeField, Range(0.85f, 1.15f)] private float variacaoPitchPasso = 0.07f;

    private CharacterController controller;
    private Transform visual;
    private Animator animator;
    private AudioSource passosAudio;

    private float speed;
    private float verticalVelocity;
    private float facing = 1f;
    private float distanciaPassos;
    private bool estavaNoChao;

    private Vector3 velocidadePlana;              // terceira pessoa: velocidade no chao (XZ)
    private Vector3 direcao = Vector3.right;      // pra onde o corpo olha, no plano
    private Quaternion rotacaoVisualRelativa = Quaternion.identity;

    /// <summary>+1 olhando para +X, -1 para -X. Quem atira precisa disso: o transform
    /// nao muda de forward quando o Visual espelha, so' o sinal da escala.
    /// Em terceira pessoa e' so' o lado (sinal de X) da Direcao - prefira Direcao.</summary>
    public float Facing => terceiraPessoa ? (direcao.x >= 0f ? 1f : -1f) : facing;

    public bool TerceiraPessoa => terceiraPessoa;

    /// <summary>Pra onde o corpo olha, no plano (unitario). No 2.5D e' +X ou -X.
    /// Golpe, lamina, escudo, tiro e esquiva leem ISTO.</summary>
    public Vector3 Direcao => direcao;

    /// <summary>Frente usada pra escolher alvo. Em terceira pessoa mistura o corpo com a
    /// camera, entao o foco cai em quem o jogador esta' OLHANDO na tela.</summary>
    public Vector3 FrenteDaMira
    {
        get
        {
            if (!terceiraPessoa) return direcao;
            Vector3 f = direcao + FrenteDaCamera();
            return f.sqrMagnitude > 0.01f ? f.normalized : direcao;
        }
    }

    /// <summary>Vira o corpo pra uma direcao do plano, na hora. No 2.5D so' o lado (X)
    /// conta. Funciona com o script desligado (esquiva, finalizacao, copia de rede).</summary>
    public void Encarar(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        if (!terceiraPessoa) { Virar(dir.x); return; }
        direcao = dir.normalized;
        AplicarRotacao();
    }

    /// <summary>Vira o personagem para um lado (+1 ou -1) sem passar pelo input.
    /// Usado pela mira automatica do combate: ao golpear, o jogador encara o inimigo
    /// focado em vez de acertar o vazio quando ele passou pro outro lado. Ignora
    /// valor 0 e nao faz nada se ja' estiver virado pra la'.</summary>
    public void Virar(float lado)
    {
        if (lado == 0f) return;
        if (terceiraPessoa) { Encarar(Vector3.right * lado); return; }
        float novo = Mathf.Sign(lado);
        if (Mathf.Approximately(novo, facing)) return;
        facing = novo;
        AplicarFacing();
    }

    /// <summary>Velocidade vertical atual (negativa caindo). Quem calcula dano de
    /// queda le' isso ANTES do Move do frame em que toca o chao.</summary>
    public float VerticalVelocity => verticalVelocity;

    /// <summary>No chao neste frame. Espelha o isGrounded do CharacterController.</summary>
    public bool NoChao => controller != null && controller.isGrounded;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private float speedZ;
    private static readonly int JumpParam = Animator.StringToHash("Jump");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        visual = transform.Find("Visual");
        animator = visual != null ? visual.GetComponent<Animator>() : null;
        passosAudio = GetComponent<AudioSource>();
        if (passosAudio == null) passosAudio = gameObject.AddComponent<AudioSource>();
        passosAudio.playOnAwake = false;
        passosAudio.spatialBlend = 0.45f;
        passosAudio.rolloffMode = AudioRolloffMode.Linear;
        passosAudio.minDistance = 2f;
        passosAudio.maxDistance = 18f;

        // A raiz fica travada olhando para +X; a virada e' toda no Visual.
        transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        // Terceira pessoa: guarda a pose do Visual relativa a raiz olhando +X. Dali em
        // diante a rotacao do Visual e' sempre LookRotation(direcao) * essa pose - nao
        // depende da raiz, entao o spawn girar a raiz nao entorta o corpo.
        if (visual != null) rotacaoVisualRelativa = Quaternion.Inverse(transform.rotation) * visual.rotation;
        AplicarFacing();
        estavaNoChao = controller.isGrounded;
    }

    /// <summary>Side-scroller: A/D no X do mundo, W/S na faixa de profundidade.
    /// Devolve se esta' correndo.</summary>
    private bool MoverLateral()
    {
        float input = LerInput();

        // Corrida e' locomocao livre. Stamina fica reservada para acoes de combate.
        // Correr vale pra QUALQUER direcao, nao so' pros lados. Antes exigia input em
        // X, entao Shift+W andava devagar e Shift+D corria: o personagem trocava de
        // velocidade conforme a direcao, que le' como controle preso.
        bool correndo = Keyboard.current != null
                     && Keyboard.current.leftShiftKey.isPressed
                     && (Mathf.Abs(input) > 0.01f || HaInputDeProfundidade());

        TentarPular();

        // Vira no mesmo frame em que a tecla e' lida, antes de mover.
        if (input > 0.01f && facing < 0f) { facing = 1f; AplicarFacing(); }
        else if (input < -0.01f && facing > 0f) { facing = -1f; AplicarFacing(); }

        float alvo = input * (correndo ? runSpeed : walkSpeed);
        float taxa = Mathf.Abs(alvo) > Mathf.Abs(speed) ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, alvo, taxa * Time.deltaTime);

        AplicarGravidade();

        // Separacao contra outros personagens fica no SeparacaoDeCorpos (penetracao real).
        Vector3 deslocamentoXY = (Vector3.right * speed + Vector3.up * verticalVelocity) * Time.deltaTime;
        Vector3 deslocamentoZ = Vector3.forward * DeltaProfundidade(correndo);
        controller.Move(deslocamentoXY + deslocamentoZ);
        GirarParaOMovimento(speed, speedZ);
        return correndo;
    }

    private void Update()
    {
        bool correndo = terceiraPessoa ? MoverTerceiraPessoa() : MoverLateral();

        if (!estavaNoChao && controller.isGrounded)
            TocarSomAleatorio(sonsAterrissagem, volumeAterrissagem);
        estavaNoChao = controller.isGrounded;
        AtualizarPassos(correndo);

        if (animator != null)
        {
            // Velocidade no PLANO, nao so' em X. Sem isto andar em profundidade
            // deixava o Blend Tree em Speed=0: o personagem deslizava pelo chao em
            // pose de parado, que e' a metade visual do "empurrado".
            float velocidade = new Vector2(speed, speedZ).magnitude;
            animator.SetFloat(SpeedParam, velocidade);
            // Mantém o Idle em loop quando parado; o Blend Tree seleciona Speed = 0.
            animator.speed = 1f;
        }
    }

    /// <summary>Gira o VISUAL pra direcao em que o personagem esta' andando, somando
    /// X e Z. Existe porque o facing deste jogo e' um ESPELHAMENTO de escala (ver
    /// AplicarFacing), que so' sabe dois estados - +X e -X. Com o passo em
    /// profundidade valendo 5,2 m, andar pra frente/tras deixava o corpo andando de
    /// lado, como caranguejo.
    ///
    /// O giro fica no Visual, nao na raiz: a raiz e' o eixo que a camera e o combate
    /// assumem travado em +X (ver PlayerCombat.CentroDoGolpe, que le o Facing e nao a
    /// rotacao). Girar a raiz quebraria a caixa de acerto e a camera lateral.
    ///
    /// Quando esta' quase parado, mantem a ultima direcao - senao o corpo volta pro
    /// eixo sozinho toda vez que o jogador solta a tecla.</summary>
    private void GirarParaOMovimento(float velX, float velZ)
    {
        if (!girarNaDirecaoDoAndar || visual == null) return;

        Vector3 dir = new Vector3(velX, 0f, velZ);
        if (dir.sqrMagnitude < 0.04f) return;   // parado: nao mexe

        // O Visual carrega a escala espelhada do facing, e espelhar em Z ja' inverte
        // o forward. Entao o angulo tem que ser calculado NO ESPACO ESPELHADO: basta
        // inverter o componente Z da direcao quando facing e' -1.
        //
        // MEDIDO caso a caso: a formula anterior (180 - alvo) acertava os quatro casos
        // puros (direita, esquerda, frente, tras) e ERRAVA nas diagonais com facing=-1,
        // apontando o corpo pro lado oposto.
        float zEfetivo = facing < 0f ? -dir.z : dir.z;
        float alvoY = Mathf.Atan2(zEfetivo, dir.x * facing) * Mathf.Rad2Deg;

        var atual = visual.localEulerAngles;
        float novoY = Mathf.MoveTowardsAngle(atual.y, -alvoY, velocidadeDoGiro * Time.deltaTime);
        visual.localEulerAngles = new Vector3(atual.x, novoY, atual.z);
    }

    private void AplicarFacing()
    {
        direcao = Vector3.right * facing;
        if (visual == null) return;
        // Espelha no Z LOCAL, nao no X: a raiz esta rotacionada 90 em Y, entao o
        // eixo local que aponta para o X do mundo (onde o personagem anda, e o unico
        // que a camera lateral enxerga como "virar") e' o forward/Z, nao o right/X.
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, facing * Mathf.Abs(s.z));
    }

    /// <summary>Terceira pessoa: anda pra onde o WASD aponta EM RELACAO A CAMERA e gira o
    /// corpo pra la'. Devolve se esta' correndo.</summary>
    private bool MoverTerceiraPessoa()
    {
        Vector3 desejo = LerDirecaoDoInput();
        bool correndo = Keyboard.current != null
                     && Keyboard.current.leftShiftKey.isPressed
                     && desejo.sqrMagnitude > 0.0001f;

        TentarPular();

        // Mesma rampa de aceleracao/desaceleracao do side-scroller, so' que no plano.
        Vector3 alvo = desejo * (correndo ? runSpeed : walkSpeed);
        float taxa = alvo.sqrMagnitude > velocidadePlana.sqrMagnitude ? acceleration : deceleration;
        velocidadePlana = Vector3.MoveTowards(velocidadePlana, alvo, taxa * Time.deltaTime);

        AplicarGravidade();

        Vector3 passo = velocidadePlana * Time.deltaTime;
        if (limitarFaixaZ)
        {
            // Mesmo corte do DeltaProfundidade: zera o excesso e a velocidade em Z,
            // senao ela fica acumulada e o corpo dispara quando a direcao inverte.
            float z = transform.position.z;
            if (z + passo.z < profundidadeMinima) { passo.z = profundidadeMinima - z; velocidadePlana.z = 0f; }
            if (z + passo.z > profundidadeMaxima) { passo.z = profundidadeMaxima - z; velocidadePlana.z = 0f; }
        }
        controller.Move(passo + Vector3.up * verticalVelocity * Time.deltaTime);

        // Gira pra onde o jogador QUER ir (input), nao pra onde a inercia leva: soltar a
        // tecla nao gira o corpo de volta, e inverter vira na hora em vez de andar de
        // costas ate' a velocidade zerar. Por angulo e nao RotateTowards: com vetores
        // opostos o RotateTowards escolhe um eixo qualquer e pode inclinar o corpo.
        if (desejo.sqrMagnitude > 0.0001f)
        {
            float atual = Mathf.Atan2(direcao.x, direcao.z) * Mathf.Rad2Deg;
            float alvoY = Mathf.Atan2(desejo.x, desejo.z) * Mathf.Rad2Deg;
            float novo = Mathf.MoveTowardsAngle(atual, alvoY, velocidadeDoGiro * Time.deltaTime);
            direcao = Quaternion.Euler(0f, novo, 0f) * Vector3.forward;
        }
        AplicarRotacao();

        // speed/speedZ alimentam passos e Animator, que ja' leem a velocidade no plano.
        speed = velocidadePlana.x;
        speedZ = velocidadePlana.z;
        return correndo;
    }

    /// <summary>WASD convertido pra direcao do mundo pela camera (W = pra onde a camera
    /// olha). Zero sem tecla; magnitude ate' 1. Publico pra esquiva usar a mesma leitura.</summary>
    public Vector3 LerDirecaoDoInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector3.zero;

        float h = LerInput();
        float v = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        if (h == 0f && v == 0f) return Vector3.zero;

        Vector3 f = FrenteDaCamera();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3 d = f * v + r * h;
        return d.sqrMagnitude > 1f ? d.normalized : d;
    }

    private static Vector3 FrenteDaCamera()
    {
        var cam = Camera.main;
        Vector3 f = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.right;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.right;
    }

    private void AplicarRotacao()
    {
        if (visual == null) return;
        visual.rotation = Quaternion.LookRotation(direcao, Vector3.up) * rotacaoVisualRelativa;
    }

    private void TentarPular()
    {
        if (Keyboard.current == null || !Keyboard.current.spaceKey.wasPressedThisFrame || !controller.isGrounded) return;
        verticalVelocity = jumpSpeed;
        animator?.SetTrigger(JumpParam);
        TocarSomAleatorio(sonsPulo, volumePulo);
    }

    private void AplicarGravidade()
    {
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;
    }

    private float LerInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;

        float v = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v -= 1f;
        return v;
    }

    /// <summary>Deslocamento em Z deste frame, ja' cortado pra nao passar de
    /// [profundidadeMinima, profundidadeMaxima]. So' zera o excesso em vez de
    /// forcar transform.position (o CharacterController trata escrita direta de
    /// posicao como colisao enquanto ligado - ver PROJETO.md).
    ///
    /// W/S: PlayerClimb LE as mesmas teclas pra agarrar escada, mas so' quando o
    /// jogador esta' dentro do alcance dela (e desliga ESTE script ao escalar) -
    /// longe de escada as duas leituras nao colidem.</summary>
    /// <summary>W/S pressionado neste frame. Separado do DeltaProfundidade porque a
    /// decisao de "esta' correndo" acontece ANTES de mover, e precisa saber disso.</summary>
    private bool HaInputDeProfundidade()
    {
        var kb = Keyboard.current;
        if (kb == null) return false;
        return kb.wKey.isPressed || kb.upArrowKey.isPressed
            || kb.sKey.isPressed || kb.downArrowKey.isPressed;
    }

    private float DeltaProfundidade(bool correndo)
    {
        var kb = Keyboard.current;

        float v = 0f;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        }

        // MESMA rampa do eixo X. Antes isto era velocidade crua ligada/desligada: o
        // corpo saia de 0 pra 3,2 m/s num frame e voltava a 0 no frame em que a tecla
        // soltava, enquanto andar pro lado tinha aceleracao 18 e desaceleracao 24.
        // Era isso que lia como "empurrado" em vez de "andando" - nao a faixa.
        float alvo = v * (correndo ? runSpeed : walkSpeed) * profundidadeFracao;
        float taxa = Mathf.Abs(alvo) > Mathf.Abs(speedZ) ? acceleration : deceleration;
        speedZ = Mathf.MoveTowards(speedZ, alvo, taxa * Time.deltaTime);

        float delta = speedZ * Time.deltaTime;
        float zAtual = transform.position.z;

        // Bateu no limite: zera a velocidade tambem, senao ela fica "acumulada" e o
        // corpo dispara no instante em que a direcao inverte.
        if (zAtual + delta < profundidadeMinima) { delta = profundidadeMinima - zAtual; speedZ = 0f; }
        if (zAtual + delta > profundidadeMaxima) { delta = profundidadeMaxima - zAtual; speedZ = 0f; }
        return delta;
    }

    private void AtualizarPassos(bool correndo)
    {
        // Velocidade no PLANO: andar em profundidade tem que fazer barulho de passo
        // igual andar pro lado. So' com |speed| o personagem atravessava a sala em Z
        // em silencio absoluto - mais um pedaco do "nao parece andar".
        float velocidadeHorizontal = new Vector2(speed, speedZ).magnitude;
        if (!controller.isGrounded || velocidadeHorizontal < 0.12f)
        {
            distanciaPassos = 0f;
            return;
        }

        distanciaPassos += velocidadeHorizontal * Time.deltaTime;
        float distanciaAlvo = correndo ? distanciaPorPassoCorrendo : distanciaPorPassoAndando;
        if (distanciaPassos < distanciaAlvo) return;

        distanciaPassos = 0f;
        var clips = correndo && passosCorrendo != null && passosCorrendo.Length > 0 ? passosCorrendo : passosAndando;
        if (clips == null || clips.Length == 0 || passosAudio == null) return;

        passosAudio.pitch = Random.Range(1f - variacaoPitchPasso, 1f + variacaoPitchPasso);
        passosAudio.PlayOneShot(clips[Random.Range(0, clips.Length)], volumePassos);
    }

    private void TocarSomAleatorio(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0 || passosAudio == null) return;
        passosAudio.pitch = Random.Range(1f - variacaoPitchPasso, 1f + variacaoPitchPasso);
        passosAudio.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }
}
