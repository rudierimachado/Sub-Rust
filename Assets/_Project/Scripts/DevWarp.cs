using UnityEngine;

/// <summary>
/// Ponte volatil entre o Menu Dev e o spawn do jogador. E' um campo ESTATICO (nao
/// objeto de cena) de proposito: precisa sobreviver ao LoadScene que troca
/// MenuPrincipal por Fase01_Castelo, e um campo estatico resolve isso sem inventar
/// mais um manager persistente na Core so' pra um dado (a area escolhida).
///
/// PlayerNetwork.PosicionarNoSpawn consome isto (quando existe) em vez do marcador
/// "PontoDeSpawn_Player" normal - ver comentario la'.
/// </summary>
public static class DevWarp
{
    public readonly struct Area
    {
        public readonly string Nome;
        public readonly Vector3 Posicao;
        public Area(string nome, Vector3 posicao) { Nome = nome; Posicao = posicao; }
    }

    /// <summary>Pontos MEDIDOS na cena real (raycast contra o chao de cada area),
    /// nao chutados. Se a geometria de alguma sala mudar de lugar, remedir com
    /// Physics.Raycast em vez de ajustar estes numeros no olho.</summary>
    public static readonly Area[] Areas =
    {
        new Area("00 - Patio (entrada)",        new Vector3(9f,      0.10f,  0f)),
        new Area("11 - Sacristia",               new Vector3(47.68f,  0.10f,  0f)),
        new Area("08 - Ponte das Correntes",     new Vector3(65f,   -35.96f,  0f)),
        new Area("14 - Arquivos Altos (terreo)", new Vector3(100f,  -36.00f,  0f)),
        new Area("14 - Galeria superior",        new Vector3(120f,  -27.94f,  0f)),
        new Area("10 - Cozinha (chegada)",       new Vector3(74.42f,-46.20f,  0f)),
    };

    public static Vector3? AlvoPendente { get; private set; }

    public static void Pedir(Vector3 posicao) => AlvoPendente = posicao;

    /// <summary>Le e LIMPA no mesmo instante - so' vale para o PROXIMO carregamento
    /// de cena. Sem isso um warp antigo continuaria valendo pra sempre em toda
    /// troca de cena futura (voltar ao menu e jogar de novo, por exemplo).</summary>
    public static bool TentarConsumir(out Vector3 posicao)
    {
        if (AlvoPendente.HasValue)
        {
            posicao = AlvoPendente.Value;
            AlvoPendente = null;
            return true;
        }
        posicao = default;
        return false;
    }
}
