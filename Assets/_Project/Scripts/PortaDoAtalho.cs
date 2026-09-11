using UnityEngine;

/// <summary>
/// Porta do atalho capela -> sacristia. Nasce trancada (colisor fechando o vao) e
/// abre girando na dobradica quando a chave e' usada.
///
/// Abre GIRANDO e nao sumindo: o jogador precisa ver que aquilo era uma porta e que
/// agora ficou aberta - e' o que transforma o caminho num atalho, em vez de uma
/// parede que evaporou.
/// </summary>
public class PortaDoAtalho : MonoBehaviour
{
    [Header("Giro")]
    [Tooltip("Angulo final da folha em relacao ao fechado. Negativo abre pro outro lado.")]
    [SerializeField] private float anguloAberto = -105f;
    [SerializeField] private float velocidadeDeAbertura = 90f;

    [Header("Colisao")]
    [Tooltip("Colisor que fecha o vao enquanto trancada. Desligado ao abrir.")]
    [SerializeField] private Collider bloqueio;

    [Header("Interacao")]
    [SerializeField] private float raioDeAbertura = 1.35f;


    public bool Destrancada { get; private set; }

    private Quaternion fechada;
    private Quaternion aberta;
    private Transform jogador;
    private bool chaveRecebida;


    private void Awake()
    {
        fechada = transform.localRotation;
        aberta = fechada * Quaternion.Euler(0f, anguloAberto, 0f);
    }

    /// <summary>Chamado pela chave; a porta espera o jogador chegar ao vao para abrir.</summary>
public void ReceberChave(Transform coletor)
    {
        chaveRecebida = true;
        jogador = coletor;
    }

    public void Destrancar()
    {
        if (Destrancada) return;
        Destrancada = true;
        if (bloqueio != null) bloqueio.enabled = false;
        Debug.Log("[PortaDoAtalho] atalho para a sacristia liberado.", this);
    }

private void Update()
    {
        if (!Destrancada)
        {
            if (chaveRecebida && jogador != null &&
                Vector3.Distance(jogador.position, transform.position) <= raioDeAbertura)
                Destrancar();

            return;
        }

        if (Quaternion.Angle(transform.localRotation, aberta) < 0.05f)
        {
            transform.localRotation = aberta;
            enabled = false;
            return;
        }

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation, aberta, velocidadeDeAbertura * Time.deltaTime);
    }
}
