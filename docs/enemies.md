# Враги и энкаунтеры

Классы: `Enemy`, `EnemyPatternData`/`EnemyActionStep`, `EncounterData`/`EncounterManager`, `EnemyActionTarget`.

Файлы: `Combatants/Enemy.cs`, `Combatants/EnemyActionStep.cs`, `Combatants/EnemyPatternData.cs`, `Encounters/EncounterData.cs`, `Managers/EncounterManager.cs`, `Enums/EnemyActionTarget.cs`, `UI/CombatantStatusDisplay.cs` (телеграф — часть общего дисплея, не отдельный класс).

См. также: [combat-core.md](combat-core.md) — `IScheduledEvent`, в реестр которого встаёт сам `Enemy`, а также `CombatManager.OnVictory`/`OnDefeat`/`SetPlayer`/`ResetForNewCombat` и `PlayerRunState`, на которых держится исход боя, описанный в этом файле; [map.md](map.md) — `MapManager`, который запускает `StartEncounter` и принимает исход боя; [cards.md](cards.md)/[effects.md](effects.md) — те же `InstantActionEntry`/`AppliedEffectEntry`, что использует `EnemyActionStep`; [deck-hand.md](deck-hand.md) — `HandManager.BeginNewHand`, которую `EncounterManager` вызывает на каждый новый бой.

## `Enemy` — `Combatant` + `IScheduledEvent`

`Enemy` реализует `IScheduledEvent` напрямую (не через `PeriodicEffectEvent`): `NextTime => nextActionTime`, `Priority => 1` (всегда проигрывает тай-брейк времени `OneShotEvent`-у карты игрока с приоритетом `0` — [combat-core.md](combat-core.md)). Регистрирует и отписывает его `EncounterManager`, а не сам `Enemy`/`Combatant` (кто заспавнил — тот и владеет подпиской).

- `pattern` (`EnemyPatternData`) — назначается на префабе конкретного врага; зацикленный список `EnemyActionStep`.
- `currentStepIndex` — какой шаг сработает следующим; продвигается по модулю `pattern.steps.Count` — паттерн крутится бесконечно.
- **`ScheduleFirstAction()`** — при пустом/отсутствующем паттерне пишет `Debug.LogError` с именем врага и ставит `NextTime = +∞` (враг не действует). Иначе — ставит таймер первого действия: `nextActionTime = CurrentTime + steps[0].delaySeconds`. `EncounterManager` вызывает её при спавне до `RegisterScheduledEvent`; задержка шага — это время *перед* этим шагом, поэтому первое действие не происходит в 0 с.
- **`Trigger()`** (вызывается `CombatManager.ResolveUpTo`, когда наступает `NextTime`): если исполнитель уже мёртв (`CurrentHP <= 0`) — не делает вообще ничего, даже не продвигает `currentStepIndex` и не планирует следующий удар (мёртвый враг уже должен быть отписан `EncounterManager`-ом, но проверка остаётся на всякий случай). Иначе: выполняет **текущий** шаг (`ExecuteStep`), продвигает `currentStepIndex` на следующий (с обёрткой по модулю), и сдвигает `nextActionTime` вперёд на `delaySeconds` **нового** текущего шага — то есть задержка читается с шага, который наступит **после** только что сработавшего, а не с только что сработавшего.
- **`GetPlannedStep()`** — единственное место, где решается, что враг сделает на текущем шаге: резолвит цели основного шага; если их нет и `hasFallback` — берёт запасной шаг (один уровень, без рекурсии). Возвращает `PlannedStep` (ключ названия, вид цели, цели, действия, эффекты); если целей нет совсем — список целей пуст, и `Trigger()` ничего не применяет. Тот же `PlannedStep` читает телеграф (`CombatantStatusDisplay`), поэтому он совпадает с реальным действием. Если цели нашлись — на каждой из них по очереди применяются все `instantActions`, затем все `appliedEffects` шага, тем же явным `Apply(combatManager, this, target)` (враг — `source`), что и у карт.
- **`ApplyStagger(seconds)`** — см. подробную формулу и обоснование `float` в [effects.md](effects.md); коротко: мгновенно двигает `nextActionTime` вперёд, `StaggerDisplay` — чисто отображаемый обратный отсчёт. Применить Пошатывание к `Player` нельзя — `AppliedEffectEntry.Apply` проверяет `target is Enemy` и молча ничего не делает иначе.

