# Враги и энкаунтеры

Классы: `Enemy`, `EnemyPatternData`/`EnemyActionStep`, `EncounterData`/`EncounterManager`, `EnemyActionTarget`.

Файлы: `Combatants/Enemy.cs`, `Combatants/EnemyActionStep.cs`, `Combatants/EnemyPatternData.cs`, `Encounters/EncounterData.cs`, `Managers/EncounterManager.cs`, `Enums/EnemyActionTarget.cs`, `UI/EnemyAttackTimerDisplay.cs`.

См. также: [combat-core.md](combat-core.md) — `IScheduledEvent`, в реестр которого встаёт сам `Enemy`; [cards.md](cards.md)/[effects.md](effects.md) — те же `InstantActionEntry`/`AppliedEffectEntry`, что использует `EnemyActionStep`.

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

- `stepName` — подпись для UI-телеграфа, показывается `EnemyAttackTimerDisplay` до срабатывания шага.
- `delaySeconds` — интервал от **предыдущего** сработавшего шага до этого (читается с нового текущего шага внутри `Enemy.Trigger`, см. выше).
- `target`/`targetEnemyIndex` — какой вид `EnemyActionTarget` и (только для `SpecificEnemyIndex`) какой индекс.
- `instantActions`/`appliedEffects` — те же списки `InstantActionEntry`/`AppliedEffectEntry`, что и у карт игрока ([cards.md](cards.md), [effects.md](effects.md)) — шаг паттерна врага и карта используют полностью одинаковую форму данных действия.
- `fallback` — необязательный `EnemyActionStep` той же формы, используется, только если `target` не резолвится ни в одну цель; `null` означает «шаг просто пропускается».
- `DescribeTarget(target)` — статический хелпер, даёт русский суффикс цели (" по игроку", " союзникам", " самому слабому союзнику" и т. д.) для текста телеграфа; используется только UI, на логику не влияет.

## `EnemyPatternData` / `EncounterData` (ScriptableObject)

- **`EnemyPatternData`** — просто `List<EnemyActionStep> steps`; один ассет на отдельный узнаваемый паттерн поведения, назначается на `Enemy.pattern` конкретного префаба врага.
- **`EncounterData`** — просто `List<GameObject> enemyPrefabs`; один ассет на конкретный бой, читает его `EncounterManager`.

## `EncounterManager` — динамический спавн

- `Start()` — спавнит каждый префаб из `encounterData.enemyPrefabs`, равномерно распределяя их по ширине `enemiesArea` (`spacing = width / (count + 1)`, позиции `-width/2 + spacing * (i + 1)`) — ряд всегда центрирован и равномерно распределён вне зависимости от числа врагов (от одного до нескольких).
- Для каждого заспавненного врага настраивает только `combatManager` и `player` — HP-бар, таймер атаки и индикатор стаков эффектов уже подключены друг к другу и к себе внутри самого префаба (см. корневой `CLAUDE.md`), спавн их не трогает, за исключением `EnemyAttackTimerDisplay.combatManager` — этому дисплею отдельно нужна ссылка на `CombatManager` для чтения `CurrentTime`.
- Регистрирует `Enemy` на `CombatManager` как `IScheduledEvent` (`RegisterScheduledEvent(enemy)` — враг передаётся напрямую, без обёртки) и подписывает отписку на его же `OnDeath`. Регистрация и отписка целиком в руках `EncounterManager`, не `Enemy`/`Combatant` — «кто регистрирует, тот и отписывает» из корневого `CLAUDE.md`.
- В самом конце, уже после цикла спавна, один раз вызывает `combatManager.RegisterEnemies(spawned)` — тогда же `CombatManager` подписывает `HandleEnemyDeath` на `OnDeath` каждого врага.

## UI-телеграф: `EnemyAttackTimerDisplay`

`UI/EnemyAttackTimerDisplay.cs` — каждый `Update()` пишет обратный отсчёт до атаки (`enemy.NextTime - combatManager.CurrentTime`) и, если у врага есть паттерн, дописывает название текущего шага и построчное описание каждого его действия/эффекта — используя тот же `ComputePreviewAmount`/`HitCountSuffix` из `CardActions.cs` и `EnemyActionStep.DescribeTarget` для суффикса цели, что и текст карты игрока ([cards.md](cards.md)). Если у шага сейчас нет резолвящейся цели, для каждого действия печатает «(нет цели)» вместо числа.
