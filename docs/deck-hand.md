# Колода и рука

Классы: `DeckManager`, `HandManager`.

Файлы: `Managers/DeckManager.cs`, `Managers/HandManager.cs`.

См. также: [cards.md](cards.md) — `CardInstance`/`CardPropertyFlags`, которыми оперируют обе колоды; [combat-core.md](combat-core.md) — `PeriodicEffectEvent`, на котором держится автодобор.

## `DeckManager` — четыре колоды

Четыре списка `CardInstance`: `characterDeck` (постоянный, инспекторный — полный набор карт персонажа) и три рабочих, которые пересобираются с нуля перед каждым боем — `drawPile`, `discardPile`, `exhaustPile`.

- `StartCombat()` — копирует `characterDeck` в `drawPile`, перемешивает (`Shuffle`), затем `MoveFrontOfDrawToEnd()`, очищает сброс и сжигание.
- `MoveFrontOfDrawToEnd()` — `DrawCard()` всегда берёт карту **с конца** списка `drawPile` (`drawPile[drawPile.Count - 1]`), поэтому этот метод находит все карты с флагом `FrontOfDraw` и переносит их в конец списка — несмотря на название «в конец», по факту это делает их первыми в очереди на добор.
- `Shuffle(list)` — стандартный тасование Фишера — Йетса на месте (идёт с конца, меняет местами с `Random.Range(0, i + 1)`).
- `DrawCard()` — забирает карту с конца `drawPile`; если он пуст, сначала перекладывает весь `discardPile` в `drawPile` и перемешивает (автоматический ресайкл сброса); возвращает `null`, только если **обе** колоды пусты — добирать больше нечего до конца боя.
- `Discard(instance)` — если `EffectiveProperties` содержит `Exhaust`, карта уходит в `exhaustPile`, иначе — в `discardPile`. Это единственное место, где Сжигаемое реально на что-то влияет; карта проходит здесь и когда она сыграна (`Card.Play` → `HandManager.OnCardPlayed` → `Discard`), и когда её принудительно сбрасывают из-за переполненной руки (см. ниже).
- Кнопки `[ContextMenu("Тест: ...")]` — ручная проверка в Play Mode по принципу «один шаг — одна проверка» из корневого `CLAUDE.md`, к реальному геймплею отношения не имеют.

## `HandManager` — рука на 7 явных слотов

- Константы: `MaxHandSize = 7`, `InitialHandSize = 4`, `DrawIntervalSeconds = 1.5f` — пока не настраиваются на персонажа (YAGNI).
- `slots` (`RectTransform[7]`) — заданы в инспекторе. Осознанный выбор вместо `Horizontal Layout Group` (см. корневой `CLAUDE.md`) — сдвиг карт при уходе одной из них делается вручную, а не автолэйаутом.
- `cardsInSlots` (`Card[7]`) — параллельный массив: занятое место `i` соответствует `slots[i]`; `null` — слот пуст.
- `Start()` — просит `DeckManager.StartCombat()`, добирает `InitialHandSize` карт сразу, затем регистрирует `PeriodicEffectEvent` с интервалом `DrawIntervalSeconds`, вечно вызывающий `TryDrawToHand()` — это и есть автодобор.
- `TryDrawToHand()` — если у колоды реально нечего дать, тихо возвращает `false`. Если карта добралась, но рука полна (`FindFirstEmptySlot()` вернул `-1`), карта **сразу** уходит обратно в `DeckManager.Discard`, ни разу не появившись в руке — переполненная рука не блокирует добор из колоды, она просто впустую тратит добранную карту.
- Создание карты: `Instantiate(cardPrefab, slots[emptySlot])`, затем вручную проставляются `instance`/`combatManager`/`nameLabel` — та же грабля с порядком `Awake()`, что задокументирована в корневом `CLAUDE.md`: к моменту этих присвоений `Card.Awake()` уже отработал, поэтому сам `Card.Awake()` защищается null-проверками.
- `OnCardPlayed(card)` — сбрасывает `card.instance` через `DeckManager.Discard`, затем `RemoveFromHand(card)`.
- `RemoveFromHand(card)` — уничтожает `GameObject`, обнуляет его слот, затем вручную сдвигает все последующие занятые слоты на одну позицию влево (перепривязывает `transform.SetParent` к более раннему `RectTransform`, сбрасывает `localPosition` в ноль) — тот самый «ручной перебор массива вместо автолэйаута» из корневого `CLAUDE.md`.
- `FindFirstEmptySlot()` — простой линейный поиск первого `null`; рука всегда уплотняется к слоту `0`.
