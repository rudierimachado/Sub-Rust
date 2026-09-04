using UnityEngine;

/// <summary>
/// Escada de mao vertical (tipo escada de bombeiro). ELA SE MEDE SOZINHA: X, Z, base
/// e topo saem da geometria real dos filhos "Degrau*" e de uma sondagem do piso.
/// Copiar/mover a escada e' suficiente - nao existe numero pra digitar.
///
/// Por que automatico (medido em setembro/2026, antes de existir): os valores eram
/// digitados a mao e ficavam para tras quando a escada era copiada ou movida.
///   - Escada_1: TopoY=-27,00 com piso real em -28,10 (1,10m de erro). Fora da
///     tolerancia de saida => o jogador chegava no topo e FICAVA PRESO na escada.
///   - Escada_1: X=90,80 e Z=1,06 com degraus em X=91,00 e Z=1,39 - o corpo subia
///     torto, ao lado dos degraus.
///   - escada_ponte: X=73,80 com degraus em 74,42 (0,62m fora).
/// Nada disso dava erro de compilacao nem aparecia no console.
///
/// O topo NAO e' o fim dos trilhos: as escadas deste projeto prolongam os trilhos
/// ~1,45m acima do piso de chegada de proposito (pegador pra sair). Por isso o topo
/// e' o PISO sondado, nao a altura da geometria.
///
/// Nao usa OnEnable/lista estatica de proposito - ver PROJETO.md.
/// </summary>
[DisallowMultipleComponent]
public class EscadaDeMao : MonoBehaviour
{
    /// <summary>Afastamento lateral usado na sondagem de piso feita fora do Play.</summary>
    private const float AfastamentoPadrao = 0.9f;

    [Header("Ajustes")]
    [Tooltip("Distancia em X dentro da qual o jogador consegue agarrar.")]
    [SerializeField] private float alcanceX = 0.9f;

    [Tooltip("Prefixo dos filhos que contam como degrau na medicao.")]
    [SerializeField] private string prefixoDegrau = "Degrau";

    [Tooltip("Plano Z em que o jogador caminha. O projeto trava o jogador em Z=0.")]
    [SerializeField] private float zCaminhavel = 0f;

    [Tooltip("Quanto o corpo fica na FRENTE dos degraus (lado da camera), pra nao clipar na escada.")]
    [SerializeField] private float folgaZ = 0.32f;

    [Header("Medido sozinho - nao editar a mao")]
    [SerializeField] private float x;
    [SerializeField] private float z;
    [SerializeField] private float baseY;
    [SerializeField] private float topoY;
    [SerializeField] private float topoGeometria;

    [Tooltip("Desligue apenas se a escada nao tiver degraus nomeados ou o piso nao puder ser sondado.")]
    [SerializeField] private bool medirAutomaticamente = true;

    private bool medida;

    public float X { get { GarantirMedida(); return x; } }
    public float Z { get { GarantirMedida(); return z; } }
    public float BaseY { get { GarantirMedida(); return baseY; } }
    public float TopoY { get { GarantirMedida(); return topoY; } }
    public float AlcanceX => alcanceX;
    public float ZCaminhavel => zCaminhavel;

    /// <summary>Z em que o corpo fica agarrado: SEMPRE no lado da camera (-Z), nunca
    /// atras dos degraus. Encostar no Z exato do degrau enfia o corpo na escada.
    ///
    /// E' -Z e ponto final, nao "o lado de onde o jogador veio": a camera deste jogo
    /// fica presa em playerZ - 14 olhando para +Z (ver PROJETO.md), entao a frente e'
    /// o Z menor, sempre. Os dois criterios coincidem nas escadas dos Arquivos Altos
    /// (degraus em Z=+1,33, jogador em Z=0) e se INVERTEM na escada_ponte, cujos
    /// degraus ficam em Z=-2,79: la' o criterio "de onde o jogador veio" punha o corpo
    /// atras dos degraus, escondido pela propria escada.</summary>
    public float ZAgarrado
    {
        get { GarantirMedida(); return z - folgaZ; }
    }

    private void Awake() => Medir();

    private void GarantirMedida()
    {
        if (!medida) Medir();
    }

    [ContextMenu("Medir agora")]
    public void Medir()
    {
        medida = true;
        if (!medirAutomaticamente) return;
        if (!MedirGeometria()) return;

        // Base e topo sao PISOS sondados, nao a altura dos degraus: em cima o trilho
        // passa do piso de chegada, e embaixo nem sempre encosta no chao (a
        // escada_ponte tem o degrau mais baixo 1,20m acima do piso da base).
        baseY = SondarPiso(x, baseY + 0.6f, 8f, float.NegativeInfinity, baseY + 0.6f) ?? baseY;

        float teto = topoGeometria + 0.2f;
        float piso = baseY + 0.8f;
        float melhor = float.NegativeInfinity;
        foreach (float dx in new[] { AfastamentoPadrao, -AfastamentoPadrao, 0f })
        {
            float? h = SondarPiso(x + dx, topoGeometria + 0.5f, topoGeometria - baseY + 2f, piso, teto);
            if (h.HasValue && h.Value > melhor) melhor = h.Value;
        }
        topoY = melhor > float.NegativeInfinity ? melhor : topoGeometria;
    }

