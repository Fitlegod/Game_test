using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Боевое ядро без UI: время, события, откат. Враги/игрок/менеджер создаются в коде, карты играются через настоящий Card.Play.
public class CombatTests
{
    CombatManager cm;
    Player player;
    List<Object> created;

    [SetUp]
    public void SetUp()
    {
        created = new List<Object>();
        cm = Make<CombatManager>();
        player = Make<Player>();
        player.maxHP = 30;
        player.combatManager = cm;
        player.Init();
        cm.ResetForNewCombat();
        cm.SetPlayer(player);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
    }

    T Make<T>() where T : Component
    {
        var go = new GameObject(typeof(T).Name);
        created.Add(go);
        return go.AddComponent<T>();
    }

    static InstantActionEntry Act(InstantActionKind kind, int amount, EffectTargetTag tag)
        => new InstantActionEntry { kind = kind, amount = amount, hitCount = 1, targetTag = tag };

    Card MakeCard(int cost, int delay, params InstantActionEntry[] actions)
    {
        var data = ScriptableObject.CreateInstance<CardData>();
        created.Add(data);
        data.timeCostSeconds = cost;
        data.effectDelaySeconds = delay;
        data.instantActions = actions.ToList();
        var card = Make<Card>();
        card.instance = new CardInstance(data);
        card.combatManager = cm;
        return card;
    }

    Card Dmg(int amount, int cost, int delay) => MakeCard(cost, delay, Act(InstantActionKind.Damage, amount, EffectTargetTag.Enemy));
    Card RewindCard(int n, int cost, int delay) => MakeCard(cost, delay, Act(InstantActionKind.Rewind, n, EffectTargetTag.Player));

    // Враг бьёт игрока каждые `delay` с; шагов в паттерне `steps` (чтобы видеть индекс шага)
    Enemy MakeEnemy(int hp, int delay, int damage, int steps = 1)
    {
        var enemy = Make<Enemy>();
        enemy.maxHP = hp;
        enemy.combatManager = cm;
        enemy.Init();
        enemy.player = player;
        var pattern = ScriptableObject.CreateInstance<EnemyPatternData>();
        created.Add(pattern);
        for (int i = 0; i < steps; i++)
            pattern.steps.Add(new EnemyActionStep
            {
                stepNameKey = "enemy.test.attack_player",
                delaySeconds = delay,
                target = EnemyActionTarget.Player,
                instantActions = new List<InstantActionEntry> { Act(InstantActionKind.Damage, damage, EffectTargetTag.Player) }
            });
        enemy.pattern = pattern;
        enemy.ScheduleFirstAction();
        return enemy;
    }

    // Враг с произвольными шагами (каждый шаг: действие и задержка до следующего)
    Enemy MakeEnemyWithSteps(int hp, params (InstantActionEntry action, EnemyActionTarget target, int delay)[] steps)
    {
        var enemy = Make<Enemy>();
        enemy.maxHP = hp;
        enemy.combatManager = cm;
        enemy.Init();
        enemy.player = player;
        var pattern = ScriptableObject.CreateInstance<EnemyPatternData>();
        created.Add(pattern);
        foreach (var st in steps)
            pattern.steps.Add(new EnemyActionStep
            {
                stepNameKey = "enemy.test.attack_player",
                delaySeconds = st.delay,
                target = st.target,
                instantActions = new List<InstantActionEntry> { st.action }
            });
        enemy.pattern = pattern;
        enemy.ScheduleFirstAction();
        return enemy;
    }

    // Как EncounterManager: регистрация врага в расписании и снятие при смерти
    void Spawn(params Enemy[] enemies)
    {
        foreach (var e in enemies)
        {
            var enemy = e;
            cm.RegisterScheduledEvent(enemy);
            enemy.OnDeath += () => cm.UnregisterScheduledEvent(enemy);
        }
        cm.RegisterEnemies(enemies.ToList());
    }

    void Advance(int seconds)
    {
        cm.AdvanceTime(seconds);
        cm.ResolveUpTo();
    }

    PeriodicEffectEvent AddDrawTimer(List<int> draws)
    {
        var timer = new PeriodicEffectEvent(cm.CurrentTime, HandManager.DrawIntervalSeconds, () => draws.Add(cm.CurrentEventTime));
        cm.RegisterScheduledEvent(timer);
        return timer;
    }

    // ---------- Этап 1: целое время ----------

    [Test]
    public void A_CardCost2Delay1_FiresAtMark1()
    {
        NoDecay();
        var card = MakeCard(2, 1, Act(InstantActionKind.Block, 5, EffectTargetTag.Player));
        card.Play(null);
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.AreEqual(5, player.CurrentBlock);
        Assert.IsTrue(ActionJournal().Count > 0 && ActionJournal().All(e => e.Time == 1), "эффект сработал на отметке 1, а не 2");
    }

    [Test]
    public void B_Weak_LosesStackAt3And6()
    {
        player.AddEffectStacks(StatusEffectType.Weak, 2);
        Advance(2); Assert.AreEqual(2, player.GetStacks(StatusEffectType.Weak));
        Advance(1); Assert.AreEqual(1, player.GetStacks(StatusEffectType.Weak));
        Advance(2); Assert.AreEqual(1, player.GetStacks(StatusEffectType.Weak));
        Advance(1); Assert.AreEqual(0, player.GetStacks(StatusEffectType.Weak));
    }

