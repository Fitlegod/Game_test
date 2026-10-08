# Экраны, ввод, строки

Классы: `ScreenManager`, `ScreenRoot`, `DeckPileScreen`, `MapScreen` (оверлеи/экраны), см. также [map.md](map.md) — цикл «карта → бой → карта».

## ScreenManager (`Managers/ScreenManager.cs`)

- **Экран** — корневой объект с компонентом `ScreenRoot` (`screenId`). Одновременно активен ровно один. Сейчас два: `Map` (`MapOverlay`, выбор узла) и `Combat` (`CombatRoot` — весь бой: HUD, рука, враги, кнопки стопок и «Карта»).
- Регистрация: `ScreenManager.Awake` находит все `ScreenRoot` в сцене (включая выключенные) и регистрирует по `screenId`, все выключает. Новый экран = новый корень с `ScreenRoot`, менеджер править не нужно. Дубль id — ошибка в Console.
- `ShowScreen(id)` — закрывает все оверлеи, выключает остальные экраны, включает нужный. Первый `ShowScreen(Map)` делает `MapManager.Start`.
- **Оверлей** — любой объект, открытый поверх экрана, не выключая его: `OpenOverlay(root)`, `CloseOverlay(root)`, `CloseTopOverlay()`, `ToggleOverlay(root)`. Закрывается верхний (последний открытый). Сейчас: `DeckPileOverlay` (экран стопок, внутри `CombatRoot`) и `MapOverlay` (`MapScreen` в режиме `View`, тот же объект, что и экран `Map`).
- Порядок отрисовки оверлеев задаёт иерархия, а не порядок открытия: `MapOverlay` лежит после `CombatRoot` и всегда выше стопок.
- Секундомер боя оверлеи не трогают — `CombatManager` на `Managers`, а не на экране.

## Кто что переключает

`MapManager`: клик по боевому узлу → `ShowScreen(Combat)` + `StartEncounter`; конец боя → `ShowScreen(Map)` + `MapScreen.Show(Select | Finished)`; кнопка «Карта» → `MapScreen.Show(View)` + `OpenOverlay`. `DeckPileScreen.OpenToPile` → `OpenOverlay`, `Close` → `CloseOverlay`. `MapScreen.Close` → `CloseOverlay`.
