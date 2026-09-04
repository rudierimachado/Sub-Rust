using UnityEngine;

/// <summary>
/// Vida do inimigo simples. Ao morrer, larga almas e (as vezes) um frasco, e some.
/// </summary>
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private int almasAoMorrer = 3;
    [SerializeField] private float chanceFrasco = 0.3f;

    private float health;
    private bool morto;
    private EnemyDamageFeedback feedback;

    public float Health => health;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        health = maxHealth;
        feedback = GetComponent<EnemyDamageFeedback>();
    }

    public void TakeHit(float damage, Vector3 hitPoint)
    {
        if (morto) return;
        health -= damage;

        if (health <= 0f) { Morrer(); return; }

        // passa o dano: o empurrao escala com a pancada (ver EnemyDamageFeedback)
        feedback?.Reagir(hitPoint, damage);
    }

    private void Morrer()
    {
        morto = true;
        CriarPickup(Pickup.Tipo.Alma, almasAoMorrer, transform.position + Vector3.up * 0.5f);
        if (Random.value < chanceFrasco)
            CriarPickup(Pickup.Tipo.Frasco, 1, transform.position + Vector3.up * 0.5f + Vector3.right * 0.4f);

        Destroy(gameObject);
    }

    private static void CriarPickup(Pickup.Tipo tipo, int quantidade, Vector3 posicao)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = tipo == Pickup.Tipo.Alma ? "SoulPickup" : "PotionPickup";
        go.transform.position = posicao;
        go.transform.localScale = Vector3.one * 0.25f;

        var col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;

        var renderer = go.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        Color cor = tipo == Pickup.Tipo.Alma ? new Color(0.1f, 0.9f, 0.6f) : new Color(0.85f, 0.05f, 0.08f);
        if (renderer.material.HasProperty("_BaseColor")) renderer.material.SetColor("_BaseColor", cor);
        if (renderer.material.HasProperty("_EmissionColor"))
        {
            renderer.material.EnableKeyword("_EMISSION");
            renderer.material.SetColor("_EmissionColor", cor * 1.5f);
        }

        var pickup = go.AddComponent<Pickup>();
        var tipoField = typeof(Pickup).GetField("tipo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var qtdField = typeof(Pickup).GetField("quantidade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        tipoField.SetValue(pickup, tipo);
        qtdField.SetValue(pickup, quantidade);
    }
}
