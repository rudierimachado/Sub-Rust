using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vai num objeto raiz de CADA cena de fase. Garante que a cena persistente "Core"
/// (HUD + EventSystem) esteja carregada aditivamente, sem duplicar se ela ja estiver.
///
/// Existe porque o jogo tera 8 fases: a HUD nao pode viver dentro do arquivo .unity de
/// uma fase, senao cada fase nova precisaria remontar (e manter) a propria copia.
/// </summary>
public class CoreSceneLoader : MonoBehaviour
{
    public const string NomeDaCena = "Core";

    private void Awake()
    {
        if (SceneManager.GetSceneByName(NomeDaCena).isLoaded) return;

        // Carregar a fase direto pelo editor (sem passar por menu) e' o fluxo normal
        // deste projeto, entao a carga aditiva acontece aqui e nao num bootstrap global.
        SceneManager.LoadScene(NomeDaCena, LoadSceneMode.Additive);
    }
}
