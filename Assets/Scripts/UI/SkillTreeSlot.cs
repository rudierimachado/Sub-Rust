using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// Um slot de habilidade na HUD. Comeca travado (cadeado visivel) ou destravado
/// direto, conforme configurado. Clicar num slot travado gasta almas pra abrir;
/// clicar num ja destravado dispara OnUsar (pra ligar a habilidade de verdade
/// depois - por enquanto so' um evento vazio pronto pra usar).
/// </summary>
[RequireComponent(typeof(Button))]
public class SkillTreeSlot : MonoBehaviour
{
    [SerializeField] private bool comecaTravado = false;
    [SerializeField] private int custoEmAlmas = 5;
    [SerializeField] private Color corTravado = new Color(0.15f, 0.15f, 0.17f, 1f);

    public UnityEvent OnUsar;

    private bool travado;
    private Image imagemFundo;
    private Button botao;
    private Color corOriginal; // cor propria de cada habilidade (roxo/laranja/azul...) - nao mexer nela

    private void Awake()
    {
        botao = GetComponent<Button>();
        imagemFundo = GetComponent<Image>();
        corOriginal = imagemFundo.color;

        travado = comecaTravado;
        AtualizarVisual();
        botao.onClick.AddListener(AoClicar);
    }

    private void AoClicar()
    {
        if (travado)
        {
            if (PlayerCurrency.TrySpendSouls(custoEmAlmas))
            {
                travado = false;
                AtualizarVisual();
            }
            else
            {
                // sem almas suficientes - feedback minimo por enquanto
                Debug.Log($"Almas insuficientes para destravar (precisa de {custoEmAlmas}).");
            }
        }
        else
        {
            OnUsar?.Invoke();
        }
    }

    private void AtualizarVisual()
    {
        imagemFundo.color = travado ? corTravado : corOriginal;
        foreach (Transform filho in transform)
        {
            if (filho.name.StartsWith("Lock"))
                filho.gameObject.SetActive(travado);
        }
    }
}
