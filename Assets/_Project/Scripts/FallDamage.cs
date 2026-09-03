using UnityEngine;

/// <summary>
/// Dano de queda do jogador.
///
/// Mede a ALTURA REAL da queda (y do ponto mais alto durante a queda menos o y do
/// pouso), nao a velocidade no impacto: a velocidade depende da gravidade e do
/// frame em que o Move roda, e da' numero diferente conforme o framerate.
///
/// Calibragem medida no Poco do Incensario: os degraus da descida tem ~3,3 m de
/// queda entre plataformas, e a rota inteira e' feita descendo por eles. Entao
/// `alturaSegura` fica acima disso (4,5 m) - descer a rota normal NUNCA machuca.
/// O que machuca e' errar o pulo e cair o vao inteiro.
///
/// O dano usa PlayerHealth.TakeHit (mesma via do dano de inimigo), entao o
/// PlayerDamageFeedback ja reage sem precisar de nada novo.
/// </summary>
[RequireComponent(typeof(PlayerMovement2_5D))]
[RequireComponent(typeof(PlayerHealth))]
public class FallDamage : MonoBehaviour
{
    [Header("Limiares (metros)")]
    [Tooltip("Ate' esta altura nao ha' dano. Acima dos 3,3m dos degraus do poco.")]
    [SerializeField] private float alturaSegura = 4.5f;

    [Tooltip("Altura em que o dano chega ao maximo.")]
    [SerializeField] private float alturaLetal = 18f;

    [Header("Dano")]
    [Tooltip("Fracao da vida MAXIMA levada ao cair de alturaLetal ou mais.")]
    [Range(0f, 1f)]
    [SerializeField] private float fracaoDanoMaximo = 1f;

    private PlayerMovement2_5D movimento;
    private PlayerHealth vida;

    private bool caindo;
    private float yMaisAlto;

    private void Awake()
    {
        movimento = GetComponent<PlayerMovement2_5D>();
        vida = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        bool noChao = movimento.NoChao;

        if (!noChao)
        {
            // no ar: guarda o ponto mais alto para medir a queda toda
            if (!caindo)
            {
                caindo = true;
                yMaisAlto = transform.position.y;
            }
            else if (transform.position.y > yMaisAlto)
            {
                yMaisAlto = transform.position.y;
            }
            return;
        }

        if (!caindo) return;

        caindo = false;
        float altura = yMaisAlto - transform.position.y;
        AplicarDanoQueda(altura);
    }

    private void AplicarDanoQueda(float altura)
    {
        if (altura <= alturaSegura) return;

        float t = Mathf.InverseLerp(alturaSegura, alturaLetal, altura);
        float dano = vida.MaxHealth * fracaoDanoMaximo * t;
        if (dano <= 0f) return;

        vida.TakeHit(dano, transform.position);
    }

    /// <summary>Dano que uma queda desta altura causaria. Serve para calibrar
    /// armadilha e plataforma sem precisar cair para descobrir.
    /// Resolve o PlayerHealth na hora porque isto tambem e' chamado no EDITOR,
    /// onde o Awake nao rodou.</summary>
    public float PreverDano(float altura)
    {
        if (altura <= alturaSegura) return 0f;

        var ph = vida != null ? vida : GetComponent<PlayerHealth>();
        float vidaMax = ph != null ? ph.MaxHealth : 100f;

        return vidaMax * fracaoDanoMaximo
             * Mathf.InverseLerp(alturaSegura, alturaLetal, altura);
    }
}
