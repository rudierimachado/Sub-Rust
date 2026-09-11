using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chefe da Capela 12. Nao substitui o EnemyAI: fica AO LADO dele e reescreve os
/// numeros conforme a vida cai, mais a arena (trava a saida) e o aviso de fase.
///
/// POR QUE APROVEITAR O EnemyAI EM VEZ DE UMA IA NOVA
/// A IA existente ja resolve percepcao com linha de visada, memoria, telegrafo de
/// golpe, recuo, borda de precipicio e atordoamento por dano. Reescrever isso pra
/// um chefe so' daria uma segunda IA pra manter, divergindo da primeira a cada
/// ajuste. O que falta num chefe nao e' percepcao - e' RITMO: ele precisa mudar de
/// comportamento conforme apanha, e e' exatamente isso que este script faz.
///
/// FASES
/// Cada fase reescreve velocidade/cooldown/dano no EnemyAI por reflexao sobre os
/// campos serializados. E' feio, mas a alternativa era tornar publico meio EnemyAI
/// so' pra o chefe mexer - e ai qualquer script poderia bagunçar inimigo comum.
///
/// ARENA
/// Ao entrar, uma parede invisivel fecha a passagem de volta ate o chefe morrer.
/// Sem isso, sair da sala reseta a luta pela metade e o chefe vira um inimigo
/// comum que voce mata em duas visitas.
///
/// Anexar na RAIZ do chefe, junto com EnemyAI/EnemyHealth.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAI))]
public class ChefeCapela : MonoBehaviour
{
    [System.Serializable]
    public class Fase
    {
        [Tooltip("A fase entra quando a vida cai ABAIXO desta fracao.")]
        [Range(0f, 1f)] public float vidaAbaixoDe = 1f;
        public string nome = "Fase";
        public float velocidade = 4.4f;
        public float cooldownAtaque = 0.9f;
        public float tempoDeMira = 0.22f;
        public float dano = 250f;
        [Tooltip("Multiplicador do clipe de ataque. Acima de 1 o golpe sai mais rapido.")]
        public float velocidadeDoClipe = 1f;
        [Tooltip("Cor que o corpo assume nesta fase (pulso). Alpha 0 = sem tingir.")]
        public Color tom = new Color(1f, 1f, 1f, 0f);
    }

    [Header("Fases (de cima pra baixo, por vida decrescente)")]
    [SerializeField] private Fase[] fases;

    [Header("Arena")]
    [Tooltip("Trava a passagem enquanto a luta acontece. Deixe vazio pra nao travar.")]
    [SerializeField] private Transform paredeDaArena;
    [Tooltip("Distancia do jogador que dispara a luta.")]
    [SerializeField] private float distanciaParaIniciar = 12f;

    [Header("Apresentacao")]
    [SerializeField] private Light luzDoChefe;
    [SerializeField] private float intensidadeLuzBase = 30f;
    [Tooltip("Pulso da luz quando muda de fase.")]
    [SerializeField] private float brilhoNaTroca = 3f;

    private EnemyHealth vida;
    private EnemyAI ia;
    private Animator animator;
    private readonly List<Renderer> corpo = new();
    private readonly List<Color> corOriginal = new();

    private int faseAtual = -1;
    private bool lutaComecou;
    private float pulso;

    private void Awake()
    {
        vida = GetComponent<EnemyHealth>();
        ia = GetComponent<EnemyAI>();
        animator = GetComponentInChildren<Animator>();

        foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            corpo.Add(r);
            // .material (instancia) e nao .sharedMaterial: tingir o compartilhado
            // pintaria TODOS os esqueletos da fase junto.
            corOriginal.Add(r.material.HasProperty("_BaseColor") ? r.material.GetColor("_BaseColor") : Color.white);
        }

        if (paredeDaArena != null) paredeDaArena.gameObject.SetActive(false);
        if (luzDoChefe != null) luzDoChefe.intensity = intensidadeLuzBase;
    }

    private void Update()
    {
        if (!lutaComecou) { VerificarInicio(); return; }
        AtualizarFase();
        PulsarLuz();
    }

    private void VerificarInicio()
    {
        var jogador = GameObject.FindGameObjectWithTag("Player");
        if (jogador == null) return;
        if (Vector3.Distance(jogador.transform.position, transform.position) > distanciaParaIniciar) return;

        lutaComecou = true;
        if (paredeDaArena != null) paredeDaArena.gameObject.SetActive(true);
        AplicarFase(0);
    }

    private void AtualizarFase()
    {
        if (fases == null || fases.Length == 0) return;

        float fracao = vida.MaxHealth > 0f ? vida.Health / vida.MaxHealth : 0f;

        // Procura a fase mais avancada cuja condicao ja foi cruzada.
        int alvo = 0;
        for (int i = 0; i < fases.Length; i++)
            if (fracao <= fases[i].vidaAbaixoDe) alvo = i;

        if (alvo != faseAtual) AplicarFase(alvo);
    }

    private void AplicarFase(int indice)
    {
        if (fases == null || indice < 0 || indice >= fases.Length) return;
        faseAtual = indice;
        var f = fases[indice];

        EscreverNoEnemyAI("velocidade", f.velocidade);
        EscreverNoEnemyAI("cooldownAtaque", f.cooldownAtaque);
        EscreverNoEnemyAI("tempoDeMira", f.tempoDeMira);
        EscreverNoEnemyAI("dano", f.dano);

        // Velocidade do clipe de ataque: o dano usa normalizedTime, entao acelerar
        // o clipe NAO desincroniza o golpe (ver PROJETO.md).
        if (animator != null) animator.SetFloat("VelocidadeAtaque", f.velocidadeDoClipe);

        if (f.tom.a > 0f)
            for (int i = 0; i < corpo.Count; i++)
                if (corpo[i] != null && corpo[i].material.HasProperty("_BaseColor"))
                    corpo[i].material.SetColor("_BaseColor", Color.Lerp(corOriginal[i], f.tom, f.tom.a));

        pulso = 1f;
        Debug.Log($"[ChefeCapela] fase {indice}: {f.nome} (vida {(vida.Health / vida.MaxHealth):P0})", this);
    }

    /// <summary>Escreve num campo serializado privado do EnemyAI.
    ///
    /// Reflexao aqui e' deliberado: tornar esses campos publicos abriria pra
    /// qualquer script alterar inimigo comum em runtime. O custo e' que renomear
    /// um campo no EnemyAI quebra AQUI em silencio - por isso o aviso no console.</summary>
    private void EscreverNoEnemyAI(string campo, float valor)
    {
        var f = typeof(EnemyAI).GetField(campo,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null)
        {
            Debug.LogWarning($"[ChefeCapela] campo '{campo}' nao existe mais no EnemyAI - fase nao aplicada.", this);
            return;
        }
        f.SetValue(ia, valor);
    }

    private void PulsarLuz()
    {
        if (luzDoChefe == null) return;
        pulso = Mathf.MoveTowards(pulso, 0f, Time.deltaTime * 1.6f);
        luzDoChefe.intensity = intensidadeLuzBase * (1f + pulso * (brilhoNaTroca - 1f));
    }

    /// <summary>Chamado pelo EnemyHealth ao morrer (ver OnDestroy dele) ou aqui
    /// mesmo quando o objeto some: libera a arena de qualquer jeito, senao o
    /// jogador ficaria preso numa sala sem chefe.</summary>
    private void OnDestroy()
    {
        if (paredeDaArena != null) paredeDaArena.gameObject.SetActive(false);
    }
}
