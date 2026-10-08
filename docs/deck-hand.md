# Колода и рука

Классы: `DeckManager`, `HandManager`, `DeckPileScreen`, `DeckPileCountDisplay`, `PileKind`.

Файлы: `Managers/DeckManager.cs`, `Managers/HandManager.cs`, `UI/DeckPileScreen.cs`, `UI/DeckPileCountDisplay.cs`.

См. также: [cards.md](cards.md) — `CardInstance`/`CardPropertyFlags`, которыми оперируют обе колоды, и `CardPreviewDisplay`/`CardPreviewPrefab`, которыми `DeckPileScreen` показывает содержимое стопки; [combat-core.md](combat-core.md) — `PeriodicEffectEvent`, на котором держится автодобор.

## `DeckManager` — четыре колоды

Четыре списка `CardInstance`: `characterDeck` (постоянный, инспекторный — полный набор карт персонажа) и три рабочих, которые пересобираются с нуля перед каждым боем — `drawPile`, `discardPile`, `exhaustPile`.

- `StartCombat()` — копирует `characterDeck` в `drawPile`, перемешивает (`Shuffle`), затем `MoveFrontOfDrawToEnd()`, очищает сброс и сжигание.
- `MoveFrontOfDrawToEnd()` — `DrawCard()` всегда берёт карту **с конца** списка `drawPile` (`drawPile[drawPile.Count - 1]`), поэтому этот метод находит все карты с флагом `FrontOfDraw` и переносит их в конец списка — несмотря на название «в конец», по факту это делает их первыми в очереди на добор.
- `Shuffle(list)` — стандартный тасование Фишера — Йетса на месте (идёт с конца, меняет местами с `Random.Range(0, i + 1)`).
- `DrawCard()` — забирает карту с конца `drawPile`; если он пуст, сначала перекладывает весь `discardPile` в `drawPile` и перемешивает (автоматический ресайкл сброса); возвращает `null`, только если **обе** колоды пусты — добирать больше нечего до конца боя.
- `Discard(instance)` — если `EffectiveProperties` содержит `Exhaust`, карта уходит в `exhaustPile`, иначе — в `discardPile`. Это единственное место, где Сжигаемое реально на что-то влияет; карта проходит здесь и когда она сыграна (`Card.Play` → `Discard`), и когда её принудительно сбрасывают из-за переполненной руки (см. ниже).
- `CharacterDeck`/`DrawPile`/`DiscardPile`/`ExhaustPile` (`IReadOnlyList<CardInstance>`) — публичные проброс-геттеры к четырём приватным спискам, только для чтения. Единственный потребитель — `DeckPileScreen` (ниже): экрану просмотра стопок нужен доступ ко всем четырём, но мутировать их снаружи `DeckManager` по-прежнему нельзя, поэтому `List<CardInstance>` наружу не отдаётся.
- Кнопки `[ContextMenu("Тест: ...")]` — ручная проверка в Play Mode по принципу «один шаг — одна проверка» из корневого `CLAUDE.md`, к реальному геймплею отношения не имеют.

## `HandManager` — рука на 7 явных слотов

- Константы: `MaxHandSize = 7`, `InitialHandSize = 4`, `DrawIntervalSeconds = 2f` — пока не настраиваются на персонажа (YAGNI).
- `slots` (`RectTransform[7]`) — заданы в инспекторе. Осознанный выбор вместо `Horizontal Layout Group` (см. корневой `CLAUDE.md`) — сдвиг карт при уходе одной из них делается вручную, а не автолэйаутом.
- `cardsInSlots` (`Card[7]`) — параллельный массив: занятое место `i` соответствует `slots[i]`; `null` — слот пуст.
- `Start()` — пустой. С циклом боёв (`EncounterManager`, см. [enemies.md](enemies.md)) руку нужно собирать заново перед **каждым** боем, не только один раз при загрузке сцены, поэтому вся прежняя логика `Start()` переехала в публичный `BeginNewHand()`.
- `BeginNewHand()` — вызывается `EncounterManager` после спавна игрока и врагов на каждый бой: сначала `ClearHand()` (уничтожает всё, что осталось от предыдущего боя — рука не переносится между боями по умолчанию), затем `DeckManager.StartCombat()`, `InitialHandSize` карт добора, и регистрация нового `PeriodicEffectEvent` с интервалом `DrawIntervalSeconds` на `TryDrawToHand()` — это и есть автодобор. Регистрируется заново на каждый вызов: старый `PeriodicEffectEvent` прошлого боя не отписывается явно, но и не мешает — он жил в `scheduledEvents`, которые `CombatManager.ResetForNewCombat()` целиком очищает до вызова `BeginNewHand()` (см. [combat-core.md](combat-core.md)).
- `ClearHand()` — проходит все `MaxHandSize` слотов, для каждого занятого уничтожает `GameObject` и обнуляет ячейку `cardsInSlots[i]`. Не различает, была ли карта уже сыграна или просто добрана — на старте нового боя обе категории одинаково не нужны.
- `TryDrawToHand()` — если у колоды реально нечего дать, тихо возвращает `false`. Если карта добралась, но рука полна (`FindFirstEmptySlot()` вернул `-1`), карта **сразу** уходит обратно в `DeckManager.Discard`, ни разу не появившись в руке — переполненная рука не блокирует добор из колоды, она просто впустую тратит добранную карту.
- Создание карты: `Instantiate(cardPrefab, slots[emptySlot])`, затем вручную проставляются `instance`/`combatManager`/`nameLabel` — та же грабля с порядком `Awake()`, что задокументирована в корневом `CLAUDE.md`: к моменту этих присвоений `Card.Awake()` уже отработал, поэтому сам `Card.Awake()` защищается null-проверками.
- `RemoveFromHand(card)` — уничтожает `GameObject`, обнуляет его слот, затем вручную сдвигает все последующие занятые слоты на одну позицию влево (перепривязывает `transform.SetParent` к более раннему `RectTransform`, сбрасывает `localPosition` в ноль) — тот самый «ручной перебор массива вместо автолэйаута» из корневого `CLAUDE.md`.
- `FindFirstEmptySlot()` — простой линейный поиск первого `null`; рука всегда уплотняется к слоту `0`.

