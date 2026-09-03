using UnityEngine;

/// <summary>
/// Impede que dois personagens ocupem o mesmo espaco, usando a GEOMETRIA REAL dos
/// colisores (Physics.ComputePenetration) em vez de uma distancia chutada.
///
/// Por que existe: o CharacterController do Unity nao resolve penetracao contra outro
/// CharacterController - ele varre contra colisores estaticos, mas dois controllers
/// cinematicos se aceitam sobrepostos. Aqui a sobreposicao real e' medida e desfeita.
///
/// So' a componente HORIZONTAL e' aplicada: no side-scroller quem manda no eixo
/// vertical e' a gravidade, e empurrar para cima faria o personagem escalar o outro.
///
/// Cada corpo se afasta pela METADE da penetracao, entao dois personagens se separam
/// simetricamente (equivalente a massas iguais).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class SeparacaoDeCorpos : MonoBehaviour
{
    [SerializeField] private LayerMask camadas = ~0;

    private CharacterController controller;
    private readonly Collider[] buffer = new Collider[8];

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // LateUpdate: depois que todo mundo ja' se moveu no frame.
    private void LateUpdate()
    {
        Vector3 centro = transform.position + controller.center;
        float meiaAltura = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 topo = centro + Vector3.up * meiaAltura;
        Vector3 baixo = centro - Vector3.up * meiaAltura;

        int n = Physics.OverlapCapsuleNonAlloc(baixo, topo, controller.radius, buffer, camadas, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var outro = buffer[i];
            if (outro == null || outro.transform == transform) continue;

            // So' separa de outros personagens; cenario ja' e' resolvido pelo proprio Move.
            var outroController = outro as CharacterController;
            if (outroController == null) continue;

            if (Physics.ComputePenetration(
                    controller, transform.position, transform.rotation,
                    outro, outro.transform.position, outro.transform.rotation,
                    out Vector3 direcao, out float distancia))
            {
                // Metade pra cada corpo, e so' no plano horizontal.
                Vector3 correcao = direcao * distancia * 0.5f;
                correcao.y = 0f;
                if (correcao.sqrMagnitude > 0f)
                    transform.position += correcao;
            }
        }
    }
}
