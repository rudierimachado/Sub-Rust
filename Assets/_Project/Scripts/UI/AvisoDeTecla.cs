using TMPro;
using UnityEngine;

/// <summary>
/// O aviso "[F]" que flutua sobre o inimigo exausto enquanto a finalizacao esta'
/// disponivel.
///
/// POR QUE NAO E' UM NumeroFlutuante
/// Aquele nasce, sobe e morre em 1,1 s - certo pra "+3 almas", que e' um fato que ja'
/// aconteceu. Este e' o oposto: um convite que precisa ficar na tela EXATAMENTE
/// enquanto a janela existe e sumir no instante em que ela fecha. Se ele durasse um
/// tempo fixo, o jogador apertaria F depois do aviso ter mentido pra ele.
///
/// POR QUE SOBRE O INIMIGO E NAO NA HUD
/// Um prompt no canto da tela obriga o olho a sair do combate pra ler, e volta sem
/// saber em QUEM executar quando ha' mais de um inimigo. Em cima da cabeca dele o
/// aviso ja' responde as duas coisas: o que apertar e em quem.
///
/// Construido por codigo, sem prefab - mesmo padrao do NumeroFlutuante e do
/// HudBarraDoChefe neste projeto.
///
/// Uso: um so' por jogador, controlado por Mostrar/Esconder (ver FinalizacaoCinematica).
/// </summary>
public class AvisoDeTecla : MonoBehaviour
{
    [SerializeField] private Color cor = new Color(1f, 0.86f, 0.35f);
    [Tooltip("Altura acima do topo do alvo.")]
    [SerializeField] private float alturaAcima = 0.55f;
    [Tooltip("Pulsacoes por segundo. E' o que separa 'aviso ativo' de 'enfeite'.")]
    [SerializeField] private float pulsacao = 2.6f;
    [Tooltip("Quanto ele cresce e encolhe ao pulsar, em fracao do tamanho.")]
    [SerializeField] private float amplitude = 0.18f;
    [Tooltip("Segundos pra aparecer/sumir. Curto: a janela toda dura poucos segundos.")]
    [SerializeField] private float tempoDeFade = 0.12f;
    [SerializeField] private float tamanhoDaFonte = 4.2f;

    private TextMeshPro texto;
    private Transform alvo;
    private Camera camera;
    private float visibilidade;
    private float tamanhoBase;

    /// <summary>Cria o aviso, escondido. Chame Mostrar pra liga-lo.</summary>
    public static AvisoDeTecla Criar(string tecla)
    {
        var go = new GameObject("AvisoDeTecla");

        var t = go.AddComponent<TextMeshPro>();
        t.text = "[" + tecla + "]";
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        // Fila de transparente por cima de tudo: sem isto o aviso some atras da
        // geometria da sala, que e' onde ele mais precisa ser visto.
        t.GetComponent<MeshRenderer>().sortingOrder = 200;

        var a = go.AddComponent<AvisoDeTecla>();
        a.texto = t;
        t.fontSize = a.tamanhoDaFonte;
        a.tamanhoBase = a.tamanhoDaFonte;
        a.camera = Camera.main;
        a.Esconder();
        return a;
    }

    /// <summary>Passa a acompanhar este alvo. Chamar todo frame e' barato e mantem o
    /// aviso colado no inimigo certo quando o foco troca.</summary>
    public void Mostrar(Transform novoAlvo)
    {
        alvo = novoAlvo;
    }

    public void Esconder()
    {
        alvo = null;
    }

    private void LateUpdate()
    {
        bool ativo = alvo != null;

        // Tempo NAO ESCALADO: o hitstop derruba o timeScale pra 0,05, e o aviso
        // demoraria quase um segundo real pra aparecer no momento exato em que o
        // jogador precisa ve-lo.
        visibilidade = Mathf.MoveTowards(visibilidade, ativo ? 1f : 0f,
                                         Time.unscaledDeltaTime / Mathf.Max(0.01f, tempoDeFade));

        if (visibilidade <= 0.001f)
        {
            if (texto.enabled) texto.enabled = false;
            return;
        }
        if (!texto.enabled) texto.enabled = true;

        if (ativo) transform.position = new Vector3(alvo.position.x, TopoDe(alvo) + alturaAcima, alvo.position.z);

        // Pulsa no tamanho, nao na opacidade: aviso piscando compete com o resto da
        // tela; aviso respirando chama o olho sem virar alarme.
        float p = 1f + Mathf.Sin(Time.unscaledTime * pulsacao * Mathf.PI * 2f) * amplitude;
        texto.fontSize = tamanhoBase * p * visibilidade;

        var c = cor;
        c.a = visibilidade;
        texto.color = c;

        // Encara a camera. Recuperada a cada frame porque a Main Camera vive na cena
        // Core (aditiva) e pode nao existir no frame em que este objeto nasce.
        if (camera == null) camera = Camera.main;
        if (camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camera.transform.position);
    }

    /// <summary>Topo real do alvo, pro aviso nao afundar no morcego nem flutuar longe
    /// da cabeca do chefe. Mesmo criterio do FocoDeAlvo.</summary>
    private static float TopoDe(Transform t)
    {
        var r = t.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.max.y;
        var col = t.GetComponent<Collider>();
        if (col != null) return col.bounds.max.y;
        return t.position.y + 2f;
    }
}
