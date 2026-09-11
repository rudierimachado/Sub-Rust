using UnityEngine;

/// <summary>
/// Tremulacao da luz de uma fornalha/lareira.
///
/// Existe porque a Cozinha 10 tinha duas "fornalhas" que eram Point Lights de
/// intensidade CONSTANTE e nenhuma particula: o fogao aparecia apagado e mesmo
/// assim iluminava, o que o olho le como cenario falso na hora.
///
/// Nao usa Random.value por frame: isso vira ruido branco e pisca feito lampada
/// com mau contato. Perlin sobre o tempo da' a oscilacao lenta e continua de
/// chama de verdade. Duas oitavas porque uma so' fica com cara de senoide.
/// </summary>
[RequireComponent(typeof(Light))]
public class FogoFornalha : MonoBehaviour
{
    [Tooltip("Intensidade media da luz. A tremulacao oscila em volta dela.")]
    [SerializeField] private float intensidadeBase = 9f;

    [Tooltip("Quanto a intensidade varia para cima e para baixo, em fracao da base.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float amplitude = 0.28f;

    [Tooltip("Velocidade da oscilacao lenta (respiro da brasa).")]
    [SerializeField] private float velocidadeLenta = 0.9f;

    [Tooltip("Velocidade da oscilacao rapida (lambida da chama).")]
    [SerializeField] private float velocidadeRapida = 4.3f;

    [Tooltip("Quanto o ponto de luz se desloca junto, em metros. Fogo nao fica parado.")]
    [SerializeField] private float deslocamento = 0.07f;

    private Light luz;
    private Vector3 origem;
    private float semente;

    private void Awake()
    {
        luz = GetComponent<Light>();
        origem = transform.localPosition;
        // Semente por instancia: sem isso as duas fornalhas da sala tremulam
        // em sincronia perfeita e denunciam que e' o mesmo script.
        semente = Random.Range(0f, 100f);
        if (intensidadeBase <= 0f) intensidadeBase = luz.intensity;
    }

    private void Update()
    {
        float t = Time.time;
        float lenta  = Mathf.PerlinNoise(semente, t * velocidadeLenta)  - 0.5f;
        float rapida = Mathf.PerlinNoise(semente + 31.7f, t * velocidadeRapida) - 0.5f;
        float mistura = lenta * 1.4f + rapida * 0.6f;

        luz.intensity = intensidadeBase * (1f + amplitude * mistura * 2f);

        if (deslocamento > 0f)
        {
            float dx = Mathf.PerlinNoise(semente + 11f, t * velocidadeRapida) - 0.5f;
            float dy = Mathf.PerlinNoise(semente + 53f, t * velocidadeRapida) - 0.5f;
            transform.localPosition = origem + new Vector3(dx, dy * 0.6f, 0f) * (deslocamento * 2f);
        }
    }
}
