# Sub-Rust — guia de orientação para IAs

Leia isto ANTES de mexer em qualquer coisa. Ele existe porque a sessão de IA
anterior a você não tem memória da conversa passada — só o que está salvo em
disco. Se você chegou aqui perdido tentando achar "a ponte" ou "a área 14",
era exatamente esse o problema que este arquivo resolve.

## O jogo

Sub-Rust é um side-scroller 2.5D de ação (estilo Dead Cells / Hollow Knight
Silksong em termos de estrutura de mapa), Unity 6000.5.3f1, URP. Movimento
travado no eixo X, câmera lateral fixa. Combate corpo a corpo (espada na mão
direita) + à distância (espingarda calibre 12 na mão esquerda).

O jogo terá **8 fases**. Só a **Fase 01 — Castelo das Cinzas** existe hoje.

**Regra de stamina**: correr é livre e nunca consome stamina. A barra fica
reservada para ações de combate, como a esquiva, através de
`PlayerHealth.TryGastarStamina`.

## A planta da Fase 01 é a fonte da verdade

`docs/Fase-01-Castelo-Planta.svg` — abra num navegador ou visualizador de SVG.
Mostra as ~24 salas planejadas (numeradas 00 a 23), a rota principal (dourado),
rotas de exploração (azul), atalhos (verde) e quedas/subidas verticais
(tracejado marrom). **Todo nome de sala no projeto usa esse número** —
`Sacristia_11`, `Ponte_das_Correntes_08`, `Arquivos_Altos_14`. Se você precisa
saber "o que vem depois de X" ou "onde essa sala se conecta", a resposta está
no SVG, não em suposição.

Não confie em geometria de pixel do SVG para inferir conectividade com
certeza absoluta — ele foi desenhado como planta conceitual, e pelo menos uma
inconsistência entre rótulo/retângulo já foi encontrada nele. Quando o SVG e
o que está construído na cena divergirem, **pergunte ao usuário**, não escolha
uma leitura sozinho.

## Onde as coisas estão

- **Cena persistente**: `Assets/_Project/Core/Scenes/Core.unity` — HUD + EventSystem.
  Carregada ADITIVAMENTE por toda cena de fase (ver "Camada Core" abaixo).
- **Cena da fase 01**: `Assets/_Project/Fases/Fase01_Castelo/Scenes/Fase01_Castelo.unity`
- **Prefabs compartilhados**: `Assets/_Project/Prefabs/` (`UI/HUD.prefab`)
- **Scripts de gameplay**: `Assets/_Project/Scripts/` (não `Assets/Scripts/` — o
  projeto foi reorganizado; se você procurar no lugar errado vai concluir que
  os scripts não existem)
- **Scripts de UI**: `Assets/_Project/Scripts/UI/`
- **Materiais/texturas/modelos da Fase 01**: `Assets/_Project/Fases/Fase01_Castelo/`

### Salas já construídas na cena (setembro/2026)

| Objeto raiz na cena | Sala (SVG) | Observação |
|---|---|---|
| `Patio_Central` | 01 · Pátio (entrada) | Sala inicial, tem a porta pra Sacristia |
| `Sacristia_11` (dentro de `Patio_Central`) | 11 · Sacristia | Galeria com pilares, ~1140 objetos |
| `Ponte_das_Correntes_08` (dentro de `Sacristia_11`) | 08 · Ponte, correntes | 3 tramos + portal de entrada, ~40m de queda até o nível da ponte |
| `Arquivos_Altos_14` (raiz da cena) | 14 · Arquivos altos | Livraria gigante do castelo, 18m de pé-direito. 4 módulos de estante (`Estante_A/B1/B2/C`) cobrindo a parede INTEIRA de ponta a ponta (76 a 132, chão a teto) com ~350 livros — zero pedra do castelo visível na parede, só madeira/couro (Polyhaven). 2 escadas de mão VERTICAIS (`Escada_1/2`, tipo escada de bombeiro — sobe parado numa coluna, não é rampa) ligam o chão ao `Nivel_01_Galeria`, a 8m de altura. A galeria possui 55,2m de extensão, três pisos caminháveis, aberturas de 1,10m para as escadas, vigas, corrimão de fundo e 13 mísulas. Acima dela existe o `Nivel_02_Superior`, também com 55,2m, quatro trechos caminháveis e três novas escadas em X=82/104/126: `Escada_3_Capela_12`, `Escada_4_Central` e `Escada_5_AtalhoC_Cozinhas_10`. Cada uma sobe 7,5m até Y=-20,60; o piso superior mantém 2,5m livres até o teto. Quatro tochas iluminam esse nível. O Z de cada escada é detectado automaticamente pela média dos degraus; durante a subida o jogador alinha ao plano visual e, ao sair, retorna ao plano caminhável anterior. Colisores individuais de livros, páginas, tochas e escadas foram removidos; somente pisos e estrutura física mantêm colisão. Escalada: `EscadaDeMao.cs` + `PlayerClimb.cs` (tecla W/S segurando perto da escada) |
| `Inimigos_Fase08` | — | Marcadores de posição de inimigos ao longo da descida/ponte |

**Isso não é hierarquia final.** Salas ficaram aninhadas umas dentro das
outras conforme foram sendo construídas em sequência (ex: a Ponte está
DENTRO da Sacristia, não como irmã dela na raiz da cena). Se for reorganizar,
avise o usuário antes — mover objetos com Player/Enemy referenciando posições
pode quebrar coisa.

### Convenção de sub-grupos dentro de cada sala

Salas grandes (Ponte, Arquivos Altos) são organizadas em sub-grupos
numerados: `00_Portal_Entrada`, `01_Estrutura`, `02_.../03_.../04_...`. A
numeração não é rígida — cada sala escolheu a própria, mas sempre entrada
primeiro, estrutura base em seguida.

## Camada Core: o que NÃO pode morar dentro de uma cena de fase

