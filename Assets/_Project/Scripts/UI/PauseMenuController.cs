using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Overlay global de pausa. Vive na cena Core para funcionar em qualquer fase.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    private const string MenuSceneName = "MenuPrincipal";

    private VisualElement root;
    private Label status;
    private bool pausado;
    private float escalaAntesDaPausa = 1f;

    private void OnEnable()
    {
        var document = GetComponent<UIDocument>();
        root = document.rootVisualElement.Q<VisualElement>("pause-root");
        status = document.rootVisualElement.Q<Label>("lbl-pause-status");

        Ligar(document.rootVisualElement, "btn-continuar", Continuar);
        Ligar(document.rootVisualElement, "btn-salvar", SalvarAindaNaoDisponivel);
        Ligar(document.rootVisualElement, "btn-menu", VoltarAoMenu);
        Ligar(document.rootVisualElement, "btn-sair", SairDoJogo);

        DefinirVisivel(false);
    }

    private void Update()
    {
        if (EstaNoMenuPrincipal()) return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

        if (pausado) Continuar();
        else Pausar();
    }

    private void Pausar()
    {
        if (pausado) return;

        pausado = true;
        escalaAntesDaPausa = Time.timeScale <= 0f ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        DefinirVisivel(true);
        EscreverStatus(string.Empty);
    }

    private void Continuar()
    {
        if (!pausado) return;

        pausado = false;
        Time.timeScale = escalaAntesDaPausa;
        DefinirVisivel(false);
        EscreverStatus(string.Empty);
    }

    private void VoltarAoMenu()
    {
        Time.timeScale = escalaAntesDaPausa <= 0f ? 1f : escalaAntesDaPausa;
        pausado = false;
        SceneManager.LoadScene(MenuSceneName, LoadSceneMode.Single);
    }

    private void SalvarAindaNaoDisponivel()
    {
        EscreverStatus("Salvar sera ligado quando o sistema de save entrar.");
    }

    private void SairDoJogo()
    {
        Time.timeScale = escalaAntesDaPausa <= 0f ? 1f : escalaAntesDaPausa;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void DefinirVisivel(bool visivel)
    {
        root?.EnableInClassList("escondido", !visivel);
    }

    private void EscreverStatus(string texto)
    {
        if (status != null) status.text = texto;
    }

    private static void Ligar(VisualElement raiz, string nome, System.Action acao)
    {
        var botao = raiz.Q<Button>(nome);
        if (botao == null)
        {
            Debug.LogError("PauseMenuController: botao \"" + nome + "\" nao existe no UXML.");
            return;
        }

        botao.clicked += acao;
    }

    private static bool EstaNoMenuPrincipal()
    {
        return SceneManager.GetActiveScene().name == MenuSceneName;
    }
}
