using UnityEngine;

/// <summary>
/// Anima a Provedora por CODIGO, girando osso direto: arremesso (armar + soltar),
/// balanco de andar e postura curvada de cansada.
///
/// POR QUE PROCEDURAL E NAO CLIPE
/// O rig dela e' Generic (foi o que consertou a malha explodindo - Humanoid
/// deformava a pose ate' com o clipe proprio dela). Generic NAO retargeta: os
/// clipes de Mixamo/DoubleL do projeto sao de outro esqueleto e nao servem aqui.
/// O FBX dela veio com um clipe so' ("Alert", uma pose de espreita). Entao ou ela
/// fica parada pra sempre, ou a animacao de acao vem daqui.
///
/// COMO FUNCIONA
/// Roda em LateUpdate, DEPOIS do Animator escrever a pose do clipe - por isso
/// consegue somar por cima em vez de ser sobrescrito. Cada osso recebe a rotacao
/// do clipe multiplicada por um offset calculado aqui. Nao guarda "pose original":
/// le' o que o Animator acabou de escrever a cada frame e soma em cima, senao o
/// offset acumularia e o braco ia girando pra sempre.
///
/// Anexar na RAIZ do chefe (mesmo objeto do ChefeCozinha_Provedora).
/// </summary>
public class PoseProceduralProvedora : MonoBehaviour
{
    [Header("Ossos (achados sozinhos pelo nome se ficarem vazios)")]
    [SerializeField] private Transform bracoDireito;
    [SerializeField] private Transform anteBracoDireito;
    [SerializeField] private Transform coluna;
    [SerializeField] private Transform quadril;
    [SerializeField] private Transform bracoEsquerdo;
    [SerializeField] private Transform coxaEsquerda;
    [SerializeField] private Transform coxaDireita;
    [SerializeField] private Transform joelhoEsquerdo;
    [SerializeField] private Transform joelhoDireito;
    [SerializeField] private Transform peito;
    [SerializeField] private Transform cabeca;
    [SerializeField] private Transform anteBracoEsquerdo;

    [Header("Arremesso")]
    [Tooltip("Duracao total do gesto: armar + soltar + voltar.")]
    [SerializeField] private float duracaoDoArremesso = 0.40f;
    [Tooltip("Fracao do gesto gasta ARMANDO (o resto e' o lancamento). O projetil " +
             "sai no fim do armar - e' o instante que o jogador tem que ler.")]
    [Range(0.1f, 0.9f)] [SerializeField] private float fracaoDoArmar = 0.45f;
    [SerializeField] private float anguloDeArmar = 95f;
    [SerializeField] private float anguloDeSoltar = -55f;
    [SerializeField] private float anguloDoCotovelo = 70f;

    [Header("Andar")]
    [Tooltip("Amplitude do passo (grau de abertura da coxa). O eixo de balanco foi " +
             "MEDIDO no rig dela, nao chutado: girar a coxa no Z local move o pe' " +
             "0,28 m pra frente sem levantar (0,00 em Y); o X local levanta a perna " +
             "(0,15 em Y) em vez de dar passo.")]
    [SerializeField] private float aberturaDoPasso = 30f;
    [Tooltip("Flexao do joelho na perna que esta' voltando.")]
    [SerializeField] private float flexaoDoJoelho = 26f;
    [SerializeField] private float balancoDeBraco = 16f;
    [SerializeField] private float balancoAoAndar = 4f;
    [Tooltip("Quanto chao um CICLO completo (os dois pes) cobre, em metros. E' o " +
             "numero que impede o pe' de patinar: a fase do passo avanca por " +
             "DESLOCAMENTO, nao por relogio, entao a cadencia acompanha a velocidade " +
             "sozinha. Sai da medicao no rig dela: 30 graus de coxa levam o pe' " +
             "0,278 m, que e' a amplitude A de cada perna. No contato um pe' esta' +A " +
             "a frente e o outro -A atras, entao o PASSO e' 2A e o ciclo (dois passos) " +
             "e' 4A = 1,11 m. Mudou aberturaDoPasso? Reescale este numero junto, senao " +
             "volta a patinar.")]
    [SerializeField] private float comprimentoDoCiclo = 1.11f;