O jogo terá 8 fases. Qualquer coisa que precise existir em todas elas mora na
cena `Core`, nunca solta dentro do `.unity` de uma fase — senão cada fase nova
teria que remontar e manter a própria cópia, e um ajuste não se propagaria.

Hoje a `Core` contém:

- `HUD` — instância do prefab `Assets/_Project/Prefabs/UI/HUD.prefab`
  (PainelStatus com Portrait/HealthBar/StaminaBar/Municao, PotionCounter,
  SoulCounter). **Editar sempre o prefab**, não a instância.
- `EventSystem` — input de UI. Foi movido para cá junto com a HUD; a cena de
  fase não tem mais o dela.

Cada cena de fase carrega a `Core` através de um objeto raiz `_CoreLoader`
(`CoreSceneLoader.cs`), que faz `LoadScene("Core", Additive)` no `Awake` e não
duplica se ela já estiver carregada. Isso mantém o fluxo normal do projeto de
dar Play direto numa cena de fase pelo editor, sem passar por menu.

**Ao criar a Fase 02+**: a cena nova só precisa de um objeto vazio com
`CoreSceneLoader` e de estar registrada no Build Settings. Não copie a HUD.

### A armadilha de ORDEM (já paga)

`PlayerHealth.Start` e `PlayerShooting.Start` publicam os eventos estáticos
(`OnHealthChanged`, `OnMunicaoMudou`) para a HUD desenhar. Mas a `Core` carrega
DEPOIS da cena da fase: quando a HUD assina os eventos, eles já dispararam, e
ela nasceria **em branco** — sem erro nenhum no console.

Solução: a HUD **puxa** o estado em vez de esperar. `HudBootstrap` (na raiz do
prefab HUD) acha o jogador e chama `PlayerHealth.PublicarEstado()` /
`PlayerShooting.PublicarEstado()`, insistindo por alguns segundos caso o
jogador apareça atrasado. `PlayerCurrency` é estático e já é lido direto no
`OnEnable` dos displays.

Qualquer indicador NOVO da HUD que dependa de um evento disparado no `Start` de
algo da fase tem o mesmo problema — dê a ele um caminho de leitura direta.

### `Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")` não existe em runtime

Esse builtin é só do editor no Unity 6; usá-lo enchia o console de
`Failed to find UI/Skin/UISprite.psd`. A HUD inteira deste projeto usa `Image`
com **sprite nulo** (retângulo sólido), que é o visual pretendido. Siga isso.

## Escala e números medidos (não chutar de novo)

O personagem tem `CharacterController`: altura 1.8, raio 0.35, `slopeLimit`
padrão 45°. Isso já foi medido e usado pra calibrar tudo abaixo — reaproveite
em vez de adivinhar:

- **Pulo máximo (correndo)**: 2.91 m (jumpSpeed=7, gravity=-25, runSpeed=5.2).
  Vãos que exigem pulo devem ficar em ~2.2 m pra ter margem de erro.
- **Rampas/escadas**: usar ~26-34° de inclinação (bem abaixo do limite de
  45°). Uma rampa de 8m de subida precisa de ~12-16m de percurso horizontal
  pra ficar confortável.
- **Pé-direito padrão**: 6-7m nas salas normais (Sacristia). Arquivos Altos
  usa 18m de propósito — é o que o nome da sala pede.
- **Alcance de tiro da espingarda**: varredura por esfera (`SphereCastNonAlloc`,
  raio 0.16-0.25), NUNCA `Raycast` fino — a mão esquerda fica ~0.31m fora do
  eixo do corpo e um raio fino passa raspando por fora da cápsula do inimigo
  (raio 0.30m). Ver `PlayerShooting.cs`.

## Técnicas de construção já validadas (reusar, não reinventar)

Tudo isso foi construído por código (`execute_code` do MCP for Unity), não
manualmente no editor. Os padrões abaixo já foram testados e funcionam:

**Porta/portal com arco de pedra**: duas paredes laterais + verga (lintel) +
batentes (`Cantaria_Clara`) + um arco de 11 "aduelas" em semicírculo
(cubos finos dispostos em leque, cada um rotacionado por
`Quaternion.LookRotation(tangente, radial)`). Usado no Pátio Central, na
entrada da Ponte e na chegada dos Arquivos Altos.

**Tocha de parede**: grupo com 7 filhos — Placa, Braço, Cesto (tudo material
`Ferro_Tocha`), três esferas de chama em degradê (`Chama_Base` → `Chama_Meio`
→ `Chama_Topo`, cores cada vez mais claras/amarelas) e uma `Light` do tipo
Point (cor ~`RGB(1, 0.57, 0.25)`, intensidade 10, alcance 12, sombra Soft).
Copiar a árvore inteira de `Tocha_Galeria_01` é mais rápido que remontar.

**Rampa (ligação vertical caminhável)**: um `Cube` esticado entre dois pontos.
**A rotação certa quando os dois pontos só variam em X e Y (Z constante)** é
`Quaternion.Euler(0, 0, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg)` — NÃO usar
`Quaternion.LookRotation` com uma rotação extra de 90° em X, isso já quebrou
uma rampa nesta mesma sessão (ela saiu de cabeça pra baixo/na direção errada
sem gerar nenhum erro de compilação, só flutuando no vazio).

**Estante gigante com livros**: grupo com painel de fundo (encostado na
parede, Z alto) + 2 postes laterais + N pranchas horizontais (`Prateleira`)
espaçadas ~4m entre si (dá pra encaixar livros "gigantes" de ~2-3m de
altura em cada vão) + fileiras de `Livro` (caixas com largura/altura
aleatórias, material alternando entre duas cores de couro) mais uma tampa
fina de cor de página (`Livro_Pagina`) por cima de cada um. Usado nos
Arquivos Altos (`Estante_85/105/125`, ~150 livros no total). Estante inteira
fica em Z positivo (é cenário, não caminho) — ver a regra do Z=0 abaixo
antes de decidir onde encostar a próxima.

