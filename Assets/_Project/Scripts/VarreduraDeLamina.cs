using UnityEngine;

/// <summary>
/// Deteccao de acerto pela LAMINA de verdade: a cada frame do golpe varre uma capsula
/// entre a posicao do fio no frame anterior e a atual.
///
/// POR QUE ISTO E NAO UMA CAIXA NA FRENTE DO JOGADOR
/// A versao anterior era um OverlapBox fixo a 0,95 m a frente, na altura 1,32. Tres
/// problemas, todos sentidos no jogo:
///
///  1. O ALCANCE NAO ERA O DA ESPADA. A caixa e' um numero solto no Inspector; a
///     lamina mede o que mede e varre onde varre. Dava pra golpear "no ar" e acertar,
///     e encostar a ponta no inimigo e nao acertar.
///  2. PARAVA DE DAR DANO DO NADA. O OverlapBox e' um teste INSTANTANEO. Num golpe de
///     0,68 s a lamina cruza mais de 2 m; entre um frame e outro ela pula ~10 cm a
///     30 fps. Inimigo estreito no meio desse pulo simplesmente nao era tocado -
///     acerto perdido sem explicacao visivel.
///  3. TINHA QUE ENCOSTAR. A caixa ficava colada no corpo, entao o alcance util era
///     bem menor que o da arma.
///
/// A varredura resolve os tres: usa a posicao REAL do fio, e o CapsuleCast entre o
/// frame anterior e o atual cobre o caminho inteiro, sem buracos.
///
/// Anexar na RAIZ da espada (RealSword_Right). Ele acha punho e ponta sozinho.
/// </summary>
public class VarreduraDeLamina : MonoBehaviour
{
    [Tooltip("Espessura da varredura. Nao e' a espessura da lamina: e' a folga que o " +
             "jogador ganha. Um pouco generoso e' proposital - errar por 2 cm num " +
             "side-scroller rapido frustra sem ensinar nada.")]
    [SerializeField] private float raio = 0.22f;
    [Tooltip("Quanto do comprimento do fio conta como area de dano, a partir da ponta. " +
             "1 = a lamina inteira, incluindo o punho (que nao deveria cortar).")]
    [Range(0.3f, 1f)] [SerializeField] private float fracaoDoFio = 0.85f;
    [SerializeField] private LayerMask mascara = ~0;

    private Transform punho;
    private PlayerMovement2_5D movimento;
    private Vector3 punhoAnterior, pontaAnterior;
    private bool temAnterior;

    private readonly RaycastHit[] buffer = new RaycastHit[16];

    [Tooltip("Comprimento do fio, em metros. Zero = calcula sozinho pelo mesh. Este " +
             "modelo de espada e' um objeto UNICO sem hierarquia, entao 'achar a ponta " +
             "pelo osso mais distante' devolvia 0,00 - a arma nao tem osso nenhum.")]
    [SerializeField] private float comprimentoDoFio = 0f;
    [Tooltip("Direcao do fio no espaco LOCAL da arma. A calibragem que o jogador fez no " +
             "punho gira a espada, entao o eixo que aponta pra ponta nao e' obvio - " +
             "por isso e' calculado do mesh e nao chutado.")]
    [SerializeField] private Vector3 direcaoDoFio = Vector3.up;

    private void Awake()
    {
        punho = transform;

        // A ponta e' um PONTO no espaco da arma, nao um Transform: modelos de arma
        // costumam ser uma malha unica, sem osso na lamina. Calculada do mesh.
        if (comprimentoDoFio <= 0.001f) MedirDoMesh();

        movimento = GetComponentInParent<PlayerMovement2_5D>();
    }