## `ResolveTargets` — 5 видов `EnemyActionTarget`

- **`Self`** → `[this]` — всегда резолвится, враг всегда может целиться в себя.
- **`Player`** → `[player]` — поле `Enemy.player`, проставляется `EncounterManager`-ом при спавне.
- **`AllOtherEnemies`** → все **другие** живые враги (`CurrentHP > 0`) из `combatManager.Enemies`, сам исполнитель всегда исключён; список может оказаться пустым, если союзников не осталось — тогда в игру вступает `fallback`.
- **`LowestHpOtherEnemy`** → единственный другой живой враг с наименьшим **отношением** `CurrentHP / maxHP` (не абсолютным HP — полный танк и почти убитая глушилка урона сравниваются честно, независимо от разницы в максимальном HP); пустой список, если живых союзников нет.
- **`SpecificEnemyIndex`** → `combatManager.Enemies[targetEnemyIndex]`, если индекс в границах списка **и** цель жива, иначе пустой список; `targetEnemyIndex` осмыслен только для этого вида цели (см. комментарий в `EnemyActionStep`).

## `EnemyActionStep` — данные, не поведение

- `stepNameKey` — ключ названия шага в `ru.txt` (`enemy.<паттерн>.<шаг>`), подпись для UI-телеграфа, показывается `CombatantStatusDisplay.BuildTelegraphText()` до срабатывания шага (см. [combat-core.md](combat-core.md)).
- `delaySeconds` — интервал от **предыдущего** сработавшего шага до этого (читается с нового текущего шага внутри `Enemy.Trigger`, см. выше).
- `target`/`targetEnemyIndex` — какой вид `EnemyActionTarget` и (только для `SpecificEnemyIndex`) какой индекс.
- `instantActions`/`appliedEffects` — те же списки `InstantActionEntry`/`AppliedEffectEntry`, что и у карт игрока ([cards.md](cards.md), [effects.md](effects.md)) — шаг паттерна врага и карта используют полностью одинаковую форму данных действия.
- `hasFallback` + `fallback` (`EnemyFallbackStep`: `stepNameKey`, `target`, `targetEnemyIndex`, `instantActions`, `appliedEffects` — без `delaySeconds` и без своего запасного шага, срабатывает в момент основного) — используется, только если `target` основного шага не резолвится ни в одну цель; `hasFallback == false` — «шаг просто пропускается». Отдельный флаг нужен, потому что поле `[Serializable]`-класса Unity никогда не хранит как `null`.
- Текст действий телеграфа — целые фразы по ключам `combat.action.<Damage|Block|Heal>.<EnemyActionTarget>` и `combat.effect.<Give|Apply>.<EnemyActionTarget>` (суффикс цели — внутри фразы); `CombatantStatusDisplay` только подставляет числа. `OnValidate` у `EnemyPatternData` предупреждает, если ключа шага нет в таблице.

## `EnemyPatternData` / `EncounterData` (ScriptableObject)

- **`EnemyPatternData`** — просто `List<EnemyActionStep> steps`; один ассет на отдельный узнаваемый паттерн поведения, назначается на `Enemy.pattern` конкретного префаба врага.
- **`EncounterData`** — просто `List<GameObject> enemyPrefabs`; один ассет на конкретный бой. Ссылка на него лежит на боевом узле карты (`MapNode.encounter`), оттуда `MapManager` передаёт его в `EncounterManager.StartEncounter`.

## `EncounterManager` — спавн игрока и врагов на один бой

`EncounterManager` больше не крутит энкаунтеры по кругу и не стартует бой сам: списка `encounters` и `currentEncounterIndex` в нём нет. Бой начинается только по вызову `StartEncounter(encounter)` — его делает `MapManager`, когда игрок кликнул боевой узел карты ([map.md](map.md)).

