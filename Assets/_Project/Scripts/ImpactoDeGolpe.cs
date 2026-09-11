using UnityEngine;

/// <summary>
/// Efeito de acerto de arma branca: fagulhas direcionais, um leve borrifo de sangue,
/// um clarao curto que ilumina o que esta' em volta e um flash de anel de corte.
///
/// POR QUE UM SISTEMA E NAO PARTICULA SOLTA NO PlayerCombat
/// A versao anterior montava um ParticleSystem na mao dentro do PlayerCombat com
/// `Shader.Find("Particles/Standard Unlit")` - shader do pipeline ANTIGO. Este
/// projeto e' URP: esse shader ou nao existe (material magenta) ou renderiza chapado
/// sem transparencia nem brilho, que e' exatamente o "efeito horrivel". Alem disso
/// so' o golpe de espada tinha efeito; tiro e caldo nao tinham nenhum.
///
/// Aqui o efeito e' um so', pedido por quem acertou, e vale pra qualquer fonte de
/// dano. Tudo construido por codigo com shader de URP - nao depende de nenhum asset
/// que alguem precise lembrar de arrastar no Inspector.
///
/// USO
///   ImpactoDeGolpe.Tocar(ponto, direcaoDoGolpe);
/// Ele se destroi sozinho quando as particulas acabam.
/// </summary>
public static class ImpactoDeGolpe
{
    private static Material matFagulha;
    private static Material matSangue;
    private static Texture2D texturaPonto;
    private static bool texturaProcurada;

    /// <summary>Mascara redonda com bordas suaves. SEM ela a particula e' o quad nu:
    /// um QUADRADO solido de cor chapada - foi exatamente o sintoma de "sangue muito
    /// quadrado". A malha da particula sempre foi um quadrado; e' a textura que
    /// recorta a gota.
    ///
    /// Reaproveita a SR_Ponto_Macio que o respingo do caldo (FX_Respingo_Caldo) ja'
    /// usa neste projeto, em vez de gerar outra: mesma cara de particula no jogo
    /// inteiro e um asset a menos. Carregada de Resources.Load se estiver la', senao
    /// gerada em memoria - assim o efeito nunca volta a ficar quadrado, mesmo que o
    /// asset saia do lugar.</summary>
    private static Texture2D TexturaDoPonto()
    {
        if (texturaProcurada) return texturaPonto;
        texturaProcurada = true;

        texturaPonto = Resources.Load<Texture2D>("SR_Ponto_Macio");
        if (texturaPonto != null) return texturaPonto;

        // Fallback: gradiente radial gerado na hora (opaco no centro, zero na borda).
        const int lado = 64;
        texturaPonto = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        texturaPonto.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[lado * lado];
        float meio = (lado - 1) * 0.5f;
        for (int y = 0; y < lado; y++)
        for (int x = 0; x < lado; x++)
        {
            float d = Mathf.Sqrt((x - meio) * (x - meio) + (y - meio) * (y - meio)) / meio;
            // quadratica: nucleo mais cheio e borda que apaga rapido, como gota
            float a = Mathf.Clamp01(1f - d);
            pixels[y * lado + x] = new Color(1f, 1f, 1f, a * a);
        }
        texturaPonto.SetPixels(pixels);
        texturaPonto.Apply();
        return texturaPonto;
    }

    /// <summary>Fagulhas + sangue + clarao no ponto do acerto. 'direcao' e' pra onde o
    /// golpe empurra (normalmente a direcao em que o jogador esta' virado): as
    /// fagulhas saem NESSE sentido, num cone, em vez de espirrar igual pra todo lado -
    /// e' o que faz o acerto ter direcao em vez de parecer uma explosaozinha.</summary>
    public static void Tocar(Vector3 ponto, Vector3 direcao, float intensidade = 1f)
    {
        intensidade = Mathf.Clamp(intensidade, 0.4f, 2.2f);

        var raiz = new GameObject("FX_Impacto");
        raiz.transform.position = ponto;
        raiz.transform.rotation = Quaternion.LookRotation(
            direcao.sqrMagnitude > 0.001f ? direcao.normalized : Vector3.right, Vector3.up);

        CriarFagulhas(raiz.transform, intensidade);
        CriarJorro(raiz.transform, intensidade);
        CriarNevoa(raiz.transform, intensidade);
        CriarClarao(raiz.transform, intensidade);
        CriarPoca(ponto, intensidade);

        // 1,6 s cobre a particula mais longa (jorro pesado, 1,1 s de vida).
        Object.Destroy(raiz, 1.6f);
    }

