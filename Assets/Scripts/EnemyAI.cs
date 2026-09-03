using UnityEngine;

/// <summary>
/// IA minima: anda no eixo X ate' ficar perto do jogador, para e ataca em loop
/// com cooldown. Dano aplicado num unico instante dentro da animacao (janela
/// medida em fracao normalizada, nao em segundos fixos).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    [SerializeField] private float velocidade = 2f;
    [SerializeField] private float alcanceAtaque = 1.3f;
    [SerializeField] private float alcancePerseguicao = 3.5f;
    [SerializeField] private float cooldownAtaque = 1.8f;
    [SerializeField] private float dano = 8f;

    // MEDIDOS no clipe Enemy_Attack_1_InPlace, nao chutados:
    // quem golpeia e' a MAO DIREITA (pico 14,03 m/s contra 6,97 do pe mais rapido),
    // o pico acontece em t=0,25 do clipe, e nesse instante a mao esta a 0,74 m da raiz.
    [SerializeField] private float momentoDoGolpe = 0.25f;
    // 0,74 (alcance da mao) + 0,35 (raio do corpo do jogador) = 1,09
    [SerializeField] private float alcanceDano = 1.10f;

    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int WalkingParam = Animator.StringToHash("Walking");

    [SerializeField] private float gravidade = -25f;

    private Transform player;
    private Transform visual;
    private Animator animator;
    private CharacterController controller;
    private float velocidadeVertical;
    private float tempoParaProximoAtaque;
    private bool atacando;
    private bool danoAplicadoNesteGolpe;
    private int attackStateHash;
    private float facing = 1f;

    private void Awake()
    {
        visual = transform.Find("Visual");
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        attackStateHash = Animator.StringToHash("Attack");

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) player = playerGO.transform;
    }

    private void Update()
    {
        if (player == null || animator == null) return;

        float diferencaX = player.position.x - transform.position.x;
        float distancia = Mathf.Abs(diferencaX);

        // Encara o jogador o tempo todo (parado ou atacando), so' nao vira no meio do golpe.
        if (!atacando && Mathf.Abs(diferencaX) > 0.05f)
        {
            float alvoFacing = diferencaX > 0f ? 1f : -1f;
            if (alvoFacing != facing) { facing = alvoFacing; AplicarFacing(); }
        }

        var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool estaAtacandoAgora = stateInfo.shortNameHash == attackStateHash;

        if (estaAtacandoAgora && !atacando)
        {
            atacando = true;
            danoAplicadoNesteGolpe = false;
        }
        else if (!estaAtacandoAgora && atacando)
        {
            atacando = false;
        }

        if (atacando)
        {
            animator.SetBool(WalkingParam, false);
            if (!danoAplicadoNesteGolpe && stateInfo.normalizedTime % 1f >= momentoDoGolpe)
            {
                danoAplicadoNesteGolpe = true;

                // Acerta dos DOIS lados (a distancia e' em modulo), mas so' quem esta na
                // frente: quem escapou para as costas no meio do golpe nao leva.
                bool naFrente = Mathf.Sign(diferencaX) == Mathf.Sign(facing);
                if (distancia <= alcanceDano && naFrente)
                {
                    var alvo = player.GetComponent<IDamageable>();
                    // Passa a posicao DO INIMIGO: e' dela que sai a direcao do empurrao.
                    alvo?.TakeHit(dano, transform.position);
                }
            }
            MoverComColisao(Vector3.zero);
            return;
        }

        tempoParaProximoAtaque -= Time.deltaTime;

        if (distancia <= alcanceAtaque)
        {
            animator.SetBool(WalkingParam, false);
            if (tempoParaProximoAtaque <= 0f)
            {
                animator.SetTrigger(AttackTrigger);
                tempoParaProximoAtaque = cooldownAtaque;
            }
        }
        else if (distancia <= alcancePerseguicao)
        {
            animator.SetBool(WalkingParam, true);
            float direcao = player.position.x > transform.position.x ? 1f : -1f;
            MoverComColisao(Vector3.right * direcao * velocidade);
            return;
        }
        else
        {
            animator.SetBool(WalkingParam, false);
        }

        MoverComColisao(Vector3.zero);
    }

    /// <summary>Move via CharacterController pra respeitar colisao real com o jogador e o cenario.</summary>
    private void MoverComColisao(Vector3 horizontal)
    {
        if (controller.isGrounded && velocidadeVertical < 0f)
            velocidadeVertical = -2f;
        else
            velocidadeVertical += gravidade * Time.deltaTime;

        // Separacao contra o jogador fica no SeparacaoDeCorpos (penetracao real dos colisores).
        controller.Move(horizontal * Time.deltaTime + Vector3.up * velocidadeVertical * Time.deltaTime);
    }

    private void AplicarFacing()
    {
        if (visual == null) return;
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, facing * Mathf.Abs(s.z));
    }
}
