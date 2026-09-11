using UnityEngine;

/// <summary>
/// Faz o chefe APARECER e a luta comecar quando o jogador passa na frente deste
/// ponto. Coloque um destes na frente de cada fornalha.
///
/// POR QUE DISTANCIA EM X E NAO UM TRIGGER DE COLLIDER
/// A versao anterior usava BoxCollider isTrigger e nunca disparava. Motivo medido:
/// a caixa ficava centrada no centro visual da fornalha (encostada na parede,
/// Z ~2,3) e o jogador anda em Z=0 - o bounds real comecava em Z=0,53, meio metro
/// atras do plano onde o jogador de fato passa. Ele atravessava a sala inteira por
/// fora da caixa, sem erro nenhum no console.
///
/// Este jogo e' 2.5D: o jogador so' anda em X (PlayerMovement2_5D so' escreve X e
/// Y; o passo lateral em Z e' de centimetros). Entao "passei na frente do fogao" e'
/// literalmente uma distancia em X - comparar isso e' exato, nao depende de
/// tamanho de caixa, de camada de fisica nem de Rigidbody, e nao tem como falhar
/// em silencio.
///
/// A altura ainda e' checada (alturaMaxima) so' pra nao disparar com o jogador num
/// andar completamente diferente, ja' que salas deste castelo se empilham em Y.
/// </summary>
public class SpawnDoChefe : MonoBehaviour
{
    [Header("Quem nasce")]
    [Tooltip("Raiz do chefe, DESATIVADA na cena. Este script liga ela aqui neste " +
             "ponto quando o jogador chega. Pode ser a mesma referencia em varios " +
             "spawners - o primeiro que disparar vence, os outros ficam inertes.")]
    [SerializeField] private GameObject chefe;
    [SerializeField] private string nomeParaBarra = "A Provedora";

    [Header("Quando dispara")]
    [Tooltip("Distancia em X entre o jogador e este ponto pra comecar a luta.")]
    [SerializeField] private float distanciaEmX = 4f;
    [Tooltip("Diferenca maxima de altura entre o jogador e este ponto. Impede " +
             "disparar com o jogador num andar de cima/baixo.")]
    [SerializeField] private float alturaMaxima = 3f;

    [Header("Onde ele nasce")]
    [Tooltip("Z em que o chefe e' posto (o jogador anda em Z=0; a fornalha fica " +
             "encostada na parede, entao nascer no Z dela deixaria ele dentro do movel).")]
    [SerializeField] private float zDeChegada = 0.6f;
    [Tooltip("X do meio da sala (a cozinha vai de 49 a 100, entao 74,5). Usado so' " +
             "pra saber PARA QUE LADO afastar do fogao.")]
    [SerializeField] private float centroDaSala = 74.5f;
    [Tooltip("Quantos metros ela nasce afastada deste ponto, na direcao do centro da " +
             "sala. Nascendo colada no fogao ela fica entalada no canto/alcova e a " +
             "luta inteira acontece espremida contra a parede; afastando, ela cai em " +
             "chao aberto e o espaco de desvio existe dos dois lados.")]
    [SerializeField] private float afastamentoDoFogao = 5f;
    [SerializeField] private LayerMask mascaraDeChao = ~0;

    private Transform jogador;
    private bool jaDisparou;

    private void Update()
    {
        if (jaDisparou || chefe == null) return;

        if (jogador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            jogador = go.transform;
        }

        if (Mathf.Abs(jogador.position.y - transform.position.y) > alturaMaxima) return;
        if (Mathf.Abs(jogador.position.x - transform.position.x) > distanciaEmX) return;

        Disparar();
    }

    private void Disparar()
    {
        jaDisparou = true;

        // Posiciona no chao SONDADO deste ponto - nunca numa altura chutada.
        float direcaoDoCentro = Mathf.Sign(centroDaSala - transform.position.x);
        float xDestino = transform.position.x + direcaoDoCentro * afastamentoDoFogao;

        Vector3 destino = new Vector3(xDestino, transform.position.y, zDeChegada);
        if (Physics.Raycast(new Vector3(xDestino, transform.position.y + 5f, zDeChegada),
                            Vector3.down, out var hit, 20f, mascaraDeChao, QueryTriggerInteraction.Ignore))
            destino.y = hit.point.y;

        var controller = chefe.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;   // escrever position com ele ligado nao funciona
        chefe.transform.position = destino;
        chefe.SetActive(true);
        if (controller != null) controller.enabled = true;

        var vida = chefe.GetComponent<EnemyHealth>();
        if (vida != null && HudBarraDoChefe.Instancia != null)
            HudBarraDoChefe.Instancia.Mostrar(vida, nomeParaBarra);

        chefe.GetComponent<ChefeCozinha_Provedora>()?.IniciarLuta();

        Debug.Log($"[SpawnDoChefe] '{name}' disparou: chefe em {destino}", this);
    }

    /// <summary>Faixa de disparo desenhada no editor - da' pra ver e ajustar sem
    /// adivinhar (era exatamente o que faltava na versao com BoxCollider: a caixa
    /// parecia certa no Inspector e estava meio metro fora do plano do jogador).</summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.85f);
        Vector3 a = new Vector3(transform.position.x - distanciaEmX, transform.position.y, 0f);
        Vector3 b = new Vector3(transform.position.x + distanciaEmX, transform.position.y, 0f);
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(a, a + Vector3.up * 2f);
        Gizmos.DrawLine(b, b + Vector3.up * 2f);
        float dir = Mathf.Sign(centroDaSala - transform.position.x);
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(new Vector3(transform.position.x + dir * afastamentoDoFogao,
                                          transform.position.y + 1f, zDeChegada), 0.4f);
    }
}