**Escada de mão vertical (tipo escada de bombeiro)**: NÃO é uma rampa
disfarçada — o jogador sobe parado numa coluna X fixa, sem andar. Geometria:
2 trilhos verticais + degraus horizontais finos a cada ~0.35m (material
`Ferro_Tocha`, combina com o visual de escada de incêndio). Mecânica:
`EscadaDeMao.cs` (marca X/baseY/topoY/alcance e calcula Z pela média dos
degraus) + `PlayerClimb.cs`
na raiz do Player (desliga `PlayerMovement2_5D`, sobe/desce em Y direto via
`CharacterController.Move` enquanto W/S estiver pressionado, solta com
Espaço ou ao chegar no topo/base). No topo, `PlayerClimb` testa o piso no X
central e nos dois lados; quando existe abertura no centro, move o jogador
lateralmente para um dos pisos antes de soltar. Se nenhum piso válido existir,
mantém o jogador agarrado e registra erro, em vez de deixá-lo cair no vazio.
Mesmo padrão do `PlayerDodge`: desliga o
movimento normal, dirige o controller na mão, devolve o controle ao sair.
Usado nos Arquivos Altos (`Escada_1` em X=91, `Escada_2` em X=117 e as
escadas superiores em X=82/104/126). A `escada_ponte` em X=73,8 possui o
`Patamar_Inferior_Ponte` com topo Y=-46,30; não remover esse piso porque ele
garante a entrada e a saída segura na base do atalho.
Todas as escadas dos Arquivos prolongam os trilhos cerca de 1,45m acima do
`TopoY`, com degraus e pegadores extras. O `TopoY` continua no nível do piso;
a extensão é visual e não possui Collider, para o personagem conseguir sair
lateralmente sem ficar preso.

**Animação de escalada do jogador**: `Assets/ThirdParty/Characters/Marauder/Mixamo/Climbing.fbx`, importada como Humanoid copiando o Avatar de `Marauder_Mixamo_Idle`. O clipe `Mixamo_Climbing` fica em loop e com root motion travado, porque o deslocamento real continua sendo dirigido pelo `CharacterController` em `PlayerClimb`. No `MarauderMixamoLocomotion.controller`, o bool `Escalando` entra no estado `Climbing` e retorna para `Locomotion` quando fica falso. O parâmetro float `DirecaoEscalada` controla a velocidade do mesmo estado: `1` sobe, `-1` reproduz o mesmo clipe ao contrário para descer e `0` mantém a pose quando o jogador para no meio da escada. Enquanto está agarrado, o `Visual` gira para +Z ou -Z conforme o lado real da escada e mantém `Escalando=true`; ao soltar, restaura rotação, espelhamento e Z anteriores. Durante a escalada, `Pistola` e `RealSword_Right` ficam ocultas e `PlayerCombat`/`PlayerShooting` são bloqueados; os estados anteriores são restaurados ao sair. Não substituir o controller nem o Avatar ao trocar esse clipe.

**Parede "cheia" (sem pedra visível) por cima de uma parede já existente**:
não é só encostar um painel de madeira em "Z da parede" — a parede original
tem ESPESSURA (ex: `Parede_Norte` vai de Z=3.70 até Z=4.30, não é uma
casca fina em Z=4.0). O painel novo precisa ficar com a FACE DA FRENTE dele
em Z **menor** que a face da frente da parede original, não no centro dela.
Meça o `Renderer.bounds` da parede que você quer cobrir antes de decidir o Z
do painel novo - já rolou (nesta mesma sala) o painel ficar ENTERRADO DENTRO
da parede de pedra por causa disso, e nenhum teste de "achei chão"/"câmera
livre" pega esse tipo de erro. O teste certo é: raycast saindo de Z bem
negativo (antes de tudo) na direção +Z, catalogando o PRIMEIRO material
atingido em vários X/Y — se aparecer o material da parede antiga em algum
ponto, alguma coisa nova não está realmente na frente dela.

**Todas essas técnicas produzem geometria sem erro de compilação mesmo
quando a MATEMÁTICA está errada.** Isso significa que "compilou" não prova
nada. O próximo item é sobre isso.

## Escadas de mão: o componente SE MEDE SOZINHO (não digite números)

`EscadaDeMao` não tem mais campos de posição para preencher. Ele deriva tudo da
geometria real:

- **X e Z** = média da posição mundial dos filhos cujo nome começa com `Degrau`.
- **BaseY** = piso sondado por raycast a partir do degrau mais baixo (a
  `escada_ponte` tem o degrau mais baixo 1,20 m acima do chão — a sondagem pega
  o chão, não o degrau).
- **TopoY** = piso sondado nos dois lados perto do alto dos trilhos. **Não é o
  fim dos trilhos**: as escadas prolongam os trilhos ~1,45 m acima do piso de
  chegada de propósito, como pegador de saída.

**Para criar uma escada nova, copie uma existente e arraste.** Não há o que
configurar. Se precisar remedir na mão, use o menu de contexto do componente
("Medir agora"). `alcanceX`, `folgaZ` e `zCaminhavel` continuam ajustáveis.

### Por que isso mudou (bugs reais que estavam na cena)

Os valores eram digitados à mão e ficavam para trás quando a escada era copiada
ou movida — sem erro de compilação e sem nada no console:

| Escada | Campo | Estava | Real | Efeito |
|---|---|---|---|---|
| `Escada_1` | `TopoY` | −27,00 | −28,10 | 1,10 m fora da tolerância de saída → **jogador ficava preso no topo** |
| `Escada_1` | `X` / `Z` | 90,80 / 1,06 | 91,00 / 1,39 | corpo subia torto, ao lado dos degraus |
| `escada_ponte` | `X` | 73,80 | 74,42 | 0,62 m fora do lugar |

