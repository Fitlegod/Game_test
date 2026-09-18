# Враги и энкаунтеры

Классы: `Enemy`, `EnemyPatternData`/`EnemyActionStep`, `EncounterData`/`EncounterManager`, `EnemyActionTarget`.

Файлы: `Combatants/Enemy.cs`, `Combatants/EnemyActionStep.cs`, `Combatants/EnemyPatternData.cs`, `Encounters/EncounterData.cs`, `Managers/EncounterManager.cs`, `Enums/EnemyActionTarget.cs`, `UI/CombatantStatusDisplay.cs` (телеграф — часть общего дисплея, не отдельный класс).

См. также: [combat-core.md](combat-core.md) — `IScheduledEvent`, в реестр которого встаёт сам `Enemy`, а также `CombatManager.OnVictory`/`SetPlayer`/`ResetForNewCombat` и `PlayerRunState`, на которых держится смена боёв, описанная в этом файле; [cards.md](cards.md)/[effects.md](effects.md) — те же `InstantActionEntry`/`AppliedEffectEntry`, что использует `EnemyActionStep`; [deck-hand.md](deck-hand.md) — `HandManager.BeginNewHand`, которую `EncounterManager` вызывает на каждый новый бой.

## `Enemy` — `Combatant` + `IScheduledEvent`

`Enemy` реализует `IScheduledEvent` напрямую (не через `PeriodicEffectEvent`): `NextTime => nextActionTime`, `Priority => 1` (всегда проигрывает тай-брейк времени `OneShotEvent`-у карты игрока с приоритетом `0` — [combat-core.md](combat-core.md)). Регистрирует и отписывает его `EncounterManager`, а не сам `Enemy`/`Combatant` (кто заспавнил — тот и владеет подпиской).

- `pattern` (`EnemyPatternData`) — назначается на префабе конкретного врага; зацикленный список `EnemyActionStep`.
- `currentStepIndex` — какой шаг сработает следующим; продвигается по модулю `pattern.steps.Count` — паттерн крутится бесконечно.
- **`Trigger()`** (вызывается `CombatManager.ResolveUpTo`, когда наступает `NextTime`): если исполнитель уже мёртв (`CurrentHP <= 0`) — не делает вообще ничего, даже не продвигает `currentStepIndex` и не планирует следующий удар (мёртвый враг уже должен быть отписан `EncounterManager`-ом, но проверка остаётся на всякий случай). Иначе: выполняет **текущий** шаг (`ExecuteStep`), продвигает `currentStepIndex` на следующий (с обёрткой по модулю), и сдвигает `nextActionTime` вперёд на `delaySeconds` **нового** текущего шага — то есть задержка читается с шага, который наступит **после** только что сработавшего, а не с только что сработавшего.
- **`ExecuteStep(step)`** — резолвит цели через `ResolveTargets(step.target, step.targetEnemyIndex)`; если список пуст — при наличии `step.fallback` рекурсивно вызывает `ExecuteStep(step.fallback)` (тот же тип, `EnemyActionStep`); если и `fallback` не задан или тоже не находит цель — шаг молча пропускается. Если цели нашлись — на каждой из них по очереди применяются все `instantActions`, затем все `appliedEffects` шага, тем же явным `Apply(combatManager, this, target)` (враг — `source`), что и у карт.
- **`ApplyStagger(seconds)`** — см. подробную формулу и обоснование `float` в [effects.md](effects.md); коротко: мгновенно двигает `nextActionTime` вперёд, `StaggerDisplay` — чисто отображаемый обратный отсчёт. Применить Пошатывание к `Player` нельзя — `AppliedEffectEntry.Apply` проверяет `target is Enemy` и молча ничего не делает иначе.

## `ResolveTargets` — 5 видов `EnemyActionTarget`