    /// <summary>X/Z/altura dos degraus. Devolve false se a escada nao tiver degrau nenhum.</summary>
    private bool MedirGeometria()
    {
        float somaX = 0f, somaZ = 0f, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        int n = 0;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(prefixoDegrau)) continue;
            somaX += t.position.x;
            somaZ += t.position.z;
            if (t.position.y < minY) minY = t.position.y;
            if (t.position.y > maxY) maxY = t.position.y;
            n++;
        }

        if (n == 0)
        {
            Debug.LogError("EscadaDeMao '" + name + "': nenhum filho comecando com \"" + prefixoDegrau +
                           "\". Sem degrau nao da pra medir X/Z/altura - a escada fica inalcancavel.", this);
            return false;
        }

        x = somaX / n;
        z = somaZ / n;
        baseY = minY;
        topoGeometria = maxY;
        return true;
    }

    /// <summary>Piso mais alto abaixo de 'deY', dentro da faixa [minAceito, maxAceito].
    /// Ignora os colisores da propria escada e do jogador.</summary>
    private float? SondarPiso(float xProbe, float deY, float alcance, float minAceito, float maxAceito)
    {
        var hits = Physics.RaycastAll(new Vector3(xProbe, deY, zCaminhavel), Vector3.down,
                                      alcance, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.NegativeInfinity;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.point.y < minAceito || h.point.y > maxAceito) continue;
            if (h.point.y > melhor) melhor = h.point.y;
        }
        return melhor > float.NegativeInfinity ? melhor : (float?)null;
    }

    /// <summary>Acha onde pousar ao sair no topo. Tenta primeiro o lado que o jogador
    /// pediu (A/D), depois o outro, e o centro por ultimo - sair no X da escada costuma
    /// cair justamente no buraco por onde ela sobe.</summary>
    public bool TentarAcharSaida(float zPlano, float raioJogador, float ladoPreferido, out Vector3 destino)
    {
        GarantirMedida();
        float af = Mathf.Max(AfastamentoPadrao, raioJogador + 0.55f);
        float lado = ladoPreferido >= 0f ? 1f : -1f;
        // Centro por ultimo: o X da escada no topo costuma ser o proprio buraco.
        return Pousar(topoY, zPlano, new[] { x + lado * af, x - lado * af, x }, out destino);
    }

    /// <summary>Onde pousar ao terminar a DESCIDA. Ao contrario do topo, o centro vem
    /// primeiro: o pe da escada normalmente e' chao firme, e sair de lado sem
    /// necessidade faz o personagem dar um passo torto ao encostar no chao.</summary>
    public bool TentarAcharPousoNaBase(float zPlano, float raioJogador, float ladoPreferido, out Vector3 destino)
    {
        GarantirMedida();
        float af = Mathf.Max(AfastamentoPadrao, raioJogador + 0.55f);
        float lado = ladoPreferido >= 0f ? 1f : -1f;
        return Pousar(baseY, zPlano, new[] { x, x + lado * af, x - lado * af }, out destino);
    }

    /// <summary>Sonda o piso real em cada X candidato, na altura pedida, e devolve o
    /// primeiro que servir. E' isso que faz a chegada ficar rente ao piso por medicao,
    /// em vez de depender de um numero serializado bater com a geometria.</summary>
    private bool Pousar(float alturaAlvo, float zPlano, float[] candidatos, out Vector3 destino)
    {
        destino = default;

        foreach (float cx in candidatos)
        {
            var hits = Physics.RaycastAll(new Vector3(cx, alturaAlvo + 1.0f, zPlano), Vector3.down,
                                          2.0f, ~0, QueryTriggerInteraction.Ignore);
            float melhor = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                // Aceita qualquer piso perto da altura medida: e' o mesmo patamar, mesmo
                // que a peca tenha alguns centimetros de diferenca.
                if (Mathf.Abs(h.point.y - alturaAlvo) > 0.35f) continue;
                if (h.point.y > melhor) melhor = h.point.y;
            }

            if (melhor <= float.NegativeInfinity) continue;

            // Um pelinho acima do piso: comecar exatamente na superficie faz o
            // CharacterController nascer dentro dela.
            destino = new Vector3(cx, melhor + 0.02f, zPlano);
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        // So' geometria aqui: OnValidate roda durante serializacao e disparar raycast
        // nesse momento e' pedir problema. A sondagem de piso acontece no Awake e no
        // menu de contexto "Medir agora".
        if (medirAutomaticamente) MedirGeometria();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(x, baseY, z), new Vector3(x, topoY, z));
        Gizmos.DrawWireSphere(new Vector3(x, baseY, z), alcanceX);
        Gizmos.DrawWireSphere(new Vector3(x, topoY, z), alcanceX);

        // ciano = coluna real onde o corpo fica, ja com a folga da frente
        Gizmos.color = Color.cyan;
        float zc = z - folgaZ;
        Gizmos.DrawLine(new Vector3(x, baseY, zc), new Vector3(x, topoY, zc));
    }
}
