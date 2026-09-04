using System.Collections;
using UnityEngine;

/// <summary>
/// Impacto quando o INIMIGO leva dano: reacao de golpe (interrompe o ataque dele),
/// flash no corpo e recuo. E' o espelho do PlayerDamageFeedback.
///
/// Anexar na raiz do inimigo (mesmo objeto do EnemyHealth).
/// </summary>
public class EnemyDamageFeedback : MonoBehaviour
{
    [SerializeField] private float forcaRecuo = 7f;
    [SerializeField] private float duracaoRecuo = 0.18f;

    [Header("Escala pelo dano")]
    [Tooltip("Dano que corresponde a um empurrao de forca 1x. Acima disso empurra mais, abaixo menos.")]
    [SerializeField] private float danoDeReferencia = 60f;
    [SerializeField] private float escalaMinima = 0.45f;
    [SerializeField] private float escalaMaxima = 2.5f;
    [SerializeField] private Color corFlash = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private float duracaoFlash = 0.10f;

    private static readonly int HitTrigger = Animator.StringToHash("Hit");

    private Animator animator;
    private CharacterController controller;
    private SkinnedMeshRenderer[] renderers;
    private Coroutine rotinaFlash, rotinaRecuo;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    /// <summary>Chamado pelo EnemyHealth quando o golpe conecta.</summary>
    public void Reagir(Vector3 origemDoGolpe) => Reagir(origemDoGolpe, danoDeReferencia);

    /// <summary>Versao com dano: o empurrao ESCALA com a pancada, entao um tiro de
    /// espingarda a queima-roupa joga o inimigo pra tras e um de raspao so' o balanca.
    /// Sem isso todo acerto empurrava igual, e a espingarda nao tinha peso.</summary>
    public void Reagir(Vector3 origemDoGolpe, float dano)
    {
        animator?.SetTrigger(HitTrigger);

        float escala = Mathf.Clamp(dano / Mathf.Max(0.01f, danoDeReferencia), escalaMinima, escalaMaxima);
        float direcao = transform.position.x >= origemDoGolpe.x ? 1f : -1f;
        if (rotinaRecuo != null) StopCoroutine(rotinaRecuo);
        rotinaRecuo = StartCoroutine(Recuar(direcao, escala));

        if (rotinaFlash != null) StopCoroutine(rotinaFlash);
        rotinaFlash = StartCoroutine(Flash());
    }

    private IEnumerator Recuar(float direcao, float escala)
    {
        float t = 0f;
        float duracao = duracaoRecuo * Mathf.Lerp(0.8f, 1.25f, Mathf.InverseLerp(escalaMinima, escalaMaxima, escala));
        while (t < duracao)
        {
            float atenuacao = 1f - (t / duracao);
            if (controller != null && controller.enabled)
                controller.Move(Vector3.right * direcao * forcaRecuo * escala * atenuacao * Time.deltaTime
                              + Vector3.down * 9.8f * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Flash()
    {
        var materiais = new Material[renderers.Length];
        var originais = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            materiais[i] = renderers[i].material; // instancia, nao suja o .mat do projeto
            if (materiais[i].HasProperty("_BaseColor"))
            {
                originais[i] = materiais[i].GetColor("_BaseColor");
                materiais[i].SetColor("_BaseColor", corFlash);
            }
        }

        yield return new WaitForSeconds(duracaoFlash);

        for (int i = 0; i < renderers.Length; i++)
            if (materiais[i] != null && materiais[i].HasProperty("_BaseColor"))
                materiais[i].SetColor("_BaseColor", originais[i]);
    }
}