    [Test]
    public void C_EnemyWithDelay2_HitsAt2_4_6()
    {
        var enemy = MakeEnemy(30, 2, 5);
        Spawn(enemy);
        Advance(1); Assert.AreEqual(30, player.CurrentHP);
        Advance(1); Assert.AreEqual(25, player.CurrentHP);
        Advance(2); Assert.AreEqual(20, player.CurrentHP);
        Advance(2); Assert.AreEqual(15, player.CurrentHP);
        Assert.AreEqual(8, enemy.NextTime);
    }

    [Test]
    public void D_OneShot_AfterFiring_StaysInListNotPending()
    {
        int fires = 0;
        var ev = new OneShotEvent(cm, 1, 0, _ => fires++);
        cm.RegisterScheduledEvent(ev);
        Advance(1);
        Assert.AreEqual(1, fires);
        Assert.AreEqual(1, ev.NextTime);
        Assert.IsFalse(ev.IsPending);
        Assert.IsTrue(cm.ScheduledEvents.Contains(ev));
        Advance(1);
        Assert.AreEqual(1, fires);
    }

    [Test]
    public void E_DrawTimerEvery2_FiresAt2_4_6()
    {
        var draws = new List<int>();
        AddDrawTimer(draws);
        Advance(7);
        CollectionAssert.AreEqual(new[] { 2, 4, 6 }, draws);
    }

    // ---------- Этап 2: откат ----------

    [Test]
    public void F_MainScenario()
    {
        var enemy = MakeEnemy(30, 2, 5, steps: 2);
        Spawn(enemy);

        Dmg(6, 1, 0).Play(enemy);
        Assert.AreEqual(24, enemy.CurrentHP); Assert.AreEqual(1, cm.CurrentTime);

        Dmg(6, 2, 1).Play(enemy);
        Assert.AreEqual(18, enemy.CurrentHP); Assert.AreEqual(25, player.CurrentHP); Assert.AreEqual(3, cm.CurrentTime);

        RewindCard(2, 1, 0).Play(null);
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.AreEqual(18, enemy.CurrentHP, "карта сработала повторно ровно один раз");
        Assert.AreEqual(25, player.CurrentHP, "удар в 2 ушёл в пустоту");
        Assert.AreEqual(1, enemy.CurrentStepIndex);
        Assert.AreEqual(4, enemy.NextTime);

        MakeCard(2, 1).Play(null); // пустая карта cost 2
        Assert.AreEqual(4, cm.CurrentTime);
        Assert.AreEqual(20, player.CurrentHP, "удар в 4 настоящий");
    }