### Agarrar TELEPORTA para a coluna — nunca interpolar

No instante do agarre o jogador é reposicionado de uma vez para
`(escada.X, altura atual, escada.ZAgarrado)`. **Não** existe aproximação
gradual: qualquer interpolação aparece em tela como o personagem descendo de
lado, torto, até chegar no lugar. Uma tentativa de resolver isso com uma "fase
de encaixe" (alinhar X/Z primeiro, com Y travado) ainda ficava visível e foi
descartada — o certo é o salto instantâneo.

O salto é grande na `escada_ponte`: **3,11 m em Z**, porque os degraus dela
ficam na face externa do convés (que vai só até Z = −2,50), enquanto o jogador
anda em Z = 0. Nas escadas dos Arquivos Altos são ~1,05 m.

Durante a escalada X e Z são **fixados** na coluna a cada frame (deltas ~0),
não interpolados — serve só para impedir deriva.

### `controller.Move` não teleporta

`Move` varre o caminho e para no primeiro obstáculo, então um salto grande
chega no lugar errado **sem avisar**. Todo reposicionamento da escada (agarre,
saída no topo, pouso na base, volta ao plano ao soltar) desliga o
`CharacterController`, escreve `transform.position` e liga de novo — que é a
forma suportada.

Antes de cada teleporte, `CabeEm` sonda a cápsula do próprio controller no
destino. O raycast de piso só prova que **existe chão**, não que **há espaço**;
teleportar sem essa checagem é o jeito clássico de enfiar o personagem numa
parede. Se não couber, cai de volta no `Move`.

A volta ao plano caminhável ao soltar (Espaço) também teleporta, e por um
motivo mais sério: o movimento 2.5D **nunca** altera Z. Um `Move` bloqueado ali
deixaria o personagem preso em Z = −3,11 na `escada_ponte`, sem nada no jogo
capaz de trazê-lo de volta.

### Saída no topo

`EscadaDeMao.TentarAcharSaida` sonda o piso na hora e devolve o ponto exato de
pouso (piso + 2 cm), então a chegada fica **sempre rente ao piso** por
construção, em vez de depender de um número serializado bater com a geometria.

A ordem dos candidatos é: lado que o jogador pediu com **A/D durante a subida**
→ lado oposto → centro. O centro é o último de propósito: o X da escada
costuma ser justamente o buraco por onde ela sobe. Sem A/D, o padrão é sair
pelo lado por onde o jogador chegou.

Se nenhum candidato tiver piso, o jogador **continua agarrado** e sai um
`Debug.LogError` — nunca cai no vazio.

### O laço agarra/solta — causa real do "tremendo as pernas"

Ao sair da escada, o jogador quase sempre **ainda está segurando W/S** — foi
segurando que ele chegou no fim. Sem exigir uma tecla nova, o `Update` seguinte
reagarra na hora, chega no fim no mesmo frame, solta, reagarra: um laço
agarra/solta **a cada frame**. Na tela isso é o personagem tremendo, porque o
Animator liga/desliga `Escalando`, o `Visual` gira 90° e as armas piscam 60x por
segundo.

Medido: `SairNaBase` solta o jogador a **dx = 0,00** da escada (o centro é o
primeiro candidato de pouso, e no pé da escada é chão firme), bem dentro do
`alcanceX` de 0,9 — reagarre garantido. No topo dá 0,90 contra alcance 0,9, que
também passa.

Correção: `PlayerClimb.exigeNovaTecla`. `Soltar` liga o flag, e o `Update` só
volta a permitir agarre depois que W/S/setas forem **soltas**. Uma linha em cada
ponta, sem timer.

**Isso enganou por muito tempo.** Foram investigados e descartados antes, todos
por medição: Foot IK (desligado em todos os estados), `FallDamage` (serializado
com `alturaSegura=12`, e nenhuma escada passa de 10,24 m), root motion
(`applyRootMotion=false`, clipe travado) e `SeparacaoDeCorpos` (que de fato
escrevia `transform.position` no `LateUpdate` durante a escalada — foi desligado
durante o agarre e continua certo, mas **não era o tremor**).

A lição: quando algo "treme", procure primeiro por **estado ligando e desligando
todo frame**, não por física.

### Saída na base

Simétrica à do topo, com uma diferença: **o centro vem primeiro**. O pé da
escada normalmente é chão firme, e sair de lado sem necessidade faz o
personagem dar um passo torto ao encostar no chão. Medido nas 6 escadas: todas
pousam no próprio pé da escada, rente ao piso (erro 0,000).

Antes disso a base chamava `Soltar()` direto, que devolvia o Z num movimento
**separado**, depois de já ter liberado o controle — dois passos no mesmo
frame, e era isso que dava o solavanco ao encostar no chão. Agora é um `Move`
só, para o ponto sondado e já no plano caminhável.

Se não houver piso sondado embaixo, solta com `LogWarning` e deixa a gravidade
resolver — ao contrário do topo, cair na base é desfecho aceitável.

### `folgaZ`: o corpo fica na FRENTE dos degraus — a frente é SEMPRE −Z

Alinhar o jogador ao Z exato do degrau enfia metade do personagem dentro da
escada. `ZAgarrado` recua `folgaZ` (0,32 m) **sempre para −Z**, porque a câmera
deste jogo fica presa em `playerZ − 14` olhando para +Z: a frente é o Z menor,
ponto final. O valor 1,06 digitado à mão na `Escada_1` era exatamente essa
folga, feita uma vez e nunca replicada.

**Não use "o lado de onde o jogador veio" como critério.** Os dois critérios
coincidem nas 5 escadas dos Arquivos Altos (degraus em Z ≈ +1,33, jogador em
Z = 0) e se **invertem** na `escada_ponte`, a única com degraus em Z negativo
(−2,79). Escrever a regra pelo lado de origem fazia o corpo descer **atrás**
dos degraus, escondido pela própria escada — e só nela.

