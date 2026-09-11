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
    private const string ChaveVolume = "Menu.VolumeGeral";
    private const string ChaveTelaCheia = "Menu.TelaCheia";

    private VisualElement telaPrincipal, telaCoop, telaHospedando, telaEntrar, telaOpcoes, telaDev;
    private ScrollView listaAreasDev;
    private Label lblIp, lblStatus;
    private TextField campoIp;
    private Slider sliderVolume;
    private Toggle toggleTelaCheia;

    private void OnEnable()
    {
        var raiz = GetComponent<UIDocument>().rootVisualElement;

        telaPrincipal   = raiz.Q<VisualElement>("tela-principal");
        telaCoop        = raiz.Q<VisualElement>("tela-coop");
        telaHospedando  = raiz.Q<VisualElement>("tela-hospedando");
        telaEntrar      = raiz.Q<VisualElement>("tela-entrar");
        telaOpcoes      = raiz.Q<VisualElement>("tela-opcoes");
        telaDev         = raiz.Q<VisualElement>("tela-dev");
        listaAreasDev   = raiz.Q<ScrollView>("lista-areas-dev");
        lblIp           = raiz.Q<Label>("lbl-ip");
        lblStatus       = raiz.Q<Label>("lbl-status");
        campoIp         = raiz.Q<TextField>("campo-ip");
        sliderVolume    = raiz.Q<Slider>("slider-volume");
        toggleTelaCheia = raiz.Q<Toggle>("toggle-tela-cheia");

        Ligar(raiz, "btn-solo",          () => Sessao()?.JogarSolo());
        Ligar(raiz, "btn-coop",          () => Mostrar(telaCoop));
        Ligar(raiz, "btn-opcoes",        () => Mostrar(telaOpcoes));
        Ligar(raiz, "btn-dev",           () => { MontarListaDev(); Mostrar(telaDev); });
        Ligar(raiz, "btn-voltar-dev",    () => Mostrar(telaPrincipal));
        Ligar(raiz, "btn-sair",          Sair);
        Ligar(raiz, "btn-hospedar",      Hospedar);
        Ligar(raiz, "btn-entrar",        () => Mostrar(telaEntrar));
        Ligar(raiz, "btn-voltar-coop",   () => { Sessao()?.Cancelar(); Mostrar(telaPrincipal); });
        Ligar(raiz, "btn-comecar",       () => Sessao()?.ComecarPartida());
        Ligar(raiz, "btn-cancelar-host", () => { Sessao()?.Cancelar(); Mostrar(telaCoop); });
        Ligar(raiz, "btn-conectar",      Conectar);
        Ligar(raiz, "btn-voltar-entrar", () => { Sessao()?.Cancelar(); Mostrar(telaCoop); });
        Ligar(raiz, "btn-aplicar-opcoes", AplicarOpcoes);
        Ligar(raiz, "btn-voltar-opcoes",  () => Mostrar(telaPrincipal));

        CarregarOpcoes();

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
        foreach (var t in new[] { telaPrincipal, telaCoop, telaHospedando, telaEntrar, telaOpcoes, telaDev })
            t?.EnableInClassList("escondido", t != alvo);
        if (alvo != telaHospedando) MostrarStatus(string.Empty, false);
    }

    /// <summary>Um botao por DevWarp.Areas - fonte unica, nada duplicado no UXML
    /// pra desincronizar. Monta de novo a cada abertura (barato, so' seis botoes)
    /// em vez de cachear, entao adicionar/remover area em DevWarp.cs ja' aparece
    /// aqui sem precisar tocar em mais nada.</summary>
    private void MontarListaDev()
    {
        if (listaAreasDev == null) return;
        listaAreasDev.Clear();

        foreach (var area in DevWarp.Areas)
        {
            var alvo = area; // copia local - closure de foreach por referencia pegaria so' a ultima area
            var botao = new Button(() =>
            {
                DevWarp.Pedir(alvo.Posicao);
                Sessao()?.JogarSolo();
            })
            {
                text = alvo.Nome,
            };
            botao.AddToClassList("botao");
            botao.AddToClassList("botao--area");
            listaAreasDev.Add(botao);
        }
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

    private void CarregarOpcoes()
    {
        float volume = PlayerPrefs.GetFloat(ChaveVolume, AudioListener.volume);
        bool telaCheia = PlayerPrefs.GetInt(ChaveTelaCheia, Screen.fullScreen ? 1 : 0) == 1;

        if (sliderVolume != null)
        {
            sliderVolume.value = volume;
            sliderVolume.RegisterValueChangedCallback(evt => AudioListener.volume = Mathf.Clamp01(evt.newValue));
        }

        if (toggleTelaCheia != null)
            toggleTelaCheia.value = telaCheia;

        AudioListener.volume = Mathf.Clamp01(volume);
        Screen.fullScreen = telaCheia;
    }

    private void AplicarOpcoes()
    {
        float volume = sliderVolume != null ? Mathf.Clamp01(sliderVolume.value) : AudioListener.volume;
        bool telaCheia = toggleTelaCheia == null || toggleTelaCheia.value;

        AudioListener.volume = volume;
        Screen.fullScreen = telaCheia;
        PlayerPrefs.SetFloat(ChaveVolume, volume);
        PlayerPrefs.SetInt(ChaveTelaCheia, telaCheia ? 1 : 0);
        PlayerPrefs.Save();

        MostrarStatus("Opcoes aplicadas.", false);
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
