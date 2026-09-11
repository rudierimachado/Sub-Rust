using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera de terceira pessoa (Cinemachine 3) orbitando o jogador com o mouse.
///
/// COMO FUNCIONA
/// A ThirdPersonFollow posiciona a camera atras do ombro usando a ROTACAO do alvo
/// (NucleoCamera). Entao o mouse gira o NucleoCamera (yaw/pitch) e a camera orbita em
/// volta do jogador. O corpo gira sozinho pra direcao em que anda (PlayerMovement2_5D),
/// independente da camera - e' essa separacao que faz parecer jogo de terceira pessoa.
///
/// O QUE ERA ANTES
/// O mouse mexia um PanTilt, que e' estagio de MIRA: a camera girava parada no lugar em
/// vez de dar a volta no jogador. E o pivo copiava o yaw do corpo, entao a camera ficava
/// presa nas costas dele e girava junto a cada virada. O PanTilt e o RotationComposer
/// ficam no objeto, desligados.
///
/// Os campos foram RENOMEADOS de proposito quando os valores mudaram: a cena guardava os
/// numeros do enquadramento lateral antigo (distancia 5,8, ombro a 1,52 m) e eles
/// sobrescreveriam os padroes novos.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachineThirdPersonFollow))]
public class CameraJogador : MonoBehaviour
{
    [Header("Corpo (ombro)")]
    [Tooltip("Metros atras do ombro. 3-4 e' a faixa dos jogos de acao em terceira pessoa; " +
             "mais que isso o personagem vira um boneco pequeno no meio da tela.")]
    [SerializeField] private float distanciaDaCamera = 3.6f;
    [Tooltip("Deslocamento do ombro a partir do NucleoCamera (que ja' fica no peito). X " +
             "positivo tira o personagem do centro e abre a visao a frente.")]
    [SerializeField] private Vector3 deslocamentoOmbro = new Vector3(0.5f, 0.15f, 0f);
    [SerializeField] private float alturaDoBraco = 0.3f;
    [Tooltip("Atraso da camera atras do corpo (x lado, y altura, z distancia). Baixo em X " +
             "pro personagem nao escorregar pro lado da tela; um pouco em Y amacia o pulo.")]
    [SerializeField] private Vector3 amortecimentoCorpo = new Vector3(0.1f, 0.3f, 0.2f);

    [Header("Orbita (mouse)")]
    [Tooltip("Graus por pixel de mouse.")]
    [SerializeField] private float sensibilidadeMouse = 0.12f;
    [SerializeField] private float tiltInicial = 12f;
    [Tooltip("Limite vertical em graus. Negativo olha de baixo pra cima, positivo de cima.")]
    [SerializeField] private Vector2 limiteVertical = new Vector2(-30f, 65f);
    [SerializeField] private bool inverterY;
    [Tooltip("Trava e esconde o cursor durante o jogo. Solta sozinho com o jogo pausado " +
             "(timeScale 0) e enquanto Alt estiver segurado.")]
    [SerializeField] private bool travarCursor = true;

    [Header("Lente")]
    [SerializeField] private float campoDeVisao = 52f;
    [SerializeField] private float nearClip = 0.18f;
    [SerializeField] private float farClip = 2800f;

    [Header("Deoccluder")]
    [SerializeField] private float raioColisao = 0.22f;
    [SerializeField] private float amortecimentoParaColisao = 0.4f;
    [SerializeField] private float amortecimentoSaidaColisao = 0.8f;

    private CinemachineCamera cam;
    private NucleoCameraJogador nucleo;

    private void Awake()
    {
        cam = GetComponent<CinemachineCamera>();
        cam.Priority = 10;
        cam.Lens.FieldOfView = campoDeVisao;
        cam.Lens.NearClipPlane = nearClip;
        cam.Lens.FarClipPlane = farClip;
        cam.Lens.ModeOverride = LensSettings.OverrideModes.Perspective;

        var corpo = GetComponent<CinemachineThirdPersonFollow>();
        corpo.CameraDistance = distanciaDaCamera;
        corpo.ShoulderOffset = deslocamentoOmbro;
        corpo.VerticalArmLength = alturaDoBraco;
        corpo.CameraSide = 1f;
        corpo.Damping = amortecimentoCorpo;
        corpo.AvoidObstacles.Enabled = true;
        corpo.AvoidObstacles.CameraRadius = raioColisao;
        // O Player (e as copias de rede) tem colisor na layer Default: sem ignorar a tag,
        // a camera tratava o PROPRIO jogador como parede e ficava colada nas costas dele.
        corpo.AvoidObstacles.IgnoreTag = "Player";
        // Entrar na parede RAPIDO (senao a camera atravessa a parede antes de chegar) e
        // sair devagar (senao ela da' um tranco pra tras quando o obstaculo passa).
        corpo.AvoidObstacles.DampingIntoCollision = amortecimentoParaColisao;
        corpo.AvoidObstacles.DampingFromCollision = amortecimentoSaidaColisao;

        // Sem estagio de mira: a ThirdPersonFollow ja' da' a rotacao certa (a do alvo).
        var panTilt = GetComponent<CinemachinePanTilt>();
        if (panTilt != null) panTilt.enabled = false;
        var composer = GetComponent<CinemachineRotationComposer>();
        if (composer != null) composer.enabled = false;
    }

    private void Update()
    {
        AtualizarCursor();

        if (cam.Follow == null) { nucleo = null; return; }

        // O alvo so' e' ligado quando o Player nasce (PlayerNetwork.LigarCameraNisto), e
        // pode trocar (reconexao) - por isso e' resolvido aqui e nao no Awake.
        if (nucleo == null || nucleo.transform != cam.Follow)
        {
            nucleo = cam.Follow.GetComponent<NucleoCameraJogador>();
            if (nucleo == null) return;
            nucleo.ComecarOrbita(tiltInicial);
        }

        // Cursor solto (menu, pausa, Alt): o mouse e' do jogador, nao da camera.
        if (travarCursor && Cursor.lockState != CursorLockMode.Locked) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        var delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude < 0.0001f) return;

        nucleo.Yaw += delta.x * sensibilidadeMouse;
        float dy = inverterY ? delta.y : -delta.y;
        nucleo.Pitch = Mathf.Clamp(nucleo.Pitch + dy * sensibilidadeMouse,
                                   limiteVertical.x, limiteVertical.y);
    }

    private void AtualizarCursor()
    {
        if (!travarCursor) return;

        bool pausado = Time.timeScale < 0.01f;
        bool alt = Keyboard.current != null && Keyboard.current.leftAltKey.isPressed;
        bool travar = !pausado && !alt && Application.isFocused;

        var modo = travar ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState == modo) return;
        Cursor.lockState = modo;
        Cursor.visible = !travar;
    }

    private void OnDisable()
    {
        // Sair da fase (menu principal, troca de cena) nao pode deixar o cursor preso.
        if (!travarCursor) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