- `playerPrefab`/`playerSpawnPoint` — префаб игрока (см. ниже про `PlayerPrefab`) и `RectTransform`-точка на Canvas, где он появляется.
- `mapManager` — обратная ссылка, по которой менеджер сообщает исход боя (`OnCombatEnded`).
- `currentPlayer`/`currentEnemies` — ссылки на объекты **текущего** боя, нужны, чтобы было что уничтожить в `CleanupCombatants()`.
- `Start()` — только подписки: `combatManager.OnVictory += HandleVictory`, `combatManager.OnDefeat += HandleDefeat` (см. [combat-core.md](combat-core.md)).
- **`StartEncounter(EncounterData)`** — то, что раньше делал `StartNextEncounter`, но для переданного энкаунтера: `combatManager.ResetForNewCombat()`, спавн игрока, спавн врагов, и в самом конце `handManager.BeginNewHand()` ([deck-hand.md](deck-hand.md)) — рука собирается, когда оба участника боя уже существуют.
- **`HandleVictory()`** — сохраняет `currentPlayer.CurrentHP` в `PlayerRunState.PersistedHP` **до** уничтожения игрока, уничтожает участников (`CleanupCombatants`), вызывает `mapManager.OnCombatEnded(true)`.
- **`HandleDefeat()`** — `PlayerRunState.PersistedHP = null` (следующий бой — с полным HP), `CleanupCombatants`, `mapManager.OnCombatEnded(false)`. Узел поражения считается пройденным.
- **`SpawnPlayer()`** — `Instantiate(playerPrefab, playerSpawnPoint)`, проставляет `player.combatManager`, накатывает `PersistedHP` через `SetCurrentHP`, если оно задано, и регистрирует игрока через `combatManager.SetPlayer(player)` (это же подписывает `HandlePlayerDeath` на его `OnDeath`).
- **`SpawnEnemies(encounter)`** — равномерное распределение по ширине `enemiesArea` (`spacing = width / (count + 1)`, позиции `-width/2 + spacing * (i + 1)`), `enemy.player = currentPlayer`. Для каждого врага настраивает `combatManager`/`player`, регистрирует его на `CombatManager` как `IScheduledEvent`, подписывает отписку на `OnDeath`, отдельно проставляет `combatManager` на его `CombatantStatusDisplay` (`GetComponentInChildren`; поле нужно телеграфу для `CurrentTime` и не часть префаба). В конце — `combatManager.RegisterEnemies(spawned)`.
- «Кто регистрирует, тот и отписывает» из корневого `CLAUDE.md` по-прежнему в силе: регистрация/отписка `Enemy` как `IScheduledEvent` целиком в руках `EncounterManager`.

## `PlayerPrefab` — игрок собран по образцу `EnemyPrefab`

Раньше единственный `Player` стоял статично на сцене (`Canvas/Combatants/Player`) с отдельным `PlayerHPBar` в другой ветке иерархии (`Canvas/HPBars`). Оба убраны со сцены целиком: игрок теперь заспавненный объект, как и враги. `PlayerPrefab` (в корне `Assets`, рядом с `Enemy.prefab`) — `Image` + `Player` + `CombatantStatusDisplay` на корне, с дочерними `Slider` (HP), `BlockIndicator`/`BlockText` и `EffectsText` — та же форма, что у `EnemyPrefab`, но без `AttackTimer`: `enemyForTelegraph`/`telegraphLabel` на его `CombatantStatusDisplay` оставлены пустыми, телеграф — только для врагов.

## Телеграф атаки — часть `CombatantStatusDisplay`

Раньше отдельный `EnemyAttackTimerDisplay`, теперь — `BuildTelegraphText()` внутри `UI/CombatantStatusDisplay.cs` (код перенесён буквально, без переписывания; подробности и сигнатура полей — в [combat-core.md](combat-core.md)). Коротко: обратный отсчёт до атаки (`enemyForTelegraph.NextTime - combatManager.CurrentTime`), название текущего шага и построчное описание каждого его действия/эффекта через тот же `ComputePreviewAmount` из `CardActions.cs`, `CardTextHelpers.HitCountSuffix` и `EnemyActionStep.DescribeTarget` для суффикса цели, что и текст карты игрока ([cards.md](cards.md)). Если у шага сейчас нет резолвящейся цели, для каждого действия печатает «(нет цели)» вместо числа.

**Сверено с кодом:** `EncounterManager.StartEncounter(EncounterData)` — `Managers/EncounterManager.cs:45`; `HandleVictory` пишет `PersistedHP` (`:25`), `HandleDefeat` обнуляет его (`:32`); `Enemy.Priority => 1` — `Combatants/Enemy.cs:13`.
