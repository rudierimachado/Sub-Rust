using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// A chave que os morcegos guardam. Ao encostar no jogador, destranca a porta do
/// atalho e some.
///
/// A porta e' apontada por referencia direta (nao por evento estatico) porque o
/// atalho e' um par unico desta fase: um evento global daria a entender que qualquer
/// chave abre qualquer porta, que nao e' o desenho.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ChaveDoAtalho : MonoBehaviour
{
    [SerializeField] private PortaDoAtalho porta;
    [Tooltip("Giro no proprio eixo enquanto espera ser pega, so' pra chamar o olho.")]
    [SerializeField] private float giroPorSegundo = 55f;
    [SerializeField] private float alturaDoBalanco = 0.08f;
    [SerializeField] private float raioDeColeta = 1.15f;


    private Vector3 pousada;
    private Transform jogador;
    private bool coletada;


    public void Apontar(PortaDoAtalho p) => porta = p;

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void Start() => pousada = transform.position;

private void Update()
    {
        transform.Rotate(Vector3.up, giroPorSegundo * Time.deltaTime, Space.World);
        transform.position = pousada + Vector3.up * Mathf.Sin(Time.time * 2f) * alturaDoBalanco;

        if (jogador == null || Vector3.Distance(jogador.position, transform.position) > raioDeColeta)
            return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            Coletar();
    }

private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            jogador = other.transform;
    }

private void Coletar()
    {
        if (coletada) return;
        coletada = true;

        if (porta != null) porta.ReceberChave(jogador);
        else Debug.LogWarning("[ChaveDoAtalho] chave coletada, mas nenhuma porta esta apontada.", this);

        Destroy(gameObject);
    }
}
