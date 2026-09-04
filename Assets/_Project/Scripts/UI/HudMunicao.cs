using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD de municao da espingarda: um cartucho desenhado por bala, apagando conforme
/// atira, mais o texto "atual / maximo". Durante a recarga os cartuchos piscam.
///
/// Os icones sao criados por codigo a partir do numero de cartuchos da arma, entao
/// mudar o tamanho do pente no PlayerShooting nao exige mexer na HUD.
/// </summary>
public class HudMunicao : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI texto;
    [SerializeField] private RectTransform linhaDeCartuchos;

    [SerializeField] private Color corCheio = new Color(1f, 0.78f, 0.35f);
    [SerializeField] private Color corVazio = new Color(0.22f, 0.20f, 0.18f);
    [SerializeField] private Vector2 tamanhoCartucho = new Vector2(10f, 26f);
    [SerializeField] private float espacamento = 5f;

    private readonly List<Image> cartuchos = new List<Image>();
    private int maximoCriado = -1;
    private bool recarregando;
    private float piscar;

    private void OnEnable() => PlayerShooting.OnMunicaoMudou += Atualizar;
    private void OnDisable() => PlayerShooting.OnMunicaoMudou -= Atualizar;

    private void Update()
    {
        if (!recarregando) return;

        // pisca durante a recarga pra deixar claro que a arma esta indisponivel
        piscar += Time.deltaTime * 6f;
        float k = (Mathf.Sin(piscar) + 1f) * 0.5f;
        var cor = Color.Lerp(corVazio, corCheio, k);
        foreach (var c in cartuchos) if (c != null) c.color = cor;
    }

    private void Atualizar(int atual, int maximo, bool emRecarga)
    {
        recarregando = emRecarga;

        if (maximo != maximoCriado) Reconstruir(maximo);

        if (texto != null)
            texto.text = emRecarga ? "recarregando" : atual + " / " + maximo;

        if (emRecarga) return;

        piscar = 0f;
        for (int i = 0; i < cartuchos.Count; i++)
            if (cartuchos[i] != null) cartuchos[i].color = i < atual ? corCheio : corVazio;
    }

    private void Reconstruir(int maximo)
    {
        if (linhaDeCartuchos == null) return;

        for (int i = linhaDeCartuchos.childCount - 1; i >= 0; i--)
            Destroy(linhaDeCartuchos.GetChild(i).gameObject);
        cartuchos.Clear();

        // Sem sprite: Image com sprite nulo desenha um retangulo solido, que e' exatamente
        // o formato do cartucho. O builtin "UI/Skin/UISprite.psd" NAO existe em runtime no
        // Unity 6 - usa-lo enchia o console de "Failed to find UI/Skin/UISprite.psd".
        for (int i = 0; i < maximo; i++)
        {
            var go = new GameObject("Cartucho" + i, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(linhaDeCartuchos, false);
            rt.sizeDelta = tamanhoCartucho;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(i * (tamanhoCartucho.x + espacamento), 0f);

            var img = go.AddComponent<Image>();
            img.color = corCheio;
            img.raycastTarget = false;
            cartuchos.Add(img);
        }
        maximoCriado = maximo;
    }
}
