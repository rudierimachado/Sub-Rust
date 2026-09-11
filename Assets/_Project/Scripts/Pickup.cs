using UnityEngine;

/// <summary>
/// Item coletavel no chao (alma ou frasco). Precisa de um Collider marcado
/// como Trigger. Some ao tocar o jogador.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Pickup : MonoBehaviour
{
    public enum Tipo { Alma, Frasco }

    [SerializeField] private Tipo tipo = Tipo.Alma;
    [SerializeField] private int quantidade = 1;

    [Header("Coleta automatica")]
    [Tooltip("Distancia em que o item comeca a VOAR na direcao do jogador. Coletar " +
             "so' por encostar obriga a mira fina num item pequeno, ainda mais agora " +
             "que o jogador anda numa faixa de 5,2 m em profundidade - o item ficava " +
             "meio metro pro lado e nao dava pra pegar.")]
    [SerializeField] private float raioDeAtracao = 3.5f;
    [SerializeField] private float velocidadeDeAtracao = 9f;
    [Tooltip("Distancia em que ele e' de fato coletado.")]
    [SerializeField] private float raioDeColeta = 0.55f;

    private Transform jogador;
    private float giro;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        // Gira parado: item estatico no chao passa despercebido no meio do cenario.
        giro += Time.deltaTime * 90f;
        transform.rotation = Quaternion.Euler(0f, giro, 0f);

        if (jogador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            jogador = go.transform;
        }

        // Mira o peito, nao os pes: o item some dentro do corpo em vez de sumir no chao.
        Vector3 alvo = jogador.position + Vector3.up * 0.9f;
        float dist = Vector3.Distance(transform.position, alvo);

        if (dist <= raioDeColeta) { Coletar(); return; }
        if (dist > raioDeAtracao) return;

        // Acelera conforme chega perto - o item "pula" pra mao no fim, que le' melhor
        // que uma aproximacao de velocidade constante.
        float aceleracao = Mathf.Lerp(2.2f, 1f, dist / raioDeAtracao);
        transform.position = Vector3.MoveTowards(transform.position, alvo,
                                                 velocidadeDeAtracao * aceleracao * Time.deltaTime);
    }

    private void Coletar()
    {
        if (tipo == Tipo.Alma) PlayerCurrency.AddSouls(quantidade);
        else PlayerCurrency.AddPotions(quantidade);

        NumeroFlutuante.Mostrar(transform.position + Vector3.up * 0.5f,
                                tipo == Tipo.Alma ? "+" + quantidade : "+" + quantidade + " remedio",
                                tipo == Tipo.Alma ? new Color(0.35f, 1f, 0.75f) : new Color(1f, 0.45f, 0.45f),
                                tipo == Tipo.Alma ? 3.2f : 2.4f);
        Destroy(gameObject);
    }

    /// <summary>Mantido: se o jogador atravessar o item mais rapido que a atracao
    /// (correndo a 5,20 m/s), o trigger pega o que o Update deixaria passar.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Coletar();
    }
}
