using UnityEngine;

/// <summary>
/// Item coletavel no chao (alma ou frasco). Precisa de um Collider marcado
/// como Trigger. Some ao tocar o jogador.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Pickup : MonoBehaviour
{
    public enum Tipo { Alma, Frasco }

    [SerializeField] private Tipo tipo = Tipo.Alma;
    [SerializeField] private int quantidade = 1;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (tipo == Tipo.Alma) PlayerCurrency.AddSouls(quantidade);
        else PlayerCurrency.AddPotions(quantidade);

        Destroy(gameObject);
    }
}
