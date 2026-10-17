# Статус-эффекты

Классы: `StatusEffectType` (7 значений), `StatusEffectRules` (`Combat/TimedEffects.cs`), формулы на `Combatant` (`Combatants/Combatant.cs`).

Файлы: `Enums/StatusEffectType.cs`, `Combat/TimedEffects.cs`, `Combatants/Combatant.cs`, `Cards/CardActions.cs` (точки входа `Apply`).

См. также: [combat-core.md](combat-core.md) — реестр `IScheduledEvent`, в который встают периодические тики; [cards.md](cards.md) и [enemies.md](enemies.md) — кто вызывает `AppliedEffectEntry.Apply`.

## Хранилище: `Dictionary<StatusEffectType, float>` на `Combatant`

Стаки хранятся на каждом `Combatant` в приватном `effectStacks`. Тип значения — **`float`**. Пошатывание в этот словарь не попадает — см. ниже.

- `GetStacks(type)` — `0f`, если ключа нет.
- `AddEffectStacks(type, amount, applier = null)` — no-op, если `CurrentHP <= 0` (мёртвый не может получить новый стак); иначе прибавляет `amount` (может быть отрицательным — так карта в теории может *снять* часть стаков напрямую, не только периодическим распадом). При первом наложении типа, для которого `StatusEffectRules.TryGetPeriodicBehavior` возвращает `true`, регистрирует новый `PeriodicEffectEvent` и запоминает его в `activeScheduledEffects[type]`; первый тик считается от **момента срабатывания эффекта** (`CombatManager.EffectTime`: внутри события — момент события, вне — `CurrentTime`), а не от конца каста: враг, наложивший Слабость в момент 2 во время каста 0→3, получает снятие стака в 5, а не в 6 (и при повторе после отката тик остаётся в том же месте); повторные наложения того же типа **не** создают второе тикающее событие — стаки просто накапливаются в уже существующем интервале. `applier` — кто наложил (`AppliedEffectEntry.Apply` передаёт `source`; `null` — неизвестно) — уходит в `PeriodicEffectEvent.Applier` и определяет группу порядка тика: группа врагов (`Priority 1`), если носитель — `Enemy` и наложил не игрок; иначе группа игрока (`0`). Группу задаёт первый наложивший (тот, кто создал тик); последующие стаки от других источников её не меняют.

- Тик, отменённый откатом, лежит в `Combatant.undoneTicks[type]`; при повторе с тем же `Applier` и `EffectTime <= ReplayUntil` его возвращает `PeriodicEffectEvent.Restart(startTime)` (`NextTime = startTime + interval`) — тот же объект, прежний порядок.
- `RemoveStacks(type, amount)` — вычитает, но не даёт уйти ниже `0` (`Mathf.Max(0f, ...)`).
- `ClearAllScheduledEvents()` — при смерти отписывает из `CombatManager` все тики из `activeScheduledEffects` (нужен словарь с самой ссылкой на событие, а не просто `HashSet`-флаг, потому что отписать можно только тот же объект, что был зарегистрирован) и чистит словарь. Сами значения стаков в `effectStacks` при этом не обнуляются — но это не имеет значения, мёртвый `Combatant` больше не участвует в бою.

## Все 7 типов — точные формулы

