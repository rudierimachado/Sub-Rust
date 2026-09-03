# Regras de construção de sala — Castelo das Cinzas

Documento de obrigação, não de sugestão. Toda sala nova da fase 01 segue isto.
Escrito depois de uma sala inteira (Ponte 08) ser construída errada e jogada
fora: cubos cinzas com `new Material(cor)`, sem porta, com um paralelepípedo
preto de 20 m fazendo as vezes de "fundo". Nada daquilo era reaproveitável.

---

## 1. Asset real, sempre. Primitivo nunca vai para a cena final

O projeto **já tem** biblioteca própria. Antes de criar qualquer geometria,
usar o que existe:

| Elemento | Asset | Caminho |
|---|---|---|
| Arco / janela gótica | `GothicWindow.fbx` | `Assets/Models/` |
| Tocha (braço, cesto, chama) | `Tocha.fbx` | `Assets/Models/` |
| Elo de corrente | `ChainLink_AtalhoB.asset` | `Assets/Models/Environment/` |
| Pedra de parede | `Parede_Castelo.mat`, `Parede_Castelo_Grande.mat` | `Assets/Materials/` |
| Piso | `Chao_Castelo.mat`, `Pedra_Fase1.mat` | `Assets/Materials/` |
| Cantaria (moldura, cornija) | `Cantaria_Clara.mat` | `Assets/Materials/` |
| Fundo escuro / silhueta | `Escuro_Fundo.mat`, `Silhueta_Media.mat`, `Silhueta_Distante.mat` | `Assets/Materials/` |
| Ferro / metal de tocha | `Ferro_Tocha.mat` | `Assets/Materials/` |
| Chama | `Chama_Base/Meio/Topo.mat`, `Fogo_Tocha.mat` | `Assets/Materials/` |

Texturas de tijolo reais em `Assets/Textures/`: `castle_brick_broken_06_*`
(Diffuse/Normal/Rough) e `medieval_blocks_02_*`.

**Proibido:** `new Material(Shader.Find(...))` com cor chapada para geometria de
sala. Material sem textura só é aceitável em teste descartável, nunca commitado.

**Se faltar o asset**, a resposta certa é *parar e pedir*, ou importar um modelo
de verdade — não substituir por cubo e seguir em frente.

## 2. O modelo de referência é o Poço do Incensário

`Atalho_B_Poco_do_Incensario` (dentro de `Sacristia_11`) é o padrão de qualidade
aprovado. Copiar a estrutura dele, incluindo a organização em grupos numerados:

```
01_Fundo_Gotico     arcos, travessas, sombras do fundo
02_Estrutura        muralhas, pilares mestres, capitéis, cornijas, mísulas
03_Rota_Jogavel     só o que o jogador pisa
04_Detalhes         bordas quebradas, dentes, lajes soltas
06_Gaiola           props específicos da sala
07_Segredo          conteúdo opcional
08_Iluminacao       tochas + luzes
```

Cada peça nomeada em português, descritiva e numerada:
`Varanda_do_Altar_Misula_2`, `Tocha_Incensario_04`. Nunca `Cube (3)`.

## 3. Sala fechada tem porta modelada

Vão de porta é **geometria**, não ausência de parede. O padrão já existe no
`Patio_Central`: `Parede_Dir_LadoA` + `Parede_Dir_LadoB` + `Parede_Dir_Verga` +
`Porta_BatenteA/B` + `Porta_Arco` (com 11 aduelas). Toda ligação entre salas
repete isso. Uma sala sem porta desenhada está incompleta, não "simplificada".

## 4. Fundo é camada, não um bloco preto

O fundo do Poço tem parede, travessas, 13 arcos góticos e sombras separadas.
O mínimo para uma sala nova:

1. **Parede de fundo** com material de pedra texturizado;
2. **Camada média** — arcos, pilastras, vergas (`Silhueta_Media.mat`);
3. **Camada distante** — silhueta com `Silhueta_Distante.mat`;
4. **Profundidade por luz**, não por cor chapada.

Um cubo escurecido no lugar de tudo isso é o erro exato que gerou este documento.

## 5. Iluminação: quente perto, fria longe

Convenção já em uso na cena:
- Tocha: `color (1, 0.55, 0.22)`, `intensity ≈ 14–16`, `range ≈ 12–20`.
- Preenchimento distante frio para contraste (azul-esverdeado, intensidade baixa).
- Fog global é **quente** (`0.06, 0.035, 0.02`, densidade `0.02`). Fog é global —
  não dá para ter cor diferente por sala sem Volume local; não mudar por capricho.
- Material branco (`1,1,1`) sob luz ambiente fria fica azulado e chapado. Foi por
  isso que os arcos do Poço pareciam recortes de papel. Tingir o material.

## 6. Medir antes de posicionar

- Ler a posição real da peça vizinha e ancorar nela. A Ponte 08 começa onde o
  Atalho B termina: `Ponte_das_Correntes_Base` em `(20.5, −36.35, 0)`.
- **Bounds:** inicializar com `rends[0].bounds`, **nunca** com
  `new Bounds(grupo.position, Vector3.zero)`. O grupo costuma estar na origem e
  o `Encapsulate` estica a caixa da origem até a peça — foi assim que o
  incensário (correto) foi diagnosticado como "espalhado em 21 m".
- Escala do jogo: personagem ~1,8 m; corredor útil ~6 m em Z; plataformas do Poço
  ~5–9 m.

## 7. Verificação visual sem enganação

`manage_camera screenshot` mente com geometria grande. Renderizar com câmera
própria via `execute_code`:

```csharp
camGO.AddComponent(System.Type.GetType(
  "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"));
cam.useOcclusionCulling = false;
cam.backgroundColor = Color.magenta;   // magenta = buraco real, não céu
```

Enquadrar como o **jogo** enquadra: câmera lateral em `z ≈ −13`, altura do piso
+ ~2 m. Câmera fora das paredes (`z = −45` num salão cujo fundo está em `z = −22`)
só fotografa o verso da parede — três renders foram perdidos assim.

Salvar em `Assets/Screenshots/`, ler o PNG, e **olhar antes de dizer que ficou pronto**.

## 8. Play Mode e salvamento

- Conferir `Application.isPlaying == false` antes de editar cena.
- `EditorSceneManager.SaveScene` (ou `manage_scene save`) ao terminar.
- Nunca `git checkout` numa cena para "limpar" — a cena tem trabalho não commitado.

---

## Checklist antes de dizer "pronto"

- [ ] Zero `new Material(cor)` na geometria; tudo usando `.mat` do projeto
- [ ] Assets reais (`GothicWindow`, `Tocha`, `ChainLink`) no lugar de primitivos
- [ ] Grupos `01_`…`08_` com nomes em português descritivos
- [ ] Porta modelada em cada ligação (lados + verga + batentes + arco)
- [ ] Fundo em 3 camadas, não um bloco
- [ ] Ancorada por medição na peça vizinha
- [ ] Render pela câmera lateral do jogo, PNG olhado
- [ ] Cena salva
