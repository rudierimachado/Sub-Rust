using UnityEngine;

/// <summary>
/// A cara do inimigo com a guarda quebrada: corpo curvado, cabeca caida, bracos
/// pendurados e o peito subindo e descendo de ofego, com baforada saindo da cabeca.
///
/// POR QUE PROCEDURAL E NAO UM CLIPE DE "CANSADO"
/// Nao existe clipe de exaustao pra este rig no projeto - e mesmo que existisse, um
/// clipe CORTA a locomocao pra assumir o corpo inteiro. Aqui a curvatura entra e sai
/// gradualmente POR CIMA da pose do frame (ver peso), entao o inimigo desaba durante
/// meio segundo em vez de trocar de pose num estalo, e volta a se endireitar do mesmo
/// jeito. E' a diferenca entre "ele esta' cansado" e "trocou a animacao".
///
/// Todos os angulos sao somados a localRotation em LateUpdate. O Animator reescreve
/// localRotation todo frame, entao nao acumula - mesmo padrao do PlayerBloqueio.
///
/// Anexar na RAIZ do inimigo, junto do EnemyStamina.
/// </summary>
[RequireComponent(typeof(EnemyStamina))]
public class ExaustaoDoInimigo : MonoBehaviour
{
    [Header("Postura")]
    [Tooltip("Graus que o tronco curva pra FRENTE. E' o que le' como 'ele nao aguenta " +
             "mais' de longe, mais que qualquer outro osso.")]
    [SerializeField] private float curvarTronco = 26f;
    [Tooltip("Graus que a cabeca cai. Cabeca baixa e' o sinal mais legivel de derrota " +
             "em silhueta - num side-scroller e' a silhueta que o jogador enxerga.")]
    [SerializeField] private float cabecaCaida = 30f;
    [Tooltip("Graus que os bracos largam pros lados. Braco solto = parou de se defender.")]
    [SerializeField] private float bracosPendurados = 22f;
    [Tooltip("Graus de dobra nos joelhos: as pernas cedem um pouco sob o peso.")]
    [SerializeField] private float joelhosCedem = 14f;

    [Header("Ofego")]
    [Tooltip("Ciclos de respiracao por segundo. 1,7 e' respiracao ofegante de quem " +
             "acabou de esgotar - mais lento parece descanso, mais rapido parece panico.")]
    [SerializeField] private float ritmoDoOfego = 1.7f;
    [Tooltip("Graus que o peito abre e fecha a cada respirada.")]
    [SerializeField] private float amplitudeDoOfego = 7f;
    [Tooltip("Tremor do corpo cansado, em graus. Pequeno de proposito: musculo que " +
             "treme, nao personagem com defeito.")]
    [SerializeField] private float tremorDeCansaco = 1.2f;

    [Header("Transicao")]
    [Tooltip("Segundos pra desabar e pra se endireitar. Sem rampa o corpo TELETRANSPORTA " +
             "pra pose curvada no frame em que a stamina zera.")]
    [SerializeField] private float tempoParaDesabar = 0.45f;
    [Tooltip("Se endireitar e' mais RAPIDO que desabar: ele volta a lutar com energia, " +
             "e uma recuperacao arrastada faria o fim da janela virar adivinhacao.")]
    [SerializeField] private float tempoParaLevantar = 0.25f;

    [Header("Aviso")]
    [SerializeField] private Color corDoAviso = new Color(1f, 0.72f, 0.15f);
    [SerializeField] private float alturaDoAviso = 2.05f;

    private EnemyStamina folego;
    private Animator anim;
    private Transform tronco, peito, cabeca, bracoEsq, bracoDir, joelhoEsq, joelhoDir;

    private float peso;          // 0 = ereto, 1 = desabado
    private float faseDoOfego;
    private ParticleSystem baforada;

    private void Awake()
    {
        folego = GetComponent<EnemyStamina>();
        anim = GetComponentInChildren<Animator>();

        if (anim != null && anim.isHuman)
        {
            // Pelo AVATAR humanoide e nao por nome: continua funcionando se o rig mudar.
            tronco   = anim.GetBoneTransform(HumanBodyBones.Spine);
            peito    = anim.GetBoneTransform(HumanBodyBones.Chest);
            cabeca   = anim.GetBoneTransform(HumanBodyBones.Head);
            bracoEsq = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            bracoDir = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            joelhoEsq = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            joelhoDir = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        }
    }

    private void OnEnable()
    {
        folego.AoQuebrarGuarda += Anunciar;
        folego.AoRecuperar += Recuperar;
    }

