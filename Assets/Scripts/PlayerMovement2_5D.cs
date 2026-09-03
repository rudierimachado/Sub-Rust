using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento lateral 2.5D (side-scroller).
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

    private CharacterController controller;
    private Transform visual;
    private Animator animator;

    private float speed;
    private float verticalVelocity;
    private float facing = 1f;

    /// <summary>+1 olhando para +X, -1 para -X. Quem atira precisa disso: o transform
    /// nao muda de forward quando o Visual espelha, so' o sinal da escala.</summary>
    public float Facing => facing;

    /// <summary>Velocidade vertical atual (negativa caindo). Quem calcula dano de
    /// queda le' isso ANTES do Move do frame em que toca o chao.</summary>
    public float VerticalVelocity => verticalVelocity;

    /// <summary>No chao neste frame. Espelha o isGrounded do CharacterController.</summary>
    public bool NoChao => controller != null && controller.isGrounded;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int JumpParam = Animator.StringToHash("Jump");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        visual = transform.Find("Visual");
        animator = visual != null ? visual.GetComponent<Animator>() : null;

        // A raiz fica travada olhando para +X; a virada e' toda no Visual.
        transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        AplicarFacing();
    }

    private void Update()
    {
        float input = LerInput();
        bool correndo = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            verticalVelocity = jumpSpeed;
            animator?.SetTrigger(JumpParam);
        }

        // Vira no mesmo frame em que a tecla e' lida, antes de mover.
        if (input > 0.01f && facing < 0f) { facing = 1f; AplicarFacing(); }
        else if (input < -0.01f && facing > 0f) { facing = -1f; AplicarFacing(); }

        float alvo = input * (correndo ? runSpeed : walkSpeed);
        float taxa = Mathf.Abs(alvo) > Mathf.Abs(speed) ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, alvo, taxa * Time.deltaTime);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;

        // Separacao contra outros personagens fica no SeparacaoDeCorpos (penetracao real).
        controller.Move((Vector3.right * speed + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (animator != null)
        {
            float velocidade = Mathf.Abs(speed);
            animator.SetFloat(SpeedParam, velocidade);
            // Mantém o Idle em loop quando parado; o Blend Tree seleciona Speed = 0.
            animator.speed = 1f;
        }
    }

    private void AplicarFacing()
    {
        if (visual == null) return;
        // Espelha no Z LOCAL, nao no X: a raiz esta rotacionada 90 em Y, entao o
        // eixo local que aponta para o X do mundo (onde o personagem anda, e o unico
        // que a camera lateral enxerga como "virar") e' o forward/Z, nao o right/X.
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, facing * Mathf.Abs(s.z));
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
}