    [Test]
    public void G_Precision_BlockAndHpRestoredExactly_EnemyAndTicksReturn()
    {
        NoDecay();
        var enemy = MakeEnemy(5, 50, 1);
        Spawn(enemy);
        enemy.GainBlock(3);
        enemy.AddEffectStacks(StatusEffectType.Weak, 1);
        var tick = cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Single(p => p.Owner == enemy && p.Priority != 2);

        Dmg(10, 2, 2).Play(enemy);
        Assert.AreEqual(0, enemy.CurrentHP);
        Assert.AreEqual(0, enemy.CurrentBlock);
        Assert.IsFalse(cm.ScheduledEvents.Contains(enemy));
        Assert.IsFalse(cm.ScheduledEvents.Contains(tick));

        RewindCard(2, 1, 1).Play(null); // T=3, R=1
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.AreEqual(5, enemy.CurrentHP);
        Assert.AreEqual(3, enemy.CurrentBlock);
        Assert.IsTrue(cm.ScheduledEvents.Contains(enemy));
        Assert.IsTrue(cm.ScheduledEvents.Contains(tick));
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Weak));
    }

    [Test]
    public void H_Death_Revives_DiesAgainOnReplay_NoVictoryWhileOtherAlive()
    {
        var e1 = MakeEnemy(5, 50, 1);
        var e2 = MakeEnemy(30, 50, 1);
        Spawn(e1, e2);
        int victories = 0;
        cm.OnVictory += () => victories++;

        Dmg(10, 2, 2).Play(e1);
        Assert.AreEqual(0, e1.CurrentHP);
        Assert.AreEqual(0, victories);

        RewindCard(2, 1, 1).Play(null);
        Assert.AreEqual(5, e1.CurrentHP);
        Assert.AreEqual(0, victories);

        Advance(1); // повтор в 2
        Assert.AreEqual(0, e1.CurrentHP);
        Assert.AreEqual(0, victories, "второй враг жив");

        Dmg(100, 1, 0).Play(e2);
        Assert.AreEqual(1, victories);
    }

    [Test]
    public void I_Boundary_RewindBeyondZero()
    {
        MakeCard(1, 1).Play(null); // время 1
        RewindCard(5, 1, 1).Play(null); // T=2, R=0, S=2
        Assert.AreEqual(0, cm.CurrentTime);
    }

    [Test]
    public void K_OwnBlockInInterval_NotDoubledNotRemoved()
    {
        NoDecay();
        MakeCard(1, 0, Act(InstantActionKind.Block, 5, EffectTargetTag.Player)).Play(null);
        RewindCard(2, 1, 1).Play(null); // T=2, R=0
        Assert.AreEqual(0, cm.CurrentTime);
        Assert.AreEqual(5, player.CurrentBlock);
    }

    [Test]
    public void L_RewindDoesNotReplaySelf_ReplayedRewindDoesNothing()
    {
        var enemy = MakeEnemy(30, 50, 1);
        Spawn(enemy);
        Dmg(6, 1, 0).Play(enemy);
        RewindCard(1, 1, 0).Play(null); // T=1, R=0: не зациклилось
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.AreEqual(24, enemy.CurrentHP);

        var rewind = RewindCard(5, 1, 0);
        int journalBefore = cm.Journal.Count;
        Card.ApplyAllEffects(rewind.data, cm, null, isReplay: true);
        Assert.AreEqual(1, cm.CurrentTime, "Откат при повторе ничего не делает");
        Assert.AreEqual(journalBefore, cm.Journal.Count);
    }

    // Добор при откате. Таймер — настоящий PeriodicEffectEvent с HandManager.DrawIntervalSeconds, «рука» — счётчик.
    [Test]
    public void M1_Draw_Cost1()
    {
        var draws = new List<int>();
        var timer = AddDrawTimer(draws);
        Advance(8);
        Assert.AreEqual(4, draws.Count); Assert.AreEqual(10, timer.NextTime);

        RewindCard(3, 1, 1).Play(null); // T=9, R=6, S=3
        Assert.AreEqual(6, cm.CurrentTime);
        Assert.AreEqual(7, timer.NextTime);
        Assert.AreEqual(4, draws.Count);
        Advance(1);
        Assert.AreEqual(5, draws.Count, "ровно одна новая карта");
        Assert.AreEqual(9, timer.NextTime);
    }

    [Test]
    public void M2_Draw_Cost2_FiresInSameResolve()
    {
        var draws = new List<int>();
        var timer = AddDrawTimer(draws);
        Advance(8);
        RewindCard(3, 2, 1).Play(null); // T=9, время 10 → 7
        Assert.AreEqual(7, cm.CurrentTime);
        Assert.AreEqual(5, draws.Count);
        Assert.AreEqual(9, timer.NextTime);
    }

    [Test]
    public void M3_Draw_ScheduledExactlyAtT()
    {
        var draws = new List<int>();
        var timer = AddDrawTimer(draws);
        Advance(9); // добор в 2,4,6,8; следующий 10
        RewindCard(3, 2, 1).Play(null); // T=10, R=7, S=3; время 11 → 8
        Assert.AreEqual(8, cm.CurrentTime);
        Assert.AreEqual(5, draws.Count, "одна карта, без пропуска и дубля");
        Assert.AreEqual(9, timer.NextTime);
    }

    [Test]
    public void M4_Draw_RewindBeyondZero_ShiftsBySNotN()
    {
        var draws = new List<int>();
        var timer = AddDrawTimer(draws);
        MakeCard(2, 0).Play(null); // время 2, добор в 2, следующий 4
        Assert.AreEqual(4, timer.NextTime);
        RewindCard(5, 1, 0).Play(null); // T=2, R=0, S=2
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.AreEqual(2, timer.NextTime);
    }

    [Test]
    public void M5_TwoRewinds_TimerStaysInList_GapsAre2()
    {
        var draws = new List<int>();
        var timer = AddDrawTimer(draws);
        Advance(8);
        RewindCard(3, 1, 1).Play(null);
        RewindCard(3, 1, 1).Play(null);
        Assert.IsTrue(cm.ScheduledEvents.Contains(timer));
        int count = draws.Count;
        for (int i = 0; i < 3; i++)
        {
            Advance(1); Assert.AreEqual(count, draws.Count);
            Advance(1); Assert.AreEqual(++count, draws.Count);
        }
    }

    [Test]
    public void N_Journal_NoEntriesFromR_NothingWrittenDuringUndo()
    {
        var enemy = MakeEnemy(30, 50, 1);
        Spawn(enemy);
        Dmg(1, 1, 0).Play(enemy); // запись в 0 — остаётся

        int writtenDuringUndo = -1;
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 3, 0, _ =>
            cm.Record(enemy, () =>
            {
                int before = cm.Journal.Count;
                cm.Record(enemy, () => { });
                writtenDuringUndo = cm.Journal.Count - before;
            })));
        Dmg(6, 3, 2).Play(enemy); // эффекты в 3 (карта и пробная запись), время 4

        RewindCard(3, 1, 1).Play(null); // T=5, R=2, время 2
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.AreEqual(0, writtenDuringUndo, "во время Undo журнал не пишется");
        Assert.IsTrue(ActionJournal().Count > 0);
        Assert.IsTrue(ActionJournal().All(e => e.Time < 2));
    }

    // Враг A в момент 6 лечит слабейшего союзника на 4; карта Jab с моментом 6 сыграна уже ПОСЛЕ хода A и убивает A.
    // Повтор после отката должен воспроизвести тот же порядок: сначала A, потом карта.
    [Test]
    public void O_Replay_KeepsOriginalOrderOfSameMoment()
    {
        var a = MakeEnemyWithSteps(5, (Act(InstantActionKind.Heal, 4, EffectTargetTag.Enemy), EnemyActionTarget.LowestHpOtherEnemy, 6),
                                      (Act(InstantActionKind.Heal, 4, EffectTargetTag.Enemy), EnemyActionTarget.LowestHpOtherEnemy, 6));
        var b = MakeEnemy(30, 99, 1);
        b.SetCurrentHP(20);
        Spawn(a, b);

        MakeCard(6, 0).Play(null);        // время 6: A ходит в 6 и лечит B
        Dmg(6, 1, 0).Play(a);             // Jab с моментом 6 — после хода A
        int aHp = a.CurrentHP, bHp = b.CurrentHP, aStep = a.CurrentStepIndex, bStep = b.CurrentStepIndex;
        Assert.AreEqual(0, aHp); Assert.AreEqual(24, bHp);

        RewindCard(3, 1, 1).Play(null);   // T=8, R=5: время 5, повтор ещё впереди
        Assert.AreEqual(5, cm.CurrentTime);
        Advance(2);                       // время 7: повтор момента 6
        Assert.AreEqual(aHp, a.CurrentHP);
        Assert.AreEqual(bHp, b.CurrentHP);
        Assert.AreEqual(aStep, a.CurrentStepIndex);
        Assert.AreEqual(bStep, b.CurrentStepIndex);
    }

    // Новая карта в тот же момент, что и повторяемое действие врага: повторяемое — первым, без исключений и циклов
    [Test]
    public void P_NewCardAtSameMomentAsReplay_ReplayFirst()
    {
        var a = MakeEnemyWithSteps(30, (Act(InstantActionKind.Heal, 4, EffectTargetTag.Enemy), EnemyActionTarget.LowestHpOtherEnemy, 6));
        var b = MakeEnemy(30, 99, 1);
        b.SetCurrentHP(29);
        Spawn(a, b);

        MakeCard(6, 0).Play(null);        // A в 6 лечит B: 30
        Assert.AreEqual(30, b.CurrentHP);
        RewindCard(3, 1, 1).Play(null);   // T=7, R=4: B снова 29, ход A в 6 ждёт повтора
        Assert.AreEqual(4, cm.CurrentTime);
        Assert.AreEqual(29, b.CurrentHP);

        Dmg(1, 2, 2).Play(b);             // эффект в 6, время 6: ход A (повтор) первым: 29→30→29
        Assert.AreEqual(29, b.CurrentHP, "повторяемое действие врага — раньше новой карты");
        Assert.AreEqual(6, cm.CurrentTime);
    }

    [Test]
    public void R_RewindFromEnemyAction_DoesNothingAndLogsError()
    {
        var enemy = MakeEnemyWithSteps(30, (Act(InstantActionKind.Rewind, 3, EffectTargetTag.Player), EnemyActionTarget.Self, 2));
        Spawn(enemy);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Откат можно вызвать только из события карты"));
        Advance(2);
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.AreEqual(30, enemy.CurrentHP);
        Assert.AreEqual(30, player.CurrentHP);
        Assert.AreEqual(1, ActionJournal().Count, "только запись о самом ходе врага");
        Assert.AreSame(enemy, ActionJournal()[0].Source);
    }

    // Враг ходит в интервале несколько раз (в 2 и в 4), карта в 4 — после его хода: порядок обоих ходов должен сохраниться
    [Test]
    public void S_Replay_EnemyActsSeveralTimesInInterval()
    {
        var a = MakeEnemyWithSteps(5, (Act(InstantActionKind.Heal, 4, EffectTargetTag.Enemy), EnemyActionTarget.LowestHpOtherEnemy, 2),
                                      (Act(InstantActionKind.Heal, 4, EffectTargetTag.Enemy), EnemyActionTarget.LowestHpOtherEnemy, 2));
        var b = MakeEnemy(30, 99, 1);
        b.SetCurrentHP(20);
        Spawn(a, b);

        MakeCard(4, 0).Play(null);        // A ходит в 2 и в 4: B 20→24→28
        Dmg(6, 1, 0).Play(a);             // эффект в 4 — после хода A, A умирает
        Assert.AreEqual(0, a.CurrentHP); Assert.AreEqual(28, b.CurrentHP);

        RewindCard(5, 1, 1).Play(null);   // T=6, R=1: время 1
        Assert.AreEqual(1, cm.CurrentTime);
        Advance(4);                       // время 5
        Assert.AreEqual(0, a.CurrentHP);
        Assert.AreEqual(28, b.CurrentHP);
        Assert.AreEqual(5, cm.CurrentTime);
    }

    // Повтор сработал меньше раз, чем первый проход (ход врага ушёл на 9): «залипший» номер не должен обгонять новую карту вне интервала повтора
    [Test]
    public void T_StaleReplayOrder_DoesNotOutrunNewCardOutsideReplayWindow()
    {
        var e = MakeEnemyWithSteps(30, (Act(InstantActionKind.Damage, 5, EffectTargetTag.Player), EnemyActionTarget.Player, 6),
                                       (Act(InstantActionKind.Damage, 5, EffectTargetTag.Player), EnemyActionTarget.Player, 6));
        Spawn(e);
        MakeCard(6, 0).Play(null);        // враг бьёт игрока в 6: 25
        Assert.AreEqual(25, player.CurrentHP);
        RewindCard(3, 1, 1).Play(null);   // T=7, R=4, ReplayUntil=7; ход в 6 ждёт повтора
        Assert.AreEqual(4, cm.CurrentTime);

        var stagger = MakeCard(1, 0);     // оглушение: ход врага 6 → 9
        stagger.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Stagger, stacks = 3, targetTag = EffectTargetTag.Enemy });
        stagger.Play(e);
        Assert.AreEqual(9, e.NextTime);

        var delay = MakeCard(4, 4);       // эффект в 9: ещё одно оглушение на 1
        delay.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Stagger, stacks = 1, targetTag = EffectTargetTag.Enemy });
        delay.Play(e);                    // время 9: карта в 9 первой — враг уходит на 10 и не бьёт
        Assert.AreEqual(10, e.NextTime);
        Assert.AreEqual(25, player.CurrentHP);
    }

    // Повтор с параметрами первого раза: Сила, полученная в интервале (игрок не откатывается), не увеличивает повторный удар
    [Test]
    public void U_Replay_UsesNumbersOfFirstPass()
    {
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        Dmg(8, 1, 0).Play(enemy);                       // 8 урона, Силы нет
        var embark = MakeCard(1, 0);
        embark.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Strength, stacks = 3, targetTag = EffectTargetTag.Player });
        embark.Play(null);
        Assert.AreEqual(3, player.GetStacks(StatusEffectType.Strength));
        int afterFirstPass = enemy.CurrentHP;
        Assert.AreEqual(42, afterFirstPass);

        RewindCard(3, 1, 1).Play(null);                 // T=3, R=0: Jab повторяется
        Assert.AreEqual(0, cm.CurrentTime);
        Assert.AreEqual(afterFirstPass, enemy.CurrentHP, "повторный удар — 8, а не 11");
        Assert.AreEqual(3, player.GetStacks(StatusEffectType.Strength), "игрок не откатывается");
    }

    // ---------- Момент эффекта (EffectTime), порядок после оживления, Откат один раз на карту ----------

    Card WithEffect(Card card, StatusEffectType type, float stacks, EffectTargetTag tag)
    {
        card.data.appliedEffects.Add(new AppliedEffectEntry { statusType = type, stacks = stacks, targetTag = tag });
        return card;
    }

    PeriodicEffectEvent TickOf(Combatant owner) => cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Single(p => p.Owner == owner && p.Priority != 2); // без тиков затухания блока (группа 2)
    PeriodicEffectEvent DecayOf(Combatant owner) => cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Single(p => p.Owner == owner && p.Priority == 2);

    [Test]
    public void F2_WeakFromCastCost3_FirstTickCountedFromEffectMoment()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        WithEffect(MakeCard(3, 0), StatusEffectType.Weak, 1, EffectTargetTag.Enemy).Play(enemy); // эффект в 0, каст 0→3
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Weak), "стак снят в 3, а не в 6");
    }

    [Test]
    public void H2_EnemyWeakOnPlayerDuringCast_TickFromEnemyMoment()
    {
        var enemy = Make<Enemy>();
        enemy.maxHP = 30; enemy.combatManager = cm; enemy.Init(); enemy.player = player;
        var pattern = ScriptableObject.CreateInstance<EnemyPatternData>();
        created.Add(pattern);
        pattern.steps.Add(new EnemyActionStep
        {
            stepNameKey = "enemy.test.attack_player", delaySeconds = 2, target = EnemyActionTarget.Player,
            appliedEffects = new List<AppliedEffectEntry> { new AppliedEffectEntry { statusType = StatusEffectType.Weak, stacks = 1, targetTag = EffectTargetTag.Player } }
        });
        pattern.steps.Add(new EnemyActionStep { stepNameKey = "enemy.test.attack_player", delaySeconds = 99, target = EnemyActionTarget.Self });
        enemy.pattern = pattern;
        enemy.ScheduleFirstAction();
        Spawn(enemy);

        MakeCard(3, 0).Play(null);        // каст 0→3, враг накладывает Слабость в 2
        Assert.AreEqual(1, player.GetStacks(StatusEffectType.Weak));
        Assert.AreEqual(5, TickOf(player).NextTime);
        Advance(1); Assert.AreEqual(1, player.GetStacks(StatusEffectType.Weak));   // 4
        Advance(1); Assert.AreEqual(0, player.GetStacks(StatusEffectType.Weak));   // 5
    }

    [Test]
    public void C2_Replay_WeakTickSameMomentAsFirstPass()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        Advance(2);
        WithEffect(MakeCard(2, 0), StatusEffectType.Weak, 1, EffectTargetTag.Enemy).Play(enemy); // эффект в 2, каст 2→4
        Assert.AreEqual(5, TickOf(enemy).NextTime);

        RewindCard(3, 2, 1).Play(null);   // T=5, R=2, S=3: время 6→3 (не 4)
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Weak));
        Assert.AreEqual(5, TickOf(enemy).NextTime, "тик повтора — от момента 2, а не от времени разбора");
        Advance(2);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Weak));
    }

    [Test]
    public void W_RevivedEnemy_KeepsItsPlaceInTurnOrder()
    {
        var a = MakeEnemy(5, 4, 1);
        var b = MakeEnemy(30, 4, 1);
        Spawn(a, b);
        Dmg(10, 2, 2).Play(a);            // A погибает в 2
        Assert.AreEqual(0, a.CurrentHP);
        RewindCard(3, 1, 1).Play(null);   // T=3, R=0: A жив, повтор удара в 2 ждёт; ReplayUntil=3
        Assert.AreEqual(0, cm.CurrentTime);
        Assert.AreEqual(5, a.CurrentHP);
        MakeCard(1, 0, Act(InstantActionKind.Block, 20, EffectTargetTag.Enemy)).Play(a); // блок, чтобы повтор не убил A
        Advance(3);                       // время 4: повтор в 2, затем общий момент 4
        Assert.Greater(a.CurrentHP, 0);
        var order = cm.Journal.Where(e => e.Time == 4 && e.Source is Enemy).Select(e => e.Source).Distinct().ToList();
        CollectionAssert.AreEqual(new IScheduledEvent[] { a, b }, order, "A ходит раньше B");
    }

    [Test]
    public void S2_StaggerAfterRewind_DisplayNeverAboveApplied()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        Advance(2);
        WithEffect(MakeCard(3, 0), StatusEffectType.Stagger, 3, EffectTargetTag.Enemy).Play(enemy); // эффект в 2, каст 2→5
        Assert.LessOrEqual(enemy.StaggerDisplay, 3);
        RewindCard(3, 1, 1).Play(null);   // T=6, R=3, S=3: время 3, Пошатывание (момент 2) не отменено
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.LessOrEqual(enemy.StaggerDisplay, 3);
        Assert.AreEqual(2, enemy.StaggerDisplay);
    }

    [Test]
    public void E_RewindOncePerCard_AndLast()
    {
        var e1 = MakeEnemy(30, 99, 1);
        var e2 = MakeEnemy(30, 99, 1);
        Spawn(e1, e2);
        Advance(5);
        var card = MakeCard(1, 1, Act(InstantActionKind.Rewind, 3, EffectTargetTag.AllEnemies), Act(InstantActionKind.Damage, 5, EffectTargetTag.Enemy));
        card.Play(e1);                    // T=6, R=3: время 6→3 один раз; урон 5 — до переноса
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.AreEqual(25, e1.CurrentHP);
        Assert.AreEqual(30, e2.CurrentHP);

        RewindCard(3, 1, 1).Play(null);   // второй Откат, покрывающий тот же момент
        Assert.AreEqual(25, e1.CurrentHP, "урон карты Отката остаётся и не повторяется: её записи уже вычеркнуты из журнала");
    }

    // ---------- A2: порядок одновременных событий ----------

    // События в порядке срабатывания (по журналу: каждое событие здесь оставляет запись)
    // Журнал без записей тиков затухания блока (группа 2)
    List<UndoEntry> ActionJournal() => cm.Journal.Where(e => e.Source.Priority != 2).ToList();

    List<IScheduledEvent> FiredOrder() => cm.Journal.Select(e => e.Source).Where(s => s.Priority != 2).Distinct().ToList(); // без тиков затухания блока

    // Событие-«карта» на момент t: пишет в лог и видит стаки Слабости игрока
    OneShotEvent LogCard(int t, List<string> log)
    {
        var ev = new OneShotEvent(cm, t, 0, _ => log.Add("card weak=" + player.GetStacks(StatusEffectType.Weak)));
        cm.RegisterScheduledEvent(ev);
        return ev;
    }

    [Test]
    public void A2_1_EnemyWeakOnPlayer_TickBeforeLaterCard()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        var log = new List<string>();
        player.AddEffectStacks(StatusEffectType.Weak, 1, enemy); // тик в 3
        LogCard(3, log);                                         // создана позже тика
        Advance(3);
        CollectionAssert.AreEqual(new[] { "card weak=0" }, log);
    }

    [Test]
    public void A2_2_CardCreatedBeforeEffect_CardBeforeTick()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        var log = new List<string>();
        LogCard(3, log);
        player.AddEffectStacks(StatusEffectType.Weak, 1, enemy);
        Advance(3);
        CollectionAssert.AreEqual(new[] { "card weak=1" }, log);
    }

    [Test]
    public void A2_3_PlayerWeakOnEnemy_TickBeforeEnemyAction()
    {
        var a = MakeEnemy(30, 3, 10);
        Spawn(a);
        a.AddEffectStacks(StatusEffectType.Weak, 1, player);
        Advance(3);
        var order = FiredOrder();
        Assert.AreEqual(2, order.Count);
        Assert.AreSame(TickOf(a), order[0]);
        Assert.AreSame(a, order[1]);
        Assert.AreEqual(20, player.CurrentHP, "тик снял Слабость раньше удара");
    }

    [Test]
    public void A2_4_EnemyWeakOnLeftEnemy_ActionTickAction()
    {
        var a = MakeEnemy(30, 3, 10);
        var b = MakeEnemy(30, 3, 10);
        Spawn(a, b);
        a.AddEffectStacks(StatusEffectType.Weak, 1, b);
        Advance(3);
        var order = FiredOrder();
        Assert.AreEqual(3, order.Count);
        Assert.AreSame(a, order[0]);
        Assert.AreSame(TickOf(a), order[1]);
        Assert.AreSame(b, order[2]);
    }

    [Test]
    public void A2_5_EnemiesLeftToRight_RegardlessOfRegistrationOrder()
    {
        var a = MakeEnemy(30, 3, 1);
        var b = MakeEnemy(30, 3, 1);
        cm.RegisterScheduledEvent(b); // правый зарегистрирован раньше
        cm.RegisterScheduledEvent(a);
        cm.RegisterEnemies(new List<Enemy> { a, b });
        Advance(3);
        var order = FiredOrder();
        Assert.AreSame(a, order[0]);
        Assert.AreSame(b, order[1]);
    }

    [Test]
    public void A2_6_CardWeakOnEnemyAtItsAttackMoment_AttackWeakened()
    {
        var enemy = MakeEnemy(30, 3, 10);
        Spawn(enemy);
        WithEffect(MakeCard(3, 3), StatusEffectType.Weak, 1, EffectTargetTag.Enemy).Play(enemy); // эффект в 3, атака врага в 3
        Assert.AreEqual(25, player.CurrentHP, "урон 10 ослаблен вдвое");
    }

    [Test]
    public void A2_7_DrawTimerBeforeCardAtSameMoment()
    {
        var log = new List<string>();
        int t = HandManager.DrawIntervalSeconds;
        cm.RegisterScheduledEvent(new PeriodicEffectEvent(0, t, () => log.Add("draw")));
        cm.RegisterScheduledEvent(new OneShotEvent(cm, t, 0, _ => log.Add("card")));
        Advance(t);
        CollectionAssert.AreEqual(new[] { "draw", "card" }, log);
    }

    // Первый проход: Уязвимость от карты игрока в 0 (тик в 3), урон 10 карты, сыгранной в 2 (эффект в 3). Затем Откат на 3 с.
    Enemy VulnScenarioFirstPass(out int hpAfterFirstPass, out PeriodicEffectEvent firstTick)
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        WithEffect(MakeCard(0, 0), StatusEffectType.Vulnerable, 1, EffectTargetTag.Enemy).Play(enemy);
        firstTick = TickOf(enemy);
        Advance(2);
        Dmg(10, 1, 1).Play(enemy);
        hpAfterFirstPass = enemy.CurrentHP;
        return enemy;
    }

    [Test]
    public void A2_8_ReplayKeepsTickBeforeCard_NoVulnerableOnReplayedHit()
    {
        var enemy = VulnScenarioFirstPass(out int hp, out _);
        Assert.AreEqual(20, hp, "в первом проходе тик раньше карты: без ×1.5");
        RewindCard(3, 0, 0).Play(null);
        Assert.AreEqual(0, cm.CurrentTime);
        Advance(3);
        Assert.AreEqual(hp, enemy.CurrentHP);
    }

    [Test]
    public void A2_9_ReplayReusesSameTickObject()
    {
        var enemy = VulnScenarioFirstPass(out _, out var first);
        RewindCard(3, 0, 0).Play(null);
        Advance(3);
        Assert.AreSame(first, TickOf(enemy));
    }

    [Test]
    public void A2_10_AfterReplayUntil_NewTickObject()
    {
        var enemy = MakeEnemyWithSteps(30, (Act(InstantActionKind.Block, 1, EffectTargetTag.Player), EnemyActionTarget.Self, 2));
        enemy.pattern.steps[0].appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Weak, stacks = 1 });
        Spawn(enemy);
        Advance(2);                       // действие врага в 2: Слабость на себя, тик в 5
        var first = TickOf(enemy);
        RewindCard(3, 0, 0).Play(null);   // R=0, ReplayUntil=2
        Assert.AreEqual(0, cm.CurrentTime);
        WithEffect(MakeCard(0, 0), StatusEffectType.Stagger, 3, EffectTargetTag.Enemy).Play(enemy); // действие врага уходит за ReplayUntil
        Assert.Greater(enemy.NextTime, cm.ReplayUntil);
        Advance(enemy.NextTime);
        Assert.AreNotSame(first, TickOf(enemy), "момент наложения позже ReplayUntil — переиспользования нет");
    }

    [Test]
    public void A2_11_DifferentApplier_NewTickInEnemyGroup()
    {
        var a = MakeEnemy(30, 99, 1);
        var b = MakeEnemy(30, 99, 1);
        Spawn(a, b);
        Advance(1);
        WithEffect(MakeCard(0, 0), StatusEffectType.Vulnerable, 1, EffectTargetTag.Enemy).Play(a); // игрок, эффект в 1
        var first = TickOf(a);
        Assert.AreEqual(0, first.Priority);
        Advance(1);
        RewindCard(3, 0, 0).Play(null);   // R=0, ReplayUntil=2
        a.AddEffectStacks(StatusEffectType.Vulnerable, 1, b); // в повторе первым накладывает враг B
        var second = TickOf(a);
        Assert.AreNotSame(first, second);
        Assert.AreEqual(1, second.Priority);
        Advance(1);                        // повтор карты игрока в 1: тик остаётся у врага
        Assert.AreSame(second, TickOf(a));
    }

    // ---------- A5: формулы урона, мёртвая цель, затухание блока ----------

    void NoDecay() { cm.blockDecayBase = 0; cm.blockDecayPerStep = 0; }

    Enemy WeakPlayerVulnerableEnemy(int hp = 50)
    {
        var enemy = MakeEnemy(hp, 99, 1);
        Spawn(enemy);
        player.AddEffectStacks(StatusEffectType.Weak, 1);
        enemy.AddEffectStacks(StatusEffectType.Vulnerable, 5);
        return enemy;
    }

    [Test]
    public void F1_Damage_SingleRoundingAtEnd()
    {
        var enemy = WeakPlayerVulnerableEnemy();
        Dmg(7, 0, 0).Play(enemy);         // (7 × 0.5) × 1.5 = 5.25 → 5, а не 3 → 4
        Assert.AreEqual(45, enemy.CurrentHP);
    }

    [Test]
    public void F2_Preview_MatchesRealDamage()
    {
        var enemy = WeakPlayerVulnerableEnemy();
        var action = Act(InstantActionKind.Damage, 7, EffectTargetTag.Enemy);
        Assert.AreEqual(5, action.ComputePreviewAmount(player, enemy));
        Assert.AreEqual(3, action.ComputePreviewAmount(player, null), "без цели — исходящее, округлённое вниз");
    }

    [Test]
    public void F3_NegativeStrength_DamageZero_NoHeal()
    {
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        enemy.SetCurrentHP(40);
        player.AddEffectStacks(StatusEffectType.Strength, -5);
        Dmg(3, 0, 0).Play(enemy);
        Assert.AreEqual(40, enemy.CurrentHP);
        Assert.AreEqual(0, Act(InstantActionKind.Damage, 3, EffectTargetTag.Enemy).ComputePreviewAmount(player, enemy));
    }

    [Test]
    public void F4_GainBlockOnDeadTarget_NothingHappensNothingLogged()
    {
        var dead = MakeEnemy(5, 99, 1); // не регистрируется: своего тика затухания нет
        dead.SetCurrentHP(0);
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 1, 0, _ => dead.GainBlock(5)));
        Advance(1);
        Assert.AreEqual(0, dead.CurrentBlock);
        Assert.AreEqual(0, cm.Journal.Count(e => e.Target == dead));
    }

    [Test]
    public void F5_BlockDecay_FormulaAndTicks()
    {
        Assert.AreEqual(2, cm.BlockDecayAt(1));
        Assert.AreEqual(2, cm.BlockDecayAt(9));
        Assert.AreEqual(3, cm.BlockDecayAt(10));
        Assert.AreEqual(4, cm.BlockDecayAt(25));

        player.GainBlock(100);
        Advance(1); Assert.AreEqual(98, player.CurrentBlock);
        Advance(1); Assert.AreEqual(96, player.CurrentBlock);
        Advance(7); Assert.AreEqual(82, player.CurrentBlock);   // моменты 3..9 по 2
        Advance(1); Assert.AreEqual(79, player.CurrentBlock);   // момент 10 — 3
        Advance(14); Assert.AreEqual(32, player.CurrentBlock);  // 11..19 по 3, 20..24 по 4
        Advance(1); Assert.AreEqual(28, player.CurrentBlock);   // момент 25 — 4

        var other = MakeEnemy(30, 99, 1);
        other.GainBlock(1);
        Spawn(other);
        Advance(1);
        Assert.AreEqual(0, other.CurrentBlock, "ниже нуля не уходит");
    }

    [Test]
    public void F6_DecayAfterAllEventsOfMoment()
    {
        var enemy = MakeEnemy(30, 4, 5);
        Spawn(enemy);
        MakeCard(4, 4, Act(InstantActionKind.Block, 5, EffectTargetTag.Player)).Play(null); // блок и удар врага — в моменте 4
        Assert.AreEqual(4, cm.CurrentTime);
        Assert.AreEqual(30, player.CurrentHP, "удар полностью поглощён");
        Assert.AreEqual(0, player.CurrentBlock);
    }

    [Test]
    public void F6b_DecayHitsLeftoverBlockAfterEnemyHit()
    {
        var enemy = MakeEnemy(30, 4, 3);
        Spawn(enemy);
        MakeCard(4, 4, Act(InstantActionKind.Block, 5, EffectTargetTag.Player)).Play(null);
        Assert.AreEqual(30, player.CurrentHP);
        Assert.AreEqual(0, player.CurrentBlock, "5 − 3 = 2, затем затухание 2 в конце момента 4");
    }

    [Test]
    public void F7_DecaySettingsAreData()
    {
        NoDecay();
        player.GainBlock(10);
        Advance(30);
        Assert.AreEqual(10, player.CurrentBlock);

        cm.blockDecayBase = 5;
        Advance(1); Assert.AreEqual(5, player.CurrentBlock);
        Advance(1); Assert.AreEqual(0, player.CurrentBlock);
    }

    [Test]
    public void F8_Rewind_EnemyDecayRewoundPlayerDecayNot()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(20);
        player.GainBlock(20);
        Advance(3);
        Assert.AreEqual(14, enemy.CurrentBlock);
        Assert.AreEqual(14, player.CurrentBlock);

        RewindCard(2, 0, 0).Play(null);   // T=3, R=1, S=2
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.AreEqual(18, enemy.CurrentBlock, "блок врага откачен до 20, затухание момента 1 уже повторено в том же разборе");
        Assert.AreEqual(14, player.CurrentBlock, "блок игрока не восстанавливается");
        Assert.AreEqual(2, DecayOf(player).NextTime, "следующее затухание игрока — через секунду по его часам");
        Assert.AreEqual(2, DecayOf(enemy).NextTime);

        Advance(2);
        Assert.AreEqual(14, enemy.CurrentBlock, "повтор затухает так же, как первый проход");
        Assert.AreEqual(10, player.CurrentBlock);
    }

    [Test]
    public void F9_Environment_AfterReplayedEventsAndNewOnes()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(20);
        Advance(3);
        RewindCard(2, 0, 0).Play(null);   // R=1, ReplayUntil=3: затухания врага в 1..3 ждут повтора
        var seen = new List<int>();
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 2, 0, _ => seen.Add(enemy.CurrentBlock)));
        Assert.AreEqual(18, enemy.CurrentBlock, "момент 1 повторён сразу");
        Advance(1);                        // момент 2: новое событие без номера повтора, затем затухание
        CollectionAssert.AreEqual(new[] { 18 }, seen, "затухание в моменте 2 — после нового события, хотя оно повтор");
        Assert.AreEqual(16, enemy.CurrentBlock);
    }

    [Test]
    public void F10_Replay_FractionalOutgoingNotRoundedTwice()
    {
        var enemy = WeakPlayerVulnerableEnemy();
        Dmg(7, 1, 0).Play(enemy);         // исходящее 3.5, итог 5
        Assert.AreEqual(45, enemy.CurrentHP);
        player.RemoveStacks(StatusEffectType.Weak, 1);

        RewindCard(1, 0, 0).Play(null);   // T=1, R=0: повтор удара с числом первого прохода
        Assert.AreEqual(0, cm.CurrentTime);
        Assert.AreEqual(45, enemy.CurrentHP, "повтор снова 5, а не 10");
    }
}