Pelo mesmo motivo, o giro do `Visual` ao agarrar é **−90 fixo**, nunca
condicional ao Z da escada. `PlayerMovement2_5D` nunca gira o Player
(`transform.rotation` é fixo em `LookRotation(right)`); virar é espelhar
`localScale.z`. Logo a rotação de origem é constante e um giro condicional só
podia estar errado — era o que deixava o corpo de costas na `escada_ponte`.

### Não existe "atravessar o piso" durante a escalada

Houve uma tentativa de fazer o jogador ignorar os colisores da coluna da escada
para vencer o convés `Ponte_Tramo_2`, que parecia bloquear a descida. Era
diagnóstico errado: o bloqueio vinha do Z invertido acima, que punha o corpo
dentro do convés. Pior, a sondagem daquele remendo pegava **o piso da base em
todas as escadas** (a cápsula encosta na laje), então ele deixava o jogador
afundar no chão ao descer. Com o Z certo a coluna fica livre nas 6 escadas,
medido. Se aparecer de novo uma escada que "não desce", meça a coluna antes de
concluir que é colisão — o Z é o suspeito mais provável.

## Objeto criado por script NÃO marca a cena como suja

**Esta é a armadilha mais cara desta sessão.** `new GameObject()` e
`PrefabUtility.InstantiatePrefab()` rodando em `execute_code` no editor criam o
objeto na cena, mas **não** ligam o flag de "cena modificada". Consequências:

```csharp
// ISSO NÃO SALVA NADA. Já custou trabalho perdido.
if (cena.isDirty) EditorSceneManager.SaveScene(cena);
```

O guard `isDirty` é falso, o save é pulado, e o trabalho existe só na memória do
editor. Aí basta entrar em Play Mode (que recarrega a cena do disco) para tudo
sumir sem aviso, sem erro e sem prompt de "salvar alterações?" — porque para o
Unity não havia alteração nenhuma.

**Sempre**, depois de criar/mover objeto por script:

```csharp
EditorSceneManager.MarkSceneDirty(cena);
EditorSceneManager.SaveScene(cena);      // incondicional
AssetDatabase.SaveAssets();
```

E **confira no disco**, não no editor. Instância de prefab não aparece como
`m_Name:` no `.unity` — o nome fica como override, então procure
`value: <Nome>` e `PrefabInstance:`.

Foi assim que `Escada_4_Central` e os quatro `Piso_Superior_*` do
`Nivel_02_Superior` se perderam: existiam na memória do editor, os "saves" foram
pulados pelo guard, e um Play Mode recarregou a cena do disco sem eles. Não há
backup (`Temp/__Backupscenes` não existe) e eles nunca chegaram a nenhum commit.

## Exportar FBX do Blender para este projeto

Os defaults **não** servem. Medido nesta sessão com a vela/candelabro: com as
opções erradas a malha chega 100× maior e deitada (altura no Z), e o
`useFileScale=0.01` do importador ainda encolhe tudo por 100 de novo.

No Blender (`bpy.ops.export_scene.fbx`):

- `apply_scale_options='FBX_SCALE_ALL'` — assa a escala na malha; sem isso o nó
  raiz vem com escala 100.
- `bake_space_transform=True` — assa a conversão de eixo nos vértices; sem isso
  a malha fica Z-up e o objeto chega deitado no Unity.
- `axis_forward='-Z'`, `axis_up='Y'`, `global_scale=1.0`, `apply_unit_scale=True`.

No Unity: `useFileScale = true`, `globalScale = 1`.

Resultado certo: prefab **sem filhos**, `localScale = 1`, e as dimensões batendo
com as do Blender. Autore a malha com a **origem na base** (bottom-center) —
assim `transform.position` posiciona em vez de deslocar, e `bounds.min.y` sai 0.

