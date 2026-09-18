# Боевое ядро

Классы: `CombatManager`, `IScheduledEvent`/`OneShotEvent`/`PeriodicEffectEvent` (`Combat/TimedEffects.cs`), `TargetSelectionManager`, `Combatant` (структура и участники), `Player`, `PlayerRunState`.

Файлы: `Managers/CombatManager.cs`, `Combat/TimedEffects.cs`, `Combat/PlayerRunState.cs`, `Managers/TargetSelectionManager.cs`, `Combatants/Combatant.cs`, `Combatants/Player.cs`, `UI/CombatantStatusDisplay.cs`.

См. также: [effects.md](effects.md) — формулы урона/блока/лечения и стаки статус-эффектов на `Combatant`; [cards.md](cards.md) — что запускает `Play()`; [enemies.md](enemies.md) — как `Enemy` встраивается в тот же реестр расписания.

## Виртуальный секундомер: `CombatManager`

Весь бой существует во времени одного `float CurrentTime`, а не в реальном времени кадров. Время не течёт само по себе — оно **прыгает вперёд ровно тогда, когда игрок играет карту**: `Card.Play()` вызывает `combatManager.AdvanceTime(data.timeCostSeconds)`. Больше ничего в проекте не двигает `CurrentTime`. Всё остальное (таймер атаки врага, тики эффектов, UI-обновления) просто читает текущее значение или ждёт, пока секундомер до него доберётся.

Поля и обязанности:
- `CurrentTime { get; private set; }` — сам секундомер, наружу только для чтения.
- `Enemies` (`IReadOnlyList<Enemy>`) — публичный вид на приватный `List<Enemy> enemies`; заполняется один раз через `RegisterEnemies(List<Enemy>)`, которую вызывает `EncounterManager` после спавна. Заодно подписывает `HandleEnemyDeath` на `OnDeath` каждого врага.
- `player` — публичное поле `Player`, но с динамическим спавном игрока (см. [enemies.md](enemies.md) про `EncounterManager`) в инспекторе больше не выставляется — присваивается только через `SetPlayer(newPlayer)`, вызываемую `EncounterManager` сразу после `Instantiate` игрока на каждый новый бой.
- `scheduledEvents` (`List<IScheduledEvent>`) — плоский список, не куча/приоритетная очередь. `ResolveUpTo` каждый шаг цикла линейно сканирует весь список заново — O(n) на срабатывание, но при масштабе «несколько врагов + несколько активных эффектов за один бой» это осознанно достаточно просто (YAGNI), а не недосмотр.
- `RegisterScheduledEvent`/`UnregisterScheduledEvent` — используются: `Card.Play` (разовый `OneShotEvent` эффекта карты), `Combatant.AddEffectStacks` (периодический тик статус-эффекта), `HandManager.BeginNewHand` (периодический автодобор, регистрируется заново на каждый бой — см. [deck-hand.md](deck-hand.md)), `EncounterManager` (регистрирует сам `Enemy`, потому что `Enemy` — это `IScheduledEvent`).

### `AdvanceTime` и `ResolveUpTo`

- `AdvanceTime(amount)` — `CurrentTime += amount`, затем обновляет текстовый UI «Прошло времени». Вызывается только из `Card.Play` с `timeCostSeconds` сыгранной карты.
- `ResolveUpTo(targetTime)` — резолвер. Пока среди `scheduledEvents` есть события с `NextTime <= targetTime`, находит **одно** с наименьшим `NextTime` (при равенстве — наименьшим `Priority`), вызывает его `Trigger()`, и повторяет проверку заново с нуля. Поскольку `Trigger()` какого-то события может зарегистрировать новое событие (например, второй тик периодического эффекта), следующий проход цикла увидит и его — резолвер не работает со снимком списка, а перечитывает его каждую итерацию.
- **Правило ничьей**: при точном совпадении `NextTime` побеждает наименьший `Priority`. `Card.Play` регистрирует свой `OneShotEvent` всегда с `priority: 0`; `Enemy.Priority` и `PeriodicEffectEvent.Priority` — оба `1`. То есть эффект карты игрока, разрешающийся в тот же момент времени, что и атака врага или тик статус-эффекта, срабатывает первым — буквально «карта игрока побеждает».