| Тип | Тикает? | Действие тика | Где применяется постоянный эффект |
|---|---|---|---|
| **Strength** (Сила) | нет | — | `CalculateOutgoingDamage`: `+ Mathf.RoundToInt(stacks)` к базовому урону |
| **Weak** (Слабость) | каждые 3 с, `RemoveStacks(Weak, 1)` | распад −1/тик | `CalculateOutgoingDamage`: если `stacks > 0`, сумма после Силы `× 0.5` (без округления — оно одно, в конце конвейера) |
| **Regen** (Лечение) | каждую 1 с | `owner.Heal(Mathf.RoundToInt(stacks))` — **не** уменьшает собственные стаки | лечит на текущее число стаков каждую секунду, пока стаки не снимут иначе |
| **Toughness** (Крепость) | нет | — | `CalculateIncomingBlock`: `+ Mathf.RoundToInt(stacks)` к базовому блоку |
| **Frailty** (Хрупкость) | каждые 3 с, `RemoveStacks(Frailty, 1)` | распад −1/тик | `CalculateIncomingBlock`: если `stacks > 0`, итог после Крепости `× 0.5` через `Mathf.FloorToInt` |
| **Vulnerable** (Уязвимость) | каждые 3 с, `RemoveStacks(Vulnerable, 1)` | распад −1/тик | `ApplyIncomingDamageModifiers`: если `stacks > 0`, урон `× 1.5`, затем единственное `Mathf.FloorToInt` |
| **Stagger** (Пошатывание) | — особый случай, см. ниже | — | напрямую двигает `Enemy.nextActionTime`, не через этот словарь |

`StatusEffectRules.TryGetPeriodicBehavior` — единственное место, где решается, какие типы вообще тикают: `Weak`/`Frailty`/`Vulnerable` (интервал 3 с) и `Regen` (интервал 1 с) возвращают `true` со своим действием; `Strength`/`Toughness`/`Stagger` попадают в `default` и возвращают `false` — это постоянные (пока не снятые вручную) значения без автораспада.

### Пошатывание — не через общий `Dictionary`

Реализовано на `Enemy` (`Combatants/Enemy.cs`), не на базовом `Combatant`, и не тикает через `StatusEffectRules`:

- `ApplyStagger(int seconds)` — применяется **мгновенно и целиком**: `nextActionTime += seconds` (следующее действие врага откладывается сразу), плюс запоминает `staggerAppliedAt`/`staggerAmount` (оба `int`, начальное значение `0`) только для отображения; `staggerAppliedAt` берётся из `CombatManager.EffectTime` (момент срабатывания эффекта), иначе после отката `StaggerDisplay` мог бы показать больше наложенного.
- `StaggerDisplay` — `Mathf.Max(0, staggerAmount - (CurrentTime - staggerAppliedAt))` (`int`): чисто косметическое обратное отсчитывание для UI, на логику не влияет — сдвиг времени уже произошёл в момент применения.
- `AppliedEffectEntry.Apply` для `StatusEffectType.Stagger` отдельной веткой проверяет `if (target is Enemy enemy) enemy.ApplyStagger(Mathf.RoundToInt(stacks))` (стаки `float`, время целое); если цель — не `Enemy` (например, `Player`), эффект тихо не применяется (кроме лога).

## Полный конвейер урона (`InstantActionKind.Damage`)

Формула (дизайн-документ §7.2): `Урон = (База + Сила) × Слабость × Уязвимость цели`, округление вниз **один раз в конце**, не ниже 0. Число остаётся `float` до самого конца.

1. `source.CalculateOutgoingDamage(baseAmount)` → `float`: `baseAmount + round(Strength)`, `× 0.5`, если `Weak > 0`. Без округления и без зажима.
2. `target.ApplyIncomingDamageModifiers(float)` → `int`: `× 1.5`, если `Vulnerable > 0`, затем `Mathf.FloorToInt` и `Mathf.Max(0, …)` — единственное округление. Пример: база 7, Слабость у источника, Уязвимость у цели → 7 × 0.5 × 1.5 = 5.25 → 5 (раньше, с округлением на каждом шаге, выходило 4). Отрицательная Сила, сводящая сумму ниже нуля, даёт 0 урона, а не лечение.
3. `target.TakeDamage(damage)`: при `damage <= 0` ничего не делает; иначе сначала поглощение блоком — `absorbed = min(CurrentBlock, damage)`, остаток вычитается из `CurrentHP`, зажимается снизу нулём. Если HP дошло до `0` — `ClearAllScheduledEvents()` и `OnDeath`.

Предпросмотр (`ComputePreviewAmount`) идёт по той же схеме; без цели (нет наведения) возвращает `FloorToInt(исходящее)`, не ниже 0 — с целью совпадает с реальным уроном.

