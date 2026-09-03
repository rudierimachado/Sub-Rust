using TMPro;
using UnityEngine;

/// <summary>
/// Liga um contador da HUD (texto) a uma das moedas do PlayerCurrency.
/// Anexar no mesmo objeto que tem o TMP_Text filho "Text" (PotionCounter/SoulCounter).
/// </summary>
public class HudResourceDisplay : MonoBehaviour
{
    private enum TipoRecurso { Pocoes, Almas }
    [SerializeField] private TipoRecurso tipo = TipoRecurso.Almas;
    [SerializeField] private TextMeshProUGUI texto;

    private void Awake()
    {
        if (texto == null) texto = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (tipo == TipoRecurso.Almas)
        {
            PlayerCurrency.OnSoulsChanged += Atualizar;
            Atualizar(PlayerCurrency.Souls);
        }
        else
        {
            PlayerCurrency.OnPotionsChanged += Atualizar;
            Atualizar(PlayerCurrency.Potions);
        }
    }

    private void OnDisable()
    {
        PlayerCurrency.OnSoulsChanged -= Atualizar;
        PlayerCurrency.OnPotionsChanged -= Atualizar;
    }

    private void Atualizar(int valor)
    {
        if (texto != null) texto.text = valor.ToString();
    }
}
