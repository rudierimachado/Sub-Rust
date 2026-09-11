using UnityEngine;

/// <summary>
/// Ve os guardioes (os dois morcegos) morrerem e larga a chave onde caiu o ultimo.
///
/// Por que aqui e nao dentro do EnemyHealth: o EnemyHealth e' o inimigo COMUM da fase
/// inteira; enfiar "as vezes solta a chave do atalho" nele espalharia a regra de uma
/// sala unica por todo inimigo do jogo. Este componente olha de fora, e o morcego nem
/// precisa saber que guarda alguma coisa.
///
/// A morte e' detectada por referencia NULA (o EnemyHealth destroi o objeto ao
/// morrer) em vez de evento, justamente pra nao depender de alterar o EnemyHealth.
/// </summary>
public class GuardioesDaChave : MonoBehaviour
{
    [SerializeField] private Transform[] guardioes;
    [SerializeField] private GameObject prefabDaChave;
    [SerializeField] private PortaDoAtalho porta;
    [Tooltip("Se preenchido, a chave cai aqui em vez de onde caiu o ultimo guardiao.")]
    [SerializeField] private Transform pontoDaQueda;
    [SerializeField] private float alturaDaChave = 0.55f;

    private Vector3 ultimaPosicaoConhecida;
    private bool jaSoltou;
    private bool algumJaExistiu;

    private void Update()
    {
        if (jaSoltou || guardioes == null || guardioes.Length == 0) return;

        bool algumVivo = false;
        foreach (var g in guardioes)
        {
            if (g == null) continue;
            algumVivo = true;
            algumJaExistiu = true;
            ultimaPosicaoConhecida = g.position;
        }

        // "!algumJaExistiu" evita soltar a chave no primeiro frame caso as
        // referencias nunca tenham apontado pra ninguem (lista mal ligada).
        if (algumVivo || !algumJaExistiu) return;

        SoltarChave();
    }

private void SoltarChave()
    {
        jaSoltou = true;
        if (prefabDaChave == null)
        {
            Debug.LogError("[GuardioesDaChave] sem prefab de chave - o atalho ficaria intransponivel.", this);
            return;
        }

        Vector3 onde;
        if (pontoDaQueda != null)
        {
            // Altar marcado: esta posicao é a autoridade, sem raycast que faria a chave cair no piso.
            onde = pontoDaQueda.position;
        }
        else
        {
            onde = ultimaPosicaoConhecida;
            if (Physics.Raycast(onde + Vector3.up * 0.5f, Vector3.down, out var hit, 12f))
                onde = hit.point;
        }

        onde += Vector3.up * alturaDaChave;

        var chave = Instantiate(prefabDaChave, onde, Quaternion.identity);
        chave.name = "Chave_Do_Atalho";
        var comp = chave.GetComponent<ChaveDoAtalho>();
        if (comp != null) comp.Apontar(porta);
        Debug.Log($"[GuardioesDaChave] guardioes mortos - chave em {onde}.", this);
    }
}
