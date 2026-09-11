using UnityEngine;

/// <summary>
/// Decalque temporario (mancha de sangue, marca de queimado) que espera um tempo e
/// entao desaparece por transparencia antes de se destruir.
///
/// POR QUE SOME EM VEZ DE FICAR PRA SEMPRE
/// A barragem da Provedora sao 8 arremessos a cada 0,45 s e o jogador acerta varias
/// vezes por janela de cansaco. Marca permanente vira centenas de quads no chao numa
/// luta so' - custo de render e um chao que fica preto de tanta mancha. Com tempo de
/// vida a luta deixa rastro sem entulhar.
///
/// O material e' COMPARTILHADO entre todas as marcas (ver ImpactoDeGolpe), entao o
/// fade precisa de uma INSTANCIA propria - mexer no shared apagaria todas as manchas
/// da cena de uma vez. A instancia e' criada aqui, no primeiro frame do fade, e nao
/// no Awake: marca que morre antes de comecar a sumir nunca chega a instanciar nada.
/// </summary>
public class MarcaQueSome : MonoBehaviour
{
    [Tooltip("Segundos com a marca em opacidade cheia, antes de comecar a sumir.")]
    [SerializeField] private float duracao = 18f;
    [Tooltip("Segundos que leva sumindo, depois da duracao.")]
    [SerializeField] private float tempoDeFade = 4f;

    private Renderer render;
    private Material instancia;
    private float relogio;
    private float alphaInicial = 1f;

    public void Configurar(float segundos, float fade)
    {
        duracao = segundos;
        tempoDeFade = fade;
    }

    private void Awake()
    {
        render = GetComponent<Renderer>();
        if (render != null && render.sharedMaterial.HasProperty("_BaseColor"))
            alphaInicial = render.sharedMaterial.GetColor("_BaseColor").a;
    }

    private void Update()
    {
        relogio += Time.deltaTime;
        if (relogio < duracao) return;

        float k = tempoDeFade > 0f ? Mathf.Clamp01((relogio - duracao) / tempoDeFade) : 1f;

        if (render != null)
        {
            // Instancia so' agora, e uma unica vez.
            if (instancia == null) instancia = render.material;

            if (instancia.HasProperty("_BaseColor"))
            {
                var c = instancia.GetColor("_BaseColor");
                c.a = alphaInicial * (1f - k);
                instancia.SetColor("_BaseColor", c);
            }
        }

        if (k >= 1f) Destroy(gameObject);
    }
}
