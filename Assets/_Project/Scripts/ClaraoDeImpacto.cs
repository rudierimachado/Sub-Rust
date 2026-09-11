using UnityEngine;

/// <summary>
/// Apaga uma Light em rampa e some. Existe porque um impacto que ACENDE o chao le'
/// muito melhor que so' particula, mas uma luz que fica acesa com intensidade fixa
/// e' pior que nenhuma - fica um poste laranja no meio da sala depois do respingo.
///
/// Generico de proposito (nao e' do caldo): qualquer FX de impacto do jogo pode usar.
/// Anexar no objeto que tem a Light. Se 'destruirObjeto' estiver ligado, ele se
/// destroi no fim - use quando a luz for filha de um FX que se limpa sozinho pelo
/// stopAction do ParticleSystem e a luz nao deva sobreviver a ele.
/// </summary>
[RequireComponent(typeof(Light))]
public class ClaraoDeImpacto : MonoBehaviour
{
    [Tooltip("Segundos da intensidade cheia ate' zero.")]
    [SerializeField] private float duracao = 0.35f;
    [SerializeField] private bool destruirObjeto = true;

    private Light luz;
    private float intensidadeInicial;
    private float relogio;

    private void Awake()
    {
        luz = GetComponent<Light>();
        intensidadeInicial = luz.intensity;
    }

    /// <summary>Para quem cria o clarao por codigo (ver ImpactoDeGolpe), que nao tem
    /// Inspector pra preencher. Chamar logo depois do AddComponent: o Awake ja' rodou
    /// e guardou a intensidade, entao aqui so' resta ajustar a duracao.</summary>
    public void Configurar(float segundos, bool destruirNoFim)
    {
        duracao = segundos;
        destruirObjeto = destruirNoFim;
    }

    private void Update()
    {
        relogio += Time.deltaTime;
        float k = duracao > 0f ? Mathf.Clamp01(relogio / duracao) : 1f;

        // Quadratica: cai rapido no comeco (o clarao do impacto) e some suave no fim,
        // em vez do corte seco de uma rampa linear.
        luz.intensity = intensidadeInicial * (1f - k) * (1f - k);

        if (k >= 1f && destruirObjeto) Destroy(gameObject);
    }
}
