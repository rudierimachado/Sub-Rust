using UnityEngine;

/// <summary>
/// Vai na raiz do prefab HUD. Resolve o problema de ORDEM: a HUD mora na cena Core,
/// carregada aditivamente DEPOIS da cena da fase, entao o Start do PlayerHealth
/// ja disparou os eventos quando ninguem estava escutando ainda.
///
/// Aqui a HUD PUXA o estado atual em vez de esperar o proximo evento. Roda tambem
/// quando o jogador aparece atrasado (troca de fase), tentando por alguns frames.
/// </summary>
public class HudBootstrap : MonoBehaviour
{
    [Tooltip("Por quantos segundos insistir procurando o jogador antes de desistir.")]
    [SerializeField] private float janelaDeBusca = 5f;

    private float prazo;
    private bool pronto;

    private void OnEnable()
    {
        pronto = false;
        prazo = Time.unscaledTime + janelaDeBusca;
        Sincronizar();
    }

    private void Update()
    {
        if (pronto) return;

        if (Time.unscaledTime > prazo)
        {
            Debug.LogWarning("HudBootstrap: nenhum PlayerHealth encontrado na janela de busca. " +
                             "A HUD fica em branco ate o jogador publicar algum evento.");
            enabled = false;
            return;
        }

        Sincronizar();
    }

    private void Sincronizar()
    {
        // Em Co-op existem DOIS PlayerHealth na cena (o local e a copia de rede do
        // outro jogador) - pegar "qualquer um" arriscaria sincronizar a HUD com a
        // vida/municao de quem nao e' voce. So' conta o Player cujo PlayerNetwork
        // diz que EhDono (que em modo Solo e' sempre true, sem rede nenhuma).
        PlayerHealth vidaDoDono = null;
        foreach (var vida in FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var rede = vida.GetComponent<PlayerNetwork>();
            if (rede == null || rede.EhDono) { vidaDoDono = vida; break; }
        }
        if (vidaDoDono == null) return;

        vidaDoDono.PublicarEstado();

        // PlayerCurrency e' estatico: os displays ja leem o valor atual no proprio OnEnable.
        pronto = true;
    }
}
