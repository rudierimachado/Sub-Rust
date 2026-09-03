using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liga uma barra (Image tipo Filled) e o texto "atual / maximo" a vida ou stamina
/// do PlayerHealth. Anexar no objeto que tem a Image "Fill" como filho.
/// </summary>
public class HudBarDisplay : MonoBehaviour
{
    private enum TipoBarra { Vida, Stamina }
    [SerializeField] private TipoBarra tipo = TipoBarra.Vida;
    [SerializeField] private Image fill;
    [SerializeField] private TextMeshProUGUI texto;

    private void Awake()
    {
        if (fill == null) fill = GetComponentInChildren<Image>();
        if (texto == null)
        {
            var t = transform.Find("Text");
            if (t != null) texto = t.GetComponent<TextMeshProUGUI>();
        }
    }

    private void OnEnable()
    {
        if (tipo == TipoBarra.Vida) PlayerHealth.OnHealthChanged += Atualizar;
        else PlayerHealth.OnStaminaChanged += Atualizar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= Atualizar;
        PlayerHealth.OnStaminaChanged -= Atualizar;
    }

    private void Atualizar(float atual, float max)
    {
        if (fill != null) fill.fillAmount = max > 0f ? atual / max : 0f;

        if (texto != null)
        {
            // Ceil: com 0,4 de vida restante o certo e' mostrar 1, nao 0 - "0" so'
            // quando estiver realmente morto.
            int atualExibido = atual > 0f ? Mathf.CeilToInt(atual) : 0;
            texto.text = atualExibido + " / " + Mathf.RoundToInt(max);
        }
    }
}
