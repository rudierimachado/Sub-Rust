using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bloqueio com escudo no botao DIREITO do mouse.
///
/// Segurar levanta o escudo e reduz o dano recebido pela frente; soltar abaixa.
/// Enquanto bloqueia, o jogador nao ataca - senao daria pra bloquear e golpear no
/// mesmo frame, e o escudo viraria bonus sem custo.
///
/// POR QUE A REDUCAO E' SO' PELA FRENTE
/// Escudo que protege pelas costas transforma "segurar o botao" na jogada dominante
/// do combate inteiro. Aqui o golpe precisa vir do lado que o jogador ENCARA, o que
/// mantem o posicionamento importando - e num side-scroller isso e' so' comparar o X
/// de quem bateu com o facing, sem angulo nenhum.
///
/// Anexar no mesmo objeto do Animator (Visual), junto do PlayerCombat.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerBloqueio : MonoBehaviour
{
    [Tooltip("Fracao do dano que AINDA passa ao bloquear pela frente. 0,25 = leva 25%.")]
    [Range(0f, 1f)] [SerializeField] private float danoQuePassa = 0.25f;

    [Header("Custo de aparar")]
    [Tooltip("Stamina gasta a cada golpe aparado, independente do tamanho dele.")]
    [SerializeField] private float custoBase = 7f;   // escala 100 (ver PlayerHealth)
    [Tooltip("Stamina extra por ponto de dano aparado. Golpe pesado cansa mais o braco " +
             "que um de raspao - com custo fixo, aparar um martelo custaria o mesmo " +
             "que aparar um arranhao. " +
             "CALIBRADO, nao chutado: com o esqueleto batendo 20, o custo total sai " +
             "7 + 1,25*20 = 32 de 100, ou seja TRES golpes aparados esvaziam a barra. " +
             "E' a mesma fracao de antes da mudanca de escala (209 de 650).")]
    [SerializeField] private float custoPorDano = 1.25f;

    private static readonly int BloqueandoParam = Animator.StringToHash("Bloqueando");

    private Animator animator;
    private PlayerMovement2_5D movimento;

    /// <summary>True enquanto o escudo esta' levantado. O PlayerCombat le' isto pra
    /// nao deixar atacar bloqueando.</summary>
    public bool Bloqueando { get; private set; }

    [Tooltip("Segundos pra levantar/abaixar o escudo. Sem rampa o braco TELETRANSPORTA " +
             "pra pose de bloqueio no frame do clique.")]
    [SerializeField] private float tempoDeSubida = 0.10f;

    [Header("Correcao do braco (o clipe deixa o escudo alto e longe)")]
    [Tooltip("Graus no ombro esquerdo pra BAIXAR o braco (eixo X local). MEDIDO neste " +
             "rig: +25 no X desce a mao 0,02 m e -35 SOBE 0,15 - ou seja o X positivo " +
             "e' que baixa. O clipe do pacote deixa a mao a 1,05 m e 0,26 m a frente " +
             "do quadril: escudo alto e longe do corpo.")]
    [SerializeField] private float baixarOmbro = 30f;
    [Tooltip("Graus no ombro no eixo Z: e' o que RECOLHE o braco pra perto do corpo. " +
             "Medido: +25 no Z traz a mao 0,20 m pra tras sem mudar a altura.")]
    [SerializeField] private float recolherOmbro = 28f;
    [Tooltip("Graus no cotovelo (Z local): aproxima mais 0,10 m e baixa 0,04.")]
    [SerializeField] private float dobrarCotovelo = 20f;

    /// <summary>Se o ULTIMO golpe que passou pelo FiltrarDano foi aparado no escudo.
    ///
    /// POR QUE UMA FLAG E NAO UM EVENTO COM A ORIGEM
    /// O inimigo precisa saber que o golpe DELE foi aparado, pra perder folego por
    /// isso. O que chega aqui e' so' a posicao de quem bateu (o EnemyAI chama
    /// TakeHit(dano, transform.position)), e casar inimigo por posicao e' fragil -
    /// dois esqueletos colados dariam o folego ao errado.
    ///
    /// Como o EnemyAI aplica o dano de forma SINCRONA, ele le' esta flag na linha
    /// seguinte a que chamou TakeHit: nesse instante ela so' pode se referir ao golpe
    /// dele, sem ambiguidade nenhuma.</summary>
    public bool AparouOUltimoGolpe { get; private set; }

    [Header("Efeito de aparar")]
    [Tooltip("Altura do choque no escudo, do chao pra cima. E' onde nascem as faiscas.")]
    [SerializeField] private float alturaDoChoque = 1.15f;
    [Tooltip("Intensidade das faiscas ao aparar um golpe normal. Aparar da' faisca de " +
             "metal, nao sangue: quem apara nao se corta.")]
    [SerializeField] private float faiscasAoAparar = 1.3f;

    private int camadaEscudo = -1;
    private float peso;
    private Transform ombroEsq, cotoveloEsq;
    private PlayerHealth vida;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movimento = GetComponentInParent<PlayerMovement2_5D>();

        // Camada propria, mascarada so' no braco esquerdo: e' o que deixa o escudo
        // subir sem roubar o braco direito, entao da' pra atacar bloqueando.
        // Indice buscado por NOME - inserir camada nova reordena os indices e
        // quebraria isto em silencio.
        camadaEscudo = animator.GetLayerIndex("BracoEscudo");
        if (camadaEscudo < 0)
            Debug.LogError("Camada 'BracoEscudo' nao existe no Animator - o escudo nao vai subir.", this);

        // Ossos pelo AVATAR humanoide, nao por nome: continua funcionando se o rig mudar.
        ombroEsq = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        cotoveloEsq = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
    }

    /// <summary>Corrige a pose DEPOIS que o Animator escreveu (por isso LateUpdate):
    /// baixa o ombro e dobra o cotovelo proporcional ao peso do bloqueio, entao a
    /// correcao entra e sai junto com o escudo em vez de aparecer de uma vez.
    ///
    /// Soma por cima da rotacao do frame, sem guardar pose de repouso - o Animator
    /// reescreve localRotation todo frame, entao nao acumula.</summary>
    private void LateUpdate()
    {
        if (peso <= 0.001f) return;

        // X baixa, Z recolhe - os dois eixos medidos neste rig (ver tooltips).
        if (ombroEsq != null)
            ombroEsq.localRotation *= Quaternion.Euler(baixarOmbro * peso, 0f, recolherOmbro * peso);
        if (cotoveloEsq != null)
            cotoveloEsq.localRotation *= Quaternion.Euler(0f, 0f, dobrarCotovelo * peso);
    }

    private void Update()
    {
        // Exausto nao levanta escudo: e' o que faz zerar a barra ter consequencia
        // em vez de ser so' um numero baixo na tela.
        if (vida == null) vida = GetComponentInParent<PlayerHealth>();
        bool exausto = vida != null && vida.Exausto;

        bool segurando = !exausto && Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (segurando != Bloqueando)
        {
            Bloqueando = segurando;
            animator.SetBool(BloqueandoParam, Bloqueando);
        }

        if (camadaEscudo < 0) return;

        // A camada nasce com peso 0; sem subir o peso o estado ate' entra mas nao move
        // osso nenhum - foi exatamente esse o motivo do escudo nao funcionar antes.
        peso = Mathf.MoveTowards(peso, Bloqueando ? 1f : 0f,
                                 Time.deltaTime / Mathf.Max(0.01f, tempoDeSubida));
        animator.SetLayerWeight(camadaEscudo, peso);
    }

    /// <summary>Quanto do dano realmente passa. Quem aplica dano no jogador chama isto
    /// antes de descontar a vida. Devolve 1 (dano cheio) quando nao ha' bloqueio ou
    /// quando o golpe vem pelas costas.
    ///
    /// Aparar CUSTA STAMINA, proporcional a pancada: sem custo, segurar o botao seria
    /// a jogada dominante do combate inteiro e nao haveria decisao nenhuma. Sem
    /// stamina o escudo ABRE - o golpe passa inteiro e o jogador fica exposto, que e'
    /// a punicao por bloquear tudo em vez de escolher a hora.</summary>
    public float FiltrarDano(Vector3 origemDoGolpe, float dano)
    {
        AparouOUltimoGolpe = false;

        if (!Bloqueando) return 1f;

        Vector3 frente = movimento != null ? movimento.Direcao : Vector3.right;
        bool pelaFrente;
        if (movimento != null && movimento.TerceiraPessoa)
        {
            // Terceira pessoa: vale o meio-espaco a frente do corpo (180 graus), no plano.
            Vector3 paraGolpe = origemDoGolpe - transform.position;
            paraGolpe.y = 0f;
            pelaFrente = Vector3.Dot(paraGolpe, frente) > 0f;
        }
        else
        {
            float facing = Mathf.Sign(frente.x);
            pelaFrente = Mathf.Approximately(facing, Mathf.Sign(origemDoGolpe.x - transform.position.x));
        }

        // Golpe pelas costas: escudo nao ajuda, e nem gasta stamina.
        if (!pelaFrente) return 1f;

        if (vida == null) vida = GetComponentInParent<PlayerHealth>();
        float custo = custoBase + dano * custoPorDano;

        if (vida != null && !vida.TryGastarStamina(custo))
        {
            // Guarda quebrada: escudo abre, o golpe passa inteiro e o folego vai a
            // ZERO. Sem esgotar, o jogador sairia de uma guarda quebrada ainda com
            // saldo na barra - castigo menor que o que o inimigo leva pelo mesmo erro.
            vida.EsgotarStamina();
            Bloqueando = false;
            animator.SetBool(BloqueandoParam, false);
            NumeroFlutuante.Mostrar(transform.position + Vector3.up * 1.9f, "GUARDA QUEBRADA",
                                    new Color(1f, 0.6f, 0.2f), 2.2f);
            return 1f;
        }

        // APAROU. As faiscas saem no escudo, viradas pra quem bateu - e' o retorno
        // visual de que o golpe morreu no metal em vez de ter passado.
        AparouOUltimoGolpe = true;

        Vector3 ponto = transform.position + Vector3.up * alturaDoChoque
                      + frente * 0.35f;
        ImpactoDeGolpe.Faiscar(ponto, -frente + Vector3.up * 0.35f,
                               faiscasAoAparar * Mathf.Clamp(dano / 150f, 0.6f, 1.8f));

        return danoQuePassa;
    }
}
