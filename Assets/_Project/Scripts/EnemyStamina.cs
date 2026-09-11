using System;
using UnityEngine;

/// <summary>
/// Folego do inimigo. Cada golpe custa stamina; quando acaba, ele PRECISA recuar e
/// respirar antes de atacar de novo.
///
/// Existe pra dar ritmo legivel ao combate: sem isso o inimigo so' tinha um cooldown
/// invisivel, e a unica leitura possivel era decorar o tempo. Com folego, a barra na
/// cabeca dele mostra exatamente quando ele vai ficar vulneravel - a janela pra
/// atacar de volta deixa de ser adivinhacao.
///
/// DOIS ESTADOS DIFERENTES, NAO CONFUNDIR
///  - SemFolegoProGolpe: falta stamina pro PROXIMO ataque. Ele recua e respira, mas
///    continua se defendendo e reagindo normalmente. E' o ritmo do combate.
///  - GuardaQuebrada: a stamina chegou a ZERO. Ele trava, curva o corpo e fica
///    completamente exposto por alguns segundos. E' a recompensa por ter pressionado.
///
/// O jogador DRENA a stamina dele de duas formas (ver Drenar): aparando os golpes com
/// o escudo, que e' o caminho principal, e acertando golpes, que acelera. Atacar
/// tambem gasta, pela propria conta dele.
///
/// Anexar na RAIZ do inimigo (mesmo objeto do EnemyHealth e do EnemyAI).
/// </summary>
public class EnemyStamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float custoDoAtaque = 40f;
    [SerializeField] private float regenPorSegundo = 22f;

    [Tooltip("Espera antes de comecar a regenerar depois de gastar - e' o que cria a janela de contra-ataque.")]
    [SerializeField] private float atrasoParaRegenerar = 0.7f;

    [Header("Quebra de guarda")]
    [Tooltip("Segundos que o inimigo fica travado e exposto quando a stamina zera. " +
             "Precisa ser maior que a duracao de um golpe seu, senao a recompensa por " +
             "quebrar a guarda nao cabe na janela e o sistema inteiro fica invisivel.")]
    [SerializeField] private float duracaoDaQuebra = 2.5f;

    [Tooltip("Quanto a stamina volta ao fim da quebra, de 0 a 1. Cheia de proposito: " +
             "quebrar a guarda de novo tem que custar a mesma pressao da primeira vez, " +
             "senao o inimigo entra num ciclo de quebras encadeadas e vira boneco.")]
    [Range(0.2f, 1f)] [SerializeField] private float recuperacaoAposQuebra = 1f;

    [Tooltip("Carencia antes de poder quebrar a guarda de novo. Sem ela, o dano que " +
             "chega no mesmo frame da recuperacao rezera a barra na hora.")]
    [SerializeField] private float carenciaEntreQuebras = 1.5f;

    public float Stamina { get; private set; }
    public float MaxStamina => maxStamina;
    public float CustoDoAtaque => custoDoAtaque;

    /// <summary>Sem folego pro proximo golpe. O EnemyAI consulta isto antes de partir
    /// pra cima - e' o que faz ele recuar em vez de atacar sem parar.
    ///
    /// NAO e' a quebra de guarda: aqui ele so' esta' cansado, e continua reagindo.</summary>
    public bool Exausto => Stamina < custoDoAtaque;

    /// <summary>Stamina zerada: travado e exposto. Enquanto isto for true o EnemyAI
    /// nao decide nada e o golpe forte do jogador entra automatico (ver PlayerCombat).</summary>
    public bool GuardaQuebrada { get; private set; }

    /// <summary>Quanto falta da quebra, de 1 (acabou de quebrar) a 0. Serve pra
    /// reacao visual saber em que ponto do cansaco esta' (ver ExaustaoDoInimigo).</summary>
    public float ProgressoDaQuebra =>
        GuardaQuebrada ? Mathf.Clamp01((quebraTerminaEm - Time.time) / Mathf.Max(0.01f, duracaoDaQuebra)) : 0f;

    /// <summary>Disparado no instante em que a guarda quebra. Quem faz efeito escuta
    /// aqui em vez de ficar consultando GuardaQuebrada todo frame procurando a borda.</summary>
    public event Action AoQuebrarGuarda;

    /// <summary>Disparado quando ele se recupera e volta a lutar.</summary>
    public event Action AoRecuperar;

    private float podeRegenerarEm;
    private float quebraTerminaEm;
    private float podeQuebrarDeNovoEm;

    private void Awake() => Stamina = maxStamina;

    private void Update()
    {
        if (GuardaQuebrada)
        {
            if (Time.time >= quebraTerminaEm) Recuperar();
            return;   // sem regeneracao durante a quebra: a exposicao e' o castigo
        }

        if (Time.time < podeRegenerarEm || Stamina >= maxStamina) return;
        Stamina = Mathf.Min(maxStamina, Stamina + regenPorSegundo * Time.deltaTime);
    }

    /// <returns>true se tinha folego e gastou.</returns>
    public bool TryGastar()
    {
        if (Exausto || GuardaQuebrada) return false;
        Stamina -= custoDoAtaque;
        podeRegenerarEm = Time.time + atrasoParaRegenerar;
        return true;
    }

    /// <summary>Tira folego dele por acao do JOGADOR - golpe aparado no escudo ou dano
    /// levado. Zerar aqui quebra a guarda.
    ///
    /// Separado do TryGastar de proposito: aquele e' o inimigo gastando o proprio
    /// folego pra atacar e pode falhar por falta de saldo; este e' pressao vinda de
    /// fora e sempre acontece, mesmo com a barra baixa.</summary>
    public void Drenar(float quanto)
    {
        if (quanto <= 0f || GuardaQuebrada) return;

        Stamina = Mathf.Max(0f, Stamina - quanto);
        podeRegenerarEm = Time.time + atrasoParaRegenerar;

        if (Stamina <= 0f && Time.time >= podeQuebrarDeNovoEm) Quebrar();
    }

    private void Quebrar()
    {
        GuardaQuebrada = true;
        quebraTerminaEm = Time.time + duracaoDaQuebra;
        AoQuebrarGuarda?.Invoke();
    }

    private void Recuperar()
    {
        GuardaQuebrada = false;
        Stamina = maxStamina * recuperacaoAposQuebra;
        podeRegenerarEm = Time.time;
        podeQuebrarDeNovoEm = Time.time + carenciaEntreQuebras;
        AoRecuperar?.Invoke();
    }
}