- **`Self`** → `[this]` — всегда резолвится, враг всегда может целиться в себя.
- **`Player`** → `[player]` — поле `Enemy.player`, проставляется `EncounterManager`-ом при спавне.
- **`AllOtherEnemies`** → все **другие** живые враги (`CurrentHP > 0`) из `combatManager.Enemies`, сам исполнитель всегда исключён; список может оказаться пустым, если союзников не осталось — тогда в игру вступает `fallback`.
- **`LowestHpOtherEnemy`** → единственный другой живой враг с наименьшим **отношением** `CurrentHP / maxHP` (не абсолютным HP — полный танк и почти убитая глушилка урона сравниваются честно, независимо от разницы в максимальном HP); пустой список, если живых союзников нет.
- **`SpecificEnemyIndex`** → `combatManager.Enemies[targetEnemyIndex]`, если индекс в границах списка **и** цель жива, иначе пустой список; `targetEnemyIndex` осмыслен только для этого вида цели (см. комментарий в `EnemyActionStep`).

## `EnemyActionStep` — данные, не поведение

- `stepName` — подпись для UI-телеграфа, показывается `CombatantStatusDisplay.BuildTelegraphText()` до срабатывания шага (см. [combat-core.md](combat-core.md)).
- `delaySeconds` — интервал от **предыдущего** сработавшего шага до этого (читается с нового текущего шага внутри `Enemy.Trigger`, см. выше).
- `target`/`targetEnemyIndex` — какой вид `EnemyActionTarget` и (только для `SpecificEnemyIndex`) какой индекс.
- `instantActions`/`appliedEffects` — те же списки `InstantActionEntry`/`AppliedEffectEntry`, что и у карт игрока ([cards.md](cards.md), [effects.md](effects.md)) — шаг паттерна врага и карта используют полностью одинаковую форму данных действия.
- `fallback` — необязательный `EnemyActionStep` той же формы, используется, только если `target` не резолвится ни в одну цель; `null` означает «шаг просто пропускается».
- `DescribeTarget(target)` — статический хелпер, даёт русский суффикс цели (" по игроку", " союзникам", " самому слабому союзнику" и т. д.) для текста телеграфа; используется только UI, на логику не влияет.

## `EnemyPatternData` / `EncounterData` (ScriptableObject)

- **`EnemyPatternData`** — просто `List<EnemyActionStep> steps`; один ассет на отдельный узнаваемый паттерн поведения, назначается на `Enemy.pattern` конкретного префаба врага.
- **`EncounterData`** — просто `List<GameObject> enemyPrefabs`; один ассет на конкретный бой, читает его `EncounterManager`.

## `EncounterManager` — оркестратор цикла боёв: динамический спавн игрока и врагов, смена энкаунтеров

С переходом на полностью динамический спавн игрока `EncounterManager` перестал быть «спавнер на один бой при старте сцены» — теперь это менеджер всего цикла забега: список энкаунтеров, спавн игрока и врагов на каждый из них, и переход к следующему при победе.

