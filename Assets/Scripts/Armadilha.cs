using UnityEngine;

/// <summary>
/// Armadilha de area: tira uma FRACAO DA VIDA MAXIMA de quem entra.
///
/// Fracao, e nao valor fixo, porque o pedido de design e' "perder metade da vida" -
/// isso tem que continuar valendo quando a vida maxima mudar.
///
/// Usa o mesmo IDamageable do resto do combate, entao o feedback de dano ja
/// existente (PlayerDamageFeedback) reage sozinho.
///
/// O collider precisa estar como isTrigger. Como o jogador anda com
/// CharacterController, o OnTriggerEnter dispara normalmente.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Armadilha : MonoBehaviour
{
    [Tooltip("Fracao da vida MAXIMA levada por acionamento. 0.5 = metade.")]
    [Range(0f, 1f)]
    [SerializeField] private float fracaoDaVida = 0.5f;

    [Tooltip("Segundos ate' poder ferir o mesmo alvo de novo. Evita drenar a vida "
           + "num piscar quando o jogador para em cima.")]
    [SerializeField] private float intervalo = 1.5f;

    [Tooltip("Empurra o alvo de volta para cima ao acionar (0 = nao empurra).")]
    [SerializeField] private float impulsoVertical = 0f;

    private float proximoAcionamento;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other) => Tentar(other);
    private void OnTriggerStay(Collider other) => Tentar(other);

    private void Tentar(Collider other)
    {
        if (Time.time < proximoAcionamento) return;

        var alvo = other.GetComponentInParent<IDamageable>();
        if (alvo == null) return;

        float dano = fracaoDaVida * VidaMaximaDe(alvo);
        if (dano <= 0f) return;

        proximoAcionamento = Time.time + intervalo;
        alvo.TakeHit(dano, transform.position);

        if (impulsoVertical > 0f)
        {
            var cc = other.GetComponentInParent<CharacterController>();
            if (cc != null) cc.Move(Vector3.up * impulsoVertical);
        }
    }

    /// <summary>Vida maxima do alvo quando ele expoe uma; senao cai num padrao de 100.</summary>
    private static float VidaMaximaDe(IDamageable alvo)
    {
        if (alvo is PlayerHealth ph) return ph.MaxHealth;
        var comp = alvo as Component;
        if (comp != null)
        {
            var eh = comp.GetComponent<EnemyHealth>();
            if (eh != null) return eh.MaxHealth;
        }
        return 100f;
    }
}
