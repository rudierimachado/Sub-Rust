using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// Espingarda calibre 12 da mao esquerda. Botao direito do mouse (ou tecla K) dispara.
///
/// Dois perfis, trocaveis no Inspector:
///   Curta        - cano mais longo: espalha menos, alcanca mais, coice menor.
///   CanoSerrado  - espalha muito, alcance curto, arranca no corpo a corpo e coiceia forte.
///
/// O tiro sai da BOCA DO CANO, nao da camera: em 2.5D a camera fica metros atras e o
/// proprio corpo comeria o tiro.
///
/// A DIRECAO vem do facing do personagem, nao do forward da arma - a mao balanca com a
/// animacao e mirar pelo transform faria o tiro sair torto conforme o quadro.
///
/// A POSE DA ARMA e' lida uma vez no Awake e nunca sobrescrita: o ajuste manual feito
/// no editor e' a referencia, o coice so' desvia a partir dela e volta.
/// </summary>
public class PlayerShooting : MonoBehaviour
{
    public enum TipoArma { Curta, CanoSerrado }

    [Header("Arma")]
    [SerializeField] private TipoArma tipo = TipoArma.CanoSerrado;

    [Header("Dano")]
    [SerializeField] private float danoTotal = 60f;
    [SerializeField] private int chumbos = 8;
    [Tooltip("Fracao do alcance a partir da qual o dano comeca a cair.")]
    [SerializeField] private float inicioQueda = 0.35f;
    [SerializeField] private float danoMinimo = 0.25f;

    [Header("Detecao")]
    // MEDIDO: a mao esquerda fica 0,31 fora do eixo em Z e a capsula do inimigo tem raio
    // 0,30 - raio fino passa raspando POR FORA. Varredura com esfera resolve.
    [SerializeField] private float raioDoChumbo = 0.16f;
    [SerializeField] private LayerMask mascara = ~0;

    [Header("Camera e impacto")]
    [SerializeField] private float hitStop = 0.045f;

    [Header("Visual")]
    [SerializeField] private Color corFogo = new Color(1f, 0.72f, 0.32f);
    [SerializeField] private Color corImpacto = new Color(1f, 0.35f, 0.12f);

    [Header("Municao")]
    [SerializeField] private int cartuchosNoPente = 6;
    [SerializeField] private float tempoRecarga = 1.4f;

    public int Cartuchos { get; private set; }
    public int CartuchosMax => cartuchosNoPente;
    public bool Recarregando { get; private set; }

    /// <summary>(atual, maximo, recarregando) - o HUD de balas escuta aqui.</summary>
    public static event System.Action<int, int, bool> OnMunicaoMudou;

    private Transform arma;
    private Transform boca;
    private PlayerMovement2_5D movimento;
    private CinemachineImpulseSource impulso;

    // pose de repouso: capturada uma unica vez, e' o ajuste manual do editor
    private Quaternion repousoLocal;
    private bool repousoValido;

    private float proximoTiro;
    private Coroutine rotinaRecuo;
    private readonly RaycastHit[] buffer = new RaycastHit[24];

    // parametros por tipo de arma
    private float Cadencia      => tipo == TipoArma.CanoSerrado ? 0.85f : 0.55f;
    private float Alcance       => tipo == TipoArma.CanoSerrado ? 9f    : 18f;
    private float EspalhaGraus  => tipo == TipoArma.CanoSerrado ? 11f   : 5f;
    private float RecuoGraus    => tipo == TipoArma.CanoSerrado ? 26f   : 15f;
    private float TempoRecuo    => tipo == TipoArma.CanoSerrado ? 0.22f : 0.15f;
    private float Tremida       => tipo == TipoArma.CanoSerrado ? 1.8f  : 1.1f;

    private void Awake()
    {
        arma = FindDeep(transform, "Pistola");
        boca = arma != null ? FindDeep(arma, "Boca") : null;
        movimento = GetComponentInParent<PlayerMovement2_5D>();
        impulso = GetComponent<CinemachineImpulseSource>();

        if (arma == null || boca == null)
        {
            Debug.LogError("PlayerShooting: nao achei 'Pistola'/'Boca' na mao esquerda.");
            return;
        }

        // A pose posicionada a mao no editor e' a verdade - guardar antes de qualquer coice.
        repousoLocal = arma.localRotation;
        repousoValido = true;

        Cartuchos = cartuchosNoPente;
    }