    /// <summary>Faiscas SEM sangue: metal batendo em metal, pra quando o golpe e'
    /// aparado pelo escudo em vez de cortar carne.
    ///
    /// Reusa as mesmas fagulhas e o mesmo clarao do acerto normal. A diferenca que
    /// importa e' o que fica de FORA: aparar um golpe nao esguicha sangue nem deixa
    /// poca no chao, e usar o Tocar completo aqui faria o escudo parecer que sangra.</summary>
    public static void Faiscar(Vector3 ponto, Vector3 direcao, float intensidade = 1f)
    {
        intensidade = Mathf.Clamp(intensidade, 0.4f, 2.2f);

        var raiz = new GameObject("FX_Aparado");
        raiz.transform.position = ponto;
        raiz.transform.rotation = Quaternion.LookRotation(
            direcao.sqrMagnitude > 0.001f ? direcao.normalized : Vector3.right, Vector3.up);

        CriarFagulhas(raiz.transform, intensidade);
        CriarClarao(raiz.transform, intensidade);

        // 0,6 s cobre a fagulha mais longa (0,30 s de vida) com folga.
        Object.Destroy(raiz, 0.6f);
    }

    /// <summary>O material translucido de nevoa, exposto pra outros efeitos do jogo
    /// reusarem (o ofego do inimigo exausto usa este). Publico pra nao existir uma
    /// SEGUNDA montagem de material de particula no projeto: a primeira tentativa de
    /// fazer isso na mao usou shader do pipeline antigo e saiu magenta.</summary>
    public static Material MaterialDeNevoa() => MaterialSangue();