    /// <summary>Descobre comprimento e direcao do fio pelo bounds do mesh, no espaco
    /// local da arma. Funciona pra qualquer modelo, inclusive malha unica.</summary>
    private void MedirDoMesh()
    {
        var filtros = GetComponentsInChildren<MeshFilter>(true);
        if (filtros.Length == 0) { comprimentoDoFio = 0.8f; return; }

        // maior eixo do bounds local = o comprimento da lamina
        var b = filtros[0].sharedMesh.bounds;
        Vector3 ext = b.extents;
        if (ext.y >= ext.x && ext.y >= ext.z)      { direcaoDoFio = Vector3.up;      comprimentoDoFio = ext.y * 2f; }
        else if (ext.x >= ext.y && ext.x >= ext.z) { direcaoDoFio = Vector3.right;   comprimentoDoFio = ext.x * 2f; }
        else                                        { direcaoDoFio = Vector3.forward; comprimentoDoFio = ext.z * 2f; }

        // do espaco do mesh pro mundo
        comprimentoDoFio *= transform.lossyScale.x;

        // A DIRECAO DA PONTA E' RESOLVIDA EM RUNTIME, nao aqui.
        //
        // Duas tentativas falharam por motivo parecido:
        //  - pelo bounds.center: nesta espada ele e' (0.002, 0.002, -0.001). A malha e'
        //    centrada na origem, entao o sinal e' RUIDO numerico e a escolha saia
        //    aleatoria.
        //  - pela distancia ate' a raiz do Player: a raiz fica nos PES, e os dois lados
        //    da lamina ficam a distancias parecidas dali - nao separa nada.
        //
        // Com a direcao errada a varredura acontece ATRAS do jogador e nunca encosta em
        // ninguem. Era essa a causa do dano nao sair.
        //
        // O criterio que funciona so' existe DURANTE o golpe: a ponta e' o lado que
        // aponta pra FRENTE do jogador (ver PontaAtual).
    }

    /// <summary>Ponta do fio no mundo, AGORA.
    ///
    /// A direcao e' escolhida aqui e nao no Awake porque so' em runtime existe o
    /// criterio confiavel: dos dois extremos possiveis da lamina, a ponta e' o que
    /// esta' mais a FRENTE do jogador (no sentido em que ele encara). Tentar deduzir
    /// isso do mesh falhou duas vezes - o bounds e' centrado na origem e a raiz do
    /// Player fica nos pes.</summary>
    private Vector3 PontaAtual()
    {
        float meia = comprimentoDoFio / Mathf.Max(0.0001f, transform.lossyScale.x);
        Vector3 a = transform.TransformPoint(direcaoDoFio.normalized * meia);
        Vector3 b = transform.TransformPoint(-direcaoDoFio.normalized * meia);

        // Direcao e' o vetor da frente do corpo: +X/-X no side-scroller, qualquer
        // direcao do plano em terceira pessoa.
        Vector3 frente = movimento != null ? movimento.Direcao : Vector3.right;
        Vector3 origem = movimento != null ? movimento.transform.position : transform.position;

        // Quem esta' mais longe do corpo NO SENTIDO EM QUE O JOGADOR OLHA e' a ponta.
        return Vector3.Dot(a - origem, frente) >= Vector3.Dot(b - origem, frente) ? a : b;
    }

    /// <summary>Comeca um golpe: zera a memoria do frame anterior pra a primeira
    /// varredura nao vir de onde a espada estava parada antes.</summary>
    public void Comecar()
    {
        temAnterior = false;
    }

    /// <summary>Atualiza a posicao do fio SEM testar acerto. Chamado nos frames do
    /// golpe que estao fora da janela de dano.
    ///
    /// Sem isto a primeira varredura da janela usaria como "frame anterior" a pose de
    /// muitos frames atras (o inicio do golpe), varrendo um arco enorme de uma vez -
    /// o jogador acertaria coisas que a lamina nem chegou perto.</summary>
    public void Acompanhar()
    {
        punhoAnterior = Vector3.Lerp(PontaAtual(), punho.position, 1f - fracaoDoFio);
        pontaAnterior = PontaAtual();
        temAnterior = true;
    }

