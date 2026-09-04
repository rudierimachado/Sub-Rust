using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Menu de entrada: Solo ou Cooperativo (hospedar / entrar por IP).
///
/// UI Toolkit + USS de proposito, nao uGUI montado por codigo: o layout e' flexbox
/// no UXML e a aparencia vive no .uss, editavel sem recompilar C#. Menu posicionado
/// por pixel em C# e' o padrao que o CLAUDE.md manda evitar.
///
/// Anexar no objeto que tem o UIDocument da cena MenuPrincipal.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MenuPrincipal : MonoBehaviour
{
    private VisualElement telaPrincipal, telaCoop, telaHospedando, telaEntrar;
    private Label lblIp, lblStatus;
    private TextField campoIp;

    private void OnEnable()
    {
        var raiz = GetComponent<UIDocument>().rootVisualElement;

        telaPrincipal   = raiz.Q<VisualElement>("tela-principal");
        telaCoop        = raiz.Q<VisualElement>("tela-coop");
        telaHospedando  = raiz.Q<VisualElement>("tela-hospedando");
        telaEntrar      = raiz.Q<VisualElement>("tela-entrar");
        lblIp           = raiz.Q<Label>("lbl-ip");
        lblStatus       = raiz.Q<Label>("lbl-status");
        campoIp         = raiz.Q<TextField>("campo-ip");

        Ligar(raiz, "btn-solo",          () => Sessao()?.JogarSolo());
        Ligar(raiz, "btn-coop",          () => Mostrar(telaCoop));
        Ligar(raiz, "btn-sair",          Sair);
        Ligar(raiz, "btn-hospedar",      Hospedar);
        Ligar(raiz, "btn-entrar",        () => Mostrar(telaEntrar));
        Ligar(raiz, "btn-voltar-coop",   () => { Sessao()?.Cancelar(); Mostrar(telaPrincipal); });
        Ligar(raiz, "btn-comecar",       () => Sessao()?.ComecarPartida());
        Ligar(raiz, "btn-cancelar-host", () => { Sessao()?.Cancelar(); Mostrar(telaCoop); });
        Ligar(raiz, "btn-conectar",      Conectar);
        Ligar(raiz, "btn-voltar-entrar", () => { Sessao()?.Cancelar(); Mostrar(telaCoop); });

        var s = Sessao();
        if (s != null) { s.OnStatus += MostrarStatus; s.OnJogadoresMudou += JogadoresMudou; }
        else MostrarStatus("Sem RedeSessao na cena: o cooperativo nao vai funcionar.", true);

        Mostrar(telaPrincipal);
    }

    private void OnDisable()
    {
        var s = Sessao();
        if (s == null) return;
        s.OnStatus -= MostrarStatus;
        s.OnJogadoresMudou -= JogadoresMudou;
    }

    private static RedeSessao Sessao() => RedeSessao.Instancia;

    private static void Ligar(VisualElement raiz, string nome, System.Action acao)
    {
        var b = raiz.Q<Button>(nome);
        if (b == null) { Debug.LogError("MenuPrincipal: botao \"" + nome + "\" nao existe no UXML."); return; }
        b.clicked += acao;
    }

    /// <summary>Uma tela visivel por vez. Trocar classe e' o jeito do UI Toolkit;
    /// mexer em style.display no C# espalha layout pelo codigo.</summary>
    private void Mostrar(VisualElement alvo)
    {
        foreach (var t in new[] { telaPrincipal, telaCoop, telaHospedando, telaEntrar })
            t?.EnableInClassList("escondido", t != alvo);
        if (alvo != telaHospedando) MostrarStatus(string.Empty, false);
    }

    private void Hospedar()
    {
        var s = Sessao();
        if (s == null) return;
        if (!s.Hospedar()) return;
        lblIp.text = RedeSessao.IpLocal() + " : " + RedeSessao.PortaPadrao;
        Mostrar(telaHospedando);
    }

    private void Conectar()
    {
        var s = Sessao();
        if (s == null) return;
        s.Conectar(campoIp != null ? campoIp.value : string.Empty);
    }

    private void MostrarStatus(string msg, bool erro)
    {
        if (lblStatus == null) return;
        lblStatus.text = msg;
        lblStatus.EnableInClassList("status--erro", erro);
    }

    private void JogadoresMudou(int quantos)
    {
        // nada a fazer por enquanto: o texto ja' vem pelo OnStatus.
    }

    private static void Sair()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
