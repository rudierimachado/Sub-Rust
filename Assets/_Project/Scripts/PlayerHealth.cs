using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vida e stamina do jogador. Stamina regenera sozinha e e' gasta ao correr;
/// vida so' muda por dano (IDamageable) ou frasco (tecla Q, gasta PlayerCurrency.Potions).
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDrainPerSecond = 20f;
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [SerializeField] private float potionHealAmount = 40f;

    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float Health { get; private set; }
    public float Stamina { get; private set; }
    public bool Morto { get; private set; }

    public static event Action<float, float> OnHealthChanged;
    public static event Action<float, float> OnStaminaChanged;
    public static event Action OnMorte;

    /// <summary>(dano, posicao de onde veio o golpe) - quem reage ao impacto escuta aqui.</summary>
    public static event Action<float, Vector3> OnDano;

    private void Awake()
    {
        Health = maxHealth;
        Stamina = maxStamina;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(Health, maxHealth);
        OnStaminaChanged?.Invoke(Stamina, maxStamina);
    }

    private void Update()
    {
        bool correndo = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        float delta = correndo ? -staminaDrainPerSecond : staminaRegenPerSecond;
        float novaStamina = Mathf.Clamp(Stamina + delta * Time.deltaTime, 0f, maxStamina);
        if (!Mathf.Approximately(novaStamina, Stamina))
        {
            Stamina = novaStamina;
            OnStaminaChanged?.Invoke(Stamina, maxStamina);
        }

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            UsarFrasco();
    }

    public void TakeHit(float damage, Vector3 hitPoint)
    {
        if (Morto) return;
        Health = Mathf.Clamp(Health - damage, 0f, maxHealth);
        OnHealthChanged?.Invoke(Health, maxHealth);
        OnDano?.Invoke(damage, hitPoint);

        if (Health <= 0f) Morrer();
    }

    private void Morrer()
    {
        Morto = true;
        OnMorte?.Invoke();

        var movimento = GetComponent<PlayerMovement2_5D>();
        if (movimento != null) movimento.enabled = false;
        var combate = GetComponentInChildren<PlayerCombat>();
        if (combate != null) combate.enabled = false;

        StartCoroutine(RespawnAposDelay());
    }

    private System.Collections.IEnumerator RespawnAposDelay()
    {
        yield return new WaitForSeconds(2f);
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void UsarFrasco()
    {
        if (Health >= maxHealth) return;
        if (!PlayerCurrency.TrySpendPotion()) return;
        Health = Mathf.Clamp(Health + potionHealAmount, 0f, maxHealth);
        OnHealthChanged?.Invoke(Health, maxHealth);
    }
}
