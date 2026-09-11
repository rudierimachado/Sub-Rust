using UnityEngine;

/// <summary>
/// Morcego da Capela 12. IA de VOO - nao reaproveita o EnemyAI de proposito: aquele
/// e' CharacterController + gravidade + checagem de borda de chao, tres coisas que
/// nao existem pra quem voa. O que da' pra reaproveitar (vida, dano recebido, almas
/// ao morrer, barra na cabeca) continua vindo dos componentes de sempre: este script
/// so' cuida de para onde ele voa e quando mergulha.
///
/// RITMO: ele nao persegue colado. Fica orbitando fora do alcance da espada e so'
/// entra em MERGULHO em linha reta, com o trajeto travado no instante em que o
/// mergulho comeca. E' isso que torna o bicho legivel - da' pra andar pro lado e ver
/// o mergulho passar reto, em vez de um teleguiado impossivel de esquivar.
///
/// ANIMACAO: as asas batem por script (senoide) em vez de clipe. O modelo foi feito
/// com as asas como filhos separados justamente pra isso, e assim a frequencia
/// acompanha o estado - bate devagar pendurado, rapido no mergulho - sem manter
/// Animator, controller e blend tree pra dois ossos.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class MorcegoVoador : MonoBehaviour
{
    private enum Estado { Empoleirado, Despertando, Orbitando, Mergulhando, Recuperando }

    [Header("Percepcao")]
    [SerializeField] private float raioVisao = 12f;
    [SerializeField] private float tempoParaDesprender = 0.55f;

    [Header("Voo")]
    [SerializeField] private float velocidadeOrbita = 3.2f;
    [SerializeField] private float velocidadeMergulho = 9.5f;
    [Tooltip("Altura acima da cabeca do jogador em que ele fica rondando.")]
    [SerializeField] private float alturaDeVoo = 2.6f;
    [SerializeField] private float raioDaOrbita = 3.4f;
    [Tooltip("Amplitude do sobe-e-desce enquanto ronda. E' o que impede o voo de virar uma reta de drone.")]
    [SerializeField] private float balanco = 0.35f;

    [Header("Ataque")]
    [SerializeField] private float cooldownMergulho = 2.6f;
    [SerializeField] private float dano = 90f;
    [Tooltip("Distancia em que a garra acerta durante o mergulho.")]
    [SerializeField] private float alcanceDano = 0.75f;
    [SerializeField] private float duracaoDoMergulho = 1.1f;
    [SerializeField] private float tempoRecuperando = 0.8f;

    [Header("Limites do salao (mundo)")]
    [Tooltip("Ele nao sai desta caixa. Evita morcego atravessando parede da capela.")]
    [SerializeField] private Vector3 limiteMin = new Vector3(48.8f, 0.9f, -4.0f);
    [SerializeField] private Vector3 limiteMax = new Vector3(73.5f, 5.6f, 2.4f);

    [Header("Asas")]
    [SerializeField] private Transform asaEsquerda;
    [SerializeField] private Transform asaDireita;
    [Tooltip("Graus de abertura no bater de asas.")]
    [SerializeField] private float amplitudeAsa = 42f;
    // MEDIDO na cena: com a asa direita em -40 e a esquerda em +40 as duas pontas
    // sobem juntas (dy=+0,140 e +0,131). A asa esquerda e' espelhada em escala,
    // mas ainda assim precisa do angulo INVERTIDO - dai' o sinal ser +1 e nao -1.
    [SerializeField] private float sinalAsaEsquerda = 1f;

    private Estado estado = Estado.Empoleirado;
    private Transform jogador;
    private Vector3 poleiro;
    private Vector3 direcaoDoMergulho;
    private float trocaDeEstadoEm;
    private float proximoMergulho;
    private float faseOrbita;
    private float faseAsa;
    private bool jaAcertouNesteMergulho;

private void Awake()
    {
        // Este jogo é 2.5D: o combate acontece no mesmo plano Z do jogador.
        // Travar já no poleiro impede o primeiro quadro fora da linha de tiro.
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        poleiro = transform.position;
        faseOrbita = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        if (jogador == null) ProcurarJogador();

        switch (estado)
        {
            case Estado.Empoleirado: Empoleirado(); break;
            case Estado.Despertando: Despertando(); break;
            case Estado.Orbitando:   Orbitar();     break;
            case Estado.Mergulhando: Mergulhar();   break;
            case Estado.Recuperando: Recuperar();   break;
        }

        BaterAsas();
    }

    // O jogador vive na cena Core e pode nao existir no Awake deste objeto (mesmo
    // motivo do retry que existe no EnemyAI). Sem isto o morcego nasce cego.
    private void ProcurarJogador()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) jogador = go.transform;
    }

    private void Empoleirado()
    {
        if (jogador == null) return;
        if (Vector3.Distance(jogador.position, transform.position) > raioVisao) return;
        estado = Estado.Despertando;
        trocaDeEstadoEm = Time.time + tempoParaDesprender;
    }

    private void Despertando()
    {
        // solta do teto: cai um pouco enquanto abre as asas
        MoverPara(transform.position + Vector3.down * 0.9f * Time.deltaTime);
        if (Time.time < trocaDeEstadoEm) return;
        estado = Estado.Orbitando;
        proximoMergulho = Time.time + cooldownMergulho * 0.5f;
    }