### Конец боя, смена боёв и `PlayerRunState`

- `Start()` — с динамическим спавном игрока (`EncounterManager` появляется позже самого `CombatManager`) здесь больше нет подписки на `player.OnDeath`: она невозможна на старте сцены, игрока ещё не существует. Остаётся только `UpdateBudgetText()` и очистка `combatResultText`.
- `SetPlayer(newPlayer)` — единственное место, где присваивается поле `player` и подписывается `HandlePlayerDeath` на его `OnDeath`. Вызывается `EncounterManager` сразу после спавна игрока на **каждый** бой (не только на первый) — старая подписка на прошлого (уже уничтоженного) игрока при этом просто перестаёт существовать вместе с ним, пересоздавать её вручную не нужно.
- `ResetForNewCombat()` — сброс между боями: чистит `scheduledEvents` (расписание прошлого боя целиком — старые `OneShotEvent`/`PeriodicEffectEvent`/`Enemy` не должны тикать во время следующего), обнуляет `CurrentTime`, снимает флаг `combatOver`, чистит список `enemies`, обновляет текст времени, **очищает `combatResultText`** и **разблокирует ввод** через `TargetSelectionManager.Instance.UnlockInput()`. Очистка текста и разблокировка обязательны по одной и той же причине: `EndCombat` выставляет их (текст результата и блокировку) без собственного таймера или условия снятия, поэтому без симметричного сброса здесь оба состояния прошлого боя — «Победа»/«Поражение» на экране и замороженный клик — просто утекли бы в начавшийся новый бой.
- `HandlePlayerDeath()` — подписан на `player.OnDeath` (через `SetPlayer`), сразу вызывает `EndCombat("Поражение")`. Следующий бой при этом **не** запускается — `EncounterManager` слушает только `OnVictory`, не поражение.
- `HandleEnemyDeath()` — подписан на `OnDeath` каждого врага; завершает бой победой, только когда **все** враги имеют `CurrentHP <= 0` (`enemies.TrueForAll(...)`) — гибель одного врага при живых остальных бой не прерывает. При победе, помимо `EndCombat("Победа")`, дополнительно поднимает `OnVictory` — на него подписан `EncounterManager.HandleVictory`, который решает, что делать дальше (см. [enemies.md](enemies.md)).
- `event Action OnVictory` — публичное событие только для победы; поражение через него не сигнализируется (в этом и разница с `OnDeath`, который на `Combatant` общий для обеих сторон).
- `EndCombat(message)` — идемпотентен (флаг `combatOver`, второй вызов — no-op), выставляет текст результата и вызывает `TargetSelectionManager.Instance.LockInput()`, замораживая ввод.

**`PlayerRunState`** (`Combat/PlayerRunState.cs`) — не MonoBehaviour, просто `public static class` с одним полем `public static int? PersistedHP`. Хранит HP игрока между боями одного забега: `EncounterManager.HandleVictory` записывает в него `currentPlayer.CurrentHP` перед уничтожением старого игрока, `EncounterManager.SpawnPlayer` читает его и накатывает через `Combatant.SetCurrentHP` на свежезаспавненного игрока следующего боя, если значение задано (`HasValue`). `null` (значение по умолчанию) означает «это первый бой забега» — тогда новый игрок остаётся на своём стартовом `CurrentHP` из `Awake()` (полное здоровье). Поскольку это статическое поле, а не часть сцены, оно переживает `ResetForNewCombat()` и любые действия внутри одного забега — очищается только настоящим перезапуском процесса/домена; экрана хаба или явного сброса забега пока нет (YAGNI), заводить их сейчас незачем.

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
- `UnlockInput()` — снимает `inputLocked`. Единственный вызывающий — `CombatManager.ResetForNewCombat()` в начале следующего боя; без него `inputLocked`, однажды выставленный `LockInput()`, не снимался бы вообще ничем, и после первой же победы весь дальнейший цикл боёв оставался бы некликабельным.
- `SetHoveredTarget`/`ClearHoveredTarget` — вызываются из `Combatant.OnPointerEnter`/`OnPointerExit`, чисто для наведения, к блокировке ввода не относятся.