**Verifique instanciando**, nunca lendo o asset: `mesh.bounds` é dado cru e mente
sobre orientação e escala do nó (ver a regra "medir o prefab não é medir a
instância").

## Velas da sala 14 (`06_Velas`)

Iluminação temática da Livraria, no grupo `Arquivos_Altos_14/06_Velas`.

- **110 velas de prateleira** (`Velas_Estantes/Velas_<Estante>`), 55 acesas.
- **6 candelabros de chão** (`Candelabros`), 1 luz cada (3,0 · 6,0 · sem sombra).
- **25 luzes de preenchimento** (`Preenchimento`). Total 94 luzes na sala.

Modelos: `Models/Vela.fbx` (0,143 × 0,229 × 0,127 m) e `Models/Candelabro.fbx`
(0,418 × 1,174 × 0,405 m), feitos no Blender, origem na base. Materiais
`Cera_Vela` e `Pavio_Vela`; ferro e chamas reaproveitam `Ferro_Tocha` e
`Chama_Base/Meio/Topo` das tochas, para a sala falar uma língua visual só.

### Iluminação: o ACES é o motivo de tudo parecer preto

O `Global Volume` usa **Tonemapping ACES**, que esmaga os tons médios-baixos.
Os materiais de livro são **brancos com textura de couro real** (medido:
`Livro_Vermelho`, `Livro_Marrom`, luminância de cor base 1,0) — se os livros
aparecem pretos, é o ACES + falta de luz, **não** material escuro. Não perca
tempo mexendo na cor deles.

Trocar ACES por Neutral resolve na hora, mas `SampleSceneProfile` é **global**:
muda todas as fases. Decisão do usuário em setembro/2026: **manter ACES** e
compensar dentro da sala.

A compensação da sala 14 tem três partes:

1. **Densidade**: 110 velas (6 por prateleira), 55 acesas. Com 3 por prateleira
   o vão entre elas era ~4 m e a queda quadrática apagava o meio.
2. **Ambiente quente**: `RenderSettings.ambientLight` = 0,30 · 0,215 · 0,155
   (era 0,19 · 0,20 · 0,22, frio). **Atenção: `RenderSettings` é da cena
   inteira** — mexer aqui muda Pátio, Sacristia e Ponte junto. Foi conferido por
   render que essas áreas continuam escuras e com clima.
3. **`06_Velas/Preenchimento`**: 25 Point Lights largas e fracas (intensidade
   1,35 · alcance 22 · sem sombra) em Z = 0,55, cinco por nível de prateleira.
   Simulam o bounce que o ACES come. Fracas de propósito: mais que isso cria
   ponto quente visível e denuncia a luz falsa.

Total: 94 luzes na sala. O renderer é **Forward+**, que usa luz clusterizada e
não tem o limite de 8 luzes por objeto do Forward tradicional — por isso essa
quantidade é viável. O `PerVertex` que aparece no asset do URP é valor legado
que o Forward+ ignora.

### Cores dos livros (`Materials/Livros/`)

14 materiais de couro + 3 de página, atribuídos aos 353 livros com pesos
(marrons e vermelhos comuns, azul/roxo raros) e sem repetir cor em vizinhos —
senão aparecem blocos da mesma cor na estante.

A cor base **multiplica** a textura, então a tinta de cada material é calculada
como `alvo ÷ média da textura`, não escolhida no olho. Média medida das duas
texturas de couro: **(0,315 · 0,177 · 0,084)**, luminância 0,20 — as duas são
quase idênticas. Como o azul da textura é só 0,084, cores frias exigem tinta
acima de 1 (o `_BaseColor` do URP Lit aceita, é multiplicador).

**Não mire albedo escuro achando que é realista.** A primeira paleta mirou
0,035–0,26 de luminância, mais escuro do que os livros já eram (branco ×
textura = 0,20), e ficou tudo preto. Os alvos bons ficam em **0,08–0,40**.

### Se algo parece "sem cor", o suspeito é INTENSIDADE, não material

Perdi várias tentativas nesta sala atrás da causa errada — mexi em densidade de
velas, ambiente, tamanho de chama e no modo de luz do URP, e nada resolveu.

O teste que resolveu em uma tentativa: **pintar alguns objetos de branco puro e
pôr uma luz forte na frente**. Tudo acendeu e as cores dos livros vizinhos
apareceram na hora — provando que luz chegava e o que faltava era quantidade.
Faça esse teste ANTES de teorizar sobre normais, modo de render ou material.

Valores que funcionaram nesta sala: velas 11–16 (alcance 9–12), preenchimento
14 (alcance 18), candelabros 26 (alcance 20), tochas 22 (alcance 20). Os
valores antigos das tochas (9 / alcance 11) eram fracos demais para o tamanho
real da sala.

### Forward+ ignora `additionalLightsRenderingMode` — confirmado por render

O `PC_RPAsset` tem `m_AdditionalLightsRenderingMode = 2 (PerVertex)` e
`m_AdditionalLightsPerObjectLimit = 8`, mas o `PC_Renderer` está em
`m_RenderingMode = 2 (Forward+)`, que usa luz clusterizada e **ignora os dois**.

Isso foi testado direto: render em PerPixel e em PerVertex saíram idênticos.
Não perca tempo mexendo nesses campos — e não os culpe por sala escura.

### Chama: material próprio, nunca o das tochas

`Chama_Vela` (7,0 · 3,6 · 1,15) e `Chama_Vela_Halo` (1,55 · 0,60 · 0,18), Unlit,
HDR bem acima do `threshold = 0,85` do Bloom. São separados do `Chama_Base/Meio/
Topo` das tochas de propósito: reaproveitar aqueles faria qualquer ajuste de vela
alterar as 8 tochas junto.

O halo começou em 2,6 e ficava um borrão uniforme, parecendo lâmpada em vez de
vela. Também vale variar intensidade/alcance por vela (5,0–7,5 / 7,5–11) e
espalhar o X: velas idênticas e alinhadas leem como enfileiradas, não como sala.

### Renderizar para conferir: LIGUE o pós-processamento

Câmera temporária criada por script vem **sem** pós-processamento, então o render
sai sem ACES, sem Bloom e sem vinheta — **muito mais claro que o jogo**. Já
levou a "está bom" numa sala que no jogo estava quase preta.

```csharp
var d = camGO.AddComponent(tipoUACD);
tipoUACD.GetProperty("renderPostProcessing").SetValue(d, true);
cam.allowHDR = true;                       // sem isso o Bloom nao tem o que ler
// RenderTexture em ARGBHalf, nao ARGB32
```

### O vão da prateleira é 0,150 m — meça antes de girar a vela

As prateleiras estão **lotadas**: zero vão ≥ 0,7 m entre livros nos 4 níveis de
baixo (o de cima, −18,38, está vazio). Então vela não vai *entre* livros, vai na
**faixa da frente**: prateleira começa em Z = 1,530 e o livro mais à frente em
Z = 1,680.

A vela tem 0,127 m de profundidade, mas girada livremente em Y a caixa alinhada
aos eixos chega a **0,190 m** — não cabe. Girar sem medir enfiou 33 velas dentro
dos livros. A solução foi testar ângulos e ficar com um cuja caixa **medida**
caiba no vão. Resultado: 0 batendo em livro, 0 sem apoio, 12 mm de folga.

Tudo que o grupo cria fica em Z ≥ 0,96 — atrás do jogador, sem tampar a câmera.

## `OnEnable` não é confiável durante construção via editor-script

Um componente com lista estática que se auto-registra em `OnEnable`
(`lista.Add(this)`) é um padrão comum em Play Mode - mas construir a cena
via `execute_code` fora do Play Mode **não garante que `OnEnable` dispare no
mesmo frame em que o componente foi criado ou reativado**. Já aconteceu:
`AddComponent<EscadaDeMao>()` seguido de leitura da lista estática = lista
vazia. Forçar `SetActive(false)` seguido de `SetActive(true)` pra tentar
re-disparar o ciclo de vida também não resolveu. Chamar o método de registro
DIRETAMENTE (sem depender do `OnEnable`) funcionou na hora - confirmando que
o método em si estava certo, só o gatilho automático que não rodava.

**Solução adotada**: em vez de manter uma lista cacheada via `OnEnable`/
`OnDisable`, `PlayerClimb` usa `FindObjectsByType<EscadaDeMao>()` sob
demanda, só no instante em que o jogador tenta agarrar uma escada (não todo
frame - o custo é desprezível porque só roda quando ainda não está
escalando e apertou W/S). Menos "elegante" que uma lista mantida, mas não
depende do timing do ciclo de vida do Unity em nenhum contexto (editor OU
Play Mode), então é a opção mais segura pra esse projeto, onde geometria e
os componentes que a acompanham costumam nascer via script de editor.

Se um componente novo precisar descobrir "quem mais existe na cena" e for
criado por script de editor (não só arrumado manualmente no Inspector),
prefira `FindObjectsByType` sob demanda a uma lista auto-registrada, ou pelo
menos teste explicitamente se o registro aconteceu antes de confiar nele.

## O jogador está TRAVADO em Z=0 — geometria fora disso é inalcançável

Exceção controlada: durante `PlayerClimb`, o personagem pode sair
temporariamente desse plano para alinhar ao Z visual da escada. Esse Z é calculado
automaticamente pela média dos objetos `Degrau`; ao soltar, o controller retorna
ao Z pelo qual entrou. A locomoção normal continua sem liberdade de profundidade.

Mais crítico ainda que a regra da câmera abaixo, porque não dá erro nenhum:
a geometria fica sólida, passa em todo teste de física, e mesmo assim o
jogador **nunca consegue pisar nela**, porque ele fisicamente não consegue
sair de Z=0.

`PlayerMovement2_5D.cs` só escreve em X e Y (`Vector3.right * speed + Vector3.up
* verticalVelocity`) — Z nunca é tocado, exceto para espelhar a escala do
Visual ao virar (`localScale.z`, isso é cosmético, não é a posição do
mundo). `SeparacaoDeCorpos.cs` também não mexe em Z. Resultado: o jogador
mantém pra sempre o Z que tinha ao entrar na cena (hoje, `0`).

**Isso já causou um bug real**: a primeira versão da galeria/escadas dos
Arquivos Altos foi construída em `Z=3.0` (pra ficar visualmente encostada
na parede/estante, longe da câmera). Cada checagem de física passou —
`Physics.Raycast` achava chão, a escada emendava nos dois extremos, nada
bloqueava a câmera. **E mesmo assim era 100% inacessível**, porque o
jogador nunca chega em Z=3.0. Só foi pego ao questionar deliberadamente "o
jogador realmente anda em Z livre?" — não apareceu em nenhum teste de
colisão porque colisão não tem opinião sobre onde o *personagem* pode ir,
só sobre se um ponto testado bate em alguma coisa.

**Toda peça CAMINHÁVEL (chão, rampa, escada, galeria, ponte) tem que ter
seu range de Z incluindo 0.** Decoração (estante, tocha, parede) pode ficar
em qualquer Z positivo — o jogador nunca vai tocar nela mesmo, é só pano de
fundo. A dúvida "isso é caminhável ou é cenário?" decide se a regra se
aplica.

Teste que pega isso (rodar depois de QUALQUER rampa/escada nova):
```csharp
// se isto der "SEM CHAO" em algum X do percurso pretendido, a peça
// caminhavel nao inclui Z=0 e ninguem vai conseguir usar ela
bool ok = Physics.Raycast(new Vector3(x, alturaBemAcima, 0f), Vector3.down, out var h, alcance);
```

## A câmera fica em Z negativo — NUNCA feche esse lado

Erro grave já cometido duas vezes na mesma sala (Arquivos Altos), então
mereceu seção própria.

A câmera (`SideScrollCam`, `CinemachinePositionComposer`) fica presa a
**14 unidades atrás do jogador no eixo Z**, olhando pra +Z
(`CameraDistance = 14`, posição típica `(jogadorX, jogadorY+0.9, jogadorZ-14)`,
forward `(0,0,1)`). Isso significa: **tudo que for sólido e opaco entre
Z = -14 e Z = 0 em qualquer X onde o jogador anda vai tampar a visão dele.**

Toda sala já construída (Pátio Central, Sacristia) segue a mesma convenção
silenciosa: **estrutura sólida só do lado positivo de Z** (paredes, pilares,
decoração). O lado negativo de Z — onde a câmera está — fica sempre aberto,
como um corte de boneca (dollhouse cutaway). É assim que dá pra ter uma sala
com pé-direito e paredes visíveis sem esconder o personagem.

Ao construir Arquivos Altos essa convenção foi quebrada duas vezes seguidas:

1. Uma parede (`Parede_Sul`, Z negativo) fechando o corredor inteiro —
   teria tampado a câmera do início ao fim da sala.
2. Depois de corrigir isso, os pilares do lado sul (`Pilar_*_S`, também Z
   negativo) ainda tampavam o jogador por completo nos 4 pontos exatos em
   que ele passava na frente deles — mesmo sendo peças finas (0.85m), um
   objeto opaco na linha reta câmera→jogador esconde 100%, não existe
   "objeto fino o bastante pra não atrapalhar".

A correção nos dois casos foi `SetActive(false)` nos objetos do lado
negativo (não deletar — reversível), mantendo só os do lado positivo.

**Antes de dar qualquer peça sólida grande por terminada, teste assim**
(substitua os X pelos pontos reais da sala, e o Y pela altura real do chão
— câmera segue `jogador.y + 0.9`, não Y=0 nem Y=3 chutados):

```csharp
foreach (float x in pontosAoLongoDaSala)
{
    Vector3 origem  = new Vector3(x, chaoDaSalaY + 0.9f, -14f);
    Vector3 destino = new Vector3(x, chaoDaSalaY + 0.9f, 0f);
    bool bloqueado = Physics.Raycast(origem, Vector3.forward, out var h, 14f);
    // bloqueado == true em QUALQUER x aqui é bug, não estilo.
}
```

Isso é mais confiável que renderizar e olhar: um objeto pode ficar fora do
enquadramento de UM screenshot e ainda assim tampar a câmera de verdade em
outro X da mesma sala. O raycast varrendo vários X pega isso; o olho não.

## Regra de ouro: meça, não assuma

Este projeto já foi corrigido várias vezes por causa de geometria que
"parecia" certa no código mas estava flutuando, com buraco, ou virada ao
contrário. **Depois de criar/mover geometria, sempre**:

1. `Physics.SyncTransforms()` antes de qualquer `Raycast`/`OverlapBox` no
   mesmo frame em que a geometria foi criada — sem isso a física ainda não
   sabe que os colisores existem, e a checagem vai mentir dizendo "vazio".
2. Testar chão real com `Physics.Raycast(pos + Vector3.up * pequeno, Vector3.down, ...)`
   em vários pontos ao longo do percurso — não só no início e no fim.
3. Pra portas/vãos: `Physics.OverlapBox` no meio da abertura tem que dar 0
   colisores; um pouco pra fora tem que achar parede.
4. Renderizar com câmera própria (`RenderTexture` + `ReadPixels`, fundo
   magenta pra distinguir "buraco no chão" de "céu") antes de dizer que
   terminou. Cuidado com a posição da câmera — ela precisa estar DENTRO do
   volume da sala (checar os X/Y/Z reais da sala, não chutar), senão a
   imagem sai vazia ou mostra o lado errado da parede.

Isso não é burocracia — cada um desses passos já pegou um bug real nesta
sessão (chão sem física sincronizada, rampa invertida, câmera fora da sala).

## Calibragem de combate (setembro/2026)

Números medidos antes de mexer, não chutados. **Os valores serializados na cena
eram diferentes dos defaults do código** — o `EnemyAI.cs` dizia `dano = 8f` mas
os inimigos tinham `60` (o `Enemy1` solto tinha `100`). Sempre leia o
serializado, nunca o default.

| | Antes | Agora |
|---|---|---|
| `dano` do inimigo | 60 | **130** |
| `cooldownAtaque` | 1,80 s | **0,90 s** |
| `tempoDeMira` (telegrafo) | 0,35 s | **0,22 s** |
| `tempoDeRecuo` | 0,55 s | **0,30 s** |
| `velocidade` | 2,0 | **3,3** |
| `distanciaDeRecuo` | 2,4 | **1,9** |
| `raioVisao` | 9 | **11** |
| Clipe `Attack` (speed) | 1,0 → 2,17 s | **1,6 → 1,35 s** |
| Clipe `Hit` (speed) | 1,0 → 0,57 s | **1,25 → 0,45 s** |
| Vida do inimigo | 30 | **100** |
| `forcaRecuo` do impacto | 1,8 · 0,12 s | **7,0 · 0,18 s** |

Ciclo de ataque: **4,87 s → 2,77 s** (mira + clipe + recuo + cooldown).

Janela para esquivar: **0,56 s** (mira 0,22 + 0,34 até o impacto), contra uma
esquiva de 0,16 s com recarga de 0,45 s — continua jogável de sobra.

Balanço resultante: o inimigo mata em **9,6 golpes** (jogador tem 1250 de vida;
antes eram 20,8), e morre em **1,7 tiros** de perto.

### O clipe de ataque era o gargalo, não o cooldown

`Enemy_Attack_1_InPlace` tem **2,17 s crus**. Nenhum ajuste de cooldown deixa o
combate ágil enquanto o golpe em si leva mais de dois segundos.

Acelerar o clipe é **seguro** aqui porque o dano é disparado por
`info.normalizedTime >= momentoDoGolpe` (normalizado 0-1), não por um timer em
segundos — mudar `state.speed` não desincroniza o golpe. Se algum dia isso virar
timer em segundos, essa liberdade acaba.

### Impacto do tiro escala com o dano

`EnemyDamageFeedback.Reagir(origem, dano)` multiplica o empurrão por
`dano / danoDeReferencia` (60), preso entre 0,45x e 2,5x. Tiro à queima-roupa
empurra 1x; de longe, onde a espingarda cai para 25% do dano, empurra 0,45x. A
direção vem da `origem` (a boca da arma), então o empurrão é sempre para longe
de quem atirou.

**A vida do inimigo teve que subir junto.** Com 30 de vida e 60 de dano por
tiro, ele morria no primeiro disparo de perto e o impacto **nunca aparecia**.
Com 100, sobrevive ao primeiro e o empurrão fica visível.

## Coisas que ainda não existem

- Salas 00, 02-07, 09-10, 12-13, 15-23 do SVG: nenhuma foi construída ainda.
- Detalhamento de conteúdo (estantes de livro, decoração) em Arquivos Altos —
  só o casco estrutural existe.
- NavMesh/IA de pathfinding entre andares — o `EnemyAI` atual é uma máquina
  de estados C# (percepção por cone de visão + raycast, patrulha, ataque
  telegrafado, recuo), sem NavMesh. Decisão deliberada, não esquecimento.
- Save/load, menu principal, transição entre fases (a camada `Core` já existe e
  é a base pra isso, mas não há nada que troque de cena ainda).

## Antes de perguntar ao usuário

Se você não sabe onde algo está: procure primeiro por nome de sala
(`GameObject.Find`, ou `find_gameobjects` do MCP) usando o número da sala do
SVG. Se não achar nada, é porque não foi construído ainda — não é erro seu,
é estado real do projeto. Diga isso claramente em vez de adivinhar uma
localização.
