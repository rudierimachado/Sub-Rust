using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Impacto ao tomar dano: reacao de golpe (animacao), tremida de camera, empurrao
/// para tras e um flash curto no corpo.
///
/// Anexar na RAIZ do Player (mesmo objeto do PlayerHealth e do CharacterController).
///
/// Sem vinheta vermelha cobrindo a tela: em side-scroller a leitura do combate
/// depende de enxergar o inimigo, e um overlay grande atrapalha exatamente no
/// momento em que mais importa ver.
/// </summary>
public class PlayerDamageFeedback : MonoBehaviour
{
    [Header("Reacao")]
    [SerializeField] private float duracaoTravaControle = 0.25f;

    [Header("Empurrao")]
    [SerializeField] private float forcaEmpurrao = 2.5f;
    [SerializeField] private float duracaoEmpurrao = 0.15f;

    [Header("Camera")]
    [SerializeField] private float forcaTremida = 1.6f;

    [Header("Congelamento no impacto")]
    [SerializeField] private float hitStop = 0.06f;

    [Header("Flash")]
    [SerializeField] private Color corFlash = new Color(1f, 0.25f, 0.2f);
    [SerializeField] private float duracaoFlash = 0.12f;

    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    private Animator animator;
    private CharacterController controller;
    private PlayerMovement2_5D movimento;
    private CinemachineImpulseSource impulso;
    private SkinnedMeshRenderer[] renderers;

    private Coroutine rotinaFlash;
    private Coroutine rotinaEmpurrao;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movimento = GetComponent<PlayerMovement2_5D>();

        var visual = transform.Find("Visual");
        animator = visual != null ? visual.GetComponent<Animator>() : GetComponentInChildren<Animator>();
        impulso = GetComponentInChildren<CinemachineImpulseSource>();
        renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    private void OnEnable() => PlayerHealth.OnDano += AoTomarDano;
    private void OnDisable() => PlayerHealth.OnDano -= AoTomarDano;

    private void AoTomarDano(float dano, Vector3 origem)
    {
        animator?.SetTrigger(HitTrigger);
        impulso?.GenerateImpulseWithForce(forcaTremida);

        // Empurra para o lado OPOSTO a quem bateu.
        float direcao = transform.position.x >= origem.x ? 1f : -1f;
        if (rotinaEmpurrao != null) StopCoroutine(rotinaEmpurrao);
        rotinaEmpurrao = StartCoroutine(Empurrar(direcao));

        if (rotinaFlash != null) StopCoroutine(rotinaFlash);
        rotinaFlash = StartCoroutine(Flash());

        HitStop.Aplicar(this, hitStop);
    }

    private IEnumerator Empurrar(float direcao)
    {
        // Trava o controle um instante: sem isso o jogador anda por cima do empurrao
        // e o golpe nao tem peso nenhum.
        if (movimento != null) movimento.enabled = false;

        float t = 0f;
        while (t < duracaoEmpurrao)
        {
            float atenuacao = 1f - (t / duracaoEmpurrao); // desacelera ate' parar
            controller.Move(Vector3.right * direcao * forcaEmpurrao * atenuacao * Time.deltaTime
                          + Vector3.down * 9.8f * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        float resto = duracaoTravaControle - duracaoEmpurrao;
        if (resto > 0f) yield return new WaitForSeconds(resto);

        if (movimento != null && !GetComponent<PlayerHealth>().Morto)
            movimento.enabled = true;
    }

    private IEnumerator Flash()
    {
        var originais = new Color[renderers.Length];
        var materiais = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            materiais[i] = renderers[i].material; // instancia - nao suja o asset
            originais[i] = materiais[i].HasProperty("_BaseColor") ? materiais[i].GetColor("_BaseColor") : Color.white;
            if (materiais[i].HasProperty("_BaseColor")) materiais[i].SetColor("_BaseColor", corFlash);
        }

        yield return new WaitForSeconds(duracaoFlash);

        for (int i = 0; i < renderers.Length; i++)
            if (materiais[i] != null && materiais[i].HasProperty("_BaseColor"))
                materiais[i].SetColor("_BaseColor", originais[i]);
    }
}
