using System;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Camada fina entre o menu e o Netcode for GameObjects. Hospeda, conecta por IP e
/// carrega a fase pela rede.
///
/// Decisao do projeto: o modo SOLO nao usa rede nenhuma (carrega a cena direto).
/// Isso deixa o solo mais leve, mas cria DOIS caminhos de execucao - todo script de
/// jogador precisa funcionar com e sem NetworkManager. Para conter isso, o resto do
/// jogo deve perguntar "eu sou o dono deste personagem?" atraves de um unico ponto
/// (ver Solo() abaixo), nunca espalhando checagens de IsClient/IsServer.
///
/// Anexar no mesmo objeto do NetworkManager, na cena MenuPrincipal.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class RedeSessao : MonoBehaviour
{
    public const ushort PortaPadrao = 7777;
    public const string CenaDoJogo = "Fase01_Castelo";

    public static RedeSessao Instancia { get; private set; }

    /// <summary>true quando nao ha' rede: partida solo. Ponto UNICO de checagem -
    /// o resto do jogo pergunta aqui em vez de olhar o NetworkManager direto.</summary>
    public static bool Solo =>
        NetworkManager.Singleton == null ||
        (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer);

    /// <summary>(mensagem, ehErro) - o menu escuta pra mostrar status.</summary>
    public event Action<string, bool> OnStatus;
    public event Action<int> OnJogadoresMudou;

    private NetworkManager rede;

    private void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        rede = GetComponent<NetworkManager>();
    }

    private void OnEnable()
    {
        if (rede == null) return;
        rede.OnClientConnectedCallback += ClienteConectou;
        rede.OnClientDisconnectCallback += ClienteSaiu;
    }

    private void OnDisable()
    {
        if (rede == null) return;
        rede.OnClientConnectedCallback -= ClienteConectou;
        rede.OnClientDisconnectCallback -= ClienteSaiu;
    }

    // ---------------- solo ----------------

    /// <summary>Solo nao sobe rede: carrega a fase direto.</summary>
    public void JogarSolo()
    {
        if (rede != null && (rede.IsClient || rede.IsServer)) rede.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene(CenaDoJogo);
    }

    // ---------------- cooperativo ----------------

    public bool Hospedar()
    {
        if (!ConfigurarTransporte("0.0.0.0", PortaPadrao, ouvirEmTudo: true)) return false;
        if (!rede.StartHost())
        {
            Avisar("Nao consegui abrir a partida. A porta " + PortaPadrao + " pode estar em uso.", true);
            return false;
        }
        Avisar("Aguardando o outro jogador...", false);
        OnJogadoresMudou?.Invoke(ContarJogadores());
        return true;
    }

    public bool Conectar(string ip)
    {
        ip = (ip ?? string.Empty).Trim();
        if (!IPAddress.TryParse(ip, out _))
        {
            Avisar("Endereco invalido: \"" + ip + "\". Use algo como 192.168.0.15", true);
            return false;
        }
        if (!ConfigurarTransporte(ip, PortaPadrao, ouvirEmTudo: false)) return false;
        if (!rede.StartClient())
        {
            Avisar("Nao consegui iniciar a conexao.", true);
            return false;
        }
        Avisar("Conectando em " + ip + "...", false);
        return true;
    }

    /// <summary>Host manda todo mundo para a fase. Usa o SceneManager DA REDE: o
    /// SceneManager do Unity carregaria so' nesta maquina.</summary>
    public void ComecarPartida()
    {
        if (rede == null || !rede.IsServer)
        {
            Avisar("So' quem hospeda pode comecar a partida.", true);
            return;
        }
        rede.SceneManager.LoadScene(CenaDoJogo, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void Cancelar()
    {
        if (rede != null && (rede.IsClient || rede.IsServer)) rede.Shutdown();
        Avisar(string.Empty, false);
        OnJogadoresMudou?.Invoke(0);
    }

    // ---------------- apoio ----------------

    private bool ConfigurarTransporte(string endereco, ushort porta, bool ouvirEmTudo)
    {
        var utp = rede != null ? rede.GetComponent<UnityTransport>() : null;
        if (utp == null)
        {
            Avisar("Falta o componente UnityTransport no NetworkManager.", true);
            return false;
        }
        utp.SetConnectionData(ouvirEmTudo ? "127.0.0.1" : endereco, porta, ouvirEmTudo ? "0.0.0.0" : null);
        return true;
    }

    private void ClienteConectou(ulong id)
    {
        OnJogadoresMudou?.Invoke(ContarJogadores());
        if (rede.IsServer) Avisar(ContarJogadores() + " jogador(es) na sala.", false);
        else if (id == rede.LocalClientId) Avisar("Conectado. Aguardando o host comecar...", false);
    }

    private void ClienteSaiu(ulong id)
    {
        OnJogadoresMudou?.Invoke(ContarJogadores());
        if (!rede.IsServer && id == rede.LocalClientId)
            Avisar("Conexao perdida. Confira o IP, a porta " + PortaPadrao + " e o firewall.", true);
    }

    private int ContarJogadores() => rede != null && rede.IsServer ? rede.ConnectedClientsIds.Count : 0;

    private void Avisar(string msg, bool erro) => OnStatus?.Invoke(msg, erro);

    /// <summary>IPv4 da maquina na rede local. Percorre as interfaces porque
    /// Dns.GetHostAddresses costuma devolver enderecos de VPN/virtualizacao junto.</summary>
    public static string IpLocal()
    {
        string melhor = null;
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                var tipo = ni.NetworkInterfaceType;
                bool boa = tipo == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                        || tipo == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;
                    if (boa) return ua.Address.ToString();
                    melhor ??= ua.Address.ToString();
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("Nao consegui ler o IP local: " + e.Message); }
        return melhor ?? "127.0.0.1";
    }
}
