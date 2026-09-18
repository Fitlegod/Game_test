# Статус-эффекты

Классы: `StatusEffectType` (7 значений), `StatusEffectRules` (`Combat/TimedEffects.cs`), формулы на `Combatant` (`Combatants/Combatant.cs`).

Файлы: `Enums/StatusEffectType.cs`, `Combat/TimedEffects.cs`, `Combatants/Combatant.cs`, `Cards/CardActions.cs` (точки входа `Apply`).

См. также: [combat-core.md](combat-core.md) — реестр `IScheduledEvent`, в который встают периодические тики; [cards.md](cards.md) и [enemies.md](enemies.md) — кто вызывает `AppliedEffectEntry.Apply`.

## Хранилище: `Dictionary<StatusEffectType, float>` на `Combatant`

Стаки хранятся на каждом `Combatant` в приватном `effectStacks`. Тип значения — **`float`, не `int`**: осознанное решение, потому что Пошатывание могло бы нести дробное значение секунд (хотя по факту Пошатывание вообще не проходит через этот словарь — см. ниже).

- `GetStacks(type)` — `0f`, если ключа нет.
- `AddEffectStacks(type, amount)` — no-op, если `CurrentHP <= 0` (мёртвый не может получить новый стак); иначе прибавляет `amount` (может быть отрицательным — так карта в теории может *снять* часть стаков напрямую, не только периодическим распадом). При первом наложении типа, для которого `StatusEffectRules.TryGetPeriodicBehavior` возвращает `true`, регистрирует новый `PeriodicEffectEvent` и запоминает его в `activeScheduledEffects[type]`; повторные наложения того же типа **не** создают второе тикающее событие — стаки просто накапливаются в уже существующем интервале.
- `RemoveStacks(type, amount)` — вычитает, но не даёт уйти ниже `0` (`Mathf.Max(0f, ...)`).
- `ClearAllScheduledEvents()` — при смерти отписывает из `CombatManager` все тики из `activeScheduledEffects` (нужен словарь с самой ссылкой на событие, а не просто `HashSet`-флаг, потому что отписать можно только тот же объект, что был зарегистрирован) и чистит словарь. Сами значения стаков в `effectStacks` при этом не обнуляются — но это не имеет значения, мёртвый `Combatant` больше не участвует в бою.

## Все 7 типов — точные формулы

| Тип | Тикает? | Действие тика | Где применяется постоянный эффект |
|---|---|---|---|
| **Strength** (Сила) | нет | — | `CalculateOutgoingDamage`: `+ Mathf.RoundToInt(stacks)` к базовому урону |
| **Weak** (Слабость) | каждые 3 с, `RemoveStacks(Weak, 1)` | распад −1/тик | `CalculateOutgoingDamage`: если `stacks > 0`, итог после Силы `× 0.5` через `Mathf.FloorToInt` |
| **Regen** (Лечение) | каждую 1 с | `owner.Heal(Mathf.RoundToInt(stacks))` — **не** уменьшает собственные стаки | лечит на текущее число стаков каждую секунду, пока стаки не снимут иначе |
| **Toughness** (Крепкость) | нет | — | `CalculateIncomingBlock`: `+ Mathf.RoundToInt(stacks)` к базовому блоку |
| **Frailty** (Хрупкость) | каждые 3 с, `RemoveStacks(Frailty, 1)` | распад −1/тик | `CalculateIncomingBlock`: если `stacks > 0`, итог после Крепкости `× 0.5` через `Mathf.FloorToInt` |
| **Vulnerable** (Уязвимость) | каждые 3 с, `RemoveStacks(Vulnerable, 1)` | распад −1/тик | `ApplyIncomingDamageModifiers`: если `stacks > 0`, входящий урон `× 1.5` через `Mathf.FloorToInt` |
| **Stagger** (Пошатывание) | — особый случай, см. ниже | — | напрямую двигает `Enemy.scheduledHitTime`-аналог (`nextActionTime`), не через этот словарь |

