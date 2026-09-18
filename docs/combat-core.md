# Боевое ядро

Классы: `CombatManager`, `IScheduledEvent`/`OneShotEvent`/`PeriodicEffectEvent` (`Combat/TimedEffects.cs`), `TargetSelectionManager`, `Combatant` (структура и участники), `Player`.

Файлы: `Managers/CombatManager.cs`, `Combat/TimedEffects.cs`, `Managers/TargetSelectionManager.cs`, `Combatants/Combatant.cs`, `Combatants/Player.cs`, `UI/HPBarDisplay.cs`.

См. также: [effects.md](effects.md) — формулы урона/блока/лечения и стаки статус-эффектов на `Combatant`; [cards.md](cards.md) — что запускает `Play()`; [enemies.md](enemies.md) — как `Enemy` встраивается в тот же реестр расписания.

## Виртуальный секундомер: `CombatManager`

Весь бой существует во времени одного `float CurrentTime`, а не в реальном времени кадров. Время не течёт само по себе — оно **прыгает вперёд ровно тогда, когда игрок играет карту**: `Card.Play()` вызывает `combatManager.AdvanceTime(data.timeCostSeconds)`. Больше ничего в проекте не двигает `CurrentTime`. Всё остальное (таймер атаки врага, тики эффектов, UI-обновления) просто читает текущее значение или ждёт, пока секундомер до него доберётся.

Поля и обязанности:
- `CurrentTime { get; private set; }` — сам секундомер, наружу только для чтения.
- `Enemies` (`IReadOnlyList<Enemy>`) — публичный вид на приватный `List<Enemy> enemies`; заполняется один раз через `RegisterEnemies(List<Enemy>)`, которую вызывает `EncounterManager` после спавна. Заодно подписывает `HandleEnemyDeath` на `OnDeath` каждого врага.
- `player` — прямая ссылка на `Player`, выставляется в инспекторе (в коде нигде не присваивается — в отличие от `Enemy.player`, который проставляет `EncounterManager`).
- `scheduledEvents` (`List<IScheduledEvent>`) — плоский список, не куча/приоритетная очередь. `ResolveUpTo` каждый шаг цикла линейно сканирует весь список заново — O(n) на срабатывание, но при масштабе «несколько врагов + несколько активных эффектов за один бой» это осознанно достаточно просто (YAGNI), а не недосмотр.
- `RegisterScheduledEvent`/`UnregisterScheduledEvent` — используются: `Card.Play` (разовый `OneShotEvent` эффекта карты), `Combatant.AddEffectStacks` (периодический тик статус-эффекта), `HandManager.Start` (периодический автодобор), `EncounterManager.Start` (регистрирует сам `Enemy`, потому что `Enemy` — это `IScheduledEvent`).

### `AdvanceTime` и `ResolveUpTo`

- `AdvanceTime(amount)` — `CurrentTime += amount`, затем обновляет текстовый UI «Прошло времени». Вызывается только из `Card.Play` с `timeCostSeconds` сыгранной карты.
- `ResolveUpTo(targetTime)` — резолвер. Пока среди `scheduledEvents` есть события с `NextTime <= targetTime`, находит **одно** с наименьшим `NextTime` (при равенстве — наименьшим `Priority`), вызывает его `Trigger()`, и повторяет проверку заново с нуля. Поскольку `Trigger()` какого-то события может зарегистрировать новое событие (например, второй тик периодического эффекта), следующий проход цикла увидит и его — резолвер не работает со снимком списка, а перечитывает его каждую итерацию.
- **Правило ничьей**: при точном совпадении `NextTime` побеждает наименьший `Priority`. `Card.Play` регистрирует свой `OneShotEvent` всегда с `priority: 0`; `Enemy.Priority` и `PeriodicEffectEvent.Priority` — оба `1`. То есть эффект карты игрока, разрешающийся в тот же момент времени, что и атака врага или тик статус-эффекта, срабатывает первым — буквально «карта игрока побеждает».

### Конец боя

- `HandlePlayerDeath()` — подписан на `player.OnDeath`, сразу вызывает `EndCombat("Поражение")`.
- `HandleEnemyDeath()` — подписан на `OnDeath` каждого врага; завершает бой победой, только когда **все** враги имеют `CurrentHP <= 0` (`enemies.TrueForAll(...)`) — гибель одного врага при живых остальных бой не прерывает.
- `EndCombat(message)` — идемпотентен (флаг `combatOver`, второй вызов — no-op), выставляет текст результата и вызывает `TargetSelectionManager.Instance.LockInput()`, замораживая ввод.

## Расписание: `IScheduledEvent` / `OneShotEvent` / `PeriodicEffectEvent`

`IScheduledEvent` — интерфейс с тремя членами: `NextTime` (когда сработать), `Priority` (тай-брейк, меньше — раньше), `Trigger()` (само действие). Всё, что происходит в конкретный момент виртуального времени, живёт в одном реестре `CombatManager.scheduledEvents` через этот интерфейс — атака врага, разовый эффект карты, тик статус-эффекта не различаются резолвером, только своей реализацией `Trigger()`.

