using UnityEngine;

/// <summary>
/// Projetil de arco (parabola) para inimigo a distancia. Move por script, nao por
/// Rigidbody - o projeto inteiro mede colisao com raycast manual (ver
/// EnemyAI.TemChaoAFrente/TemParedeAFrente, VarreduraDeLamina.CapsuleCastNonAlloc); um
/// Rigidbody aqui seria um segundo jeito de detectar acerto, com sua propria camada
/// de bugs de fisica/camada de colisao.
///
/// Generico de proposito: nao pertence a nenhum chefe. Qualquer inimigo a distancia
/// futuro instancia isto e chama Lancar - ver ChefeCozinha_Provedora para o uso.
///
/// A parabola (altura) e' resolvida no plano X/Y como em qualquer 2.5D, mas o Z NAO
/// fica congelado no valor de lancamento: ele caminha em linha reta ate' o Z que o
/// alvo tinha no disparo. Isso e' obrigatorio desde que o arremessador ganhou
/// liberdade de andar em profundidade - lancando de Z=-2 com o Z travado, o caldo
/// passava 2 m ao lado do jogador e o teste de acerto (distancia 3D contra
/// raioDeAcerto 0,62) NUNCA podia passar. Continua sem "mirar em profundidade" no
/// sentido de perseguir: o Z do alvo e' lido uma vez, no lancamento.
/// </summary>
public class ProjetilArremessado : MonoBehaviour
{
    [SerializeField] private float raioDeAcerto = 0.40f;
    [SerializeField] private float raioContraCenario = 0.18f;
    [SerializeField] private float vidaUtilMaxima = 4f;
    [SerializeField] private LayerMask mascaraCenario = ~0;
    [Tooltip("Respingo instanciado no ponto de impacto, acertando chao OU jogador. Opcional.")]
    [SerializeField] private GameObject prefabImpacto;

    private Vector3 velocidade;
    private float gravidade;
    private float dano;
    private float relogio;
    private float zInicial;
    private float zFinal;
    private float duracao;
    private Transform alvo;
    private bool lancado;

    /// <summary>
    /// Configura a parabola para o projetil sair daqui e cruzar 'pontoDeMira' no fim
    /// do voo. Nao segue o alvo depois de lancado - e' um arremesso, nao um missil
    /// teleguiado; quem decide ONDE mirar e' o arremessador (ver
    /// ChefeCozinha_Provedora.PreverPosicaoDoJogador), e uma vez solto o caldo obedece
    /// so' a parabola.
    ///
    /// 'alvoParaAcerto' continua sendo passado porque o teste de acerto e' por
    /// DISTANCIA ate' o jogador durante o voo - o ponto de mira e' onde ele vai
    /// cruzar, nao a unica coisa em que pode bater.
    ///
    /// 'tempoDeVoo' agora VEM DA DISTANCIA (o chefe calcula distancia/velocidade de
    /// lancamento) em vez de ser fixo. Com tempo fixo, a mesma panela saia a 17 m/s
    /// de longe e a 5 m/s de perto - o contrario de um braco de verdade, onde a forca
    /// e' mais ou menos constante e o que cresce com a distancia e' o TEMPO.
    ///
    /// Formula fechada de projetil: para y(t) = y0 + vy*t + 0.5*g*t^2 valer y(0)=0,
    /// y(tempoDeVoo)=dy (altura do alvo) e ter o pico em alturaDoArco no meio do
    /// voo, resolve-se g = -8*alturaDoArco / t^2  e  vy = dy/t - 0.5*g*t.
    /// </summary>
    public void Lancar(Transform alvoParaAcerto, Vector3 pontoDeMira,
                       float alturaDoArco, float tempoDeVoo, float danoDoAcerto)
    {
        alvo = alvoParaAcerto;
        dano = danoDoAcerto;
        zInicial = transform.position.z;
        zFinal = pontoDeMira.z;

        float t = Mathf.Max(tempoDeVoo, 0.05f);
        duracao = t;
        float dx = pontoDeMira.x - transform.position.x;
        float dy = pontoDeMira.y - transform.position.y;

        gravidade = -8f * alturaDoArco / (t * t);
        float velY = dy / t - 0.5f * gravidade * t;

        velocidade = new Vector3(dx / t, velY, 0f);
        lancado = true;
    }

    private void Update()
    {
        if (!lancado) return;

        relogio += Time.deltaTime;
        if (relogio > vidaUtilMaxima) { Destroy(gameObject); return; }

        velocidade.y += gravidade * Time.deltaTime;
        Vector3 proximo = transform.position + velocidade * Time.deltaTime;
        proximo.z = Mathf.Lerp(zInicial, zFinal, Mathf.Clamp01(relogio / duracao));

        // Acertou cenario no caminho? Para e desaparece - nao atravessa parede/piso.
        Vector3 delta = proximo - transform.position;
        float dist = delta.magnitude;
        if (dist > 0.0001f &&
            Physics.SphereCast(transform.position, raioContraCenario, delta.normalized, out var hitCenario,
                                dist, mascaraCenario, QueryTriggerInteraction.Ignore) &&
            hitCenario.collider.GetComponentInParent<IDamageable>() == null)
        {
            Estourar(hitCenario.point);
            return;
        }

        transform.position = proximo;

        // Acertou o jogador? Checagem por distancia, nao por trigger - mesmo padrao
        // de medicao direta do resto do projeto.
        if (alvo != null && Vector3.Distance(transform.position, alvo.position) <= raioDeAcerto)
        {
            alvo.GetComponentInParent<IDamageable>()?.TakeHit(dano, transform.position);
            Estourar(transform.position);
        }
    }

    private void Estourar(Vector3 ponto)
    {
        if (prefabImpacto != null) Instantiate(prefabImpacto, ponto, Quaternion.identity);
        MarcaDeCaldo(ponto);
        Destroy(gameObject);
    }

    /// <summary>Mancha escura de caldo derramado no chao. Mesma ideia da marca de
    /// sangue do ImpactoDeGolpe: a luta deixa rastro em vez de resetar a cada golpe -
    /// depois de uma barragem da' pra ler no chao por onde o caldo passou.
    ///
    /// Sonda o chao ignorando a layer Inimigo (8), senao a mancha gruda no corpo de
    /// quem foi atingido e sai andando junto com ele.</summary>
    private void MarcaDeCaldo(Vector3 ponto)
    {
        if (!Physics.Raycast(ponto + Vector3.up * 0.3f, Vector3.down, out var hit, 4f,
                             ~(1 << 8), QueryTriggerInteraction.Ignore)) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "MarcaDeCaldo";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = hit.point + Vector3.up * 0.01f;   // z-fighting com o piso
        go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        float d = Random.Range(0.5f, 0.95f);
        go.transform.localScale = new Vector3(d, d * Random.Range(0.75f, 1.25f), 1f);

        if (materialDaMancha == null)
        {
            materialDaMancha = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            materialDaMancha.SetFloat("_Surface", 1f);
            materialDaMancha.SetFloat("_Blend", 0f);
            materialDaMancha.SetColor("_BaseColor", new Color(0.14f, 0.09f, 0.03f, 0.8f));
            materialDaMancha.renderQueue = 3000;
        }
        go.GetComponent<MeshRenderer>().sharedMaterial = materialDaMancha;
        go.AddComponent<MarcaQueSome>().Configurar(Random.Range(10f, 16f), 4f);
    }

    // Compartilhado por todos os projeteis: um material por estouro vazaria memoria
    // numa barragem de 8 arremessos a cada 0,45 s.
    private static Material materialDaMancha;
}
