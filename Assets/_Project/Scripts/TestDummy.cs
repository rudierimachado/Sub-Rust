using UnityEngine;

public class TestDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private float knockback = 4f;
    private Renderer rend;
    private Color originalColor;
    private Vector3 basePosition;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) originalColor = rend.material.color;
        basePosition = transform.position;
    }

    public void TakeHit(float damage, Vector3 hitPoint)
    {
        Debug.Log($"TestDummy levou {damage} de dano em {hitPoint}");
        Vector3 pushDir = (transform.position - hitPoint);
        pushDir.y = 0f;
        pushDir = pushDir.sqrMagnitude > 0.0001f ? pushDir.normalized : Vector3.right;
        transform.position = basePosition + pushDir * 0.3f;
        StopAllCoroutines();
        StartCoroutine(FlashAndReturn());
    }

    private System.Collections.IEnumerator FlashAndReturn()
    {
        if (rend != null) rend.material.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        if (rend != null) rend.material.color = originalColor;
        yield return new WaitForSeconds(0.15f);
        transform.position = basePosition;
    }
}
