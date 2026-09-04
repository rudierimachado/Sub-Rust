using UnityEngine;

/// <summary>
/// Vai na raiz do prefab HUD. Resolve o problema de ORDEM: a HUD mora na cena Core,
/// carregada aditivamente DEPOIS da cena da fase, entao o Start do PlayerHealth /
/// PlayerShooting ja disparou os eventos quando ninguem estava escutando ainda.
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
        var vida = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
        if (vida == null) return;

        vida.PublicarEstado();

        var arma = FindAnyObjectByType<PlayerShooting>(FindObjectsInactive.Exclude);
        if (arma != null) arma.PublicarEstado();

        // PlayerCurrency e' estatico: os displays ja leem o valor atual no proprio OnEnable.
        pronto = true;
    }
}
