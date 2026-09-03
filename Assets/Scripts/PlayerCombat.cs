using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Combate corpo a corpo com a espada da mao DIREITA: clique rapido = golpe leve,
/// segurar = golpe forte. A mao esquerda agora carrega a pistola (ver PlayerShooting),
/// entao nao existe mais alternancia de combo entre as duas maos.
/// Anexar no mesmo objeto do Animator (Visual), nao na raiz do Player.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Tempo pra distinguir clique de segurar")]
    [SerializeField] private float holdThreshold = 0.25f;

    [Header("Janela de acerto (fracao da animacao)")]
    [SerializeField] private float hitCheckStart = 0.3f;
    [SerializeField] private float hitCheckEnd = 0.7f;
    [SerializeField] private float hitRadius = 0.5f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float hitStopSeconds = 0.05f;

    [Header("Efeito de impacto")]
    [SerializeField] private float shakeForce = 1.2f;
    [SerializeField] private Color sparkColor = new Color(1f, 0.75f, 0.2f);

    private static readonly int LightTrigger = Animator.StringToHash("AttackLight");
    private static readonly int HeavyTrigger = Animator.StringToHash("AttackHeavy");

    private Animator animator;
    private CinemachineImpulseSource impulseSource;
    private Transform rightSword;

    private bool botaoPressionado;
    private float tempoPressionado;
    private bool golpeForteDisparado;

    private enum GolpePendente { Nenhum, Leve, Forte }
    private GolpePendente golpePendente = GolpePendente.Nenhum;

    private bool isAttacking;
    private bool hitAppliedThisSwing;

    // Camada so' do tronco: as pernas seguem na locomocao, entao da' pra atacar andando
    // e correndo. Indice buscado por NOME - inserir uma camada nova reordena os indices
    // e quebraria tudo em silencio.
    private int camadaTronco;
    private float pesoTronco;
    [SerializeField] private float tempoDeSaidaDoTronco = 0.12f;
    private int lightStateHash;
    private int heavyStateHash;
    private readonly Collider[] hitBuffer = new Collider[16];

    private void Awake()
    {
        animator = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();

        var rightHand = FindDeep(transform, "mixamorig:RightHand");
        rightSword = rightHand != null ? FindDeep(rightHand, "RealSword_Right") : null;

        lightStateHash = Animator.StringToHash("AttackLightR");
        heavyStateHash = Animator.StringToHash("AttackHeavy");

        camadaTronco = animator.GetLayerIndex("TroncoAtaque");
        if (camadaTronco < 0)
            Debug.LogError("Camada 'TroncoAtaque' nao existe no Animator Controller - o ataque nao vai aparecer.");
    }

    private void Update()
    {
        LerBotaoDeAtaque();

        if (camadaTronco < 0) return;

        var stateInfo = animator.GetCurrentAnimatorStateInfo(camadaTronco);
        bool emGolpeAgora = stateInfo.shortNameHash == heavyStateHash
                          || stateInfo.shortNameHash == lightStateHash;

        // A camada so' pesa enquanto o golpe roda; some suave para os bracos voltarem
        // a balancar junto com a corrida em vez de cortar seco.
        pesoTronco = Mathf.MoveTowards(pesoTronco, emGolpeAgora ? 1f : 0f,
                                       Time.deltaTime / Mathf.Max(0.01f, tempoDeSaidaDoTronco));
        animator.SetLayerWeight(camadaTronco, pesoTronco);

        if (emGolpeAgora && !isAttacking)
        {
            isAttacking = true;
            hitAppliedThisSwing = false;
        }
        else if (!emGolpeAgora && isAttacking)
        {
            isAttacking = false;
            DispararPendente();
        }

        if (isAttacking)
        {
            float t = stateInfo.normalizedTime % 1f;
            if (!hitAppliedThisSwing && t >= hitCheckStart && t <= hitCheckEnd)
                CheckHit();
        }
    }

    private void LerBotaoDeAtaque()
    {
        bool down = (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
                    (Keyboard.current != null && Keyboard.current.jKey.isPressed);

        if (down && !botaoPressionado)
        {
            botaoPressionado = true;
            tempoPressionado = 0f;
            golpeForteDisparado = false;
        }
        else if (down && botaoPressionado)
        {
            tempoPressionado += Time.deltaTime;
            if (!golpeForteDisparado && tempoPressionado >= holdThreshold)
            {
                golpeForteDisparado = true;
                DispararOuEnfileirar(GolpePendente.Forte);
            }
        }
        else if (!down && botaoPressionado)
        {
            if (!golpeForteDisparado)
                DispararOuEnfileirar(GolpePendente.Leve);
            botaoPressionado = false;
        }
    }

    private void DispararOuEnfileirar(GolpePendente golpe)
    {
        if (!isAttacking) Disparar(golpe);
        else golpePendente = golpe;
    }

    private void DispararPendente()
    {
        if (golpePendente == GolpePendente.Nenhum) return;
        var golpe = golpePendente;
        golpePendente = GolpePendente.Nenhum;
        Disparar(golpe);
    }

    private void Disparar(GolpePendente golpe)
    {
        // Peso cheio JA': a transicao leva alguns frames e sem isso os primeiros
        // quadros do golpe sairiam apagados.
        if (camadaTronco >= 0)
        {
            pesoTronco = 1f;
            animator.SetLayerWeight(camadaTronco, 1f);
        }

        if (golpe == GolpePendente.Forte)
        {
            animator.SetTrigger(HeavyTrigger);
        }
        else if (golpe == GolpePendente.Leve)
        {
            animator.SetTrigger(LightTrigger);
        }
    }

    private void CheckHit()
    {
        CheckHitAt(rightSword);
    }

    private bool CheckHitAt(Transform ponto)
    {
        if (ponto == null) return false;

        int count = Physics.OverlapSphereNonAlloc(ponto.position, hitRadius, hitBuffer, hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var col = hitBuffer[i];
            if (col.transform.IsChildOf(transform)) continue;
            if (!col.CompareTag("Enemy")) continue;

            hitAppliedThisSwing = true;
            var damageable = col.GetComponent<IDamageable>();
            damageable?.TakeHit(10f, ponto.position);

            impulseSource?.GenerateImpulseWithForce(shakeForce);
            SpawnSpark(ponto.position);
            HitStop.Aplicar(this, hitStopSeconds, 0.02f);
            return true;
        }
        return false;
    }

    /// <summary>Rajada rapida de particulas no ponto do acerto - sem depender de asset externo.</summary>
    private void SpawnSpark(Vector3 posicao)
    {
        var go = new GameObject("HitSpark");
        go.transform.position = posicao;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = 0.18f;
        main.startSpeed = 4f;
        main.startSize = 0.08f;
        main.startColor = sparkColor;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

        ps.Play();
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
