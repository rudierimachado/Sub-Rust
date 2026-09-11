using UnityEngine;

/// <summary>
/// A cara do jogador sem folego: tronco curvado, ombros caidos e o peito arfando,
/// com baforada saindo da boca.
///
/// POR QUE ISTO EXISTE
/// Zerar a stamina ja' trava ataque, esquiva e escudo (ver PlayerHealth.Exausto), mas
/// travar sem MOSTRAR e' o pior tipo de castigo: o jogador aperta, nada acontece e ele
/// conclui que o comando falhou, nao que ele errou. A postura e o ofego dizem "voce
/// esta' sem folego" no corpo do personagem, onde o olho ja' esta' olhando - sem
/// depender de ele conferir a barra no canto da tela.
///
/// Mesma linguagem visual do inimigo exausto (ver ExaustaoDoInimigo), de proposito:
/// se ficar sem folego tem a mesma cara nos dois lados, o jogador aprende a LER esse
/// estado no inimigo a partir do que sentiu no proprio personagem.
///
/// Os angulos entram POR CIMA da pose do frame, em LateUpdate. O Animator reescreve
/// localRotation todo frame, entao nao acumula - mesmo padrao do PlayerBloqueio.
///
/// Anexar na RAIZ do Player.
/// </summary>
public class PlayerExaustao : MonoBehaviour
{
    [Header("Postura")]
    [Tooltip("Graus que o tronco curva pra frente ao ficar sem folego.")]
    [SerializeField] private float curvarTronco = 17f;
    [Tooltip("Graus que a cabeca cai. Menor que a do inimigo: o jogador precisa " +
             "continuar enxergando pra onde esta' indo, e cabeca muito baixa vira " +
             "camera olhando pro chao.")]
    [SerializeField] private float cabecaCaida = 14f;
    [Tooltip("Graus que os ombros desabam pros lados.")]
    [SerializeField] private float ombrosCaidos = 15f;

    [Header("Ofego")]
    [Tooltip("Respiradas por segundo.")]
    [SerializeField] private float ritmoDoOfego = 2.1f;
    [Tooltip("Graus de abertura do peito a cada respirada.")]
    [SerializeField] private float amplitudeDoOfego = 6f;

    [Header("Transicao")]
    [Tooltip("Segundos pra curvar. Curto: a exaustao inteira dura ~1,2 s, entao uma " +
             "rampa lenta comeria a janela toda e o jogador nunca veria a pose.")]
    [SerializeField] private float tempoParaCurvar = 0.18f;
    [Tooltip("Se endireitar e' mais rapido: voltar a poder agir precisa ser legivel " +
             "no instante em que acontece.")]
    [SerializeField] private float tempoParaLevantar = 0.12f;

    private PlayerHealth vida;
    private Animator anim;
    private Transform tronco, peito, cabeca, ombroEsq, ombroDir;

    private float peso;
    private float faseDoOfego;
    private ParticleSystem baforada;

    private void Awake()
    {
        vida = GetComponent<PlayerHealth>();

        var visual = transform.Find("Visual");
        anim = visual != null ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();

        if (anim != null && anim.isHuman)
        {
            // Pelo AVATAR humanoide, nao por nome de osso: sobrevive a troca de rig.
            tronco   = anim.GetBoneTransform(HumanBodyBones.Spine);
            peito    = anim.GetBoneTransform(HumanBodyBones.Chest);
            cabeca   = anim.GetBoneTransform(HumanBodyBones.Head);
            ombroEsq = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            ombroDir = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
        }
    }

    private void OnEnable() => PlayerHealth.OnExaustao += AoMudarExaustao;
    private void OnDisable() => PlayerHealth.OnExaustao -= AoMudarExaustao;

    private void AoMudarExaustao(bool entrou)
    {
        if (baforada == null) baforada = CriarBaforada();
        if (baforada == null) return;

        if (entrou) baforada.Play();
        else baforada.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void LateUpdate()
    {
        if (vida == null) return;

        bool exausto = vida.Exausto;
        float tempo = exausto ? tempoParaCurvar : tempoParaLevantar;
        peso = Mathf.MoveTowards(peso, exausto ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, tempo));

        if (peso <= 0.001f) return;

        // A fase so' avanca enquanto ele esta' de fato sem folego: congelar na saida
        // evita o peito continuar arfando durante a volta a postura ereta.
        if (exausto) faseDoOfego += Time.deltaTime * ritmoDoOfego * Mathf.PI * 2f;
        float respirar = Mathf.Sin(faseDoOfego);

        if (tronco != null)
            tronco.localRotation *= Quaternion.Euler(curvarTronco * peso, 0f, 0f);
        if (peito != null)
            peito.localRotation *= Quaternion.Euler((curvarTronco * 0.3f - respirar * amplitudeDoOfego) * peso, 0f, 0f);
        if (cabeca != null)
            cabeca.localRotation *= Quaternion.Euler((cabecaCaida + respirar * amplitudeDoOfego * 0.4f) * peso, 0f, 0f);

        float cair = ombrosCaidos * peso;
        if (ombroEsq != null) ombroEsq.localRotation *= Quaternion.Euler(0f, 0f, cair);
        if (ombroDir != null) ombroDir.localRotation *= Quaternion.Euler(0f, 0f, -cair);
    }

    /// <summary>Baforada de folego na altura da cabeca, em rajadas no ritmo da
    /// respiracao. Reusa o material de particula do ImpactoDeGolpe - montar um
    /// material de particula na mao neste projeto ja' custou uma vez sair magenta,
    /// por usar shader do pipeline antigo.</summary>
    private ParticleSystem CriarBaforada()
    {
        float alturaCabeca = cabeca != null ? cabeca.position.y - transform.position.y : 1.6f;

        var go = new GameObject("FX_OfegoJogador");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.12f, alturaCabeca, 0f);
        go.transform.localRotation = Quaternion.Euler(12f, 90f, 0f);   // sai pela frente

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.gravityModifier = -0.05f;              // vapor sobe devagar
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.88f, 0.90f, 0.94f, 0.30f), new Color(0.75f, 0.77f, 0.82f, 0.16f));

        // Rajada por respirada, nao fluxo continuo: fluxo le' como fumaca, rajada le'
        // como alguem arfando.
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)5, (short)8, 30, 1f / Mathf.Max(0.1f, ritmoDoOfego))
        });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 20f;
        sh.radius = 0.04f;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.7f));

        var cor = ps.colorOverLifetime;
        cor.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f), new GradientAlphaKey(0f, 1f) });
        cor.color = new ParticleSystem.MinMaxGradient(grad);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = ImpactoDeGolpe.MaterialDeNevoa();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return ps;
    }
}
