using UnityEngine;

/// <summary>
/// Ponto de pivô da camera no peito do personagem. A ThirdPersonFollow da CameraJogador
/// posiciona a camera pela ROTACAO deste ponto - entao quem gira aqui decide a orbita.
///
/// Dois modos:
///  - ORBITA LIVRE (terceira pessoa): a rotacao vem do mouse (Yaw/Pitch, escritos pela
///    CameraJogador). A camera da' a volta no jogador e o corpo gira independente, pra
///    onde anda. Ligada pela CameraJogador via ComecarOrbita.
///  - SEGUIR O CORPO (2.5D antigo): copia o yaw do Visual pra camera ficar ATRAS de onde
///    o jogador olha - a raiz do Player fica travada em +X e nao gira com W/S.
///
/// Roda antes do CinemachineBrain (ordem -50): se rodasse depois, a camera leria a
/// rotacao do frame anterior e a orbita tremeria.
/// </summary>
[DefaultExecutionOrder(-50)]
public class NucleoCameraJogador : MonoBehaviour
{
    [SerializeField] private float altura = 1.45f;

    /// <summary>Graus em Y: pra onde a camera olha no horizontal.</summary>
    public float Yaw { get; set; }
    /// <summary>Graus em X: positivo olha de cima pra baixo.</summary>
    public float Pitch { get; set; }
    public bool OrbitaLivre { get; private set; }

    private Transform visual;
    private PlayerMovement2_5D movimento;

    private void Awake()
    {
        visual = transform.parent != null ? transform.parent.Find("Visual") : null;
        movimento = GetComponentInParent<PlayerMovement2_5D>();
    }

    /// <summary>Liga a orbita livre com a camera nascendo ATRAS do corpo - comecar
    /// olhando pra cara do personagem desorienta no primeiro frame.</summary>
    public void ComecarOrbita(float pitchInicial)
    {
        Vector3 f = movimento != null ? movimento.Direcao : transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;

        Yaw = Quaternion.LookRotation(f, Vector3.up).eulerAngles.y;
        Pitch = pitchInicial;
        OrbitaLivre = true;
    }

    private void LateUpdate()
    {
        if (transform.parent == null) return;

        transform.position = transform.parent.position + Vector3.up * altura;

        if (OrbitaLivre)
        {
            transform.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            return;
        }

        if (visual == null) return;

        var fwd = Vector3.ProjectOnPlane(visual.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f)
            fwd = Vector3.ProjectOnPlane(visual.right, Vector3.up);
        if (fwd.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }
}