private void Orbitar()
    {
        if (jogador == null) { VoarPara(poleiro, velocidadeOrbita); return; }

        faseOrbita += Time.deltaTime * 1.4f;
        Vector3 centro = jogador.position + Vector3.up * alturaDeVoo;
        // Ronda só no eixo X/Y: nenhuma profundidade Z que tire o alvo da mira 2.5D.
        Vector3 offset = new Vector3(Mathf.Cos(faseOrbita) * raioDaOrbita,
                                     Mathf.Sin(faseOrbita * 2.1f) * balanco,
                                     0f);
        VoarPara(centro + offset, velocidadeOrbita);

        if (Time.time < proximoMergulho) return;
        if (Vector3.Distance(jogador.position, transform.position) > raioVisao) return;

        direcaoDoMergulho = ((jogador.position + Vector3.up * 0.9f) - transform.position).normalized;
        estado = Estado.Mergulhando;
        trocaDeEstadoEm = Time.time + duracaoDoMergulho;
        jaAcertouNesteMergulho = false;
    }

    private void Mergulhar()
    {
        MoverPara(transform.position + direcaoDoMergulho * velocidadeMergulho * Time.deltaTime);
        Encarar(direcaoDoMergulho);

        if (!jaAcertouNesteMergulho && jogador != null)
        {
            Vector3 peito = jogador.position + Vector3.up * 0.9f;
            if (Vector3.Distance(peito, transform.position) <= alcanceDano)
            {
                var vida = jogador.GetComponentInParent<PlayerHealth>();
                if (vida != null) vida.TakeHit(dano, transform.position);
                jaAcertouNesteMergulho = true;
            }
        }

        if (Time.time < trocaDeEstadoEm) return;
        estado = Estado.Recuperando;
        trocaDeEstadoEm = Time.time + tempoRecuperando;
        proximoMergulho = Time.time + cooldownMergulho;
    }

    private void Recuperar()
    {
        // sobe de volta pra altura de voo antes de rondar de novo
        Vector3 alto = new Vector3(transform.position.x, limiteMax.y - 0.4f, transform.position.z);
        VoarPara(alto, velocidadeOrbita * 1.3f);
        if (Time.time >= trocaDeEstadoEm) estado = Estado.Orbitando;
    }

    private void VoarPara(Vector3 destino, float velocidade)
    {
        Encarar(destino - transform.position);
        MoverPara(Vector3.MoveTowards(transform.position, destino, velocidade * Time.deltaTime));
    }

private void MoverPara(Vector3 destino)
    {
        destino.x = Mathf.Clamp(destino.x, limiteMin.x, limiteMax.x);
        destino.y = Mathf.Clamp(destino.y, limiteMin.y, limiteMax.y);
        destino.z = 0f;
        transform.position = destino;
    }

    private void Encarar(Vector3 dir)
    {
        dir.y *= 0.35f;                       // nao deixa o bicho apontar o nariz pro chao
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation,
            Quaternion.LookRotation(dir.normalized, Vector3.up), Time.deltaTime * 7f);
    }

    private void BaterAsas()
    {
        if (asaEsquerda == null && asaDireita == null) return;

        float frequencia = estado == Estado.Empoleirado ? 0.6f
                         : estado == Estado.Mergulhando ? 11f : 7f;
        float amplitude = estado == Estado.Empoleirado ? amplitudeAsa * 0.12f : amplitudeAsa;

        faseAsa += Time.deltaTime * frequencia;
        float angulo = Mathf.Sin(faseAsa) * amplitude;

        if (asaDireita != null) asaDireita.localRotation = Quaternion.Euler(0f, 0f, -angulo);
        if (asaEsquerda != null) asaEsquerda.localRotation = Quaternion.Euler(0f, 0f, angulo * sinalAsaEsquerda);
    }
}
