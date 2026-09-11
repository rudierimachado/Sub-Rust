using TMPro;
using UnityEngine;

/// <summary>
/// Numero que aparece no mundo, sobe e some - o "+12" das almas ao matar um inimigo.
///
/// POR QUE NO MUNDO E NAO NA HUD
/// O feedback tem que sair de ONDE o inimigo morreu, senao o jogador nao liga o
/// numero a morte que acabou de causar. Numero pulando no canto da tela nao conta
/// essa historia.
///
/// Construido por codigo, sem prefab: e' o mesmo padrao do HudBarraDoChefe e do
/// ImpactoDeGolpe neste projeto, e evita mais um asset que alguem precisa lembrar
/// de ligar no Inspector.
///
/// Uso: NumeroFlutuante.Mostrar(posicao, "+12", cor);
/// </summary>
public class NumeroFlutuante : MonoBehaviour
{
    private TextMeshPro texto;
    private float relogio;
    private float duracao = 1.1f;
    private float subida = 1.3f;
    private Vector3 inicio;
    private Camera camera;

    /// <summary>Cria o numero no mundo. Ele se destroi sozinho quando termina.</summary>
    public static void Mostrar(Vector3 posicao, string conteudo, Color cor, float tamanho = 3.2f)
    {
        var go = new GameObject("NumeroFlutuante");
        go.transform.position = posicao;

        var t = go.AddComponent<TextMeshPro>();
        t.text = conteudo;
        t.fontSize = tamanho;
        t.color = cor;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        // Fila de transparente: sem isto o texto some atras da geometria da sala.
        t.GetComponent<MeshRenderer>().sortingOrder = 100;

        var n = go.AddComponent<NumeroFlutuante>();
        n.texto = t;
        n.inicio = posicao;
        n.camera = Camera.main;
    }

    private void Update()
    {
        relogio += Time.deltaTime;
        float k = relogio / duracao;

        if (k >= 1f) { Destroy(gameObject); return; }

        // Sobe desacelerando: o movimento chama atencao no comeco e entrega o numero
        // parado no fim, que e' quando o jogador de fato le'.
        transform.position = inicio + Vector3.up * (subida * Mathf.Sqrt(k));

        // Encara a camera. Recuperada a cada frame porque a Main Camera deste jogo
        // vive na cena Core (aditiva) e pode nao existir no frame em que o numero
        // nasce.
        if (camera == null) camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);

        // Some so' no fim: apagar desde o inicio deixa o numero ilegivel.
        var c = texto.color;
        c.a = k < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, k);
        texto.color = c;
    }
}
