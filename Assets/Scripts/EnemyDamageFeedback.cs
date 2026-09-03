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
    [SerializeField] private float forcaRecuo = 1.8f;
    [SerializeField] private float duracaoRecuo = 0.12f;
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
    public void Reagir(Vector3 origemDoGolpe)
    {
        animator?.SetTrigger(HitTrigger);

        float direcao = transform.position.x >= origemDoGolpe.x ? 1f : -1f;
        if (rotinaRecuo != null) StopCoroutine(rotinaRecuo);
        rotinaRecuo = StartCoroutine(Recuar(direcao));

        if (rotinaFlash != null) StopCoroutine(rotinaFlash);
        rotinaFlash = StartCoroutine(Flash());
    }

    private IEnumerator Recuar(float direcao)
    {
        float t = 0f;
        while (t < duracaoRecuo)
        {
            float atenuacao = 1f - (t / duracaoRecuo);
            if (controller != null && controller.enabled)
                controller.Move(Vector3.right * direcao * forcaRecuo * atenuacao * Time.deltaTime
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