    private void Start()
    {
        PublicarEstado();
    }

    /// <summary>Re-dispara o evento de municao com os valores atuais. Mesmo motivo do
    /// PlayerHealth.PublicarEstado: a HUD carrega numa cena aditiva depois deste Start.</summary>
    public void PublicarEstado()
    {
        OnMunicaoMudou?.Invoke(Cartuchos, cartuchosNoPente, Recarregando);
    }

    private void Update()
    {
        if (boca == null) return;

        bool pediuRecarga = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        if (pediuRecarga && !Recarregando && Cartuchos < cartuchosNoPente)
            StartCoroutine(Recarregar());

        bool atirou = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                   || (Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame);

        if (!atirou || Time.time < proximoTiro || Recarregando) return;

        if (Cartuchos <= 0)
        {
            // pente vazio: recarrega sozinho em vez de so' nao responder
            StartCoroutine(Recarregar());
            return;
        }

        Cartuchos--;
        OnMunicaoMudou?.Invoke(Cartuchos, cartuchosNoPente, false);
        proximoTiro = Time.time + Cadencia;
        Atirar();
    }

    private IEnumerator Recarregar()
    {
        Recarregando = true;
        OnMunicaoMudou?.Invoke(Cartuchos, cartuchosNoPente, true);

        yield return new WaitForSeconds(tempoRecarga);

        Cartuchos = cartuchosNoPente;
        Recarregando = false;
        OnMunicaoMudou?.Invoke(Cartuchos, cartuchosNoPente, false);
    }

    private void Atirar()
    {
        float facing = movimento != null ? Mathf.Sign(movimento.Facing) : 1f;
        Vector3 origem = boca.position;
        Vector3 eixo = Vector3.right * facing;

        float danoPorChumbo = danoTotal / Mathf.Max(1, chumbos);
        var atingidos = new Dictionary<IDamageable, float>();
        var pontosImpacto = new List<Vector3>();

        for (int p = 0; p < chumbos; p++)
        {
            // leque no plano da tela (Y) com um pouco de profundidade (Z), como um
            // padrao de chumbo real visto de lado
            float aY = Random.Range(-EspalhaGraus, EspalhaGraus);
            float aZ = Random.Range(-EspalhaGraus, EspalhaGraus) * 0.45f;
            Vector3 dir = (Quaternion.Euler(aY, 0f, 0f) * Quaternion.Euler(0f, aZ, 0f) * eixo).normalized;

            Vector3 fim = origem + dir * Alcance;

            int n = Physics.SphereCastNonAlloc(origem, raioDoChumbo, dir, buffer, Alcance, mascara, QueryTriggerInteraction.Ignore);
            System.Array.Sort(buffer, 0, n, Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance)));

            for (int i = 0; i < n; i++)
            {
                var h = buffer[i];
                if (movimento != null && h.collider.transform.IsChildOf(movimento.transform)) continue;

                fim = h.point;
                if (h.collider.CompareTag("Enemy"))
                {
                    var alvo = h.collider.GetComponent<IDamageable>();
                    if (alvo != null)
                    {
                        // queda de dano com a distancia: e' o que da' identidade de espingarda
                        float t = Mathf.InverseLerp(Alcance * inicioQueda, Alcance, h.distance);
                        float mult = Mathf.Lerp(1f, danoMinimo, t);
                        atingidos.TryGetValue(alvo, out float acum);
                        atingidos[alvo] = acum + danoPorChumbo * mult;
                    }
                    pontosImpacto.Add(h.point);
                }
                break;
            }