## `Combatant` — участник боя (структура)

Абстрактная база `Player`/`Enemy`. Здесь описана только структурная роль в бою и обработка кликов/смерти; формулы урона/блока/лечения и хранилище стаков — в [effects.md](effects.md).

- `CurrentHP`/`CurrentBlock` — публичные для чтения, `protected`/`private set`. `maxHP` — сериализуемое поле на 30 по умолчанию.
- Реализует `IPointerClickHandler`, `IPointerEnterHandler`, `IPointerExitHandler` — все три сразу пробрасываются в `TargetSelectionManager` (клик → `SelectTarget`, наведение/уход → `SetHoveredTarget`/`ClearHoveredTarget`).
- `event Action OnDeath` — единственный способ, которым внешний код (в первую очередь `CombatManager`) узнаёт о смерти участника; стреляет внутри `TakeDamage` ровно в момент, когда `CurrentHP` впервые достигает `0`. Перед этим вызывается `ClearAllScheduledEvents()` — снимает собственные тикающие статус-эффекты носителя (не его расписание как атакующего — то отдельно, см. выше про `EncounterManager`).
- `Player` — пустой класс без переопределений: игровая сторона игрока целиком собрана из `Combatant` + окружающих менеджеров (`HandManager`, `DeckManager`, `TargetSelectionManager`), ничего специфичного для игрока в самом классе нет.

**UI-потребитель:** `CombatantStatusDisplay` (`UI/CombatantStatusDisplay.cs`) — один компонент на любого `Combatant` (и `Player`, и `Enemy`), объединяющий все четыре прежних отдельных дисплея (`HPBarDisplay`, отдельностоящий Block-индикатор, `EffectStacksDisplay`, `EnemyAttackTimerDisplay`) — они удалены как файлы, их код перенесён сюда буквально. Подробности по каждому куску:
- `hpSlider`/`blockLabel`/`effectsLabel` — те же формулы, что раньше в `HPBarDisplay`/`EffectStacksDisplay`: `Start()` разово ставит `hpSlider.maxValue = target.maxHP`, `Update()` каждый кадр пишет `hpSlider.value = target.CurrentHP` и `blockLabel.text = target.CurrentBlock.ToString()`. `effectsLabel` — точный перенос `BuildEffectsText()` из бывшего `EffectStacksDisplay.Update()`, включая особый случай Пошатывания через `enemy.StaggerDisplay` (см. [effects.md](effects.md)).
- `enemyForTelegraph`/`telegraphLabel`/`combatManager` — заполняются **только** у `EnemyPrefab` (пусто у `PlayerPrefab`, игроку телеграф не нужен); `Update()` пишет в `telegraphLabel` только если `enemyForTelegraph != null`. Логика `BuildTelegraphText()` — точный перенос `EnemyAttackTimerDisplay.Update()` (тот же fallback на «(нет цели)», те же `ComputePreviewAmount` и `CardTextHelpers.HitCountSuffix`, см. [cards.md](cards.md)), см. [enemies.md](enemies.md).
- `combatManager` на этом компоненте нужен исключительно для чтения `CurrentTime` в телеграфе — это не тот `CombatManager`, что на `Combatant`/`Card`, и не часть префаба: `EncounterManager` проставляет его отдельно каждому заспавненному врагу через `GetComponentInChildren<CombatantStatusDisplay>()`, как раньше делал для `EnemyAttackTimerDisplay`.
- Block-индикатор — простой `Image` (квадрат-заглушка, замена на спрайт щита не сделана намеренно, YAGNI) с дочерним `TMP_Text` поверх; отдельным файлом/классом никогда не был, целиком часть `CombatantStatusDisplay` с рождения.
- Сам компонент ничего не пересчитывает — вся математика урона/блока/лечения/статус-эффектов уже применена на `Combatant`/`Enemy` до того, как этот компонент её прочитает (точные формулы — [effects.md](effects.md)); он существует только затем, чтобы игрок видел уже случившийся результат.
