using UnityEngine;

/// <summary>
/// IA do inimigo como maquina de estados.
///
/// O que mudou em relacao a versao antiga (que era um if/else de tres ramos):
///  - PERCEPCAO: cone de visao com linha de visada real (parede bloqueia) + raio de
///    audicao que funciona pelas costas. Antes ele te via atravessado na parede.
///  - MEMORIA: guarda a ultima posicao vista e vai investigar la' antes de desistir,
///    em vez de esquecer voce no frame em que sai do alcance.
///  - ESPACAMENTO: depois de golpear ele RECUA. Antes ficava colado trocando golpe,
///    o que tirava qualquer leitura do combate.
///  - TELEGRAFO: para e encara antes de girar o golpe, entao da' pra esquivar.
///  - BORDA E PAREDE: checa chao a frente e nao anda pro precipicio nem empurra parede.
///  - ATORDOAMENTO: enquanto a animacao de dano roda, a IA nao decide nada.
///
/// Os numeros de dano continuam sendo os MEDIDOS no clipe, nao chutes (ver abaixo).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    private enum Estado { Patrulha, Alerta, Perseguir, Mirar, Atacar, Recuar, Procurar, Atordoado }

    [Header("Movimento")]
    [SerializeField] private float velocidade = 3.3f;
    [SerializeField] private float velocidadePatrulha = 1.1f;
    [SerializeField] private float gravidade = -25f;

    [Header("Percepcao")]
    [SerializeField] private float raioVisao = 11f;
    [Tooltip("Angulo TOTAL do cone de visao, em graus.")]
    [SerializeField] private float anguloVisao = 120f;
    [Tooltip("Ouve pelas costas dentro deste raio, mesmo sem linha de visada.")]
    [SerializeField] private float raioAudicao = 3.5f;
    [SerializeField] private float alturaOlho = 1.45f;
    [SerializeField] private LayerMask bloqueiaVisao = ~0;

    // Calibragem de combate (setembro/2026). O jogador tem 1250 de vida e a
    // espingarda faz 60 por tiro; com dano=60 o inimigo precisava de 21 golpes pra
    // matar, o que lia como inofensivo. 130 poe a morte em ~10 golpes.
    // O ritmo vem de tres numeros juntos, nao so' do cooldown: mira (telegrafo),
    // recuo (o vai-e-vem) e a VELOCIDADE DO CLIPE de ataque no Animator - o
    // Enemy_Attack_1_InPlace tem 2,17s crus, que sozinho ja' arrastava a luta.
    // O dano usa normalizedTime, entao acelerar o clipe NAO desincroniza o golpe.
    [Header("Combate")]
    [Tooltip("Distancia em que ele quer ficar pra golpear.")]
    [SerializeField] private float distanciaDeGolpe = 1.15f;
    [Tooltip("Para onde recua depois de atacar - e' o que cria o vai-e-vem.")]
    [SerializeField] private float distanciaDeRecuo = 1.9f;
    [SerializeField] private float tempoDeMira = 0.22f;
    [SerializeField] private float tempoDeRecuo = 0.3f;
    [SerializeField] private float cooldownAtaque = 0.9f;
    [SerializeField] private float dano = 130f;

    // MEDIDOS no clipe Enemy_Attack_1_InPlace, nao chutados: quem golpeia e' a MAO
    // DIREITA (pico 14,03 m/s contra 6,97 do pe mais rapido), o pico acontece em
    // t=0,25 do clipe, e nesse instante a mao esta a 0,74 m da raiz.
    [SerializeField] private float momentoDoGolpe = 0.25f;
    // 0,74 (alcance da mao) + 0,35 (raio do corpo do jogador) = 1,09
    [SerializeField] private float alcanceDano = 1.10f;

    [Header("Patrulha")]
    [SerializeField] private float raioPatrulha = 4f;
    [SerializeField] private float pausaNaPonta = 1.2f;

    [Header("Memoria")]
    [SerializeField] private float memoriaSegundos = 4f;
    [SerializeField] private float tempoProcurando = 2.5f;

    private static readonly int AttackTrigger = Animator.StringToHash("Attack");
    private static readonly int WalkingParam = Animator.StringToHash("Walking");

    private Transform player;
    private Transform visual;
    private Animator animator;
    private CharacterController controller;
    private int attackStateHash, hitStateHash;

    private Estado estado = Estado.Patrulha;
    private float tempoNoEstado;
    private float facing = 1f;
    private float velocidadeVertical;

    private float xInicial;
    private float alvoPatrulha;
    private float esperaPatrulha;

    private Vector3 ultimaPosicaoVista;
    private float tempoDesdeQueViu = 999f;

    private float proximoAtaqueLiberado;
    private bool danoAplicadoNesteGolpe;

    private void Awake()
    {
        visual = transform.Find("Visual");
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        attackStateHash = Animator.StringToHash("Attack");
        hitStateHash = Animator.StringToHash("Hit");

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) player = playerGO.transform;

        xInicial = transform.position.x;
        alvoPatrulha = xInicial + raioPatrulha;
    }

    private void Update()
    {
        if (animator == null) return;

        var info = animator.GetCurrentAnimatorStateInfo(0);
        bool naAnimacaoDeDano = info.shortNameHash == hitStateHash;
        bool naAnimacaoDeAtaque = info.shortNameHash == attackStateHash;

        // Levar dano interrompe qualquer decisao: sem isso ele continuava avancando
        // durante a propria animacao de impacto.
        if (naAnimacaoDeDano)
        {
            if (estado != Estado.Atordoado) TrocarPara(Estado.Atordoado);
            animator.SetBool(WalkingParam, false);
            MoverComColisao(0f);
            return;
        }

        AtualizarPercepcao();
        tempoNoEstado += Time.deltaTime;

        switch (estado)
        {
            case Estado.Patrulha:   Patrulhar();                     break;
            case Estado.Alerta:     Alertar();                       break;
            case Estado.Perseguir:  Perseguir();                     break;
            case Estado.Mirar:      Mirar();                         break;
            case Estado.Atacar:     Atacar(info, naAnimacaoDeAtaque); break;
            case Estado.Recuar:     Recuar();                        break;
            case Estado.Procurar:   Procurar();                      break;
            case Estado.Atordoado:  TrocarPara(EstadoDeCombateOuPatrulha()); break;
        }
    }

    // ---------------------------------------------------------------- percepcao

    private void AtualizarPercepcao()
    {
        tempoDesdeQueViu += Time.deltaTime;
        if (player == null) return;

        if (ConsegueVer())
        {
            tempoDesdeQueViu = 0f;
            ultimaPosicaoVista = player.position;
        }
    }

    private bool ConsegueVer()
    {
        Vector3 olho = transform.position + Vector3.up * alturaOlho;
        Vector3 alvo = player.position + Vector3.up * 1.0f;
        Vector3 paraAlvo = alvo - olho;
        float dist = paraAlvo.magnitude;

        // ouvido: perto o bastante, percebe mesmo de costas e mesmo sem linha limpa
        if (dist <= raioAudicao) return true;
        if (dist > raioVisao) return false;

        // cone de visao a partir do lado pra onde esta virado
        Vector3 frente = Vector3.right * facing;
        float angulo = Vector3.Angle(frente, new Vector3(paraAlvo.x, 0f, 0f));
        if (angulo > anguloVisao * 0.5f) return false;

        // linha de visada de verdade: parede no meio bloqueia
        if (Physics.Raycast(olho, paraAlvo.normalized, out var h, dist, bloqueiaVisao, QueryTriggerInteraction.Ignore))
            if (!h.collider.transform.IsChildOf(player)) return false;

        return true;
    }

    private bool LembraDoJogador => tempoDesdeQueViu < memoriaSegundos;
    private bool VendoAgora => tempoDesdeQueViu < 0.15f;

    private float DistanciaX => player == null ? 999f : Mathf.Abs(player.position.x - transform.position.x);

    // ---------------------------------------------------------------- estados

    private void Patrulhar()
    {
        animator.SetBool(WalkingParam, esperaPatrulha <= 0f);

        if (VendoAgora) { TrocarPara(Estado.Alerta); return; }

        if (esperaPatrulha > 0f)
        {
            esperaPatrulha -= Time.deltaTime;
            MoverComColisao(0f);
            return;
        }

        float dir = Mathf.Sign(alvoPatrulha - transform.position.x);
        Encarar(dir);

        // nao anda pro precipicio nem fica empurrando parede
        if (!TemChaoAFrente(dir) || TemParedeAFrente(dir))
        {
            InverterPatrulha();
            MoverComColisao(0f);
            return;
        }

        if (Mathf.Abs(alvoPatrulha - transform.position.x) < 0.25f) InverterPatrulha();
        else MoverComColisao(dir * velocidadePatrulha);
    }

    private void InverterPatrulha()
    {
        alvoPatrulha = Mathf.Abs(alvoPatrulha - (xInicial + raioPatrulha)) < 0.01f
            ? xInicial - raioPatrulha
            : xInicial + raioPatrulha;
        esperaPatrulha = pausaNaPonta;
    }

    /// <summary>Percebeu: para, encara e so' entao parte pra cima. Da' um tempo de leitura.</summary>
    private void Alertar()
    {
        animator.SetBool(WalkingParam, false);
        if (player != null) Encarar(Mathf.Sign(player.position.x - transform.position.x));
        MoverComColisao(0f);

        if (tempoNoEstado >= 0.3f) TrocarPara(Estado.Perseguir);
    }

    private void Perseguir()
    {
        if (!LembraDoJogador) { TrocarPara(Estado.Procurar); return; }

        Vector3 destino = VendoAgora ? player.position : ultimaPosicaoVista;
        float dx = destino.x - transform.position.x;
        float dir = Mathf.Sign(dx);
        Encarar(dir);

        if (VendoAgora && DistanciaX <= distanciaDeGolpe)
        {
            animator.SetBool(WalkingParam, false);
            MoverComColisao(0f);
            if (Time.time >= proximoAtaqueLiberado) TrocarPara(Estado.Mirar);
            return;
        }

        if (!TemChaoAFrente(dir)) { animator.SetBool(WalkingParam, false); MoverComColisao(0f); return; }

        animator.SetBool(WalkingParam, true);
        MoverComColisao(dir * velocidade);
    }

    /// <summary>Telegrafo: parado e encarando por um instante antes do golpe sair.</summary>
    private void Mirar()
    {
        animator.SetBool(WalkingParam, false);
        if (player != null) Encarar(Mathf.Sign(player.position.x - transform.position.x));
        MoverComColisao(0f);

        if (tempoNoEstado < tempoDeMira) return;

        // se voce saiu do alcance durante a mira, ele nao golpeia o ar
        if (DistanciaX > alcanceDano * 1.25f) { TrocarPara(Estado.Perseguir); return; }

        animator.SetTrigger(AttackTrigger);
        danoAplicadoNesteGolpe = false;
        TrocarPara(Estado.Atacar);
    }

    private void Atacar(AnimatorStateInfo info, bool naAnimacao)
    {
        animator.SetBool(WalkingParam, false);
        MoverComColisao(0f);

        // ainda em transicao pro estado de ataque
        if (!naAnimacao)
        {
            if (tempoNoEstado > 0.6f) TrocarPara(Estado.Recuar);
            return;
        }

        float t = info.normalizedTime % 1f;
        if (!danoAplicadoNesteGolpe && t >= momentoDoGolpe)
        {
            danoAplicadoNesteGolpe = true;
            AplicarDano();
        }

        if (t >= 0.85f)
        {
            proximoAtaqueLiberado = Time.time + cooldownAtaque;
            TrocarPara(Estado.Recuar);
        }
    }

    private void AplicarDano()
    {
        if (player == null) return;
        float diferencaX = player.position.x - transform.position.x;

        // Acerta dos DOIS lados (distancia em modulo), mas so' quem esta na FRENTE:
        // quem escapou pelas costas no meio do golpe nao leva.
        bool naFrente = Mathf.Sign(diferencaX) == Mathf.Sign(facing);
        if (Mathf.Abs(diferencaX) <= alcanceDano && naFrente)
        {
            // Passa a posicao DO INIMIGO: e' dela que sai a direcao do empurrao.
            player.GetComponent<IDamageable>()?.TakeHit(dano, transform.position);
        }
    }

    /// <summary>Depois de golpear, afasta. E' o que cria espaco pra voce revidar.</summary>
    private void Recuar()
    {
        if (player == null || !LembraDoJogador) { TrocarPara(Estado.Procurar); return; }

        float paraPlayer = Mathf.Sign(player.position.x - transform.position.x);
        Encarar(paraPlayer);   // recua de frente, sem dar as costas

        bool longeOSuficiente = DistanciaX >= distanciaDeRecuo;
        bool semChaoAtras = !TemChaoAFrente(-paraPlayer);

        if (tempoNoEstado >= tempoDeRecuo || longeOSuficiente || semChaoAtras)
        {
            animator.SetBool(WalkingParam, false);
            MoverComColisao(0f);
            TrocarPara(Estado.Perseguir);
            return;
        }

        animator.SetBool(WalkingParam, true);
        MoverComColisao(-paraPlayer * velocidade * 0.75f);
    }

    /// <summary>Perdeu de vista: vai ate' onde viu por ultimo e olha em volta.</summary>
    private void Procurar()
    {
        if (VendoAgora) { TrocarPara(Estado.Alerta); return; }

        float dx = ultimaPosicaoVista.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.4f && tempoNoEstado < tempoProcurando)
        {
            float dir = Mathf.Sign(dx);
            Encarar(dir);
            if (TemChaoAFrente(dir))
            {
                animator.SetBool(WalkingParam, true);
                MoverComColisao(dir * velocidade * 0.8f);
                return;
            }
        }

        animator.SetBool(WalkingParam, false);
        MoverComColisao(0f);

        // olha pros dois lados antes de desistir
        if (tempoNoEstado > tempoProcurando * 0.5f && tempoNoEstado < tempoProcurando * 0.55f)
            Encarar(-facing);

        if (tempoNoEstado >= tempoProcurando) TrocarPara(Estado.Patrulha);
    }

    private Estado EstadoDeCombateOuPatrulha() => LembraDoJogador ? Estado.Perseguir : Estado.Patrulha;

    private void TrocarPara(Estado novo)
    {
        estado = novo;
        tempoNoEstado = 0f;
    }

    // ---------------------------------------------------------------- utilitarios

    /// <summary>Raio pra baixo um pouco a frente: e' o que impede de andar pro vao.</summary>
    private bool TemChaoAFrente(float dir)
    {
        Vector3 p = transform.position + Vector3.right * dir * 0.55f + Vector3.up * 0.4f;
        return Physics.Raycast(p, Vector3.down, 1.4f, bloqueiaVisao, QueryTriggerInteraction.Ignore);
    }

    private bool TemParedeAFrente(float dir)
    {
        Vector3 p = transform.position + Vector3.up * 0.9f;
        if (!Physics.Raycast(p, Vector3.right * dir, out var h, 0.7f, bloqueiaVisao, QueryTriggerInteraction.Ignore))
            return false;
        return player == null || !h.collider.transform.IsChildOf(player);
    }

    private void Encarar(float dir)
    {
        if (Mathf.Abs(dir) < 0.01f) return;
        float alvo = Mathf.Sign(dir);
        if (alvo == facing) return;
        facing = alvo;
        AplicarFacing();
    }

    private void MoverComColisao(float horizontal)
    {
        if (controller.isGrounded && velocidadeVertical < 0f)
            velocidadeVertical = -2f;
        else
            velocidadeVertical += gravidade * Time.deltaTime;

        // Separacao contra o jogador fica no SeparacaoDeCorpos (penetracao real dos colisores).
        controller.Move(new Vector3(horizontal, velocidadeVertical, 0f) * Time.deltaTime);
    }

    private void AplicarFacing()
    {
        if (visual == null) return;
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, facing * Mathf.Abs(s.z));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * alturaOlho, raioVisao);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, raioAudicao);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, alcanceDano);
    }
}