## Просмотр четырёх стопок — `DeckPileScreen` + `DeckPileCountDisplay`

Один общий оверлей с вкладками на все четыре стопки — не четыре отдельных экрана.

- **`DeckPileScreen`** (`UI/DeckPileScreen.cs`) — `panelRoot` (полупрозрачная фоновая панель на весь экран, `SetActive(false)` в `Start()`), `cardContainer` (Content внутри Scroll View с `GridLayoutGroup`, размер ячейки как у карты в руке), `cardPreviewPrefab` (`CardPreviewPrefab`, см. [cards.md](cards.md)), ссылка на `DeckManager` и пять кнопок (четыре вкладки + закрыть).
  - `Start()` навешивает по одному слушателю на каждую вкладку — каждый просто вызывает `ShowPile` с соответствующим геттером `DeckManager` (`CharacterDeck`/`DrawPile`/`DiscardPile`/`ExhaustPile`).
  - `Open()` — тонкий проброс к `OpenToPile(PileKind.CharacterDeck)`: включает `panelRoot` и показывает «Колоду персонажа» (вкладка по умолчанию, например при первом открытии оверлея не через конкретную HUD-кнопку).
  - `OpenToPile(pile)` — включает `panelRoot` и показывает **именно ту** стопку, что передана в `pile` (`switch` по `PileKind` на четыре геттера `DeckManager`). Раньше `Open()` был единственным способом открыть панель и жёстко показывал `CharacterDeck` независимо от того, какая кнопка на HUD была нажата — все четыре кнопки `DeckPileCountDisplay` вели на одну и ту же вкладку. `OpenToPile` — исправление именно этого: HUD-кнопка своей стопки передаёт свой `pile`.
  - `ShowPile(pile)` — уничтожает всех текущих детей `cardContainer` и по одному инстанцирует `cardPreviewPrefab` на каждую `CardInstance` стопки, сразу вызывая `Show(instance)`. Работает с `IReadOnlyList<CardInstance>` — ей всё равно, какая из четырёх стопок передана. И `Open()`/`OpenToPile`, и вкладки внутри уже открытой панели (`characterDeckTab`/`drawPileTab`/...) в итоге вызывают её же — единая точка показа стопки.
  - Оверлей **не блокирует и не ставит на паузу бой**: не трогает `TargetSelectionManager.LockInput`, не останавливает `CombatManager` — секундомер и розыгрыш карт продолжают работать, даже пока панель открыта (закрыть её можно в любой момент, ничего не потеряв).
- **`DeckPileCountDisplay`** (`UI/DeckPileCountDisplay.cs`) — маленькая HUD-кнопка на конкретную стопку: `pile` (`PileKind`: `CharacterDeck`/`Draw`/`Discard`/`Exhaust`), `countLabel` (число карт, обновляется каждый `Update()` чтением соответствующего геттера `DeckManager`), `button` (по клику вызывает `screen.OpenToPile(pile)` — открывает именно СВОЮ стопку, а не всегда «Колоду персонажа») и общая ссылка на один `DeckPileScreen`. Четыре экземпляра на HUD (по одному на `PileKind`) делят один и тот же экран, каждый передаёт свой `pile` при открытии.

**Сверено с кодом:** константы руки `MaxHandSize = 7`, `InitialHandSize = 4`, `DrawIntervalSeconds = 2f` — `Managers/HandManager.cs:5-7`; рука создаёт массив слотов по `MaxHandSize` (`Managers/HandManager.cs:16`); `DeckManager.Shuffle` — `Managers/DeckManager.cs:37`.
