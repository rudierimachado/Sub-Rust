using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ponto central de rede do Player. Decide quem e' "dono" deste personagem (o
/// jogador que joga com ELE nesta maquina) e faz o que depende disso:
///
///   1. Desliga os scripts de INPUT nas copias remotas - a copia continua existindo
///      e sendo desenhada (e' o corpo do outro jogador na sua tela), so' nao le
///      teclado/mouse local.
///   2. Liga a camera Cinemachine (CameraJogador) para seguir SO' o Player dono -
///      sem isso a camera segue qualquer Player que aparecer primeiro na cena.
///   3. Posiciona o dono no marcador "PontoDeSpawn_Player" da cena de jogo.
///   4. Replica o FACING (vira pra esquerda/direita) por NetworkVariable, porque o
///      flip do personagem e' feito trocando o sinal da escala Z do Visual
///      (ver PlayerMovement2_5D) e o dono e' quem manda essa informacao.
///
/// ORDEM QUE JUSTIFICA A CENA, NAO O SPAWN: em Co-op, o Player e' criado pelo
/// Netcode assim que o cliente CONECTA (ainda na cena MenuPrincipal, ANTES do
/// host mandar todo mundo pra fase com RedeSessao.ComecarPartida). Ou seja,
/// OnNetworkSpawn roda achando uma cena sem "CameraJogador" e sem
/// "PontoDeSpawn_Player" nenhum - fazer essas ligacoes ali direto falharia sempre
/// no Co-op. Por isso a ligacao de camera/posicao acontece quando a cena de JOGO
/// termina de carregar (NetworkSceneManager.OnLoadComplete), nao no spawn em si.
/// Em modo Solo nao ha essa etapa de troca de cena via rede, entao o Start() aqui
/// mesmo ja basta (a cena de jogo e' a que esta ativa desde o load direto).
///
/// Modo SOLO (RedeSessao.Solo == true, sem NetworkManager rodando): este script
/// se comporta como se sempre fosse dono, sem exigir spawn de rede. E' assim que
/// o jogo continua funcionando IDENTICO ao single-player de antes, sem duplicar
/// caminho de codigo - RedeSessao.Solo e o UNICO lugar que decide isso (ver a
/// doc de RedeSessao.cs), aqui so' se consulta.
///
/// Anexar na RAIZ do Player, junto com o NetworkObject e o NetworkTransform.
/// </summary>
public class PlayerNetwork : NetworkBehaviour
{
    [Tooltip("Preenchido automaticamente se vazio: procura por nome na cena.")]
    [SerializeField] private string nomeDaCamera = "CameraJogador";

    [Tooltip("Nome do objeto marcador de onde o Player nasce em cada cena de jogo.")]
    [SerializeField] private string nomeDoPontoDeSpawn = "PontoDeSpawn_Player";

    [Tooltip("Espacamento em X entre jogadores nascendo no mesmo marcador - evita os dois surgirem exatamente sobrepostos em Co-op.")]
    [SerializeField] private float espacamentoEntreJogadores = 1.6f;

