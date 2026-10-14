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
        cm.SetPlayer(player);
        cm.ResetForNewCombat();
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
        var card = MakeCard(2, 1, Act(InstantActionKind.Block, 5, EffectTargetTag.Player));
        card.Play(null);
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.AreEqual(5, player.CurrentBlock);
        Assert.IsTrue(cm.Journal.Count > 0 && cm.Journal.All(e => e.Time == 1), "эффект сработал на отметке 1, а не 2");
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
        var enemy = MakeEnemy(5, 50, 1);
        Spawn(enemy);
        enemy.GainBlock(3);
        enemy.AddEffectStacks(StatusEffectType.Weak, 1);
        var tick = cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Single(p => p.Owner == enemy);

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
        Assert.IsTrue(cm.Journal.Count > 0);
        Assert.IsTrue(cm.Journal.All(e => e.Time < 2));
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
        Assert.AreEqual(1, cm.Journal.Count, "только запись о самом ходе врага");
        Assert.AreSame(enemy, cm.Journal[0].Source);
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
}