- **`OneShotEvent`** — одноразовое событие с явно заданным `Priority` в конструкторе (карты всегда передают `0`). После первого `Trigger()` выставляет `NextTime = float.PositiveInfinity` — оно больше никогда не пройдёт фильтр `NextTime <= targetTime`, но из списка явно не удаляется (безобидный «мёртвый» элемент до конца боя; сцена боя короткая и одноразовая, так что накопление не проблема).
- **`PeriodicEffectEvent`** — интервальное повторение: каждый `Trigger()` вызывает `onTrigger()`, затем сдвигает `NextTime += interval`. Продолжает тикать бесконечно, пока его явно не отпишут через `UnregisterScheduledEvent` — это делает `Combatant.ClearAllScheduledEvents()` при смерти носителя, по словарю `activeScheduledEffects` (см. [effects.md](effects.md)), или `HandManager` неявно никогда не отписывает свой автодобор (он живёт до конца сцены боя).
- **`Enemy` сам реализует `IScheduledEvent` напрямую** (не через `PeriodicEffectEvent`) — его `Trigger()` разбирается в [enemies.md](enemies.md). Регистрирует/отписывает его `EncounterManager` (кто заспавнил — тот и владеет подпиской), а не сам `Combatant`/`Enemy` — симметрично тому, как `ClearAllScheduledEvents` работает только с собственными тикающими эффектами носителя.

## Единая точка кликов: `TargetSelectionManager`

Синглтон (`Instance`, выставляется в `Awake`). И `Card`, и `Combatant` реализуют `IPointerClickHandler`, но оба **не** обрабатывают клик сами — оба сразу зовут сюда (`SelectCard`/`SelectTarget`).

- `PendingCard` — какая карта сейчас ждёт клика по цели (или `null`).
- `HoveredTarget` — над каким `Combatant` сейчас курсор; используется только для живого предпросмотра урона/блока в тексте карты (см. [cards.md](cards.md)), к выбору цели отношения не имеет.
- `SelectCard(card)` — если `card.RequiresTarget` (есть хоть одно действие/эффект с `EffectTargetTag.Enemy`), карта становится `PendingCard` и ждёт клика по цели; иначе разыгрывается немедленно с `target: null`.
- `SelectTarget(target)` — срабатывает только если есть `PendingCard` и `PendingCard.IsValidTarget(target)` (сейчас — `target is Enemy`); разыгрывает карту по цели и сбрасывает `PendingCard`.
- `LockInput()` — выставляет `inputLocked`, проверяется в начале и `SelectCard`, и `SelectTarget`. Единственный способ, которым `CombatManager.EndCombat` замораживает ввод после конца боя.
- `SetHoveredTarget`/`ClearHoveredTarget` — вызываются из `Combatant.OnPointerEnter`/`OnPointerExit`, чисто для наведения, к блокировке ввода не относятся.

## `Combatant` — участник боя (структура)

Абстрактная база `Player`/`Enemy`. Здесь описана только структурная роль в бою и обработка кликов/смерти; формулы урона/блока/лечения и хранилище стаков — в [effects.md](effects.md).

- `CurrentHP`/`CurrentBlock` — публичные для чтения, `protected`/`private set`. `maxHP` — сериализуемое поле на 30 по умолчанию.
- Реализует `IPointerClickHandler`, `IPointerEnterHandler`, `IPointerExitHandler` — все три сразу пробрасываются в `TargetSelectionManager` (клик → `SelectTarget`, наведение/уход → `SetHoveredTarget`/`ClearHoveredTarget`).
- `event Action OnDeath` — единственный способ, которым внешний код (в первую очередь `CombatManager`) узнаёт о смерти участника; стреляет внутри `TakeDamage` ровно в момент, когда `CurrentHP` впервые достигает `0`. Перед этим вызывается `ClearAllScheduledEvents()` — снимает собственные тикающие статус-эффекты носителя (не его расписание как атакующего — то отдельно, см. выше про `EncounterManager`).
- `Player` — пустой класс без переопределений: игровая сторона игрока целиком собрана из `Combatant` + окружающих менеджеров (`HandManager`, `DeckManager`, `TargetSelectionManager`), ничего специфичного для игрока в самом классе нет.

**UI-потребитель:** `HPBarDisplay` (`UI/HPBarDisplay.cs`) — `[RequireComponent(typeof(Slider))]` вешается на любой `Combatant`, а не отдельно на `Player` и отдельно на `Enemy`: поле `target` типизировано базовым классом, поэтому один и тот же компонент без дублирования кода обслуживает и полосу здоровья игрока, и полосу здоровья каждого врага. В `Start()` читает `slider.maxValue = target.maxHP` один раз (`maxHP` не меняется в течение боя), в `Update()` каждый кадр пишет `slider.value = target.CurrentHP`. Сам не пересчитывает ничего — вся математика урона/блока/лечения уже применена к `CurrentHP` на `Combatant` до того, как этот компонент вообще его прочитает (точные формулы — [effects.md](effects.md)); он существует только затем, чтобы игрок видел уже случившийся результат боевого события, а не участвует в его вычислении. Показывает только HP: `CurrentBlock` этим компонентом не отображается вовсе — отдельного UI-элемента для блока в текущей реализации нет.