    [Header("Cansada - ela SENTA e bufa")]
    [Tooltip("Quanto o quadril desce, em METROS DE MUNDO (a conversao pro espaco do " +
             "osso e' feita no codigo - ver AplicarCansada). Calibrado medindo o OSSO " +
             "do joelho com as pernas ja' dobradas: em pe' ele fica a 1,24 m do chao; " +
             "com 0,60 de queda vai pra 0,64 e com 0,95 pra 0,29. 1,20 poe o joelho em " +
             "~0,04 - encostado no chao, que e' sentar. Dobrar as pernas LEVANTA o " +
             "corpo, entao a queda precisa ser bem maior do que parece.")]
    [SerializeField] private float quedaDoQuadril = 1.20f;
    [Tooltip("Dobra da coxa. Eixo Z local - o mesmo do passo, medido no rig.")]
    [SerializeField] private float dobraDaCoxa = 62f;
    [Tooltip("Dobra do joelho. Eixo X LOCAL, nao Z: a 45 graus no X o pe' sobe 0,30 m " +
             "e recua 0,27 m (dobra como joelho); no Z ele so' varre pra tras sem " +
             "subir, que e' passo, nao ajoelhar.")]
    [SerializeField] private float dobraDoJoelho = 96f;
    [SerializeField] private float curvaturaCansada = 26f;
    [Tooltip("Cabeca pendurada pra frente - le' exaustao muito mais que a coluna sozinha.")]
    [SerializeField] private float cabecaPendurada = 24f;
    [Tooltip("Bracos caidos, cotovelo morto.")]
    [SerializeField] private float bracosCaidos = 38f;
    [Tooltip("Segundos pra entrar e sair da pose sentada. Sem rampa ela TELETRANSPORTA " +
             "pra sentada no frame em que o folego acaba.")]
    [SerializeField] private float tempoParaSentar = 0.35f;

    [Header("Levar dano - cambaleio")]
    [Tooltip("Quanto o tronco chicoteia pra tras no acerto, em graus. O corpo dobra " +
             "SOBRE o golpe: quem leva uma pancada na barriga fecha, nao abre.")]
    [SerializeField] private float dobraDoImpacto = 34f;
    [Tooltip("Quanto a cabeca chicoteia. Vai um pouco alem do tronco e volta depois - " +
             "e' o atraso do pescoco que faz a pancada parecer ter peso.")]
    [SerializeField] private float chicoteDaCabeca = 26f;
    [Tooltip("Duracao do cambaleio. Curto: passa disso e vira animacao de tontura.")]
    [SerializeField] private float duracaoDoImpacto = 0.30f;
    [Tooltip("Quantas oscilacoes o corpo da' enquanto se recompoe. 1,5 da' um " +
             "chicote forte e um resto - mais que isso vira gelatina.")]
    [SerializeField] private float oscilacoesDoImpacto = 1.5f;

    [Header("Cansada - bufar")]
    [Tooltip("Ciclos de respiracao por segundo. Ofegante e' ~1,1 (66 por minuto), nao " +
             "os 0,25 de alguem em repouso.")]
    [SerializeField] private float respiracoesPorSegundo = 1.1f;
    [Tooltip("Amplitude do arfar do peito, em graus.")]
    [SerializeField] private float respiroCansada = 9f;

    private float relogioDoArremesso = -1f;
    private bool andando;
    private bool cansada;
    private float faseDoBalanco;
    private float faseDoPasso;
    private float velocidadeAtual;
    private float pesoSentada;
    private float faseDoRespiro;
    private bool jaExalou;
    private Vector3 deslocamentoDoQuadrilAnterior;
    private float relogioDoImpacto = -1f;
    private float forcaDoImpacto;
    private float ladoDoImpacto = 1f;

    /// <summary>Disparado no fundo de cada expiracao. O chefe usa pra soltar o baforo
    /// de vapor no ritmo da respiracao dela - particula continua nao le' como bufar,
    /// le' como fumaca de fundo.</summary>
    public event System.Action AoExalar;