## Конвейер блока (`kind == Block`)

1. `recipient = target ?? source`.
2. `recipient.CalculateIncomingBlock(baseAmount)`: `baseAmount + round(Toughness)`, затем `× 0.5` (floor), если `Frailty > 0`; итог не ниже нуля — одно умножение и одно округление.
3. `recipient.GainBlock(finalBlock)`: `CurrentBlock += finalBlock` без верхнего предела. **No-op у мёртвого** (`CurrentHP <= 0`), в журнал ничего не пишется — как у `Heal` и `AddEffectStacks`.

### Затухание блока (дизайн-документ §7.4)

Раз в секунду у **каждого** бойца (игрок и враги) блок уменьшается на `CombatManager.BlockDecayAt(t) = blockDecayBase + blockDecayPerStep × (t / blockDecayStepSeconds)` (целочисленное деление; при `blockDecayStepSeconds <= 0` — без прибавки). `t` — момент события затухания (`CurrentEventTime`). Умолчания: 2 / 10 / 1, то есть 2 в секундах 1–9, 3 в 10–19, 4 в 20–29. Поля — в инспекторе `CombatManager` под заголовком «Затухание блока» (`blockDecayBase`, `blockDecayStepSeconds`, `blockDecayPerStep`); `0` / `0` отключает затухание.

- `Combatant.DecayBlock(amount)`: no-op у мёртвого, при `amount <= 0` или нулевом блоке; иначе снимает `min(блок, amount)`, в журнал пишется реальная разница. Ниже нуля блок не уходит.
- Тик — `PeriodicEffectEvent` с интервалом 1 и флагом `isEnvironment`; первое затухание — в момент 1. Игрока создаёт `CombatManager.SetPlayer` (вызывать после `ResetForNewCombat`), врагов — `RegisterEnemies`. В `activeScheduledEffects` тики затухания не попадают (это не статус-эффект) и не снимаются при смерти — мёртвый боец просто ничего не затухает.
- Порядок в моменте и поведение при откате — см. [combat-core.md](combat-core.md): группа 2 «окружение» срабатывает после всех остальных событий момента.

## Конвейер лечения (`kind == Heal`)

1. `recipient = target ?? source`.
2. `recipient.Heal(amount)` — **без каких-либо модификаторов** (Сила/Слабость и т. д. к лечению не относятся); no-op, если получатель уже мёртв (`CurrentHP <= 0`); зажимается сверху `maxHP`.

## Повтор через `hitCount`

Каждое из `hitCount` повторений заново проходит **весь** конвейер выше с нуля — Сила/Слабость/Уязвимость пересчитываются на каждый удар отдельно, а не применяются один раз к заранее просуммированному числу. Сейчас в рамках одного синхронного вызова `Apply` стаки между повторами не меняются, но раздельный пересчёт — сознательное архитектурное решение под будущие взаимодействия, завязанные на стеки (см. корневой `CLAUDE.md`).

## UI-потребитель: `CombatantStatusDisplay.BuildEffectsText()`

`UI/CombatantStatusDisplay.cs` (бывший отдельный `EffectStacksDisplay.cs`, слит в общий дисплей бойца — см. [combat-core.md](combat-core.md)) — каждый `Update()` строит строку из `target.GetStacks(type)` для Strength/Weak/Regen/Toughness/Frailty/Vulnerable (через `AppendIfPresent`, пропуская нулевые), и отдельно — `Enemy.StaggerDisplay`, если цель является `Enemy` и значение больше нуля. Сам ничего не считает, только форматирует то, что уже посчитано на `Combatant`/`Enemy`; код метода перенесён из старого файла буквально, без переписывания.

**Сверено с кодом:** `GetStacks` возвращает `0f`, если ключа нет (`Combatants/Combatant.cs:30`); `RemoveStacks` не опускает стаки ниже нуля через `Mathf.Max(0f, …)` (`Combatants/Combatant.cs:72`); `AddEffectStacks` у мёртвого — no-op (`Combatants/Combatant.cs:37`).