    private void OnDisable()
    {
        folego.AoQuebrarGuarda -= Anunciar;
        folego.AoRecuperar -= Recuperar;
    }

    /// <summary>O aviso na tela. Sem ele o jogador nao tem como saber que a janela
    /// abriu: a barra de stamina do inimigo e' pequena e ele esta' olhando pro combate,
    /// nao pra ela.</summary>
    private void Anunciar()
    {
        NumeroFlutuante.Mostrar(transform.position + Vector3.up * alturaDoAviso,
                                "EXAUSTO", corDoAviso, 1.6f);
        if (baforada == null) baforada = CriarBaforada();
        if (baforada != null) baforada.Play();
    }

    private void Recuperar()
    {
        if (baforada != null) baforada.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void LateUpdate()
    {
        bool quebrado = folego.GuardaQuebrada;

        float destino = quebrado ? 1f : 0f;
        float tempo = quebrado ? tempoParaDesabar : tempoParaLevantar;
        peso = Mathf.MoveTowards(peso, destino, Time.deltaTime / Mathf.Max(0.01f, tempo));

        if (peso <= 0.001f) return;

        // O ofego so' avanca enquanto ele esta' de fato quebrado: congelar a fase na
        // saida evita o peito continuar arfando durante a volta a postura ereta.
        if (quebrado) faseDoOfego += Time.deltaTime * ritmoDoOfego * Mathf.PI * 2f;

        float respirar = Mathf.Sin(faseDoOfego);
        float tremor = (Mathf.PerlinNoise(Time.time * 9f, 0f) - 0.5f) * 2f * tremorDeCansaco * peso;

        // Tronco e cabeca caem; o peito abre e fecha por cima disso, que e' o ofego.
        if (tronco != null)
            tronco.localRotation *= Quaternion.Euler(curvarTronco * peso + tremor, 0f, 0f);
        if (peito != null)
            peito.localRotation *= Quaternion.Euler(
                (curvarTronco * 0.35f - respirar * amplitudeDoOfego) * peso, 0f, 0f);
        if (cabeca != null)
            cabeca.localRotation *= Quaternion.Euler(
                (cabecaCaida + respirar * amplitudeDoOfego * 0.5f) * peso, 0f, tremor);

        // Bracos largados, cada um pro seu lado.
        float largar = bracosPendurados * peso;
        if (bracoEsq != null) bracoEsq.localRotation *= Quaternion.Euler(0f, 0f, largar);
        if (bracoDir != null) bracoDir.localRotation *= Quaternion.Euler(0f, 0f, -largar);

        // Joelhos cedendo: sem isto o corpo curva mas as pernas ficam retas feito
        // manequim, e a pose nao ganha peso nenhum.
        float ceder = joelhosCedem * peso;
        if (joelhoEsq != null) joelhoEsq.localRotation *= Quaternion.Euler(ceder, 0f, 0f);
        if (joelhoDir != null) joelhoDir.localRotation *= Quaternion.Euler(ceder, 0f, 0f);
    }

    /// <summary>Baforada de folego saindo da altura da cabeca. Construida por codigo
    /// com shader de URP - o projeto ja' pagou o preco de usar shader do pipeline
    /// antigo em particula uma vez, e o resultado foi material magenta (ver
    /// ImpactoDeGolpe).</summary>
    private ParticleSystem CriarBaforada()
    {
        var alturaCabeca = cabeca != null ? cabeca.position.y - transform.position.y : 1.6f;

        var go = new GameObject("FX_Ofego");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, alturaCabeca, 0.12f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        // Sobe devagar: vapor de folego nao cai nem dispara.
        main.gravityModifier = -0.06f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.87f, 0.92f, 0.32f), new Color(0.7f, 0.72f, 0.78f, 0.18f));

        // Rajadas no RITMO da respiracao, nao um fluxo continuo: fluxo constante le'
        // como fumaca de cano furado; rajada le' como alguem arfando.
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)6, (short)9, 30, 1f / Mathf.Max(0.1f, ritmoDoOfego))
        });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 22f;
        sh.radius = 0.05f;
        // Aponta pra frente e um pouco pra baixo: cabeca caida, folego indo pro chao.
        go.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);

        var sobreVida = ps.sizeOverLifetime;
        sobreVida.enabled = true;
        sobreVida.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

        var corSobreVida = ps.colorOverLifetime;
        corSobreVida.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        corSobreVida.color = new ParticleSystem.MinMaxGradient(grad);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = ImpactoDeGolpe.MaterialDeNevoa();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        return ps;
    }
}
