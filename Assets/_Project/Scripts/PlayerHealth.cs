using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vida e stamina do jogador. Stamina regenera sozinha e e' gasta por acoes de combate;
/// vida so' muda por dano (IDamageable) ou frasco (tecla Q, gasta PlayerCurrency.Potions).
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenPerSecond = 18f;
    [SerializeField] private float potionHealAmount = 40f;

    [Header("Folego")]
    [Tooltip("Segundos de espera antes da stamina voltar a encher, contados do ultimo " +
             "gasto. E' ESTA PAUSA que transforma stamina em decisao: antes a barra " +
             "comecava a regenerar no MESMO frame do gasto, a 15% por segundo, e o " +
             "resultado pratico era que bloquear nao custava nada.")]
    [SerializeField] private float atrasoParaRegenerar = 0.8f;

    [Tooltip("Segundos travado quando a stamina zera. Simetrico a guarda quebrada do " +
             "inimigo: se esgotar a barra nao tivesse consequencia, gastar ate' o fim " +
             "seria sempre a jogada certa.")]
    [SerializeField] private float duracaoDaExaustao = 1.2f;

    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float Health { get; private set; }
    public float Stamina { get; private set; }
    public bool Morto { get; private set; }

    /// <summary>Ligado pela esquiva: e' o que transforma o dash em ESQUIVA de verdade,
    /// em vez de so' um deslocamento rapido.</summary>
    public bool Invulneravel { get; set; }

    /// <summary>Stamina zerada: nao ataca, nao esquiva e nao bloqueia ate' recuperar.
    /// Quem le' input de combate consulta isto.</summary>
    public bool Exausto { get; private set; }

    /// <summary>Quanto falta da exaustao, de 1 (acabou de zerar) a 0. A HUD usa pra
    /// piscar a barra no ritmo certo.</summary>
    public float ProgressoDaExaustao =>
        Exausto ? Mathf.Clamp01((exaustaoTerminaEm - Time.time) / Mathf.Max(0.01f, duracaoDaExaustao)) : 0f;

    public static event Action<float, float> OnHealthChanged;
    public static event Action<float, float> OnStaminaChanged;
    public static event Action OnMorte;

    /// <summary>(dano, posicao de onde veio o golpe) - quem reage ao impacto escuta aqui.</summary>
    public static event Action<float, Vector3> OnDano;

    /// <summary>Disparado no instante em que a stamina zera, e de novo ao recuperar
    /// (bool: true = entrou em exaustao). Quem faz efeito escuta aqui em vez de
    /// procurar a borda consultando Exausto todo frame.</summary>
    public static event Action<bool> OnExaustao;

    // Estes eventos sao ESTATICOS (pensados para um Player so'). Com dois Players na
    // mesma cena (o local e a copia de rede do outro jogador), so' o DONO desta
    // instancia pode disparar - senao a HUD local piscaria entre a vida dos dois.
    // PlayerNetwork.EhDono ja cobre Solo (sem rede) e Co-op (IsOwner) num lugar so'.
    private float podeRegenerarEm;
    private float exaustaoTerminaEm;

    private PlayerNetwork rede;
    private bool DevoAvisarHud => rede == null || rede.EhDono;

    private void Awake()
    {
        Health = maxHealth;
        Stamina = maxStamina;
        rede = GetComponent<PlayerNetwork>();
    }

    private void Start()
    {
        PublicarEstado();
    }

    /// <summary>Re-dispara os eventos de vida/stamina com os valores atuais.
    /// Existe porque a HUD vive numa cena aditiva (Core) que costuma carregar DEPOIS
    /// do Start daqui: sem isso ela assina os eventos tarde demais e nasce vazia.</summary>
    public void PublicarEstado()
    {
        if (!DevoAvisarHud) return;
        OnHealthChanged?.Invoke(Health, maxHealth);
        OnStaminaChanged?.Invoke(Stamina, maxStamina);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            UsarFrasco();
    }

    private void LateUpdate()
    {
        if (Exausto)
        {
            if (Time.time < exaustaoTerminaEm) return;   // travado: nem age, nem regenera
            Exausto = false;
            if (DevoAvisarHud) OnExaustao?.Invoke(false);
        }

        // A PAUSA antes de regenerar e' o que da' peso ao gasto - ver atrasoParaRegenerar.
        if (Time.time < podeRegenerarEm || Stamina >= maxStamina) return;

        Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);
        if (DevoAvisarHud) OnStaminaChanged?.Invoke(Stamina, maxStamina);
    }

    /// <returns>true se tinha stamina e gastou.</returns>
    ///
    /// <remarks>Gastar ate' o ZERO exato entra em exaustao. Repare que gastar quase
    /// tudo e ficar com 1 de sobra NAO entra: quem administra a barra com cuidado nao
    /// e' punido, so' quem raspa o fundo.</remarks>
    public bool TryGastarStamina(float custo)
    {
        if (Exausto || Stamina < custo) return false;

        Stamina -= custo;
        podeRegenerarEm = Time.time + atrasoParaRegenerar;
        if (DevoAvisarHud) OnStaminaChanged?.Invoke(Stamina, maxStamina);

        if (Stamina <= 0f) EntrarEmExaustao();
        return true;
    }

    /// <summary>Zera o folego a forca - usado quando o custo e' MAIOR que o saldo e a
    /// acao acontece assim mesmo (guarda quebrada no PlayerBloqueio). Sem isto o
    /// jogador terminaria uma guarda quebrada ainda com stamina sobrando, o que faria
    /// o castigo parecer menor que o do inimigo.</summary>
    public void EsgotarStamina()
    {
        Stamina = 0f;
        podeRegenerarEm = Time.time + atrasoParaRegenerar;
        if (DevoAvisarHud) OnStaminaChanged?.Invoke(Stamina, maxStamina);
        EntrarEmExaustao();
    }

    private void EntrarEmExaustao()
    {
        if (Exausto) return;
        Exausto = true;
        exaustaoTerminaEm = Time.time + duracaoDaExaustao;
        if (DevoAvisarHud) OnExaustao?.Invoke(true);
    }

    private PlayerBloqueio bloqueio;

    public void TakeHit(float damage, Vector3 hitPoint)
    {
        if (Morto || Invulneravel) return;

        // Escudo levantado corta o dano que vem PELA FRENTE (ver PlayerBloqueio).
        // Resolvido aqui, no unico ponto por onde todo dano ao jogador passa - em vez
        // de cada fonte de dano ter que lembrar de perguntar se ha' bloqueio.
        if (bloqueio == null) bloqueio = GetComponentInChildren<PlayerBloqueio>();
        if (bloqueio != null) damage *= bloqueio.FiltrarDano(hitPoint, damage);

        Health = Mathf.Clamp(Health - damage, 0f, maxHealth);
        if (DevoAvisarHud)
        {
            OnHealthChanged?.Invoke(Health, maxHealth);
            OnDano?.Invoke(damage, hitPoint);
        }

        if (Health <= 0f) Morrer();
    }

    private static readonly int MorrerTrigger = Animator.StringToHash("Morrer");

    /// <summary>Mata o jogador AGORA, sem passar por dano. Usado pela queda no abismo,
    /// onde nao ha' impacto nenhum pra converter em dano - ele so' cai pra sempre.</summary>
    public void MatarInstantaneamente()
    {
        if (Morto) return;
        Health = 0f;
        if (DevoAvisarHud) OnHealthChanged?.Invoke(Health, maxHealth);
        Morrer();
    }

    private void Morrer()
    {
        Morto = true;
        Invulneravel = true;   // nada mais o atinge enquanto o corpo cai
        if (DevoAvisarHud) OnMorte?.Invoke();

        // Desliga tudo que le' input.
        //
        // ATENCAO: desligar o PlayerMovement2_5D tira a GRAVIDADE junto - ela mora
        // inteira no Update dele. Um CharacterController nao cai sozinho: se ninguem
        // chama Move(), o corpo simplesmente congela onde estava e a animacao de morte
        // toca no ar. Por isso AssentarCorpo() abaixo continua puxando o corpo pro chao.
        var movimento = GetComponent<PlayerMovement2_5D>();
        if (movimento != null) movimento.enabled = false;
        var combate = GetComponentInChildren<PlayerCombat>();
        if (combate != null) combate.enabled = false;
        var bloqueio = GetComponentInChildren<PlayerBloqueio>();
        if (bloqueio != null) bloqueio.enabled = false;
        var esquiva = GetComponent<PlayerDodge>();
        if (esquiva != null) esquiva.enabled = false;

        // A ANIMACAO DE MORTE. Vai por AnyState no Animator, entao interrompe qualquer
        // estado - inclusive escalada e ataque, que e' onde a morte costuma pegar.
        var visual = transform.Find("Visual");
        var animator = visual != null ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // O tempo pode estar em hitstop no frame da morte; a animacao roda em tempo
            // escalado e ficaria em camera lenta. Devolver a escala aqui tambem evita
            // que um hitstop interrompido deixe o jogo lento para sempre.
            Time.timeScale = 1f;

            // ZERA AS CAMADAS DE CIMA. TroncoAtaque e BracoEscudo escrevem por cima da
            // Base Layer; morrer no meio de um golpe (que e' quando a morte costuma
            // acontecer) deixaria o tronco preso na pose de ataque enquanto as pernas
            // desabam. Os scripts que controlam esses pesos acabaram de ser desligados,
            // entao ninguem mais os abaixaria.
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);

            animator.SetTrigger(MorrerTrigger);
        }

        // A exaustao dobra a coluna em LateUpdate; num corpo caido isso vira o cadaver
        // se contorcendo. O estado critico (vinheta/batimento) fica LIGADO de proposito
        // - ele acompanha bem o apagar.
        var exaustao = GetComponent<PlayerExaustao>();
        if (exaustao != null) exaustao.enabled = false;

        StartCoroutine(AssentarCorpo());
        StartCoroutine(RespawnAposDelay());
    }

    [Tooltip("Gravidade aplicada ao corpo depois de morto. Igual a' do " +
             "PlayerMovement2_5D pra queda ter o mesmo peso do resto do jogo.")]
    [SerializeField] private float gravidadeDoCorpo = -25f;

    /// <summary>Continua puxando o corpo pro chao depois da morte.
    ///
    /// Sem isto o cadaver fica FLUTUANDO: o PlayerMovement2_5D, que e' quem chama
    /// controller.Move() com a gravidade, acabou de ser desligado, e o
    /// CharacterController nao cai por conta propria.
    ///
    /// Para quando encosta no chao, mas segue tentando por um tempo - morrer no ar
    /// (empurrado de uma plataforma) e' comum, e a queda pode ser longa.</summary>
    private System.Collections.IEnumerator AssentarCorpo()
    {
        var controller = GetComponent<CharacterController>();
        if (controller == null) yield break;

        float vertical = 0f;
        float limite = 0f;

        while (limite < 8f)
        {
            limite += Time.deltaTime;

            if (controller.isGrounded)
            {
                // Cola no chao em vez de zerar: sem uma forca pra baixo o isGrounded
                // fica piscando e o corpo treme sobre o piso.
                vertical = -2f;
                controller.Move(Vector3.up * vertical * Time.deltaTime);
                yield return null;
                continue;
            }

            vertical += gravidadeDoCorpo * Time.deltaTime;
            controller.Move(Vector3.up * vertical * Time.deltaTime);
            yield return null;
        }
    }

    private System.Collections.IEnumerator RespawnAposDelay()
    {
        // Deixa a animacao de morte (2,30 s) acontecer antes da tela cobrir tudo - a
        // tela existe pra confirmar o fracasso, nao pra esconder o que causou ele.
        yield return new WaitForSecondsRealtime(1.5f);

        bool liberado = false;
        TelaDeMorte.Mostrar(() => liberado = true);
        while (!liberado) yield return null;

        // Recarregar a cena INTEIRA so' faz sentido sem rede: em Co-op isso
        // derrubaria a fase do outro jogador tambem, cada cliente por conta
        // propria e fora de sincronia com o host. Ate existir um respawn de
        // posicao de verdade, em rede o personagem so' volta a responder.
        if (RedeSessao.Solo)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            yield break;
        }

        Health = maxHealth;
        Morto = false;
        var movimento = GetComponent<PlayerMovement2_5D>();
        if (movimento != null) movimento.enabled = true;
        var combate = GetComponentInChildren<PlayerCombat>();
        if (combate != null) combate.enabled = true;
        if (DevoAvisarHud) OnHealthChanged?.Invoke(Health, maxHealth);
    }

    public void UsarFrasco()
    {
        if (Health >= maxHealth) return;
        if (!PlayerCurrency.TrySpendPotion()) return;
        Health = Mathf.Clamp(Health + potionHealAmount, 0f, maxHealth);
        if (DevoAvisarHud) OnHealthChanged?.Invoke(Health, maxHealth);
    }
}
