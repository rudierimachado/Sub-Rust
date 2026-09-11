using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elevador de contrapeso acionado por PLACA DE PRESSAO, como nos jogos classicos:
/// nao ha tecla. Pisar no centro da cabine afunda a placa e o mecanismo parte
/// sozinho; sair antes do tempo cancela e a placa volta.
///
/// POR QUE PESO E NAO TECLA
/// Um botao exige o jogador descobrir que existe um botao. A placa se explica
/// sozinha: ela afunda sob os pes e o elevador anda. E o atraso de partida vira
/// leitura - da' pra pular fora se voce mudar de ideia.
///
/// POR QUE FixedUpdate E NAO Update
/// A cabine tem Rigidbody kinematic. Mexer no transform fora do passo de fisica
/// deixa a colisao um frame atras: o CharacterController do passageiro afunda no
/// piso ou e' expelido na subida. MovePosition dentro do FixedUpdate reposiciona
/// os contatos no mesmo passo.
///
/// POR QUE O PASSAGEIRO E' MOVIDO NA MAO
/// CharacterController NAO herda movimento de plataforma - nao e' Rigidbody
/// dinamico, nao existe atrito que o carregue. Sem aplicar o mesmo delta nele, o
/// jogador fica parado no ar enquanto o piso sobe e atravessa a cabine.
///
/// Anexar na CABINE (objeto com o Rigidbody kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ElevadorVertical : MonoBehaviour
{
    [Header("Estrutura")]
    [Tooltip("Marcadores de parada, de BAIXO pra CIMA.")]
    [SerializeField] private Transform[] andares;

    [Tooltip("Placa que afunda sob o peso (so' visual).")]
    [SerializeField] private Transform placaDePressao;
    [SerializeField] private float afundamentoDaPlaca = 0.05f;

    [Tooltip("Quanto tempo a placa leva pra afundar sob o peso.")]
    [SerializeField] private float tempoDeAfundar = 0.35f;
    [Tooltip("Quanto ela desce ALEM do curso normal no tranco da partida.")]
    [SerializeField] private float trancoDaPartida = 0.03f;

    [Header("Mecanismo (opcional, so' visual)")]
    [Tooltip("Peca que desce junto da placa - eixo, pistao, contrapeso.")]
    [SerializeField] private Transform mecanismo;
    [SerializeField] private float cursoDoMecanismo = 0.12f;
    [Tooltip("Roda/engrenagem que gira enquanto a cabine anda.")]
    [SerializeField] private Transform rodaDoMecanismo;
    [SerializeField] private float grausPorMetro = 220f;

    [Header("Movimento")]
    [SerializeField] private float velocidadeMaxima = 5.6f;
    [SerializeField] private float aceleracao = 4.5f;
    [Tooltip("Distancia em que comeca a frear.")]
    [SerializeField] private float distanciaDeFrenagem = 2.8f;

    [Header("Acionamento por peso")]
    [Tooltip("Tempo pisando na placa antes de partir. E' a janela pra desistir.")]
    [SerializeField] private float tempoDeArmar = 0.9f;
    [Tooltip("Espera parado no andar antes de aceitar novo acionamento.")]
    [SerializeField] private float descansoNoAndar = 1.2f;

    [Header("Som")]
    [SerializeField] private AudioClip somPlaca;
    [SerializeField] private AudioClip somPartida;
    [SerializeField] private AudioClip somMovendo;
    [SerializeField] private AudioClip somParada;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Balanco")]
    [SerializeField] private float balanco = 0.03f;

    private readonly List<CharacterController> passageiros = new();
    private readonly HashSet<Collider> dentro = new();

    private Rigidbody corpo;
    private AudioSource audioFonte;
    private Vector3 posicaoDesejada;
    private Vector3 placaRepouso, mecanismoRepouso;
    private float alturaPlacaAtual, velocidadePlaca, alturaAnteriorParaRoda;

    private int andarAtual, andarAlvo;
    private bool emMovimento;
    private float velocidadeAtual, tempoArmando, descansoRestante, faseBalanco;

    private bool TemPeso => passageiros.Count > 0;

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        corpo.isKinematic = true;
        corpo.useGravity = false;
        // Sem interpolar, a cabine anda em degraus do passo de fisica (50Hz) enquanto
        // a tela roda a 100+ fps: e' a tremida classica de plataforma movel.
        corpo.interpolation = RigidbodyInterpolation.Interpolate;
        corpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        posicaoDesejada = transform.position;
        if (placaDePressao != null) placaRepouso = placaDePressao.localPosition;
        if (mecanismo != null) mecanismoRepouso = mecanismo.localPosition;
        alturaAnteriorParaRoda = posicaoDesejada.y;

        audioFonte = GetComponent<AudioSource>();
        if (audioFonte == null) audioFonte = gameObject.AddComponent<AudioSource>();
        audioFonte.playOnAwake = false;
        audioFonte.spatialBlend = 0.75f;
        audioFonte.minDistance = 3f;
        audioFonte.maxDistance = 28f;
        audioFonte.rolloffMode = AudioRolloffMode.Linear;

        andarAtual = AndarMaisProximo(transform.position.y);
        andarAlvo = andarAtual;
    }

    private void OnValidate()
    {
        // A logica de "proximo andar" depende da ordem; arrastar fora de ordem no
        // Inspector e' facil demais.
        if (andares == null || andares.Length < 2) return;
        System.Array.Sort(andares, (a, b) =>
            a == null ? 1 : b == null ? -1 : a.position.y.CompareTo(b.position.y));
    }

    private void Update()
    {
        AnimarPlaca();
        AnimarMecanismo();
    }

    /// <summary>
    /// A placa nao desce reta: ela AFUNDA amortecida sob o peso, da um tranco extra
    /// no instante em que o mecanismo engata, e volta devagar quando voce sai.
    ///
    /// O amortecimento (SmoothDamp) e' o que faz parecer que existe uma mola por
    /// baixo. MoveTowards puro descia em velocidade constante e lia como um painel
    /// deslizando, nao como peso apoiado.
    /// </summary>
    private void AnimarPlaca()
    {
        if (placaDePressao == null) return;

        float alvo = 0f;
        if (emMovimento)
        {
            // Tranco: no comeco do percurso a placa afunda ALEM do curso, e volta
            // ao curso normal conforme a cabine ganha velocidade. E' o peso do
            // mecanismo "pegando".
            float engate = 1f - Mathf.Clamp01(velocidadeAtual / Mathf.Max(0.01f, velocidadeMaxima));
            alvo = afundamentoDaPlaca + trancoDaPartida * engate;
        }
        else if (TemPeso)
        {
            // Enquanto arma, afunda progressivamente: vira uma barra de progresso
            // fisica do tempo que falta pra partir.
            float t = Mathf.Clamp01(tempoArmando / Mathf.Max(0.01f, tempoDeArmar));
            alvo = afundamentoDaPlaca * Mathf.SmoothStep(0.35f, 1f, t);
        }

        // Subir de volta e' mais lento que afundar: peso desce rapido, mola devolve devagar.
        float suavizacao = alvo > alturaPlacaAtual ? tempoDeAfundar : tempoDeAfundar * 2.2f;
        alturaPlacaAtual = Mathf.SmoothDamp(alturaPlacaAtual, alvo, ref velocidadePlaca, suavizacao);
        placaDePressao.localPosition = placaRepouso - new Vector3(0f, alturaPlacaAtual, 0f);
    }

    /// <summary>Pecas que reagem ao mecanismo: o eixo desce junto da placa e a roda
    /// gira proporcional ao percurso ja andado (nao ao tempo, senao ela giraria
    /// mesmo com a cabine parada no meio da frenagem).</summary>
    private void AnimarMecanismo()
    {
        if (mecanismo != null)
        {
            float fracao = afundamentoDaPlaca <= 0f ? 0f : Mathf.Clamp01(alturaPlacaAtual / afundamentoDaPlaca);
            mecanismo.localPosition = mecanismoRepouso - new Vector3(0f, cursoDoMecanismo * fracao, 0f);
        }

        if (rodaDoMecanismo != null && emMovimento)
        {
            float andouAgora = posicaoDesejada.y - alturaAnteriorParaRoda;
            alturaAnteriorParaRoda = posicaoDesejada.y;
            rodaDoMecanismo.Rotate(Vector3.right, andouAgora * grausPorMetro, Space.Self);
        }
        else alturaAnteriorParaRoda = posicaoDesejada.y;
    }

    private void FixedUpdate()
    {
        if (andares == null || andares.Length < 2) return;

        if (descansoRestante > 0f)
        {
            descansoRestante -= Time.fixedDeltaTime;
            return;
        }

        if (!emMovimento) { ArmarPorPeso(); return; }

        Mover();
    }

    /// <summary>Peso sobre a placa por tempo suficiente = parte. Tirar o pe cancela.</summary>
    private void ArmarPorPeso()
    {
        if (!TemPeso) { tempoArmando = 0f; return; }

        tempoArmando += Time.fixedDeltaTime;
        if (tempoArmando < tempoDeArmar) return;

        tempoArmando = 0f;
        int destino = ProximoAndar();
        if (destino == andarAtual) return;

        andarAlvo = destino;
        emMovimento = true;
        velocidadeAtual = 0f;
        faseBalanco = 0f;

        if (somPartida != null) audioFonte.PlayOneShot(somPartida, volume);
        if (somMovendo != null)
        {
            audioFonte.clip = somMovendo;
            audioFonte.loop = true;
            audioFonte.volume = volume * 0.5f;
            audioFonte.Play();
        }
    }

    /// <summary>Sobe ate o topo, desce ate a base. Com 2 andares e' o alternar de sempre.</summary>
    private int ProximoAndar()
    {
        if (andarAtual >= andares.Length - 1) return andarAtual - 1;
        if (andarAtual <= 0) return andarAtual + 1;
        return andarAlvo >= andarAtual ? andarAtual + 1 : andarAtual - 1;
    }

    private void Mover()
    {
        float alvoY = andares[andarAlvo].position.y;
        float restante = Mathf.Abs(alvoY - posicaoDesejada.y);
        float sentido = Mathf.Sign(alvoY - posicaoDesejada.y);

        // Dois tetos: rampa de arranque e frenagem por distancia restante. O menor
        // manda - e' o que da' peso ao mecanismo em vez de partir/parar seco.
        velocidadeAtual = Mathf.MoveTowards(velocidadeAtual, velocidadeMaxima, aceleracao * Time.fixedDeltaTime);
        float tetoFreio = velocidadeMaxima * Mathf.Clamp01(restante / Mathf.Max(0.01f, distanciaDeFrenagem));
        float v = Mathf.Min(velocidadeAtual, Mathf.Max(tetoFreio, 0.3f));

        float passo = Mathf.Min(v * Time.fixedDeltaTime, restante);
        float antesY = posicaoDesejada.y;
        posicaoDesejada.y += sentido * passo;

        // Balanco some conforme ganha velocidade: aparece no arranque e na parada,
        // onde uma cabine de verdade oscila.
        Vector3 comBalanco = posicaoDesejada;
        if (balanco > 0f)
        {
            faseBalanco += Time.fixedDeltaTime * 9f;
            comBalanco.x += Mathf.Sin(faseBalanco) * balanco * (1f - Mathf.Clamp01(v / velocidadeMaxima));
        }
        corpo.MovePosition(comBalanco);

        // Passageiro recebe SO' o delta vertical: incluir o balanco o faria escorregar
        // de lado junto com a cabine.
        MoverPassageiros(new Vector3(0f, posicaoDesejada.y - antesY, 0f));

        if (restante - passo <= 0.0005f) Chegar();
    }

    private void Chegar()
    {
        posicaoDesejada.y = andares[andarAlvo].position.y;
        corpo.MovePosition(posicaoDesejada);
        andarAtual = andarAlvo;
        emMovimento = false;
        velocidadeAtual = 0f;
        tempoArmando = 0f;
        descansoRestante = descansoNoAndar;

        if (audioFonte.isPlaying && audioFonte.loop) audioFonte.Stop();
        audioFonte.loop = false;
        if (somParada != null) audioFonte.PlayOneShot(somParada, volume);
    }

    private void MoverPassageiros(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0f) return;
        for (int i = passageiros.Count - 1; i >= 0; i--)
        {
            var p = passageiros[i];
            if (p == null) { passageiros.RemoveAt(i); continue; }
            if (!p.enabled || !p.gameObject.activeInHierarchy) continue;
            p.Move(delta);
        }
    }

    private int AndarMaisProximo(float y)
    {
        int melhor = 0; float menor = float.PositiveInfinity;
        for (int i = 0; i < andares.Length; i++)
        {
            if (andares[i] == null) continue;
            float d = Mathf.Abs(andares[i].position.y - y);
            if (d < menor) { menor = d; melhor = i; }
        }
        return melhor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!dentro.Add(other)) return;
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc == null || passageiros.Contains(cc)) return;
        passageiros.Add(cc);
        if (somPlaca != null && passageiros.Count == 1) audioFonte.PlayOneShot(somPlaca, volume * 0.8f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!dentro.Remove(other)) return;
        var cc = other.GetComponentInParent<CharacterController>();
        if (cc == null) return;
        // So' sai da lista se NENHUM colisor daquele personagem continua dentro.
        foreach (var c in dentro)
            if (c != null && c.GetComponentInParent<CharacterController>() == cc) return;
        passageiros.Remove(cc);
    }

    private void OnDrawGizmosSelected()
    {
        if (andares == null) return;
        Gizmos.color = new Color(1f, 0.75f, 0.2f);
        for (int i = 0; i < andares.Length; i++)
        {
            if (andares[i] == null) continue;
            Gizmos.DrawWireCube(andares[i].position, new Vector3(2.8f, 0.12f, 1.9f));
            if (i > 0 && andares[i - 1] != null) Gizmos.DrawLine(andares[i - 1].position, andares[i].position);
        }
    }
}
