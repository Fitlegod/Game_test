# Документация проекта

Карта систем `Assets/Scripts` — построена по графу зависимостей (Graphify: 207 узлов, 370 рёбер, 9 сообществ, циклов импорта не найдено; сырые данные в `graphify-out/`). Игровой замысел — в `design-document.md` в корне; здесь — как устроен код.

Правило поддержания этих файлов при изменении кода — в разделе «Поддержание документации docs/» корневого `CLAUDE.md`.

## Файлы

| Файл | Что внутри | Ключевые классы |
|---|---|---|
| [combat-core.md](combat-core.md) | Виртуальный секундомер боя, реестр расписания, резолвер, правило ничьей, единая точка кликов, базовый участник боя | `CombatManager`, `IScheduledEvent`, `OneShotEvent`, `PeriodicEffectEvent`, `TargetSelectionManager`, `Combatant`, `Player`, `HPBarDisplay` |
| [cards.md](cards.md) | Три уровня данных карты (шаблон/копия/объект в руке), розыгрыш карты, действия карты, живой текст на карте | `Card`, `CardData`, `CardInstance`, `InstantActionEntry`, `AppliedEffectEntry`, `InstantActionKind`, `CardPropertyFlags`, `EffectTargetTag`, `CardType` |
| [effects.md](effects.md) | Все 7 статус-эффектов с точными формулами, порядком применения, округлением и распадом; хранилище стаков; особый случай Пошатывания | `StatusEffectType`, `StatusEffectRules`, формулы на `Combatant`, `EffectStacksDisplay` |
| [deck-hand.md](deck-hand.md) | Четыре колоды персонажа, перемешивание, приоритет «Впереди»; рука на 7 явных слотов, ручной сдвиг, автодобор | `DeckManager`, `HandManager` |
| [enemies.md](enemies.md) | Паттерны поведения врага, пять видов цели и цепочка fallback, динамический спавн, телеграф атаки | `Enemy`, `EnemyPatternData`, `EnemyActionStep`, `EncounterData`, `EncounterManager`, `EnemyActionTarget`, `EnemyAttackTimerDisplay` |

## Как это связано (коротко)

Игрок кликает карту → `TargetSelectionManager` либо сразу играет её, либо ждёт клика по цели → `Card.Play()` двигает виртуальное время `CombatManager` и регистрирует эффект карты как `OneShotEvent` → `CombatManager.ResolveUpTo` резолвит все события (карты, атаки врагов из `EnemyPatternData`, тики статус-эффектов) в порядке времени, при равенстве — в пользу карты игрока → эффекты карты/шага паттерна врага (`InstantActionEntry`/`AppliedEffectEntry`) считают урон/блок/лечение через формулы `Combatant`, учитывающие текущие статус-эффекты → карта уходит из `HandManager` в одну из колод `DeckManager` → бой заканчивается, когда `Combatant.OnDeath` игрока или всех врагов долетает до `CombatManager`.