    /// <summary>Replicado pelo dono. So' o SINAL importa (visual espelha por ele).</summary>
    private readonly NetworkVariable<float> facingRede = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    /// <summary>Terceira pessoa: o corpo gira de verdade (nao espelha), entao o sinal do
    /// facing nao basta - replica o angulo em Y do corpo.</summary>
    private readonly NetworkVariable<float> yawRede = new NetworkVariable<float>(
        90f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform visual;
    private PlayerMovement2_5D movimento;
    private CharacterController controller;
    private Unity.Netcode.Components.NetworkTransform networkTransform;

    /// <summary>true quando ESTE Player e' controlado por esta maquina - seja porque
    /// e' o dono na rede, seja porque a partida e' solo (sem rede nenhuma).</summary>
    public bool EhDono => RedeSessao.Solo || IsOwner;

    private void Awake()
    {
        visual = transform.Find("Visual");
        movimento = GetComponent<PlayerMovement2_5D>();
        controller = GetComponent<CharacterController>();
        networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
    }

    private void Start()
    {
        // Modo solo: nunca passa por OnNetworkSpawn (nao ha rede), entao a
        // configuracao de dono e o posicionamento acontecem aqui direto - a cena
        // ativa JA' e' a cena de jogo, nao existe troca de cena via rede no meio.
        if (RedeSessao.Solo)
        {
            ConfigurarComoDono();
            PosicionarNoSpawn();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner) ConfigurarComoDono();
        else ConfigurarComoRemoto();

        facingRede.OnValueChanged += AplicarFacingRemoto;
        AplicarFacingRemoto(facingRede.Value, facingRede.Value);
        yawRede.OnValueChanged += AplicarYawRemoto;
        AplicarYawRemoto(yawRede.Value, yawRede.Value);

        // Ver o comentario de classe: a cena de jogo pode ainda nao existir agora
        // (spawn acontece na MenuPrincipal, antes da troca). So' o dono precisa
        // se posicionar/religar camera; o remoto so' segue a posicao replicada.
        if (IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete += QuandoCenaDeJogoCarregou;

        // Se, por algum motivo, o Player ja' nascer DENTRO da cena de jogo (ex:
        // reconexao no meio da partida, cena ja' trocada), tenta posicionar de
        // imediato tambem - o metodo e' seguro pra chamar sem o marcador existir.
        if (IsOwner) PosicionarNoSpawn();
    }

    public override void OnNetworkDespawn()
    {
        facingRede.OnValueChanged -= AplicarFacingRemoto;
        yawRede.OnValueChanged -= AplicarYawRemoto;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnLoadComplete -= QuandoCenaDeJogoCarregou;
        base.OnNetworkDespawn();
    }

    private void QuandoCenaDeJogoCarregou(ulong clientId, string sceneName, LoadSceneMode mode)
    {
        // So' reage a troca da PROPRIA maquina (o callback dispara para varios
        // clientId conforme o host fica sabendo de cada um terminando de carregar).
        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId) return;

        LigarCameraNisto();
        PosicionarNoSpawn();
    }

    private void Update()
    {
        // So' o dono PUBLICA o facing. O remoto so' recebe (OnValueChanged acima).
        if (!EhDono || movimento == null) return;

        // Em Co-op, IsSpawned so' fica true DEPOIS que o Netcode termina de vincular
        // este NetworkBehaviour ao NetworkVariable internamente. Escrever antes disso
        // (Update roda desde o primeiro frame, spawn ainda pode estar em andamento)
        // gera "NetworkVariable is written to, but doesn't know its NetworkBehaviour
        // yet" no console - inofensivo (a escrita e' descartada), mas suja o log e
        // indica que o valor daquele frame se perdeu. Em modo Solo IsSpawned e'
        // sempre false (nunca ha' Spawn() de verdade), entao a checagem precisa
        // aceitar os dois: RedeSessao.Solo OU IsSpawned.
        if (!RedeSessao.Solo && !IsSpawned) return;

        float facingAtual = Mathf.Sign(movimento.Facing);
        if (!Mathf.Approximately(facingAtual, facingRede.Value))
            facingRede.Value = facingAtual;

        if (movimento.TerceiraPessoa)
        {
            float yaw = Quaternion.LookRotation(movimento.Direcao, Vector3.up).eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, yawRede.Value)) > 0.5f)
                yawRede.Value = yaw;
        }
    }

    private void ConfigurarComoDono()
    {
        LigarCameraNisto();
        // Os scripts de input continuam ligados por padrao (e' como o Player
        // sempre existiu antes da rede); nada a desligar aqui.
    }

    private void ConfigurarComoRemoto()
    {
        // Copia de rede de OUTRO jogador: desliga tudo que le' teclado/mouse local
        // OU que reage a mudanca de posicao/animacao como se fosse decisao local.
        // O corpo continua vivo (Animator toca pelo NetworkAnimator do dono,
        // fisica de escalada quando o dono manda a posicao) - so' para de agir
        // por conta propria nesta maquina.
        DesligarSeExistir<PlayerMovement2_5D>();
        DesligarSeExistir<PlayerDodge>();
        DesligarSeExistir<PlayerClimb>();
        DesligarSeExistir<PlayerHealth>(); // Update() so' le tecla Q (frasco); TakeHit continua chamavel via IDamageable, so' desligado o polling de input.
        DesligarSeExistir<FallDamage>();   // sem isso o dano de queda seria aplicado 2x: uma vez no cliente DONO de verdade, outra aqui lendo a MESMA queda replicada pela rede.
        if (visual != null)
        {
            var combate = visual.GetComponent<PlayerCombat>();
            if (combate != null) combate.enabled = false;
        }

        // SeparacaoDeCorpos fica LIGADO nos dois lados de proposito: ele resolve
        // colisao FISICA entre os dois corpos (Physics.ComputePenetration), nao
        // input. Desligar no remoto faria os dois personagens se atravessarem.

        // CharacterController continua LIGADO: e' ele que o NetworkTransform usa
        // pra mover o corpo remoto sem que a fisica local brigue com a posicao
        // vinda da rede (CharacterController.Move nao e' chamado por ninguem aqui
        // porque PlayerMovement2_5D esta desligado).
    }

    private void DesligarSeExistir<T>() where T : Behaviour
    {
        var c = GetComponent<T>();
        if (c != null) c.enabled = false;
    }

    private void LigarCameraNisto()
    {
        var camGO = GameObject.Find(nomeDaCamera);
        if (camGO == null) return; // cena ainda sem camera de jogo (ex: ainda na MenuPrincipal) - normal, tenta de novo no proximo LoadComplete.
        var cine = camGO.GetComponent<CinemachineCamera>();
        if (cine == null)
        {
            Debug.LogWarning("PlayerNetwork: '" + nomeDaCamera + "' nao tem CinemachineCamera.", this);
            return;
        }

        var nucleo = transform.Find("NucleoCamera");
        var alvo = nucleo != null ? nucleo : transform;
        cine.Follow = alvo;
        cine.LookAt = alvo;
    }

    /// <summary>Teleporta ESTE Player (que precisa ser o dono) para o marcador de
    /// spawn da cena atual. So' escreve posicao diretamente porque isto roda antes
    /// do jogo comecar de verdade (chegada na fase) - depois disso quem move o
    /// personagem e' sempre o CharacterController via PlayerMovement2_5D.</summary>
    private void PosicionarNoSpawn()
    {
        // DevWarp.TentarConsumir tem prioridade sobre o marcador normal: o Menu Dev
        // guarda a posicao escolhida num campo estatico ANTES de chamar
        // RedeSessao.JogarSolo() (sobrevive ao LoadScene que troca MenuPrincipal por
        // Fase01_Castelo), e "consumir" ja' limpa - senao um warp antigo continuaria
        // valendo pra sempre em toda troca de cena futura. So' vale em Solo de
        // proposito (nao faz sentido teleportar jogador especifico em Co-op).
        Vector3 posicaoBase;
        Quaternion rotacaoBase;
        if (RedeSessao.Solo && DevWarp.TentarConsumir(out var alvoDev))
        {
            posicaoBase = alvoDev;
            // NUNCA Quaternion.identity aqui: a raiz do Player fica sempre travada
            // olhando pra +X (PlayerMovement2_5D.Awake) - a virada de verdade e' so'
            // no Visual, por escala. Usar identity sobrescrevia essa trava (o Awake
            // ja' rodou antes deste metodo) e o personagem nascia com a raiz virada
            // errado - era isso que fazia o warp do Dev Menu nascer "de costas".
            rotacaoBase = Quaternion.LookRotation(Vector3.right, Vector3.up);
        }
        else
        {
            var marco = GameObject.Find(nomeDoPontoDeSpawn);
            if (marco == null) return; // cena sem marcador (ex: ainda na MenuPrincipal) - normal.
            posicaoBase = marco.transform.position;
            rotacaoBase = marco.transform.rotation;
        }

        // Em Solo so' existe um Player: sem offset. Em Co-op, o id da conexao
        // (0 = host, 1 = primeiro cliente, ...) decide o lado - host fica no
        // marcador exato, os demais se afastam alternando +/- pra nao nascer
        // empilhado. Deterministico, sem precisar de um contador a parte.
        float offsetX = 0f;
        if (!RedeSessao.Solo && OwnerClientId > 0)
        {
            float lado = (OwnerClientId % 2 == 1) ? 1f : -1f;
            float passo = Mathf.Ceil(OwnerClientId / 2f);
            offsetX = lado * passo * espacamentoEntreJogadores;
        }

        Vector3 destino = posicaoBase + new Vector3(offsetX, 0f, 0f);

        // Desliga o CharacterController um frame pra escrever a posicao direto:
        // ele bloqueia escrita de transform.position enquanto ligado (trata como
        // colisao) e o teleporte ficaria preso na posicao antiga.
        bool estava = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;

        // NetworkTransform.Teleport() e' a API oficial pra "mudar de posicao AGORA,
        // sem interpolar e notificando a replicacao no mesmo instante" - sem isso o
        // Player poderia aparecer deslizando do spawn ate o ponto real, em vez de
        // ja' surgir la'. So' pode ser chamado quando o objeto FOI SPAWNADO pelo
        // Netcode (lanca excecao fora disso, ver codigo-fonte) - em modo Solo o
        // NetworkObject existe no prefab mas nunca passou por Spawn(), entao aqui
        // cai sempre no SetPositionAndRotation comum.
        if (!RedeSessao.Solo && networkTransform != null && IsSpawned)
            networkTransform.Teleport(destino, rotacaoBase, transform.localScale);
        else
            transform.SetPositionAndRotation(destino, rotacaoBase);

        if (controller != null) controller.enabled = estava;
    }

    private void AplicarYawRemoto(float anterior, float novo)
    {
        // Encarar funciona com o PlayerMovement2_5D desligado (como fica no remoto).
        if (EhDono || movimento == null || !movimento.TerceiraPessoa) return;
        movimento.Encarar(Quaternion.Euler(0f, novo, 0f) * Vector3.forward);
    }

    private void AplicarFacingRemoto(float anterior, float novo)
    {
        // O dono ja' aplica o proprio facing dentro de PlayerMovement2_5D.AplicarFacing;
        // aqui so' o REMOTO precisa espelhar o Visual a partir do valor de rede.
        if (EhDono || visual == null) return;
        // Terceira pessoa nao espelha: quem vira o corpo remoto e' o yawRede.
        if (movimento != null && movimento.TerceiraPessoa) return;
        var s = visual.localScale;
        visual.localScale = new Vector3(s.x, s.y, Mathf.Sign(novo) * Mathf.Abs(s.z));
    }
}
