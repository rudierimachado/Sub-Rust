using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Subir escada de mao (vertical, tipo escada de bombeiro): parado numa coluna,
/// sobe/desce com W/S ou as setas, sem pular de patamar em patamar como a rampa.
///
/// Mesmo padrao do PlayerDodge: desliga o PlayerMovement2_5D emquanto ativo e
/// dirige o CharacterController direto, devolvendo o controle ao soltar/sair.
///
/// Entrar exige apertar pra cima/baixo perto o bastante (em X) de uma EscadaDeMao
/// registrada, dentro do intervalo vertical dela. Sair: chegar no topo subindo,
/// chegar na base descendo, ou apertar Espaco (solta e cai).
///
/// Anexar na RAIZ do Player (mesmo objeto do CharacterController).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerClimb : MonoBehaviour
{
    [SerializeField] private float velocidadeSubida = 3.2f;

    // Nao depende de OnEnable registrar numa lista estatica: isso se mostrou fragil
    // quando os objetos sao criados/reativados por script de editor (fora do Play
    // Mode o Unity nem sempre dispara OnEnable no mesmo frame). FindObjectsByType
    // sob demanda e' mais lento por chamada, mas so' roda quando o jogador tenta
    // agarrar (nao esta' escalando e apertou pra cima/baixo), nunca todo frame.

    private CharacterController controller;
    private PlayerMovement2_5D movimento;
    private Transform visual;
    private Animator animator;
    private GameObject armaEsquerda;
    private GameObject armaDireita;
    private PlayerCombat combate;

    private Quaternion rotacaoVisualAntesEscalada;
    private Vector3 escalaVisualAntesEscalada;
    private bool armaEsquerdaAtivaAntesEscalada;
    private bool armaDireitaAtivaAntesEscalada;
    private bool combateAtivoAntesEscalada;
    private float zAntesEscalada;
    private float zEscadaAtual;
    private SeparacaoDeCorpos separacao;
    private bool separacaoAtivaAntesEscalada;

    private bool escalando;
    private EscadaDeMao escadaAtual;

    // Lado que o jogador pediu com A/D durante a subida. Decide por onde ele sai la'
    // em cima: quem segura pra direita espera desembarcar a direita, nao no primeiro
    // lado que a sondagem achar.
    private float ladoDesejado = 1f;

    /// <summary>Ao sair da escada o jogador quase sempre AINDA esta segurando W/S - foi
    /// segurando que ele chegou no fim. Sem exigir uma tecla nova, o Update seguinte
    /// reagarra na hora, chega no fim no mesmo frame, solta, reagarra: um laco
    /// agarra/solta a cada frame. Na tela isso e' o personagem tremendo, porque o
    /// Animator liga/desliga "Escalando", o Visual gira 90 graus e as armas piscam,
    /// tudo 60x por segundo. Medido: na base ele e' solto a dx=0,00 da escada, bem
    /// dentro do alcance de agarre.</summary>
    private bool exigeNovaTecla;

    
    private static readonly int DirecaoEscaladaParam = Animator.StringToHash("DirecaoEscalada");
private static readonly int EscalandoParam = Animator.StringToHash("Escalando");

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movimento = GetComponent<PlayerMovement2_5D>();
        separacao = GetComponent<SeparacaoDeCorpos>();
        visual = transform.Find("Visual");
        animator = visual != null ? visual.GetComponent<Animator>() : null;
        if (visual != null)
        {
            var pistola = FindDeep(visual, "Pistola");
            var espada = FindDeep(visual, "RealSword_Right");
            armaEsquerda = pistola != null ? pistola.gameObject : null;
            armaDireita = espada != null ? espada.gameObject : null;
            combate = visual.GetComponent<PlayerCombat>();
        }
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool subir = kb.wKey.isPressed || kb.upArrowKey.isPressed;
        bool descer = kb.sKey.isPressed || kb.downArrowKey.isPressed;

        if (!escalando)
        {
            // Soltou a tecla: libera o proximo agarre.
            if (!subir && !descer) { exigeNovaTecla = false; return; }
            if (exigeNovaTecla) return;
            TentarAgarrar(subir ? 1f : -1f);
            return;
        }

        // Espaco solta a escada de proposito (deixa cair) - nao trava o jogador nela.
        if (kb.spaceKey.wasPressedThisFrame) { Soltar(); return; }

        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ladoDesejado = 1f;
        else if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) ladoDesejado = -1f;

        float v = subir ? 1f : (descer ? -1f : 0f);
        var pos = transform.position;
        float novoY = Mathf.Clamp(pos.y + v * velocidadeSubida * Time.deltaTime,
                                  escadaAtual.BaseY, escadaAtual.TopoY);

        // X e Z sao FIXADOS na coluna, nao interpolados: o jogador ja' foi
        // teleportado pra ca' no instante do agarre (ver TentarAgarrar), entao aqui os
        // deltas laterais sao ~0 e servem so' pra impedir deriva. Interpolar era o que
        // fazia ele descer de lado, deslizando pro lugar enquanto ja' caia.
        controller.Move(new Vector3(escadaAtual.X - pos.x, novoY - pos.y, zEscadaAtual - pos.z));
        // O estado permanece ativo enquanto estiver agarrado. Se dependesse de v,
        // soltar W/S por um instante faria o personagem voltar para Idle no meio da escada.
        if (animator != null)
        {
            animator.SetBool(EscalandoParam, true);
            animator.SetFloat(DirecaoEscaladaParam, v);
        }

        // No topo, primeiro encontra piso ao lado da abertura e so' depois solta.
        // Soltar no X exato da escada deixava o personagem cair pelo mesmo buraco.
        if (v > 0f && novoY >= escadaAtual.TopoY - 0.01f)
        {
            SairNoTopo();
            return;
        }

        if (v < 0f && novoY <= escadaAtual.BaseY + 0.01f)
            SairNaBase();
    }

    private void TentarAgarrar(float direcaoInicial)
    {
        float x = transform.position.x, y = transform.position.y;
        // FindObjectsByType em vez de lista cacheada: so' roda neste instante (nao
        // escalando + apertou W/S), nunca todo frame - custo desprezivel.
        var escadas = FindObjectsByType<EscadaDeMao>();
        foreach (var e in escadas)
        {
            if (e == null) continue;
            if (Mathf.Abs(x - e.X) > e.AlcanceX) continue;
            if (y < e.BaseY - 0.6f || y > e.TopoY + 0.6f) continue;

            escalando = true;
            escadaAtual = e;
            zAntesEscalada = transform.position.z;
            // ZAgarrado, nao Z: o corpo fica na FRENTE dos degraus (lado da camera).
            // Ir para o Z exato do degrau enfia metade do personagem dentro da escada.
            zEscadaAtual = e.ZAgarrado;
            // Por padrao sai de volta pelo lado por onde chegou; A/D durante a subida
            // sobrescreve isso.
            float aproximacao = x - e.X;
            ladoDesejado = Mathf.Abs(aproximacao) < 0.01f ? 1f : Mathf.Sign(aproximacao);

            // TELEPORTA para a coluna no mesmo instante do agarre, mantendo a altura.
            // Nao interpola: qualquer aproximacao gradual aparece em tela como o
            // personagem descendo de lado, torto, ate' chegar no lugar. Na escada_ponte
            // isso e' 3,11m em Z (os degraus ficam na face de fora do convés).
            Teleportar(new Vector3(e.X, transform.position.y, zEscadaAtual));
            if (movimento != null) movimento.enabled = false;

            // SeparacaoDeCorpos escreve transform.position DIRETO, todo frame, no
            // LateUpdate. Com a escalada prendendo X/Z na coluna no Update seguinte, os
            // dois se revezam empurrando e prendendo o mesmo corpo - isso e' tremor na
            // tela, nao "animacao ruim". Mesmo cuidado que o PlayerDodge ja' toma com o
            // movimento normal: um escritor de posicao por vez.
            if (separacao != null)
            {
                separacaoAtivaAntesEscalada = separacao.enabled;
                separacao.enabled = false;
            }

            // A locomocao normal olha para +/-X. Na escada o corpo inteiro precisa
            // encarar a parede (+Z), incluindo maos e armas que seguem os ossos.
            if (visual != null)
            {
                rotacaoVisualAntesEscalada = visual.localRotation;
                escalaVisualAntesEscalada = visual.localScale;
                // -90 fixo, nao condicional ao Z da escada. O corpo agarrado fica
                // SEMPRE do lado -Z encarando +Z, entao o giro e' sempre o mesmo.
                // Alem disso o PlayerMovement2_5D nunca gira o Player (transform.rotation
                // e' fixo em LookRotation(right)); virar e' espelhar localScale.z. Logo
                // rotacaoVisualAntesEscalada e' constante e um giro condicional so'
                // podia estar errado - era o que deixava o corpo de costas na
                // escada_ponte, a unica com degraus em Z negativo.
                visual.localRotation = rotacaoVisualAntesEscalada * Quaternion.Euler(0f, -90f, 0f);
                visual.localScale = new Vector3(
                    escalaVisualAntesEscalada.x,
                    escalaVisualAntesEscalada.y,
                    Mathf.Abs(escalaVisualAntesEscalada.z));
            }

            if (animator != null)
            {
                animator.SetBool(EscalandoParam, true);
                animator.SetFloat(DirecaoEscaladaParam, direcaoInicial);
            }
            DesequiparDuranteEscalada();
            return;
        }
    }

    private void Soltar() => Soltar(true);

    /// <param name="devolverZ">false quando quem chamou ja' posicionou o jogador no
    /// plano caminhavel (saida no topo) - mover de novo so' desperdicaria um passo.</param>
    private void Soltar(bool devolverZ)
    {
        escalando = false;
        // Trava o proximo agarre ate a tecla ser solta - ver o campo para o motivo.
        exigeNovaTecla = true;

        // O movimento normal e' 2.5D e NUNCA altera Z: se o jogador nao voltar ao plano
        // caminhavel agora, ele fica fora dele pra sempre. Por isso teleporta em vez de
        // varrer - um Move bloqueado aqui deixaria o personagem preso em Z=-3,11 na
        // escada_ponte, sem nada no jogo capaz de trazer ele de volta.
        if (devolverZ)
        {
            var alvo = new Vector3(transform.position.x, transform.position.y, zAntesEscalada);
            if (CabeEm(alvo)) Teleportar(alvo);
            else controller.Move(new Vector3(0f, 0f, zAntesEscalada - transform.position.z));
        }

        escadaAtual = null;
        if (movimento != null) movimento.enabled = true;
        if (animator != null)
        {
            animator.SetBool(EscalandoParam, false);
            animator.SetFloat(DirecaoEscaladaParam, 1f);
        }
        if (visual != null)
        {
            visual.localRotation = rotacaoVisualAntesEscalada;
            visual.localScale = escalaVisualAntesEscalada;
        }
        RestaurarEquipamento();
        if (separacao != null) separacao.enabled = separacaoAtivaAntesEscalada;
    }

    private void SairNoTopo()
    {
        // A escada sonda o piso de verdade e devolve o ponto exato de pouso, entao o
        // jogador chega SEMPRE encostado no piso - nao numa altura decorada em campo
        // serializado, que era o que prendia ele no topo quando os dois divergiam.
        bool achouPiso = escadaAtual.TentarAcharSaida(zAntesEscalada, controller.radius, ladoDesejado, out var destino);
        if (achouPiso && CabeEm(destino))
        {
            Teleportar(destino);
            Soltar(false);
            return;
        }

        // Geometria mal configurada: permanece agarrado em vez de cair no vazio.
        // Duas causas bem diferentes escondidas atras do mesmo sintoma - "achou piso
        // mas nao cabe" (algo bloqueia o espaco de chegada) e' outro problema de
        // "nao achou piso nenhum" (a sondagem em si falhou), e sem distinguir os dois
        // qualquer investigacao vira suposicao as cegas.
        if (!achouPiso)
            Debug.LogError($"Escada '{escadaAtual.name}' nao tem piso de saida perto de Y={escadaAtual.TopoY:F2} " +
                           $"(zPlano={zAntesEscalada:F2}, lado={ladoDesejado:F0}). O jogador segue agarrado de proposito.", escadaAtual);
        else
            Debug.LogError($"Escada '{escadaAtual.name}' achou piso de saida em {destino:F2} mas o jogador nao " +
                           "cabe la' (algo solido ocupa o espaco de chegada). O jogador segue agarrado de proposito.", escadaAtual);
    }

    /// <summary>Chegou no fim da descida. Mesma ideia da saida no topo: pousa no piso
    /// SONDADO e ja' no plano caminhavel, num Move so'.
    ///
    /// Antes daqui a base chamava Soltar() direto, que devolvia o Z num movimento
    /// separado depois de liberar o controle - dois passos no mesmo frame, e' o que
    /// dava o solavanco ao encostar no chao.</summary>
    private void SairNaBase()
    {
        if (escadaAtual.TentarAcharPousoNaBase(zAntesEscalada, controller.radius, ladoDesejado, out var destino)
            && CabeEm(destino))
        {
            Teleportar(destino);
            Soltar(false);
            return;
        }

        // Sem piso sondado embaixo, solta do jeito antigo: a gravidade do movimento
        // normal resolve. Nao e' caso de travar o jogador na escada como no topo -
        // aqui embaixo cair e' um desfecho aceitavel.
        Debug.LogWarning($"Escada '{escadaAtual.name}': nenhum piso sondado em Y={escadaAtual.BaseY:F2}. " +
                         "Soltando na base sem pouso ajustado.", escadaAtual);
        Soltar();
    }

    private void DesequiparDuranteEscalada()
    {
        if (armaEsquerda != null)
        {
            armaEsquerdaAtivaAntesEscalada = armaEsquerda.activeSelf;
            armaEsquerda.SetActive(false);
        }
        if (armaDireita != null)
        {
            armaDireitaAtivaAntesEscalada = armaDireita.activeSelf;
            armaDireita.SetActive(false);
        }
        if (combate != null)
        {
            combateAtivoAntesEscalada = combate.enabled;
            combate.enabled = false;
        }
    }

    private void RestaurarEquipamento()
    {
        if (armaEsquerda != null) armaEsquerda.SetActive(armaEsquerdaAtivaAntesEscalada);
        if (armaDireita != null) armaDireita.SetActive(armaDireitaAtivaAntesEscalada);
        if (combate != null) combate.enabled = combateAtivoAntesEscalada;
    }

    /// <summary>Reposicionamento instantaneo. `controller.Move` NAO serve pra isso: ele
    /// varre o caminho e para no primeiro obstaculo, entao um salto grande (a saida no
    /// topo da escada_ponte anda 3,11m em Z) chega no lugar errado sem avisar. Desligar
    /// o CharacterController e' a forma suportada de escrever position na mao.</summary>
    private void Teleportar(Vector3 destino)
    {
        bool estava = controller.enabled;
        controller.enabled = false;
        transform.position = destino;
        controller.enabled = estava;
        Physics.SyncTransforms();
    }

    /// <summary>O corpo cabe em pe nesse ponto? Sonda a capsula do proprio
    /// CharacterController, ignorando os colisores do jogador e os da escada.
    /// Teleportar sem checar isso e' o jeito classico de enfiar o personagem numa
    /// parede - o raycast de piso so' prova que existe chao, nao que ha espaco.</summary>
    private bool CabeEm(Vector3 destino)
    {
        float meia = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 centro = destino + controller.center;
        var achados = Physics.OverlapCapsule(centro - Vector3.up * meia, centro + Vector3.up * meia,
                                             controller.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in achados)
        {
            if (c == null) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (escadaAtual != null && c.transform.IsChildOf(escadaAtual.transform)) continue;
            return false;
        }
        return true;
    }

    private static Transform FindDeep(Transform root, string nome)
    {
        if (root.name == nome) return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, nome);
            if (found != null) return found;
        }
        return null;
    }
}
