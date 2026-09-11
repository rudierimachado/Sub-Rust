using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera exclusiva da finalizacao: corta para um plano fechado, baixo e de tres
/// quartos, e faz uma aproximacao lenta enquanto o golpe acontece.
///
/// POR QUE UMA CAMERA PROPRIA E NAO MEXER NA DO JOGO
/// A CameraJogador segue o jogador em terceira pessoa, que e' o enquadramento certo
/// pra LER a luta - e exatamente o errado pra sentir um golpe. Mexer nela significaria
/// guardar e restaurar posicao, damping e composer, e qualquer falha no meio deixaria
/// a camera do jogo quebrada pro resto da partida. Uma camera separada com prioridade
/// maior e' reversivel por construcao: baixou a prioridade, a do jogo volta como estava.
///
/// O corte e' CURTO de proposito (ver tempoDoCorte). O Cinemachine mistura em tempo
/// ESCALADO e a finalizacao roda em camera lenta - uma transicao de 0,5 s viraria
/// quase 2 s reais e o corte perderia o impacto. O blend padrao do Brain e' trocado
/// durante a sequencia e devolvido no fim.
/// </summary>
public class CameraDeFinalizacao : MonoBehaviour
{
    [Header("Enquadramento")]
    [SerializeField] private float distanciaInicial = 4.6f;
    [Tooltip("Distancia no fim. Menor que a inicial = a camera entra devagar, que e' o " +
             "que da' a sensacao de aproximacao sem mexer no zoom.")]
    [SerializeField] private float distanciaFinal = 3.2f;
    [Tooltip("Deslocamento pra frente do eixo do side-scroller, em metros. Zero seria o " +
             "mesmo perfil chapado do jogo; este offset e' o que abre os tres quartos.")]
    [SerializeField] private float aberturaEmZ = 3.1f;
    [Tooltip("Altura da camera em relacao ao ponto olhado. Negativo = olha de BAIXO pra " +
             "cima, que engrandece quem golpeia.")]
    [SerializeField] private float alturaRelativa = -0.35f;
    [Tooltip("Altura do ponto olhado, do chao pra cima: o peito da vitima.")]
    [SerializeField] private float alturaDoAlvo = 1.25f;
    [Tooltip("Graus orbitados durante a sequencia. Movimento lento e continuo, mesmo " +
             "pequeno, e' o que separa 'cena' de 'foto parada'.")]
    [SerializeField] private float orbita = 14f;

    [SerializeField] private float fovInicial = 34f;
    [SerializeField] private float fovFinal = 27f;
    [SerializeField] private float tempoDoCorte = 0.18f;

    private CinemachineCamera cam;
    private CinemachineBrain brain;
    private CinemachineBlendDefinition blendOriginal;
    private bool blendTrocado;

    private Vector3 centro;
    private float lado;

    /// <summary>Assume o enquadramento. <paramref name="lado"/> e' +1 se o jogador
    /// encara a direita: decide de que lado a camera nasce, pra ela nunca ficar atras
    /// da nuca de ninguem.</summary>
    public void Assumir(Vector3 posJogador, Vector3 posVitima, float lado)
    {
        this.lado = Mathf.Sign(lado);
        centro = (posJogador + posVitima) * 0.5f;
        centro.y = Mathf.Min(posJogador.y, posVitima.y) + alturaDoAlvo;

        if (cam == null)
        {
            var go = new GameObject("CamFinalizacao");
            cam = go.AddComponent<CinemachineCamera>();
            // Sem Follow/LookAt: posicao e mira sao escritas a mao em Posicionar,
            // porque um composer com damping suavizaria justamente o corte seco.
        }

        cam.Lens.FieldOfView = fovInicial;
        Posicionar(0f);

        cam.Priority = 100;   // acima da CameraJogador (10)
        cam.gameObject.SetActive(true);

        if (brain == null) brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null && !blendTrocado)
        {
            blendOriginal = brain.DefaultBlend;
            brain.DefaultBlend = new CinemachineBlendDefinition
            {
                Style = CinemachineBlendDefinition.Styles.EaseInOut,
                Time = tempoDoCorte
            };
            blendTrocado = true;
        }
    }

    /// <summary>Avanca o enquadramento. <paramref name="t"/> vai de 0 a 1 ao longo da
    /// sequencia inteira.</summary>
    public void Posicionar(float t)
    {
        if (cam == null) return;

        t = Mathf.Clamp01(t);
        float s = t * t * (3f - 2f * t);   // suave nas duas pontas

        float dist = Mathf.Lerp(distanciaInicial, distanciaFinal, s);
        Vector3 baseDir = new Vector3(-lado * 0.55f, 0f, -aberturaEmZ).normalized;
        Vector3 dir = Quaternion.Euler(0f, orbita * s * lado, 0f) * baseDir;

        Vector3 pos = centro + dir * dist + Vector3.up * alturaRelativa;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(centro - pos, Vector3.up);
        cam.Lens.FieldOfView = Mathf.Lerp(fovInicial, fovFinal, s);
    }

    /// <summary>Sacode a camera SEM Impulse: a fonte de impulso vive no corpo do
    /// jogador, e no impacto o corpo esta' parado em camera lenta - o ruido do impulse
    /// quase nao apareceria. Aqui o deslocamento entra direto no transform.</summary>
    public void Tremer(float forca)
    {
        if (cam != null) cam.transform.position += Random.insideUnitSphere * forca;
    }

    /// <summary>Devolve o controle pra camera do jogo e restaura o blend padrao.
    /// Chamado SEMPRE no fim, inclusive se a sequencia for abortada.</summary>
    public void Devolver()
    {
        if (cam != null) cam.Priority = -10;
        if (brain != null && blendTrocado)
        {
            brain.DefaultBlend = blendOriginal;
            blendTrocado = false;
        }
    }

    private void OnDestroy()
    {
        Devolver();
        if (cam != null) Destroy(cam.gameObject);
    }
}
