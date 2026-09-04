using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Esquiva/dash: deslocamento horizontal rapido na direcao apertada (ou na direcao
/// que o personagem encara, se nao houver tecla), com invulnerabilidade durante o
/// movimento.
///
/// NAO e' teleporte instantaneo de proposito: o movimento passa pelo CharacterController,
/// entao parede e inimigo continuam bloqueando. Um teleporte puro atravessaria a
/// geometria da fase. A leitura de "piscada" vem da duracao curta + rastro fantasma,
/// nao de pular o percurso.
///
/// Anexar na RAIZ do Player (mesmo objeto do CharacterController e do PlayerHealth).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerDodge : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float distancia = 3.6f;
    [SerializeField] private float duracao = 0.16f;
    [SerializeField] private float recarga = 0.45f;

    [Header("Custo")]
    [SerializeField] private float custoStamina = 120f;

    [Header("Invulnerabilidade")]
    [Tooltip("Sobra de invulnerabilidade depois que o dash termina, pra perdoar o timing.")]
    [SerializeField] private float graca = 0.06f;

    [Header("Rastro")]
    [SerializeField] private int fantasmas = 5;
    [SerializeField] private float duracaoFantasma = 0.28f;
    [SerializeField] private Color corFantasma = new Color(0.45f, 0.75f, 1f, 0.55f);

    private CharacterController controller;
    private PlayerMovement2_5D movimento;
    private PlayerHealth vida;
    private SkinnedMeshRenderer[] malhas;

    private float proximaEsquiva;
    private bool esquivando;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        movimento = GetComponent<PlayerMovement2_5D>();
        vida = GetComponent<PlayerHealth>();
        malhas = GetComponentsInChildren<SkinnedMeshRenderer>();
    }

    private void Update()
    {
        if (esquivando) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        bool pediu = kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame;
        if (!pediu || Time.time < proximaEsquiva) return;
        if (vida != null && vida.Morto) return;
        if (vida != null && !vida.TryGastarStamina(custoStamina)) return;

        proximaEsquiva = Time.time + recarga;
        StartCoroutine(Esquivar(DirecaoEscolhida()));
    }

    /// <summary>Direcao apertada agora; sem tecla, esquiva para onde ja' esta olhando.</summary>
    private float DirecaoEscolhida()
    {
        var kb = Keyboard.current;
        float input = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input -= 1f;

        if (Mathf.Abs(input) > 0.01f) return Mathf.Sign(input);
        return movimento != null ? Mathf.Sign(movimento.Facing) : 1f;
    }

    private IEnumerator Esquivar(float direcao)
    {
        esquivando = true;
        if (vida != null) vida.Invulneravel = true;

        // Solta o controle normal: se os dois escreverem no controller no mesmo frame,
        // o dash briga com o input e sai mais curto do que o configurado.
        bool movimentoEstava = movimento != null && movimento.enabled;
        if (movimento != null) movimento.enabled = false;

        StartCoroutine(SoltarFantasmas());

        float velocidade = distancia / Mathf.Max(0.01f, duracao);
        float t = 0f;
        while (t < duracao)
        {
            float passo = Mathf.Min(Time.deltaTime, duracao - t);

            // A 22 m/s um quadro de 30 fps avanca 0,75 m, e as paredes tem 0,60 m de
            // espessura. Subdividir mantem cada Move abaixo de 0,25 m e tira o risco
            // de varar geometria fina num frame lento.
            int fatias = Mathf.Max(1, Mathf.CeilToInt(velocidade * passo / 0.25f));
            float dt = passo / fatias;
            for (int i = 0; i < fatias; i++)
            {
                // gravidade leve durante o dash: sem isso ele flutua ao sair de uma borda
                controller.Move(Vector3.right * direcao * velocidade * dt + Vector3.down * 4f * dt);
            }

            t += passo;
            yield return null;
        }

        if (movimento != null && movimentoEstava && (vida == null || !vida.Morto))
            movimento.enabled = true;
        esquivando = false;

        if (graca > 0f) yield return new WaitForSeconds(graca);
        if (vida != null) vida.Invulneravel = false;
    }

    /// <summary>Copias congeladas da malha ao longo do percurso - e' o que vende a "piscada".</summary>
    private IEnumerator SoltarFantasmas()
    {
        if (malhas == null || malhas.Length == 0) yield break;

        float intervalo = duracao / Mathf.Max(1, fantasmas);
        for (int i = 0; i < fantasmas; i++)
        {
            CriarFantasma(1f - (float)i / fantasmas);
            yield return new WaitForSeconds(intervalo);
        }
    }

    private void CriarFantasma(float forca)
    {
        foreach (var smr in malhas)
        {
            if (smr == null || !smr.gameObject.activeInHierarchy) continue;

            var malha = new Mesh();
            smr.BakeMesh(malha, true);   // useScale=true: a malha ja' sai no tamanho do mundo

            var go = new GameObject("Fantasma");
            go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);

            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>();

            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 1f);                 // transparente
            mat.SetFloat("_Blend", 0f);                   // alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);  // aditivo: brilha
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", corFantasma * forca);
            mr.sharedMaterial = mat;

            StartCoroutine(ApagarFantasma(go, mat));
        }
    }

    private IEnumerator ApagarFantasma(GameObject go, Material mat)
    {
        Color inicial = mat.GetColor("_BaseColor");
        float t = 0f;
        while (t < duracaoFantasma && go != null)
        {
            float k = 1f - (t / duracaoFantasma);
            mat.SetColor("_BaseColor", inicial * k);
            t += Time.deltaTime;
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