    private void Awake()
    {
        if (bracoDireito == null || anteBracoDireito == null || coluna == null || quadril == null)
            AcharOssos();
    }

    /// <summary>Acha os ossos pelo nome em vez de exigir arrastar 4 campos na mao.
    /// Se o rig mudar de nomenclatura, o aviso aparece uma vez e o resto do chefe
    /// continua funcionando (ele so' fica sem gesto).</summary>
    private void AcharOssos()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (bracoDireito == null && t.name == "RightArm") bracoDireito = t;
            else if (anteBracoDireito == null && t.name == "RightForeArm") anteBracoDireito = t;
            else if (bracoEsquerdo == null && t.name == "LeftArm") bracoEsquerdo = t;
            else if (coluna == null && t.name == "Spine02") coluna = t;   // lombar: a cadeia deste rig e Hips/Spine02/Spine01/Spine/neck/Head, entao Spine02 e a BASE e Spine o topo
            else if (quadril == null && t.name == "Hips") quadril = t;
            else if (coxaEsquerda == null && t.name == "LeftUpLeg") coxaEsquerda = t;
            else if (coxaDireita == null && t.name == "RightUpLeg") coxaDireita = t;
            else if (joelhoEsquerdo == null && t.name == "LeftLeg") joelhoEsquerdo = t;
            else if (joelhoDireito == null && t.name == "RightLeg") joelhoDireito = t;
            else if (peito == null && t.name == "Spine01") peito = t;   // meio do tronco
            else if (cabeca == null && t.name == "Head") cabeca = t;
            else if (anteBracoEsquerdo == null && t.name == "LeftForeArm") anteBracoEsquerdo = t;
        }

        if (bracoDireito == null || anteBracoDireito == null)
            Debug.LogWarning($"{name}: nao achei RightArm/RightForeArm - o gesto de arremesso " +
                             "nao vai aparecer (o arremesso em si continua funcionando).", this);
    }

    /// <summary>Dispara o gesto. Chamado pelo ChefeCozinha_Provedora no mesmo
    /// instante em que o projetil e' criado.</summary>
    public void Arremessar() => relogioDoArremesso = 0f;

    /// <summary>Quanto tempo, em segundos, o gesto passa ARMANDO antes de soltar.
    /// O chefe usa isto pra criar o caldo no instante do lancamento em vez de no
    /// comeco do gesto - sem isso o projetil sai antes do braco se mexer e o aviso
    /// que o jogador precisa pra desviar nao vale nada.</summary>
    public float TempoDeArmar => duracaoDoArremesso * fracaoDoArmar;

    public void DefinirAndando(bool valor) => andando = valor;

    /// <summary>Cambaleio ao levar dano. 'forca' e' 0..1 (o quanto a pancada foi forte
    /// em relacao ao golpe de referencia) e 'lado' e' -1 ou 1, o sentido de onde veio
    /// o golpe. Chamado pelo chefe, que recebe isso do EnemyHealth.
    ///
    /// O rig dela nao tem clipe de dano - o Animator dela so' tem "Alert" e os
    /// parametros Cansada/Arremessar, sem nenhum "Hit". O EnemyDamageFeedback dispara
    /// um trigger "Hit" que nao existe neste controller, entao ela apanhava sem mexer
    /// um musculo: so' deslizava pra tras e piscava. E' isto que da' corpo ao acerto.</summary>
    public void LevarImpacto(float forca, float lado)
    {
        // Nao reinicia do zero se ja' esta cambaleando: soma, ate o teto. Assim uma
        // sequencia rapida de golpes acumula em vez de zerar o chicote a cada acerto.
        forcaDoImpacto = Mathf.Min(1.3f, (relogioDoImpacto >= 0f ? forcaDoImpacto : 0f) + Mathf.Clamp01(forca));
        ladoDoImpacto = Mathf.Sign(lado == 0f ? 1f : lado);
        relogioDoImpacto = 0f;
    }

    /// <summary>Velocidade horizontal REAL dela, em m/s. E' daqui que sai a cadencia
    /// do passo - andar mais rapido da' mais passos, nao passos maiores. Sem isto o
    /// ciclo corre num relogio fixo e o pe' patina no chao (le' como flutuar).</summary>
    public void DefinirVelocidade(float metrosPorSegundo)
    {
        velocidadeAtual = Mathf.Abs(metrosPorSegundo);
        andando = velocidadeAtual > 0.05f;
    }
    public void DefinirCansada(bool valor) => cansada = valor;

    private void LateUpdate()
    {
        if (relogioDoArremesso >= 0f)
        {
            relogioDoArremesso += Time.deltaTime;
            if (relogioDoArremesso > duracaoDoArremesso) relogioDoArremesso = -1f;
        }

        // Rampa da pose sentada primeiro: tudo abaixo se mistura com ela.
        pesoSentada = Mathf.MoveTowards(pesoSentada, cansada ? 1f : 0f,
                                        Time.deltaTime / Mathf.Max(0.05f, tempoParaSentar));

        if (pesoSentada < 0.999f)
        {
            AplicarArremesso();
            AplicarAndar();
        }
        AplicarCansada();
        AplicarImpacto();   // por ultimo: o cambaleio sobrepoe qualquer outra pose
    }

    private void AplicarArremesso()
    {
        if (relogioDoArremesso < 0f || bracoDireito == null) return;

        float t = relogioDoArremesso / duracaoDoArremesso;
        float ombro, cotovelo;

        if (t < fracaoDoArmar)
        {
            // Armando: sobe o braco pra tras, desacelerando (leitura clara do "vem coisa").
            float k = Mathf.SmoothStep(0f, 1f, t / fracaoDoArmar);
            ombro = anguloDeArmar * k;
            cotovelo = anguloDoCotovelo * k;
        }
        else
        {
            // Soltando: rapido pra frente e volta. Curva mais seca que o armar de
            // proposito - e' o golpe, nao o aviso.
            float k = (t - fracaoDoArmar) / (1f - fracaoDoArmar);
            float chicote = Mathf.Sin(k * Mathf.PI);
            ombro = Mathf.Lerp(anguloDeArmar, anguloDeSoltar, Mathf.SmoothStep(0f, 1f, k));
            cotovelo = anguloDoCotovelo * (1f - k) * (1f - chicote * 0.4f);
        }

        bracoDireito.localRotation *= Quaternion.Euler(0f, 0f, ombro);
        if (anteBracoDireito != null) anteBracoDireito.localRotation *= Quaternion.Euler(0f, 0f, cotovelo);
    }

    /// <summary>Ciclo de caminhada montado no osso: pernas em contrafase, joelho
    /// flexionando na perna que volta, bracos contra as pernas e um balanco leve de
    /// quadril. Entra e sai por interpolacao (faseDoBalanco), senao a perna saltaria
    /// de pose no instante em que ela para de andar.</summary>
    /// <summary>Chicote amortecido: uma senoide que perde forca com o tempo. O corpo
    /// vai pra tras, passa do ponto, volta e sobra um tremor. Coluna, peito e cabeca
    /// entram com atrasos diferentes - o pescoco chega DEPOIS do tronco, que e' o que
    /// separa "levou uma pancada" de "girou como um bloco".</summary>
    private void AplicarImpacto()
    {
        if (relogioDoImpacto < 0f) return;

        relogioDoImpacto += Time.deltaTime;
        if (relogioDoImpacto > duracaoDoImpacto)
        {
            relogioDoImpacto = -1f;
            forcaDoImpacto = 0f;
            return;
        }

        float t = relogioDoImpacto / duracaoDoImpacto;
        float amortecimento = (1f - t) * (1f - t);
        float onda = Mathf.Sin(t * Mathf.PI * 2f * oscilacoesDoImpacto) * amortecimento * forcaDoImpacto;

        // Atraso do pescoco: meia oscilacao atras do tronco.
        float ondaAtrasada = Mathf.Sin((t - 0.12f) * Mathf.PI * 2f * oscilacoesDoImpacto) * amortecimento * forcaDoImpacto;

        if (coluna != null) coluna.localRotation *= Quaternion.Euler(onda * dobraDoImpacto, 0f, 0f);
        if (peito != null)  peito.localRotation  *= Quaternion.Euler(onda * dobraDoImpacto * 0.6f, 0f, 0f);
        if (cabeca != null) cabeca.localRotation *= Quaternion.Euler(ondaAtrasada * chicoteDaCabeca, 0f, 0f);

        // Torcao lateral pro lado de onde veio o golpe - o corpo nao dobra so' no plano.
        if (quadril != null)
            quadril.localRotation *= Quaternion.Euler(0f, 0f, onda * dobraDoImpacto * 0.35f * ladoDoImpacto);

        // Bracos soltos acompanham com atraso, como peso morto.
        if (bracoDireito != null)  bracoDireito.localRotation  *= Quaternion.Euler(0f, 0f,  ondaAtrasada * chicoteDaCabeca * 0.8f);
        if (bracoEsquerdo != null) bracoEsquerdo.localRotation *= Quaternion.Euler(0f, 0f, -ondaAtrasada * chicoteDaCabeca * 0.8f);
    }

    private void AplicarAndar()
    {
        float alvo = andando ? 1f : 0f;
        faseDoBalanco = Mathf.MoveTowards(faseDoBalanco, alvo, Time.deltaTime * 4f);
        if (faseDoBalanco <= 0.001f) return;

        // Fase por DESLOCAMENTO: 1 ciclo a cada comprimentoDoCiclo metros andados.
        // Assim a frequencia acompanha a velocidade sozinha e o pe' nao patina.
        if (comprimentoDoCiclo > 0.01f)
            faseDoPasso += (velocidadeAtual / comprimentoDoCiclo) * 2f * Mathf.PI * Time.deltaTime;
        float t = faseDoPasso;
        float ciclo = Mathf.Sin(t) * faseDoBalanco;
        float passo = ciclo * aberturaDoPasso;

        // Z local: eixo de passo medido no rig (ver tooltip de aberturaDoPasso).
        if (coxaDireita != null) coxaDireita.localRotation *= Quaternion.Euler(0f, 0f, -passo);
        if (coxaEsquerda != null) coxaEsquerda.localRotation *= Quaternion.Euler(0f, 0f, passo);

        // Joelho so' dobra na perna que RECUA (metade do ciclo), nunca pra tras.
        float flexDir = Mathf.Max(0f, ciclo) * flexaoDoJoelho;
        float flexEsq = Mathf.Max(0f, -ciclo) * flexaoDoJoelho;
        if (joelhoDireito != null) joelhoDireito.localRotation *= Quaternion.Euler(0f, 0f, flexDir);
        if (joelhoEsquerdo != null) joelhoEsquerdo.localRotation *= Quaternion.Euler(0f, 0f, flexEsq);

        // Bracos contra as pernas. O direito so' balanca quando nao esta' arremessando,
        // pra nao brigar com o gesto.
        float bracoSwing = ciclo * balancoDeBraco;
        if (bracoEsquerdo != null) bracoEsquerdo.localRotation *= Quaternion.Euler(0f, 0f, -bracoSwing);
        if (bracoDireito != null && relogioDoArremesso < 0f)
            bracoDireito.localRotation *= Quaternion.Euler(0f, 0f, bracoSwing);

        if (quadril != null)
            quadril.localRotation *= Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * balancoAoAndar * faseDoBalanco);
    }

    /// <summary>Ela SENTA: quadril desce sobre os calcanhares, coxas dobram, joelhos
    /// fecham, tronco desaba pra frente e o peito arfa. Tudo escalado por pesoSentada,
    /// entao entrar e sair e' uma rampa e nao um corte.
    ///
    /// O deslocamento do quadril e' o unico canal de POSICAO daqui, e ele desconta o
    /// deslocamento do frame anterior antes de aplicar o novo: se o Animator estiver
    /// escrevendo a posicao do quadril o desconto e' inofensivo; se NAO estiver (clipe
    /// so' de rotacao), sem o desconto ela afundaria um pouco mais no chao a cada
    /// frame ate' sumir. As rotacoes nao precisam disso porque o Animator reescreve
    /// localRotation todo frame.</summary>
    private void AplicarCansada()
    {
        if (quadril != null && deslocamentoDoQuadrilAnterior != Vector3.zero)
            quadril.localPosition -= deslocamentoDoQuadrilAnterior;
        deslocamentoDoQuadrilAnterior = Vector3.zero;

        if (pesoSentada <= 0.001f) return;

        float p = Mathf.SmoothStep(0f, 1f, pesoSentada);

        // Respiracao com fase propria (nao Time.time cru) pra dar pra achar o fundo da
        // expiracao e avisar quem quiser sincronizar efeito com ela.
        faseDoRespiro += Time.deltaTime * respiracoesPorSegundo * 2f * Mathf.PI;
        if (faseDoRespiro > 2f * Mathf.PI)
        {
            faseDoRespiro -= 2f * Mathf.PI;
            jaExalou = false;
        }
        float onda = Mathf.Sin(faseDoRespiro);
        if (!jaExalou && faseDoRespiro > Mathf.PI)
        {
            jaExalou = true;
            AoExalar?.Invoke();
        }

        // Pernas dobradas sob o corpo.
        if (coxaDireita != null)    coxaDireita.localRotation    *= Quaternion.Euler(0f, 0f, -dobraDaCoxa * p);
        if (coxaEsquerda != null)   coxaEsquerda.localRotation   *= Quaternion.Euler(0f, 0f,  dobraDaCoxa * p);
        if (joelhoDireito != null)  joelhoDireito.localRotation  *= Quaternion.Euler(dobraDoJoelho * p, 0f, 0f);
        if (joelhoEsquerdo != null) joelhoEsquerdo.localRotation *= Quaternion.Euler(dobraDoJoelho * p, 0f, 0f);

        // Tronco desabado, arfando. O peito puxa mais que a lombar - quem respira
        // ofegante move a caixa toracica, nao a cintura.
        if (coluna != null) coluna.localRotation *= Quaternion.Euler((curvaturaCansada + onda * respiroCansada * 0.4f) * p, 0f, 0f);
        if (peito != null)  peito.localRotation  *= Quaternion.Euler((onda * respiroCansada - respiroCansada * 0.5f) * p, 0f, 0f);
        if (cabeca != null) cabeca.localRotation *= Quaternion.Euler((cabecaPendurada - onda * respiroCansada * 0.5f) * p, 0f, 0f);

        // Bracos mortos ao lado do corpo.
        if (bracoDireito != null)      bracoDireito.localRotation      *= Quaternion.Euler(0f, 0f,  bracosCaidos * p);
        if (bracoEsquerdo != null)     bracoEsquerdo.localRotation     *= Quaternion.Euler(0f, 0f, -bracosCaidos * p);
        if (anteBracoDireito != null)  anteBracoDireito.localRotation  *= Quaternion.Euler(0f, 0f,  bracosCaidos * 0.5f * p);
        if (anteBracoEsquerdo != null) anteBracoEsquerdo.localRotation *= Quaternion.Euler(0f, 0f, -bracosCaidos * 0.5f * p);

        // Quadril desce; o corpo sobe/desce de leve no ritmo do folego.
        //
        // A queda e' calculada em METROS DE MUNDO e convertida pro espaco do pai do
        // osso, nunca escrita direto no Y local. O Armature deste FBX vem rotacionado
        // (Z-up), entao "Y local" do quadril aponta pra outra direcao: escrever
        // localPosition.y -= 0,5 mudava o numero no Inspector e nao movia o corpo um
        // milimetro - foi assim que a primeira versao da pose deu "sentada" com a
        // personagem de pe'. InverseTransformVector resolve pra qualquer rig.
        if (quadril != null && quadril.parent != null)
        {
            float queda = -(quedaDoQuadril * p) + onda * 0.012f * p;
            deslocamentoDoQuadrilAnterior = quadril.parent.InverseTransformVector(Vector3.up * queda);
            quadril.localPosition += deslocamentoDoQuadrilAnterior;
        }
    }
}
