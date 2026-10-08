# Экраны, ввод, строки

Классы: `ScreenManager`, `ScreenRoot`, `DeckPileScreen`, `MapScreen` (оверлеи/экраны), см. также [map.md](map.md) — цикл «карта → бой → карта».

## ScreenManager (`Managers/ScreenManager.cs`)

- **Экран** — корневой объект с компонентом `ScreenRoot` (`screenId`). Одновременно активен ровно один. Сейчас два: `Map` (`MapOverlay`, выбор узла) и `Combat` (`CombatRoot` — весь бой: HUD, рука, враги, кнопки стопок и «Карта»).
- Регистрация: `ScreenManager.Awake` находит все `ScreenRoot` в сцене (включая выключенные) и регистрирует по `screenId`, все выключает. Новый экран = новый корень с `ScreenRoot`, менеджер править не нужно. Дубль id — ошибка в Console.
- `ShowScreen(id)` — закрывает все оверлеи, выключает остальные экраны, включает нужный. Первый `ShowScreen(Map)` делает `MapManager.Start`.
- **Оверлей** — любой объект, открытый поверх экрана, не выключая его: `OpenOverlay(root)`, `CloseOverlay(root)`, `CloseTopOverlay()`, `ToggleOverlay(root)`. Закрывается верхний (последний открытый). Сейчас: `DeckPileOverlay` (экран стопок, лежит прямо под `Canvas`, на нём `OverlayRoot`) и `MapOverlay` (`MapScreen` в режиме `View`, тот же объект, что и экран `Map`).
- **Оверлеи на старте скрыты, видимостью управляет только `ScreenManager`.** Компонент `OverlayRoot` помечает оверлей: `ScreenManager.Awake` выключает все такие объекты, а каждый `ShowScreen` закрывает открытые и принудительно выключает помеченные. Открывать и закрывать оверлей можно только через `OpenOverlay/CloseOverlay/ToggleOverlay`; `SetActive` в обход менеджера запрещён. Состояние «открыт» не может разойтись с видимостью: `HasOverlay/TopOverlay/IsOverlayOpen` сначала выбрасывают из списка объекты с `activeSelf == false`. (Баг, ради которого это сделано: панель была включена в сцене по умолчанию, но менеджер о ней не знал — «Закрыть» и хоткеи смотрели в пустой реестр.)
- `OpenOverlay` делает `SetAsLastSibling()`, поэтому порядок отрисовки = порядок открытия. Для этого оверлеи лежат под одним родителем (`Canvas`), а не внутри `CombatRoot`.
- Секундомер боя оверлеи не трогают — `CombatManager` на `Managers`, а не на экране.

## Кто что переключает

`MapManager`: клик по боевому узлу → `ShowScreen(Combat)` + `StartEncounter`; конец боя → `ShowScreen(Map)` + `MapScreen.Show(Select | Finished)`; кнопка «Карта» → `MapScreen.Show(View)` + `OpenOverlay`. `DeckPileScreen.OpenToPile` → `OpenOverlay`, `Close` → `CloseOverlay`. `MapScreen.Close` → `CloseOverlay`.

## Ввод и горячие клавиши

- `Assets/Settings/GameInput.inputactions` — карта `Game`: `Slot1`–`Slot7` (клавиши 1–7), `Wait` (Пробел), `Cancel` (Esc), `ToggleMap` (M), `ToggleDeck` (D).
- `GameInput` (`Managers/GameInput.cs`) — единственный компонент, который читает действия (работает с копией ассета действий — `Instantiate`, иначе состояние ассета переживает выход из Play Mode). Боевые действия (`OnSlot`, `OnWait`) не доходят до подписчиков, пока открыт любой оверлей; `OnCancel`, `OnToggleMap`, `OnToggleDeck` работают всегда. Раздаёт C#-события `OnSlot(int 0..6)`, `OnWait`, `OnCancel`, `OnToggleMap`, `OnToggleDeck`. Остальной код клавиатуру не читает, а подписывается на события.
- `CombatHotkeys` (`Managers/CombatHotkeys.cs`) — подписчик с правилами контекста:
  - 1–7: только на экране `Combat` (открытые оверлеи отсекает `GameInput`); `HandManager.GetCardInSlot(i)` → `TargetSelectionManager.SelectCard` (как клик). Пустой слот — ничего.
  - Esc: если ждёт цель карта — `TargetSelectionManager.CancelSelection()` (карта остаётся в руке), иначе `ScreenManager.CloseTopOverlay()`.
  - M: только в бою — `MapManager.ToggleMapView()` (оверлей просмотра карты). На экране `Map` ничего.
  - D: только в бою — открыть/закрыть оверлей стопок (`DeckPileScreen.Open/Close`).
  - `Wait`: подписчиков пока нет (кнопку «Подождать» сделает задача A10).
- На каждом слоте руки — подпись `KeyLabel` с номером клавиши (под картой).

## Таблица строк (`Loc`)

- Весь текст, который видит игрок, берётся по ключу из `Assets/Resources/Strings/ru.txt` (UTF-8, `ключ = текст`, `#` — комментарий, `\n` в тексте — перенос). Группы ключей по префиксу: `ui.`, `combat.`, `card.`, `enemy.`, `effect.`, `map.`. `Debug.Log` не локализуется.
- `Loc` (`Localization/Loc.cs`): `Get(key)`, `Format(key, args...)`, `Has(key)`. Файл грузится один раз (сбрасывается при старте Play Mode). Нет ключа → `[ключ]` в тексте и одно предупреждение на ключ. Повтор ключа или неверная строка → `Debug.LogError` при загрузке (строка пропускается).
- Фразы хранятся целиком с подстановками (`Наносит {0} урона{1} всем врагам`), а не склейкой кусков. Ведущий пробел перед `{1}` (число ударов) добавляет код — у значений в файле пробелы по краям обрезаются.
- `LocalizedText` (`Localization/LocalizedText.cs`, поле `locKey`) — компонент на статичных `TMP_Text` сцены/префабов, ставит текст при включении. Подписи кнопок стопок («Колода 0» и т. д.) им не охвачены: `DeckPileCountDisplay` всё равно перезаписывает их числом.
- Контент: `CardData.nameKey` (`card.<id>.name`), `EnemyActionStep.stepNameKey` и `EnemyFallbackStep.stepNameKey` (`enemy.<паттерн>.<шаг>`). `OnValidate` у `CardData` и `EnemyPatternData` предупреждает об отсутствующем ключе. Новая карта или шаг врага = ассет + строка в `ru.txt`.
- Динамические ключи (по значению enum): `effect.genitive.<StatusEffectType>`, `effect.name.<StatusEffectType>`, `card.action.<Kind>.<Enemy|AllEnemies|Player>`, `card.effect.<positive|negative>.<...>`, `combat.action.<Kind>.<EnemyActionTarget>`, `combat.effect.<Give|Apply>.<EnemyActionTarget>`, `map.room.<RoomType>` (небоевые). Новое значение enum требует новых ключей — это проверяют тесты.
- Тесты (`Tests/EditMode/LocTests.cs`): нет повторов и неверных строк; все литеральные ключи в коде и `locKey` в сцене/префабах есть в файле; динамические ключи перечислены по значениям enum; ключи названий всех `CardData` и шагов всех `EnemyPatternData` есть в файле.