    /// <summary>Fagulhas: rapidas, finas e ESTICADAS na direcao do movimento
    /// (renderMode Stretch). Bolinha redonda a 4 m/s le' como confete; risco esticado
    /// le' como metal raspando.</summary>
    private static void CriarFagulhas(Transform pai, float intensidade)
    {
        var go = new GameObject("Fagulhas");
        go.transform.SetParent(pai, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
        main.gravityModifier = 1.1f;               // fagulha cai, nao paira
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.65f), new Color(1f, 0.55f, 0.15f));

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(22 * intensidade)) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 38f;
        sh.radius = 0.04f;

        // Perde velocidade no ar: sem isso a fagulha viaja reto e some longe do corpo.
        var vel = ps.limitVelocityOverLifetime;
        vel.enabled = true;
        vel.dampen = 0.22f;
        vel.limit = new ParticleSystem.MinMaxCurve(3f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                          new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f),
                          new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        // Fagulha pode esticar mais que a gota (e' um risco de luz), mas a conta e' a
        // mesma: 0,06 de tamanho com lengthScale 2,2 e velocityScale 0,06 a 11 m/s
        // dava 0,79 m de comprimento por 0,06 de largura - uma barra 13:1 atravessando
        // a tela. Aqui o risco fica curto e fino.
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.018f;
        r.lengthScale = 1.4f;
        r.sharedMaterial = MaterialFagulha();
        ps.Play();
    }

    /// <summary>Jorro: as gotas GRANDES, esticadas na direcao do voo e com gravidade
    /// alta, que descrevem um arco e caem. Sao elas que leem como sangue de verdade -
    /// billboard redondo e pequeno le' como poeira vermelha.</summary>
    private static void CriarJorro(Transform pai, float intensidade)
    {
        var go = new GameObject("Jorro");
        go.transform.SetParent(pai, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.25f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7f * intensidade);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.10f);
        main.gravityModifier = 2.6f;              // arco curto e queda pesada
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 90;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.45f, 0.02f, 0.02f), new Color(0.20f, 0.005f, 0.005f));

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(26 * intensidade)) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 42f;
        sh.radius = 0.05f;

        // Esticada no sentido do voo, mas POUCO. No modo Stretch o comprimento e'
        // "tamanho * lengthScale + velocidade * velocityScale", e essa conta explode
        // rapido: com lengthScale 1,6 e velocityScale 0,05 uma gota de 0,16 a 7 m/s
        // virava uma TIRA de 0,60 m por 0,16 de largura - proporcao 4:1. Era isso o
        // "sangue muito quadrado": retangulos compridos, nao falta de textura.
        // lengthScale 1,15 mantem a gota quase redonda e o velocityScale baixo so'
        // sugere o rastro nas mais rapidas.
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.012f;
        r.lengthScale = 1.15f;
        r.sharedMaterial = MaterialSangue();
        ps.Play();
    }

    /// <summary>Nevoa fina: a poeira vermelha que fica suspensa no ar por um instante
    /// depois do corte. Curta e transparente - e' o que separa "corte" de "explosao".</summary>
    private static void CriarNevoa(Transform pai, float intensidade)
    {
        var go = new GameObject("Nevoa");
        go.transform.SetParent(pai, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.02f, 0.02f, 0.5f));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(9 * intensidade)) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 60f;
        sh.radius = 0.08f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(1f, 1.8f)));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = MaterialSangue();
        ps.Play();
    }

    /// <summary>Mancha que FICA no chao. E' o que faz a luta deixar marca em vez de
    /// resetar a cada golpe: no fim de uma barragem o chao em volta dela conta a
    /// historia. Sonda o piso pra assentar em cima dele, e ignora a layer Inimigo
    /// pra nao grudar no corpo de quem levou o golpe.</summary>
    private static void CriarPoca(Vector3 ponto, float intensidade)
    {
        if (!Physics.Raycast(ponto, Vector3.down, out var hit, 4f,
                             ~(1 << 8), QueryTriggerInteraction.Ignore)) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "MarcaDeSangue";
        Object.Destroy(go.GetComponent<Collider>());
        // 1 mm acima do chao: no mesmo plano ele briga com o piso (z-fighting).
        go.transform.position = hit.point + Vector3.up * 0.01f;
        go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        float d = Random.Range(0.35f, 0.7f) * intensidade;
        go.transform.localScale = new Vector3(d, d * Random.Range(0.7f, 1.3f), 1f);

        go.GetComponent<MeshRenderer>().sharedMaterial = MaterialMancha();
        go.AddComponent<MarcaQueSome>().Configurar(Random.Range(14f, 22f), 4f);
    }

    /// <summary>Clarao de 1 frame e meio. E' o que faz o golpe "estalar": a luz pega
    /// a parede e o corpo do inimigo por um instante, entao o acerto existe no mundo
    /// e nao so' na frente da camera.</summary>
    private static void CriarClarao(Transform pai, float intensidade)
    {
        var go = new GameObject("Clarao");
        go.transform.SetParent(pai, false);
        var luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.82f, 0.5f);
        luz.intensity = 9f * intensidade;
        luz.range = 4.5f;
        luz.shadows = LightShadows.None;

        var fade = go.AddComponent<ClaraoDeImpacto>();
        fade.Configurar(0.14f, true);
    }

    /// <summary>Materiais criados uma vez e reaproveitados - um material novo por
    /// golpe vazaria memoria numa luta de barragem.</summary>
    /// <summary>Poe o material em modo transparente DE VERDADE.
    ///
    /// Definir _Surface/_Blend so' mexe no que o Inspector mostra: quem realmente
    /// liga a transparencia em runtime sao o blend do render state, a fila e os
    /// KEYWORDS do shader. Sem isto o material fica opaco com a textura carregada -
    /// a particula desenha o quad inteiro e ignora o alfa da mascara, que foi
    /// exatamente o "sangue quadrado" que sobreviveu a primeira correcao.
    ///
    /// aditivo=true soma luz (fagulha, brasa); false e' alfa normal (sangue, poca).</summary>
    private static void PorEmTransparente(Material m, bool aditivo)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", aditivo ? 1f : 0f);
        m.SetFloat("_AlphaClip", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)(aditivo ? UnityEngine.Rendering.BlendMode.One
                                            : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        if (aditivo) m.EnableKeyword("_ALPHAMODULATE_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Material MaterialFagulha()
    {
        if (matFagulha != null) return matFagulha;
        matFagulha = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        PorEmTransparente(matFagulha, aditivo: true);
        matFagulha.SetColor("_BaseColor", new Color(1f, 0.75f, 0.35f, 1f));
        matFagulha.SetTexture("_BaseMap", TexturaDoPonto());
        return matFagulha;
    }

    private static Material matMancha;

    /// <summary>Mancha no chao: transparente e SEM brilho especular. Sangue fresco
    /// brilharia, mas numa cozinha ja' toda laranja de fornalha um decalque brilhante
    /// vira uma poca de plastico - o realismo aqui pede fosco.</summary>
    private static Material MaterialMancha()
    {
        if (matMancha != null) return matMancha;
        matMancha = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        PorEmTransparente(matMancha, aditivo: false);
        matMancha.SetColor("_BaseColor", new Color(0.20f, 0.012f, 0.012f, 0.85f));
        // Mesma razao das particulas: sem mascara a poca e' um retangulo perfeito.
        matMancha.SetTexture("_BaseMap", TexturaDoPonto());
        return matMancha;
    }

    private static Material MaterialSangue()
    {
        if (matSangue != null) return matSangue;
        matSangue = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        PorEmTransparente(matSangue, aditivo: false);   // sangue nao brilha
        matSangue.SetColor("_BaseColor", Color.white);
        matSangue.SetTexture("_BaseMap", TexturaDoPonto());
        return matSangue;
    }
}