            Tracer(origem, fim, p == 0);
        }

        // Um TakeHit por inimigo com a soma dos chumbos - senao 8 chamadas seguidas
        // reiniciariam a reacao de dano dele oito vezes e ela nao apareceria.
        foreach (var kv in atingidos)
            kv.Key.TakeHit(kv.Value, origem);

        foreach (var ponto in pontosImpacto) Impacto(ponto);
        if (pontosImpacto.Count > 0) HitStop.Aplicar(this, hitStop, 0.04f);

        Clarao(origem, eixo);
        impulso?.GenerateImpulseWithForce(Tremida);

        if (rotinaRecuo != null) StopCoroutine(rotinaRecuo);
        rotinaRecuo = StartCoroutine(Recuo(facing));
    }

    /// <summary>Clarao de boca: luz curta e forte + leque de fagulhas.</summary>
    private void Clarao(Vector3 origem, Vector3 dir)
    {
        var go = new GameObject("Clarao");
        go.transform.position = origem + dir * 0.1f;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = corFogo;
        l.intensity = tipo == TipoArma.CanoSerrado ? 60f : 40f;
        l.range = tipo == TipoArma.CanoSerrado ? 11f : 8f;
        Destroy(go, 0.07f);

        Particulas(origem, Quaternion.LookRotation(dir, Vector3.up), corFogo,
                   quantidade: 26, velocidade: 7f, tamanho: 0.11f, vida: 0.22f, angulo: 20f);
    }

    /// <summary>Estouro no ponto de acerto.</summary>
    private void Impacto(Vector3 ponto)
    {
        Particulas(ponto, Quaternion.identity, corImpacto,
                   quantidade: 14, velocidade: 4.5f, tamanho: 0.07f, vida: 0.28f, angulo: 180f);

        var go = new GameObject("LuzImpacto");
        go.transform.position = ponto;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = corImpacto;
        l.intensity = 14f;
        l.range = 4f;
        Destroy(go, 0.09f);
    }

    private void Particulas(Vector3 pos, Quaternion rot, Color cor, int quantidade,
                            float velocidade, float tamanho, float vida, float angulo)
    {
        var go = new GameObject("VFX");
        go.transform.SetPositionAndRotation(pos, rot);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = vida;
        main.startSpeed = velocidade;
        main.startSize = tamanho;
        main.startColor = cor;
        main.gravityModifier = 0.4f;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)quantidade) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = angulo;
        sh.radius = 0.03f;

        ps.GetComponent<ParticleSystemRenderer>().material = new Material(Shader.Find("Particles/Standard Unlit"));
        ps.Play();
    }

    private void Tracer(Vector3 de, Vector3 ate, bool grosso)
    {
        var go = new GameObject("Tracer");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, de);
        lr.SetPosition(1, ate);
        lr.startWidth = grosso ? 0.04f : 0.018f;
        lr.endWidth = 0.004f;
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = corFogo;
        lr.material = mat;
        lr.startColor = corFogo;
        lr.endColor = new Color(corFogo.r, corFogo.g, corFogo.b, 0f);
        Destroy(go, 0.05f);
    }

    /// <summary>
    /// Coice: sempre parte da pose de repouso guardada no Awake, nunca da rotacao atual.
    /// Ler a rotacao atual fazia dois tiros seguidos empilharem o desvio e a arma
    /// nunca voltava pro lugar que voce ajustou.
    /// </summary>
    private IEnumerator Recuo(float facing)
    {
        if (arma == null || !repousoValido) yield break;

        // "levantar o cano" e' girar em torno do eixo Z do MUNDO; converte pro espaco
        // do osso da mao, que gira com a animacao.
        Vector3 eixoLocal = arma.parent.InverseTransformDirection(Vector3.forward);
        Quaternion recuada = Quaternion.AngleAxis(RecuoGraus * facing, eixoLocal) * repousoLocal;

        float t = 0f;
        while (t < TempoRecuo)
        {
            float k = t / TempoRecuo;
            float curva = k < 0.22f ? (k / 0.22f) : 1f - ((k - 0.22f) / 0.78f);
            arma.localRotation = Quaternion.Slerp(repousoLocal, recuada, curva);
            t += Time.deltaTime;
            yield return null;
        }
        arma.localRotation = repousoLocal;
    }

    private static Transform FindDeep(Transform root, string nome)
    {
        if (root.name == nome) return root;
        foreach (Transform c in root)
        {
            var f = FindDeep(c, nome);
            if (f != null) return f;
        }
        return null;
    }
}