`StatusEffectRules.TryGetPeriodicBehavior` — единственное место, где решается, какие типы вообще тикают: `Weak`/`Frailty`/`Vulnerable` (интервал 3 с) и `Regen` (интервал 1 с) возвращают `true` со своим действием; `Strength`/`Toughness`/`Stagger` попадают в `default` и возвращают `false` — это постоянные (пока не снятые вручную) значения без автораспада.

### Пошатывание — не через общий `Dictionary`

Реализовано на `Enemy` (`Combatants/Enemy.cs`), не на базовом `Combatant`, и не тикает через `StatusEffectRules`:

- `ApplyStagger(seconds)` — применяется **мгновенно и целиком**: `nextActionTime += seconds` (следующее действие врага откладывается сразу), плюс запоминает `staggerAppliedAt`/`staggerAmount` только для отображения.
- `StaggerDisplay` — `Mathf.Max(0f, staggerAmount - (CurrentTime - staggerAppliedAt))`: чисто косметическое обратное отсчитывание для UI, на логику не влияет — сдвиг времени уже произошёл в момент применения.
- `AppliedEffectEntry.Apply` для `StatusEffectType.Stagger` отдельной веткой проверяет `if (target is Enemy enemy) enemy.ApplyStagger(stacks)`; если цель — не `Enemy` (например, `Player`), эффект тихо не применяется (кроме лога).

## Полный конвейер урона (`InstantActionEntry.Apply`, `kind == Damage`)

1. `source.CalculateOutgoingDamage(baseAmount)`: `baseAmount + round(Strength)`, затем `× 0.5` (floor), если `Weak > 0`.
2. `target.ApplyIncomingDamageModifiers(damage)`: `× 1.5` (floor), если `Vulnerable > 0`.
3. `target.TakeDamage(damage)`: сначала поглощение блоком — `absorbed = min(CurrentBlock, damage)`, `CurrentBlock -= absorbed`, `damage -= absorbed`; остаток вычитается из `CurrentHP`, зажимается снизу нулём. Если HP дошло ровно до `0` — вызывается `ClearAllScheduledEvents()` и стреляет `OnDeath`.

## Конвейер блока (`kind == Block`)

1. `recipient = target ?? source`.
2. `recipient.CalculateIncomingBlock(baseAmount)`: `baseAmount + round(Toughness)`, затем `× 0.5` (floor), если `Frailty > 0`; итог зажимается снизу нулём (`Mathf.Max(0, result)`).
3. `recipient.GainBlock(finalBlock)`: `CurrentBlock += finalBlock` — накапливается без верхнего предела.

## Конвейер лечения (`kind == Heal`)

1. `recipient = target ?? source`.
2. `recipient.Heal(amount)` — **без каких-либо модификаторов** (Сила/Слабость и т. д. к лечению не относятся); no-op, если получатель уже мёртв (`CurrentHP <= 0`); зажимается сверху `maxHP`.

## Повтор через `hitCount`

Каждое из `hitCount` повторений заново проходит **весь** конвейер выше с нуля — Сила/Слабость/Уязвимость пересчитываются на каждый удар отдельно, а не применяются один раз к заранее просуммированному числу. Сейчас в рамках одного синхронного вызова `Apply` стаки между повторами не меняются, но раздельный пересчёт — сознательное архитектурное решение под будущие взаимодействия, завязанные на стеки (см. корневой `CLAUDE.md`).

## UI-потребитель: `EffectStacksDisplay`

`UI/EffectStacksDisplay.cs` — каждый `Update()` строит строку из `target.GetStacks(type)` для Strength/Weak/Regen/Toughness/Frailty/Vulnerable (через `AppendIfPresent`, пропуская нулевые), и отдельно — `Enemy.StaggerDisplay`, если цель является `Enemy` и значение больше нуля. Сам ничего не считает, только форматирует то, что уже посчитано на `Combatant`/`Enemy`.