- `encounters` (`List<EncounterData>`) — весь список боёв забега в фиксированном порядке (в текущей сцене — `TestEncounter_1Enemy`/`_2Enemies`/`_3Enemies`, в этом порядке).
- `playerPrefab`/`playerSpawnPoint` — префаб игрока (см. ниже про `PlayerPrefab`) и `RectTransform`-точка на Canvas, где он появляется; сам объект `Player` с этой точки убран — раньше он стоял на сцене статично, теперь спавнится в неё как дочерний.
- `currentEncounterIndex` — индекс текущего боя в `encounters`; начинается с `-1`, чтобы первый же `StartNextEncounter()` увеличил его до `0`.
- `currentPlayer`/`currentEnemies` — собственные ссылки менеджера на объекты **текущего** боя, нужны, чтобы было что уничтожить в `CleanupCombatants()` при переходе к следующему.
- `Start()` — подписывается на `combatManager.OnVictory` (см. [combat-core.md](combat-core.md)) и сразу запускает первый бой через `StartNextEncounter()`.
- **`HandleVictory()`** — обработчик `OnVictory`: сохраняет `currentPlayer.CurrentHP` в `PlayerRunState.PersistedHP` **до** уничтожения игрока, уничтожает всех участников только что законченного боя (`CleanupCombatants`), и сразу запускает следующий (`StartNextEncounter`). Поражение (`CombatManager.OnDeath` игрока, не `OnVictory`) сюда не ведёт вообще — `EncounterManager` не подписан на смерть игрока, поэтому после «Поражение» никакой следующий бой не стартует, а `TargetSelectionManager` остаётся заблокированным `LockInput()`-ом до конца сцены.
- **`StartNextEncounter()`** — `currentEncounterIndex = (currentEncounterIndex + 1) % encounters.Count`: индекс закольцован по модулю размера списка, поэтому после последнего энкаунтера в списке следующий вызов возвращается к нулевому — цикл бесконечный, не одноразовый прогон списка. Далее: `combatManager.ResetForNewCombat()` (обнуляет секундомер/расписание/блокировку ввода — см. [combat-core.md](combat-core.md)), затем спавн игрока, спавн врагов, и в самом конце `handManager.BeginNewHand()` (см. [deck-hand.md](deck-hand.md)) — рука собирается только после того, как оба участника боя уже существуют.
- **`SpawnPlayer()`** — `Instantiate(playerPrefab, playerSpawnPoint)`, проставляет `player.combatManager`, накатывает `PlayerRunState.PersistedHP` через `SetCurrentHP`, если оно задано (первый бой забега — `PersistedHP == null`, игрок остаётся на полном HP из `Awake()`), и регистрирует его на `CombatManager` через `SetPlayer(player)` — это же вызов подписывает `HandlePlayerDeath` на его `OnDeath`.
- **`SpawnEnemies(encounter)`** — та же логика равномерного распределения по ширине `enemiesArea`, что была раньше (`spacing = width / (count + 1)`, позиции `-width/2 + spacing * (i + 1)`), только теперь `enemy.player = currentPlayer` берётся из только что заспавненного в этом же вызове игрока, а не из инспекторной ссылки. Для каждого врага настраивает `combatManager`/`player`, регистрирует его на `CombatManager` как `IScheduledEvent`, подписывает отписку на `OnDeath`, и отдельно проставляет `combatManager` на его `CombatantStatusDisplay` (`GetComponentInChildren`) — этому конкретному полю дисплея, как раньше `EnemyAttackTimerDisplay.combatManager`, нужна прямая ссылка на менеджер для чтения `CurrentTime` в телеграфе, и она не часть префаба (см. [combat-core.md](combat-core.md)). В конце — `combatManager.RegisterEnemies(spawned)`.
- «Кто регистрирует, тот и отписывает» из корневого `CLAUDE.md` по-прежнему в силе: регистрация/отписка `Enemy` как `IScheduledEvent` целиком в руках `EncounterManager`, не `Enemy`/`Combatant`.

## `PlayerPrefab` — игрок собран по образцу `EnemyPrefab`

Раньше единственный `Player` стоял статично на сцене (`Canvas/Combatants/Player`) с отдельным `PlayerHPBar` в другой ветке иерархии (`Canvas/HPBars`). Оба убраны со сцены целиком: игрок теперь заспавненный объект, как и враги. `PlayerPrefab` (в корне `Assets`, рядом с `Enemy.prefab`) — `Image` + `Player` + `CombatantStatusDisplay` на корне, с дочерними `Slider` (HP), `BlockIndicator`/`BlockText` и `EffectsText` — та же форма, что у `EnemyPrefab`, но без `AttackTimer`: `enemyForTelegraph`/`telegraphLabel` на его `CombatantStatusDisplay` оставлены пустыми, телеграф — только для врагов.

## Телеграф атаки — часть `CombatantStatusDisplay`

Раньше отдельный `EnemyAttackTimerDisplay`, теперь — `BuildTelegraphText()` внутри `UI/CombatantStatusDisplay.cs` (код перенесён буквально, без переписывания; подробности и сигнатура полей — в [combat-core.md](combat-core.md)). Коротко: обратный отсчёт до атаки (`enemyForTelegraph.NextTime - combatManager.CurrentTime`), название текущего шага и построчное описание каждого его действия/эффекта через тот же `ComputePreviewAmount` из `CardActions.cs`, `CardTextHelpers.HitCountSuffix` и `EnemyActionStep.DescribeTarget` для суффикса цели, что и текст карты игрока ([cards.md](cards.md)). Если у шага сейчас нет резолвящейся цели, для каждого действия печатает «(нет цели)» вместо числа.