    /// <summary>Varre o caminho da lamina desde o ultimo frame e devolve quantos
    /// alvos NOVOS foram atingidos. Quem chama decide o que fazer com cada um -
    /// este script nao conhece dano nem inimigo.</summary>
    public int Varrer(System.Collections.Generic.HashSet<IDamageable> jaAtingidos,
                      System.Action<IDamageable, Vector3> aoAcertar)
    {
        Vector3 pA = Vector3.Lerp(PontaAtual(), punho.position, 1f - fracaoDoFio);
        Vector3 pB = PontaAtual();

        if (!temAnterior)
        {
            punhoAnterior = pA; pontaAnterior = pB; temAnterior = true;
            return 0;   // sem frame anterior nao ha' caminho pra varrer
        }

        // Capsula do fio no frame anterior ate' o atual: e' o "rastro" que a lamina
        // desenhou, nao um instante solto.
        Vector3 centroAntes = (punhoAnterior + pontaAnterior) * 0.5f;
        Vector3 centroAgora = (pA + pB) * 0.5f;
        Vector3 delta = centroAgora - centroAntes;
        float dist = delta.magnitude;

        int acertos = 0;

        // DOIS testes, e os dois sao necessarios:
        //
        //  1. OverlapCapsule na posicao ATUAL do fio. E' o que pega o inimigo que a
        //     lamina esta' atravessando AGORA. So' o CapsuleCast nao basta: ele
        //     IGNORA o que ja' esta' sobreposto no ponto de partida, e entre dois
        //     frames a lamina anda poucos centimetros - quase sempre ela ja' comeca
        //     dentro do inimigo. Foi por isso que o dano sumiu quando troquei a caixa
        //     pela varredura.
        //  2. CapsuleCast do frame anterior ate' aqui. E' o que fecha o buraco quando
        //     a lamina passa RAPIDO demais e pula por cima de um inimigo estreito.
        var encontrados = Physics.OverlapCapsule(pA, pB, raio, mascara, QueryTriggerInteraction.Collide);
        for (int i = 0; i < encontrados.Length; i++)
            acertos += Processar(encontrados[i], centroAgora, jaAtingidos, aoAcertar);

        if (dist > 0.0001f)
        {
            int n = Physics.CapsuleCastNonAlloc(punhoAnterior, pontaAnterior, raio,
                                                delta.normalized, buffer, dist,
                                                mascara, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
                acertos += Processar(buffer[i].collider, centroAgora, jaAtingidos, aoAcertar);
        }

        punhoAnterior = pA; pontaAnterior = pB;
        return acertos;
    }

    /// <summary>Filtra um colisor e aplica o acerto, se valer. Devolve 1 se acertou.
    /// Separado porque os dois testes (overlap e cast) usam a mesma regra.</summary>
    private int Processar(Collider col, Vector3 referencia,
                          System.Collections.Generic.HashSet<IDamageable> jaAtingidos,
                          System.Action<IDamageable, Vector3> aoAcertar)
    {
        if (col == null) return 0;
        if (col.transform.IsChildOf(transform.root)) return 0;   // o proprio jogador
        if (!col.CompareTag("Enemy")) return 0;

        var alvo = col.GetComponentInParent<IDamageable>();
        if (alvo == null || jaAtingidos.Contains(alvo)) return 0;

        jaAtingidos.Add(alvo);
        aoAcertar?.Invoke(alvo, col.ClosestPoint(referencia));
        return 1;
    }

    private void OnDrawGizmosSelected()
    {
        if (comprimentoDoFio <= 0f) return;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Vector3 a = Vector3.Lerp(PontaAtual(), transform.position, 1f - fracaoDoFio);
        Gizmos.DrawLine(a, PontaAtual());
        Gizmos.DrawWireSphere(PontaAtual(), raio);
        Gizmos.DrawWireSphere(a, raio);
    }
}
