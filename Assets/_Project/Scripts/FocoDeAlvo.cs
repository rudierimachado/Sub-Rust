using UnityEngine;

/// <summary>
/// Escolhe qual inimigo o jogador esta' encarando e expoe em <see cref="Alvo"/>.
///
/// POR QUE VIVE NO JOGADOR E NAO EM CADA INIMIGO
/// So' existe UM alvo focado por vez. Nao exige componente nenhum nos inimigos, entao
/// vale pra TODO inimigo do jogo - caveira, morcego, chefe - sem tocar em prefab nenhum.
/// Mesmo principio do LootService, que e' chamado de dentro do EnemyHealth em vez de
/// exigir um componente por inimigo.
///
/// COMO ESCOLHE
/// Varre por OverlapSphere a tag "Enemy" e pontua cada candidato por dois criterios:
/// quem esta' na FRENTE (o lado pra onde o jogador olha) e quem esta' PERTO. Um alvo
/// ja' focado ganha um bonus de histerese, senao o foco pula entre dois inimigos quase
/// empatados a cada frame.
///
/// Anexar na RAIZ do Player.
/// </summary>
public class FocoDeAlvo : MonoBehaviour
{
    [Header("Busca")]
    [Tooltip("Raio da varredura em volta do jogador.")]
    [SerializeField] private float alcance = 14f;
    [Tooltip("Peso de estar na frente do jogador. Quanto maior, mais ele ignora quem " +
             "esta' atras mesmo estando perto.")]
    [SerializeField] private float pesoDaFrente = 6f;
    [Tooltip("Vantagem que o alvo JA focado leva na disputa, em pontos. Sem isso o " +
             "foco pisca entre dois inimigos quase empatados.")]
    [SerializeField] private float histerese = 2.5f;
    [Tooltip("Alvo com vida zerada/destruido e' largado na hora; este e' o intervalo " +
             "da varredura completa. Nao precisa rodar todo frame - e' O(n) na sala.")]
    [SerializeField] private float intervaloDaBusca = 0.15f;

    /// <summary>O inimigo focado agora, ou null. Publico pra quem quiser mirar nele.</summary>
    public Transform Alvo { get; private set; }

    private PlayerMovement2_5D movimento;
    private float proximaBusca;
    // 128 e nao 32: OverlapSphereNonAlloc PARA de escrever quando o buffer enche, em
    // SILENCIO. Num raio de 14 m dentro do castelo, parede, chao e mobilia estouram 32
    // slots facil - e o inimigo simplesmente nunca aparecia na lista, entao o foco
    // (e tudo que depende dele) piscava sem motivo aparente.
    private readonly Collider[] buffer = new Collider[128];

    private void Awake()
    {
        movimento = GetComponent<PlayerMovement2_5D>();
    }

    private void Update()
    {
        if (Time.time < proximaBusca) return;

        proximaBusca = Time.time + intervaloDaBusca;
        Alvo = Procurar();
    }

    private Transform Procurar()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, alcance, buffer,
                                              ~0, QueryTriggerInteraction.Collide);
        float facing = movimento != null ? Mathf.Sign(movimento.Facing) : 1f;
        bool tresD = movimento != null && movimento.TerceiraPessoa;
        Vector3 frente = movimento != null ? movimento.FrenteDaMira : Vector3.right;

        Transform melhor = null;
        float melhorNota = float.NegativeInfinity;

        for (int i = 0; i < n; i++)
        {
            var col = buffer[i];
            if (col == null || !col.CompareTag("Enemy")) continue;

            // Um inimigo pode ter varios colisores; o dono e' quem tem a vida.
            var vida = col.GetComponentInParent<EnemyHealth>();
            if (vida == null) continue;
            var t = vida.transform;

            Vector3 d = t.position - transform.position;
            float dist = d.magnitude;
            if (dist > alcance) continue;

            // Nota: perto vale mais, e estar do lado pra onde o jogador olha vale muito.
            float nota = (alcance - dist);
            if (tresD)
            {
                // Terceira pessoa: "na frente" e' angulo, nao lado. Quem esta' no centro
                // do que o jogador olha leva o bonus inteiro; de lado, parte; atras, nada.
                Vector3 plano = new Vector3(d.x, 0f, d.z);
                float alinhamento = plano.sqrMagnitude > 0.25f ? Vector3.Dot(plano.normalized, frente) : 1f;
                if (alinhamento > 0f) nota += pesoDaFrente * alinhamento;
            }
            else if (Mathf.Sign(d.x) == facing || Mathf.Abs(d.x) < 0.5f) nota += pesoDaFrente;
            if (t == Alvo) nota += histerese;

            if (nota > melhorNota) { melhorNota = nota; melhor = t; }
        }

        return melhor;
    }
}
