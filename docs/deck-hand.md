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

- Константы: `MaxHandSize = 7`, `InitialHandSize = 4`, `DrawIntervalSeconds = 2` (`int`) — пока не настраиваются на персонажа (YAGNI).
- `slots` (`RectTransform[7]`) — заданы в инспекторе. Осознанный выбор вместо `Horizontal Layout Group` (см. корневой `CLAUDE.md`) — сдвиг карт при уходе одной из них делается вручную, а не автолэйаутом.
- `cardsInSlots` (`Card[7]`) — параллельный массив: занятое место `i` соответствует `slots[i]`; `null` — слот пуст.
- `Start()` — пустой. С циклом боёв (`EncounterManager`, см. [enemies.md](enemies.md)) руку нужно собирать заново перед **каждым** боем, не только один раз при загрузке сцены, поэтому вся прежняя логика `Start()` переехала в публичный `BeginNewHand()`.
- `BeginNewHand()` — вызывается `EncounterManager` после спавна игрока и врагов на каждый бой: сначала `ClearHand()` (уничтожает всё, что осталось от предыдущего боя — рука не переносится между боями по умолчанию), затем `DeckManager.StartCombat()`, `InitialHandSize` карт добора, и регистрация нового `PeriodicEffectEvent` с интервалом `DrawIntervalSeconds` на `TryDrawToHand()` — это и есть автодобор. Таймер создаётся с `Owner == null`, то есть относится к «стороне игрока»: откат его никогда не удаляет из списка и не откатывает его срабатывания (добор — не отменяется, рука при откате не меняется), а только сдвигает `NextTime −= S`, сохраняя фазу: `(NextTime − T)` до отката равно `(NextTime − R)` после. Если после сдвига `NextTime <= CurrentTime`, добор срабатывает в том же разборе — ровно один раз, без пропуска и дубля. Подробности — [combat-core.md](combat-core.md), раздел «Откат». Регистрируется заново на каждый вызов: старый `PeriodicEffectEvent` прошлого боя не отписывается явно, но и не мешает — он жил в `scheduledEvents`, которые `CombatManager.ResetForNewCombat()` целиком очищает до вызова `BeginNewHand()` (см. [combat-core.md](combat-core.md)).
- `ClearHand()` (публичный) — вызывается и в `BeginNewHand`, и `EncounterManager.CleanupCombatants` в конце боя (победа или поражение), вместе с `TargetSelectionManager.CancelSelection()`: карты руки не живут до следующего боя, незавершённый выбор цели сбрасывается. Проходит все `MaxHandSize` слотов, для каждого занятого уничтожает `GameObject` и обнуляет ячейку `cardsInSlots[i]`. Не различает, была ли карта уже сыграна или просто добрана — на старте нового боя обе категории одинаково не нужны.
- `TryDrawToHand()` — если у колоды реально нечего дать, тихо возвращает `false`. Если карта добралась, но рука полна (`FindFirstEmptySlot()` вернул `-1`), карта **сразу** уходит обратно в `DeckManager.Discard`, ни разу не появившись в руке — переполненная рука не блокирует добор из колоды, она просто впустую тратит добранную карту.
- Создание карты: `Instantiate(cardPrefab, slots[emptySlot])`, затем вручную проставляются `instance`/`combatManager`/`nameLabel` — та же грабля с порядком `Awake()`, что задокументирована в корневом `CLAUDE.md`: к моменту этих присвоений `Card.Awake()` уже отработал, поэтому сам `Card.Awake()` защищается null-проверками.
- `RemoveFromHand(card)` — уничтожает `GameObject`, обнуляет его слот, затем вручную сдвигает все последующие занятые слоты на одну позицию влево (перепривязывает `transform.SetParent` к более раннему `RectTransform`, сбрасывает `localPosition` в ноль) — тот самый «ручной перебор массива вместо автолэйаута» из корневого `CLAUDE.md`.
- `FindFirstEmptySlot()` — простой линейный поиск первого `null`; рука всегда уплотняется к слоту `0`.

## Просмотр четырёх стопок — `DeckPileScreen` + `DeckPileCountDisplay`

Один общий оверлей с вкладками на все четыре стопки — не четыре отдельных экрана.

- **`DeckPileScreen`** (`UI/DeckPileScreen.cs`) — `cardContainer` (Content внутри Scroll View с `GridLayoutGroup`, размер ячейки как у карты в руке), `cardPreviewPrefab` (`CardPreviewPrefab`, см. [cards.md](cards.md)), ссылка на `DeckManager` и пять кнопок (четыре вкладки + «Закрыть»). Корневой объект — `OverlayRoot` (`DeckPileOverlay`): видимостью управляет `ScreenManager`, собственного `panelRoot` нет ([ui.md](ui.md)).
  - `Awake()` навешивает слушатели: на каждую вкладку — `ShowPile` с соответствующим геттером `DeckManager` (`CharacterDeck`/`DrawPile`/`DiscardPile`/`ExhaustPile`), на «Закрыть» — `Close`.
  - `Open()` — тонкий проброс к `OpenToPile(PileKind.CharacterDeck)`: показывает «Колоду персонажа» (вкладка по умолчанию, например при открытии клавишей D).
  - `OpenToPile(pile)` — показывает **именно ту** стопку, что передана в `pile` (`switch` по `PileKind` на четыре геттера `DeckManager`), и вызывает `ScreenManager.Instance.OpenOverlay(gameObject)`. HUD-кнопка своей стопки передаёт свой `pile`. `Close()` — `ScreenManager.Instance.CloseOverlay(gameObject)`.
  - `ShowPile(pile)` — уничтожает всех текущих детей `cardContainer` и по одному инстанцирует `cardPreviewPrefab` на каждую `CardInstance` стопки, сразу вызывая `Show(instance)`. Работает с `IReadOnlyList<CardInstance>` — ей всё равно, какая из четырёх стопок передана. И `Open()`/`OpenToPile`, и вкладки внутри уже открытой панели (`characterDeckTab`/`drawPileTab`/...) в итоге вызывают её же — единая точка показа стопки.
  - Оверлей не трогает `TargetSelectionManager.LockInput` и `CombatManager`. Время само не идёт (его двигают только карты), но пока открыт любой оверлей, `GameInput` не передаёт клавиши слотов и «Подождать» ([ui.md](ui.md)).
- **`DeckPileCountDisplay`** (`UI/DeckPileCountDisplay.cs`) — маленькая HUD-кнопка на конкретную стопку: `pile` (`PileKind`: `CharacterDeck`/`Draw`/`Discard`/`Exhaust`), `countLabel` (число карт, проверяется каждый `Update()` чтением соответствующего геттера `DeckManager`, текст переписывается только при изменении числа), `button` (по клику вызывает `screen.OpenToPile(pile)` — открывает именно СВОЮ стопку, а не всегда «Колоду персонажа») и общая ссылка на один `DeckPileScreen`. Четыре экземпляра на HUD (по одному на `PileKind`) делят один и тот же экран, каждый передаёт свой `pile` при открытии.

**Сверено с кодом:** константы руки `MaxHandSize = 7`, `InitialHandSize = 4`, `DrawIntervalSeconds = 2` — `Managers/HandManager.cs`; рука создаёт массив слотов по `MaxHandSize` (`Managers/HandManager.cs:16`); `DeckManager.Shuffle` — `Managers/DeckManager.cs:37`.
