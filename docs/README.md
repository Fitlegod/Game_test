# Документация проекта

Карта систем `Assets/Scripts` — построена по графу зависимостей (Graphify: 207 узлов, 370 рёбер, 9 сообществ, циклов импорта не найдено; сырые данные в `graphify-out/`). Игровой замысел — в `design-document.md` в корне; здесь — как устроен код.

Правило поддержания этих файлов при изменении кода — в разделе «Поддержание документации docs/» корневого `CLAUDE.md`.

## Файлы

| Файл | Что внутри | Ключевые классы |
|---|---|---|
| [combat-core.md](combat-core.md) | Виртуальный секундомер боя, реестр расписания, резолвер, правило ничьей, единая точка кликов, базовый участник боя, смена боёв и перенос HP между ними | `CombatManager`, `IScheduledEvent`, `OneShotEvent`, `PeriodicEffectEvent`, `TargetSelectionManager`, `Combatant`, `Player`, `PlayerRunState`, `CombatantStatusDisplay` |
| [cards.md](cards.md) | Три уровня данных карты (шаблон/копия/объект в руке), розыгрыш карты, действия карты, форматирование текста рядом с `Apply()`, статичная карточка-превью | `Card`, `CardData`, `CardInstance`, `InstantActionEntry`, `AppliedEffectEntry`, `InstantActionKind`, `CardPropertyFlags`, `CardTextHelpers`, `CardPreviewDisplay`, `EffectTargetTag`, `CardType` |
| [effects.md](effects.md) | Все 7 статус-эффектов с точными формулами, порядком применения, округлением и распадом; хранилище стаков; особый случай Пошатывания | `StatusEffectType`, `StatusEffectRules`, формулы на `Combatant`, `CombatantStatusDisplay` |
| [deck-hand.md](deck-hand.md) | Четыре колоды персонажа, перемешивание, приоритет «Впереди»; рука на 7 явных слотов, ручной сдвиг, автодобор, пересборка руки на каждый новый бой; общий оверлей просмотра всех четырёх стопок | `DeckManager`, `HandManager`, `DeckPileScreen`, `DeckPileCountDisplay` |
| [enemies.md](enemies.md) | Паттерны поведения врага, пять видов цели и запасной шаг (`EnemyFallbackStep`), динамический спавн игрока и врагов на один бой (`StartEncounter`), исход боя (победа/поражение) наружу, телеграф атаки | `Enemy`, `EnemyPatternData`, `EnemyActionStep`, `EncounterData`, `EncounterManager`, `EnemyActionTarget`, `CombatantStatusDisplay` |
| [map.md](map.md) | Процедурная карта локации (20 строк × 7 столбцов): данные, шаги генерации по сиду и причины решений, навигация `MapRunState`, `MapManager`, экран карты, цикл «карта → бой → карта», источник иконки, тесты | `RoomType`, `MapGenerationConfig`, `MapData`, `MapGenerator`, `MapRunState`, `MapManager`, `MapScreen` |

## Как это связано (коротко)

Игрок кликает карту → `TargetSelectionManager` либо сразу играет её, либо ждёт клика по цели → `Card.Play()` двигает виртуальное время `CombatManager` и регистрирует эффект карты как `OneShotEvent` → `CombatManager.ResolveUpTo` резолвит все события (карты, атаки врагов из `EnemyPatternData`, тики статус-эффектов) в порядке времени, при равенстве — в пользу карты игрока → эффекты карты/шага паттерна врага (`InstantActionEntry`/`AppliedEffectEntry`) считают урон/блок/лечение через формулы `Combatant`, учитывающие текущие статус-эффекты → карта уходит из `HandManager` в одну из колод `DeckManager` → бой заканчивается, когда `Combatant.OnDeath` игрока или всех врагов долетает до `CombatManager`.

Дальше — цикл: бой не стартует сам. `MapManager` генерирует карту по сиду и показывает `MapScreen`; клик по боевому узлу вызывает `EncounterManager.StartEncounter(node.encounter)` — `CombatManager.ResetForNewCombat()` сбрасывает секундомер/расписание/блокировку ввода, `EncounterManager` спавнит игрока (накатывая `PlayerRunState.PersistedHP`, если он есть) и врагов, `HandManager.BeginNewHand()` собирает руку. Победа (`CombatManager.OnVictory`) → `EncounterManager.HandleVictory` сохраняет HP в `PersistedHP`; поражение (`OnDefeat`) → `HandleDefeat` сбрасывает его в `null` (следующий бой с полным HP). В обоих случаях участники удаляются, и `MapManager.OnCombatEnded` снова открывает карту. Подробности — [map.md](map.md).

**Сверено с кодом:** `MapManager.OnCombatEnded` — `Managers/MapManager.cs:55`; `CombatManager.OnVictory`/`OnDefeat` — `Managers/CombatManager.cs:18-19`; `EncounterManager.HandleVictory` — `Managers/EncounterManager.cs:23`.
