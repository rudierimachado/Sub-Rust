using UnityEngine;

/// <summary>
/// Instancia o Player.prefab quando o jogo comeca em modo SOLO (sem rede nenhuma).
///
/// Antes desta sessao o Player era um objeto FIXO dentro da cena Fase01_Castelo.
/// Ele virou prefab (Assets/_Project/Prefabs/Player.prefab) pra poder ser spawnado
/// pelo Netcode em Co-op - e como so' pode existir UM prefab fonte de verdade
/// (senao Solo e Co-op divergiriam com o tempo), o modo Solo tambem passou a
/// INSTANCIAR o mesmo prefab em vez de contar com um Player ja' presente na cena.
///
/// Em Co-op este script nao faz nada: quem spawna o Player ali e' o proprio
/// Netcode, via NetworkConfig.PlayerPrefab (configurado no NetworkManager, cena
/// MenuPrincipal) - ver RedeSessao.Solo, o ponto unico que decide qual caminho
/// esta rodando.
///
/// Anexar num objeto raiz de CADA cena de fase, do mesmo jeito que CoreSceneLoader.
/// </summary>
public class PlayerSpawnerSolo : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private string nomeDoPontoDeSpawn = "PontoDeSpawn_Player";

    private void Awake()
    {
        if (!RedeSessao.Solo) return; // Co-op: o Netcode cuida do spawn.

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawnerSolo: playerPrefab nao atribuido no Inspector.", this);
            return;
        }

        var marco = GameObject.Find(nomeDoPontoDeSpawn);
        Vector3 pos = marco != null ? marco.transform.position : Vector3.zero;
        Quaternion rot = marco != null ? marco.transform.rotation : Quaternion.identity;
        if (marco == null)
            Debug.LogWarning("PlayerSpawnerSolo: '" + nomeDoPontoDeSpawn + "' nao encontrado nesta cena; nascendo na origem.", this);

        Instantiate(playerPrefab, pos, rot);
    }
}
