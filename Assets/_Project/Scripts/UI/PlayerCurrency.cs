using System;
using UnityEngine;

/// <summary>
/// Moedas do jogador. Estatico e simples de proposito - o jogo ainda nao tem
/// save/inventario real, isso e' so' a base pra ligar a HUD e a arvore de
/// habilidades. Trocar por um sistema de save quando existir.
/// </summary>
public static class PlayerCurrency
{
    public static int Souls { get; private set; } = 0;
    public static int Potions { get; private set; } = 0;

    public static event Action<int> OnSoulsChanged;
    public static event Action<int> OnPotionsChanged;

    public static void AddSouls(int quantidade)
    {
        Souls += quantidade;
        OnSoulsChanged?.Invoke(Souls);
    }

    /// <returns>true se tinha almas suficientes e gastou.</returns>
    public static bool TrySpendSouls(int custo)
    {
        if (Souls < custo) return false;
        Souls -= custo;
        OnSoulsChanged?.Invoke(Souls);
        return true;
    }

    public static void AddPotions(int quantidade)
    {
        Potions += quantidade;
        OnPotionsChanged?.Invoke(Potions);
    }

    public static bool TrySpendPotion()
    {
        if (Potions <= 0) return false;
        Potions -= 1;
        OnPotionsChanged?.Invoke(Potions);
        return true;
    }
}
