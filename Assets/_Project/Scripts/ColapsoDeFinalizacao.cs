using UnityEngine;

/// <summary>
/// A reacao da VITIMA na finalizacao: o espasmo enquanto a lamina esta' cravada e a
/// queda dura pra tras quando ela sai.
///
/// POR QUE NAO USAR UM CLIPE DE MORTE
/// E' aqui que quase toda finalizacao de pacote gratuito entrega o jogo: o executor
/// faz um golpe caprichado e a vitima toca a MESMA morte generica de sempre, que
/// comeca em pe' e ignora completamente o que acabou de acontecer com ela. Os dois
/// corpos deixam de conversar e a cena vira duas animacoes rodando lado a lado.
/// Aqui a reacao e' construida em cima da pose do frame, entao responde a ESTE golpe:
/// o corpo arqueia no eixo em que a lamina entrou e cai pro lado oposto a quem golpeou.
///
/// COMO A QUEDA E' FEITA
/// Girando a RAIZ inteira em torno dos pes, como um tronco tombando - nao dobrando
/// osso por osso ate' deitar. Deitar por ossos exige acertar quadril, joelhos, coluna
/// e bracos ao mesmo tempo pra nao afundar no chao nem virar cambalhota; a queda dura
/// resolve com uma rotacao unica e le' muito bem em silhueta, que e' como o jogador
/// enxerga num side-scroller.
///
/// Os angulos entram POR CIMA da pose do Animator, em LateUpdate - mesmo padrao do
/// PlayerBloqueio. Adicionado em runtime pela FinalizacaoCinematica.
/// </summary>
public class ColapsoDeFinalizacao : MonoBehaviour
{
    [SerializeField] private float cabecaPraTras = 34f;
    [Tooltip("Arco na coluna. E' o que mostra que a lamina ATRAVESSOU, em vez de so' encostar.")]
    [SerializeField] private float arcoDaColuna = 22f;
    [Tooltip("Quanto os bracos largam pros lados. Braco solto = corpo que parou de se defender.")]
    [SerializeField] private float bracosLargados = 40f;
    [Tooltip("Angulo final da queda. Um pouco menos que 90 evita o corpo passar do " +
             "ponto e afundar no chao.")]
    [SerializeField] private float anguloDaQueda = 88f;
    [Tooltip("Quanto o corpo pende enquanto a lamina o sustenta, antes de cair.")]
    [SerializeField] private float pendurado = 9f;

    private Transform cabeca, peito, coluna, bracoEsq, bracoDir;
    private Quaternion rotBase;
    private Vector3 posBase, pivo, dirQueda;

    private float espasmo;
    private float queda;
    private float tremor;

    /// <summary>Prepara a vitima e guarda a pose base da queda.
    /// <paramref name="direcaoDaQueda"/> aponta pra LONGE de quem golpeou.</summary>
    public void Preparar(Vector3 direcaoDaQueda)
    {
        var anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            cabeca   = anim.GetBoneTransform(HumanBodyBones.Head);
            peito    = anim.GetBoneTransform(HumanBodyBones.Chest);
            coluna   = anim.GetBoneTransform(HumanBodyBones.Spine);
            bracoEsq = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            bracoDir = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        // A IA continuaria andando e virando o corpo por baixo da encenacao; o
        // CharacterController continuaria aplicando gravidade e brigando com a queda.
        var ia = GetComponent<EnemyAI>();
        if (ia != null) ia.enabled = false;
        var exaustao = GetComponent<ExaustaoDoInimigo>();
        if (exaustao != null) exaustao.enabled = false;   // senao ele curva e cai ao mesmo tempo
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        dirQueda = new Vector3(Mathf.Sign(direcaoDaQueda.x), 0f, 0f);
        rotBase = transform.rotation;
        posBase = transform.position;
        pivo = posBase;   // pes: e' o que faz tombar em vez de girar no ar
    }

    /// <summary>Momento em que a lamina entra.</summary>
    public void Cravar()
    {
        espasmo = 1f;
        tremor = 1f;
    }

    /// <summary>Avanca a queda, de 0 (pendurado na lamina) a 1 (deitado).</summary>
    public void Tombar(float t) => queda = Mathf.Clamp01(t);

    private void LateUpdate()
    {
        if (tremor > 0f) tremor = Mathf.Max(0f, tremor - Time.unscaledDeltaTime * 3.5f);
        AplicarPose();
        AplicarQueda();
    }

    private void AplicarPose()
    {
        if (espasmo <= 0.001f) return;

        float lado = dirQueda.x;
        float t = tremor * tremor;
        float ruido = Mathf.Sin(Time.unscaledTime * 42f) * 4f * t;

        // O sinal segue o lado da queda pra arquear pra TRAS de verdade, e nao pra
        // frente, quando o golpe vem do outro lado.
        if (cabeca != null)
            cabeca.localRotation *= Quaternion.Euler(-(cabecaPraTras + ruido) * espasmo * lado, 0f, 0f);
        if (peito != null)
            peito.localRotation *= Quaternion.Euler(-(arcoDaColuna + ruido * 0.5f) * espasmo * lado, 0f, 0f);
        if (coluna != null)
            coluna.localRotation *= Quaternion.Euler(-arcoDaColuna * 0.45f * espasmo * lado, 0f, 0f);

        float largar = bracosLargados * espasmo * Mathf.Lerp(0.55f, 1f, queda);
        if (bracoEsq != null) bracoEsq.localRotation *= Quaternion.Euler(0f, 0f, largar);
        if (bracoDir != null) bracoDir.localRotation *= Quaternion.Euler(0f, 0f, -largar);
    }

    private void AplicarQueda()
    {
        if (espasmo <= 0.001f) return;

        float ang = pendurado;
        if (queda > 0f)
        {
            // Acelera como coisa que cai: devagar enquanto ainda equilibra, rapido no
            // fim. Queda linear le' como elevador descendo.
            float q = queda * queda * (1.6f - 0.6f * queda);
            ang = Mathf.Lerp(pendurado, anguloDaQueda, Mathf.Clamp01(q));
        }

        Vector3 eixo = Vector3.Cross(Vector3.up, dirQueda).normalized;
        if (eixo.sqrMagnitude < 0.5f) return;

        Quaternion giro = Quaternion.AngleAxis(ang, eixo);
        transform.rotation = giro * rotBase;
        transform.position = pivo + giro * (posBase - pivo);
    }
}
