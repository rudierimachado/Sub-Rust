using UnityEngine;

/// <summary>
/// Vida do inimigo simples. Ao morrer, larga almas e (as vezes) um frasco, e some.
/// Se o Animator tiver um estado "Dead", toca o clipe antes de destruir o objeto.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    private static readonly int EstadoDead = Animator.StringToHash("Dead");
    [SerializeField] private float maxHealth = 150f;
    [Tooltip("Folego perdido por PONTO de dano recebido. Com 0,30 e uma espada de 34, " +
             "cada golpe tira ~10 de stamina: sozinho, o dano quase nunca quebra a " +
             "guarda (seriam 10 golpes). E' acelerador, nao o caminho - quem quebra a " +
             "guarda e' o escudo (ver custoDeSerAparado no EnemyAI).")]
    [SerializeField] private float folegoPorDano = 0.30f;
    [SerializeField] private int almasAoMorrer = 3;
    [SerializeField] private float chanceFrasco = 0.3f;

    [Tooltip("Se este inimigo pode ser morto pela FINALIZACAO (tecla F na guarda " +
             "quebrada). " +
             "DESLIGAR EM CHEFE. A finalizacao mata na hora, ignorando a vida: com ela " +
             "ligada, 10 golpes de espada zeram a stamina da Provedora e o F apaga uma " +
             "chefa de 520 de vida como se fosse um esqueleto. Chefe tem que cair pela " +
             "vida, nao por um atalho.")]
    [SerializeField] private bool podeSerFinalizado = true;

    /// <summary>Alvo valido pra finalizacao. Ver podeSerFinalizado.</summary>
    public bool PodeSerFinalizado => podeSerFinalizado && !morto;

    private float health;
    private bool morto;
    private EnemyDamageFeedback feedback;
    private EnemyStamina folego;

    public float Health => health;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        health = maxHealth;
        feedback = GetComponent<EnemyDamageFeedback>();
        folego = GetComponent<EnemyStamina>();
    }

    /// <summary>Trava a vitima pela duracao da finalizacao: para de aceitar dano e nao
    /// morre sozinha. Sem isto um segundo golpe (ou outro inimigo) podia destruir o
    /// objeto no meio da cena e deixar a camera olhando pro vazio.
    ///
    /// Nao paga recompensa nem destroi - quem faz isso e' ConcluirFinalizacao.</summary>
    public void PrepararFinalizacao()
    {
        morto = true;
    }

    /// <summary>Fecha a finalizacao: paga as almas com bonus e some com o corpo.</summary>
    public void ConcluirFinalizacao(float multiplicadorDeAlmas)
    {
        int almas = Mathf.RoundToInt(almasAoMorrer * Mathf.Max(1f, multiplicadorDeAlmas));
        if (almas > 0)
        {
            PlayerCurrency.AddSouls(almas);
            NumeroFlutuante.Mostrar(transform.position + Vector3.up * 1.6f,
                                    "+" + almas,
                                    new Color(1f, 0.85f, 0.35f));
        }

        // Frasco garantido: executar exige gastar a janela de exaustao chegando perto
        // de um inimigo ainda vivo, em vez de so' bater de longe.
        CriarPickup(Pickup.Tipo.Frasco, 1, transform.position + Vector3.up * 0.5f);

        Destroy(gameObject);
    }

    public void TakeHit(float damage, Vector3 hitPoint)
    {
        if (morto) return;
        health -= damage;

        // Apanhar cansa: pressao constante ajuda a quebrar a guarda, mesmo sem escudo.
        // Depois de morto nao ha' folego a perder.
        folego?.Drenar(damage * folegoPorDano);

        if (health <= 0f) { Morrer(); return; }

        // passa o dano: o empurrao escala com a pancada (ver EnemyDamageFeedback)
        feedback?.Reagir(hitPoint, damage);
    }

    private void Morrer()
    {
        morto = true;

        // ALMA: soma direto, sem item pra catar no chao.
        // Alma que cai no chao obriga o jogador a voltar buscar depois de cada luta,
        // o que quebra o ritmo do combate e ainda some se cair num buraco. O feedback
        // vira o numero subindo de onde o inimigo morreu (ver NumeroFlutuante) - o
        // jogador ve quanto ganhou e de quem, sem parar de lutar.
        if (almasAoMorrer > 0)
        {
            PlayerCurrency.AddSouls(almasAoMorrer);
            NumeroFlutuante.Mostrar(transform.position + Vector3.up * 1.6f,
                                    "+" + almasAoMorrer,
                                    new Color(0.35f, 1f, 0.75f));
        }

        // REMEDIO: este sim continua sendo item, porque e' um recurso que o jogador
        // decide QUANDO pegar. Mas e' coletado automaticamente ao encostar.
        if (Random.value < chanceFrasco)
            CriarPickup(Pickup.Tipo.Frasco, 1, transform.position + Vector3.up * 0.5f);

        if (TentarMortoAnimado())
            return;

        Destroy(gameObject);
    }

    /// <summary>Estado "Dead" no Animator Controller: desliga a IA, toca o clipe e
    /// destrói no fim. Inimigos sem esse estado (ex.: esqueleto antigo) caem no
    /// Destroy imediato de sempre.</summary>
    private bool TentarMortoAnimado()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.HasState(0, EstadoDead))
            return false;

        PararComportamentoNaMorte();

        animator.SetBool("Walking", false);
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("Hit");
        animator.CrossFade(EstadoDead, 0.08f, 0);

        float duracao = DuracaoDoClipDead(animator);
        Destroy(gameObject, duracao);
        return true;
    }

    private void PararComportamentoNaMorte()
    {
        var ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        foreach (var b in GetComponents<MonoBehaviour>())
        {
            if (b is EnemyStamina || b is SeparacaoDeCorpos || b is ExaustaoDoInimigo || b is BarraDoInimigo)
                b.enabled = false;
        }
    }

    private static float DuracaoDoClipDead(Animator animator)
    {
        var ctrl = animator.runtimeAnimatorController;
        if (ctrl != null)
        {
            foreach (var clip in ctrl.animationClips)
            {
                if (clip == null || clip.name.EndsWith(".001")) continue;
                if (clip.name.Contains("Dead"))
                    return clip.length;
            }
        }

        return 2.5f;
    }

    private static void CriarPickup(Pickup.Tipo tipo, int quantidade, Vector3 posicao)
    {
        // Formato de FRASCO: capsula em pe' com um vidro em volta. O projeto nao tem
        // modelo de pocao (procurado: nenhum prefab de potion/flask/bottle/vial), e uma
        // esfera nao le' como remedio. Uma capsula vertical, sim.
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = tipo == Pickup.Tipo.Alma ? "SoulPickup" : "RemedioPickup";
        go.transform.position = posicao;
        go.transform.localScale = new Vector3(0.16f, 0.20f, 0.16f);

        var col = go.GetComponent<Collider>();
        col.isTrigger = true;

        var renderer = go.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Vermelho de remedio, bem saturado e emissivo: no chao de pedra escuro da
        // cozinha um vermelho fosco desaparece.
        Color cor = tipo == Pickup.Tipo.Alma ? new Color(0.1f, 0.9f, 0.6f) : new Color(0.95f, 0.12f, 0.16f);
        if (renderer.material.HasProperty("_BaseColor")) renderer.material.SetColor("_BaseColor", cor);
        if (renderer.material.HasProperty("_EmissionColor"))
        {
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", cor * 2.2f);
        }

        // Luz propria: e' o que faz o item ser VISTO no meio do cenario, mais que a
        // cor. Alcance curto pra nao virar iluminacao de sala.
        //
        // A escala local compensa a do item (0,16-0,20): o range de uma Light escala
        // com o transform do pai, entao sem isto os 2,2 m viravam 35 cm e a luz nao
        // saia de dentro do frasco.
        var luzGo = new GameObject("Brilho");
        luzGo.transform.SetParent(go.transform, false);
        luzGo.transform.localScale = new Vector3(1f / go.transform.localScale.x,
                                                 1f / go.transform.localScale.y,
                                                 1f / go.transform.localScale.z);
        var luz = luzGo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = cor;
        luz.intensity = 2.5f;
        luz.range = 2.2f;
        luz.shadows = LightShadows.None;

        var pickup = go.AddComponent<Pickup>();
        var tipoField = typeof(Pickup).GetField("tipo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var qtdField = typeof(Pickup).GetField("quantidade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        tipoField.SetValue(pickup, tipo);
        qtdField.SetValue(pickup, quantidade);
    }
}
