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
        Spawn(enemy, MakeEnemy(30, 50, 1)); // второй враг жив: смерть первого не заканчивает бой, откат возможен
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
        stagger.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = 3, targetTag = EffectTargetTag.Enemy });
        stagger.Play(e);
        Assert.AreEqual(9, e.NextTime);

        var delay = MakeCard(4, 4);       // эффект в 9: ещё одно оглушение на 1
        delay.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = 1, targetTag = EffectTargetTag.Enemy });
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
    public void S2_StunAfterRewind_DisplayNeverAboveApplied()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        Advance(2);
        WithEffect(MakeCard(3, 0), StatusEffectType.Stun, 3, EffectTargetTag.Enemy).Play(enemy); // эффект в 2, каст 2→5
        Assert.LessOrEqual(enemy.StunDisplay, 3);
        RewindCard(3, 1, 1).Play(null);   // T=6, R=3, S=3: время 3, Оглушение (момент 2) не отменено
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.LessOrEqual(enemy.StunDisplay, 3);
        Assert.AreEqual(2, enemy.StunDisplay);
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
        var tick = TickOf(a); // после срабатывания тик снимается (стаки кончились)
        Advance(3);
        var order = FiredOrder();
        Assert.AreEqual(2, order.Count);
        Assert.AreSame(tick, order[0]);
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
        var tick = TickOf(a); // после срабатывания тик снимается (стаки кончились)
        Advance(3);
        var order = FiredOrder();
        Assert.AreEqual(3, order.Count);
        Assert.AreSame(a, order[0]);
        Assert.AreSame(tick, order[1]);
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
        Assert.AreSame(first, TickOf(enemy)); // до срабатывания: после него тик снимается
        Advance(3);
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
        WithEffect(MakeCard(0, 0), StatusEffectType.Stun, 3, EffectTargetTag.Enemy).Play(enemy); // действие врага уходит за ReplayUntil
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

    // ---------- A6–A8: система эффектов ----------

    bool HasTick(Combatant c, StatusEffectType t) => cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Any(p => p.Owner == c && p.EffectType == t);
    PeriodicEffectEvent EffectTick(Combatant c, StatusEffectType t) => cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Single(p => p.Owner == c && p.EffectType == t);

    [Test]
    public void E1_ZeroStacks_TickRemoved_ReapplyStartsNewTick()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.AddEffectStacks(StatusEffectType.Weak, 1);
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Weak));
        Advance(3);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Weak));
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Weak), "тик снят из очереди");
        Advance(2);                       // время 5
        enemy.AddEffectStacks(StatusEffectType.Weak, 1);
        Assert.AreEqual(8, EffectTick(enemy, StatusEffectType.Weak).NextTime);
    }

    [Test]
    public void E2_WeakIsPresenceNotAmount()
    {
        var other = Make<Player>();
        other.combatManager = cm; other.Init();
        player.AddEffectStacks(StatusEffectType.Weak, 1);
        other.AddEffectStacks(StatusEffectType.Weak, 3);
        Assert.AreEqual(5, player.CalculateOutgoingDamage(10));
        Assert.AreEqual(5, other.CalculateOutgoingDamage(10));
    }

    [Test]
    public void E3_Regen3_Heals3_2_1_ThenTickRemoved()
    {
        player.SetCurrentHP(10);
        player.AddEffectStacks(StatusEffectType.Regen, 3);
        Advance(1); Assert.AreEqual(13, player.CurrentHP);
        Advance(1); Assert.AreEqual(15, player.CurrentHP);
        Advance(1); Assert.AreEqual(16, player.CurrentHP);
        Assert.AreEqual(0, player.GetStacks(StatusEffectType.Regen));
        Assert.IsFalse(HasTick(player, StatusEffectType.Regen));
        Advance(2); Assert.AreEqual(16, player.CurrentHP);
    }

    [Test]
    public void E4_Poison_IgnoresBlockAndVulnerable_DecaysByOne()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(10);
        enemy.AddEffectStacks(StatusEffectType.Vulnerable, 9);
        enemy.AddEffectStacks(StatusEffectType.Poison, 3);
        Advance(1); Assert.AreEqual(27, enemy.CurrentHP);
        Advance(1); Assert.AreEqual(25, enemy.CurrentHP);
        Advance(1); Assert.AreEqual(24, enemy.CurrentHP);
        Assert.AreEqual(10, enemy.CurrentBlock);
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Poison));
    }

    [Test]
    public void E5_Bleeding_BlockAbsorbs_NoVulnerable_DoesNotDecay()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(2);
        enemy.AddEffectStacks(StatusEffectType.Vulnerable, 9);
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 4);
        Advance(3);
        Assert.AreEqual(28, enemy.CurrentHP, "блок 2 поглотил часть удара 4");
        Assert.AreEqual(0, enemy.CurrentBlock);
        Assert.AreEqual(4, enemy.GetStacks(StatusEffectType.Bleeding));
        Advance(3);
        Assert.AreEqual(24, enemy.CurrentHP, "−4 без ×1.5");
    }

    [Test]
    public void E6_HealRemovesBleedingByActualHealed()
    {
        player.AddEffectStacks(StatusEffectType.Bleeding, 5);
        player.SetCurrentHP(20);
        player.Heal(3);
        Assert.AreEqual(2, player.GetStacks(StatusEffectType.Bleeding));
        player.SetCurrentHP(30);
        player.Heal(3);
        Assert.AreEqual(2, player.GetStacks(StatusEffectType.Bleeding), "на полном HP лечение 0");
        player.SetCurrentHP(29);
        player.Heal(3);
        Assert.AreEqual(1, player.GetStacks(StatusEffectType.Bleeding), "вылечено 1");
    }

    [Test]
    public void E7_RegenBeforePoisonAndBleedingOfSameFighter()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        enemy.AddEffectStacks(StatusEffectType.Poison, 5, player);     // группа 0, создан первым
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 20, player);
        enemy.AddEffectStacks(StatusEffectType.Regen, 5, enemy);       // группа 1
        Advance(3);
        var order = cm.Journal.Where(e => e.Time == 3 && e.Source is PeriodicEffectEvent p && p.Owner == enemy)
                              .Select(e => ((PeriodicEffectEvent)e.Source).EffectType).Where(t => t != null).Distinct().ToList();
        Assert.AreEqual(StatusEffectType.Regen, order[0]);
        CollectionAssert.Contains(order, StatusEffectType.Poison);
        CollectionAssert.Contains(order, StatusEffectType.Bleeding);
    }

    [Test]
    public void E8_RegenRemovesBleedingBeforeItsHit()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        enemy.SetCurrentHP(20);
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 3);            // тик в 3
        Advance(2);
        enemy.AddEffectStacks(StatusEffectType.Regen, 2);               // первый тик в 3
        Advance(1);
        Assert.AreEqual(21, enemy.CurrentHP, "лечение 2, затем удар 1");
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Bleeding));
    }

    [Test]
    public void E9_Invulnerable_BlocksDamageAndHpLossKeepsBlock_HealAndEffectsWork()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(5);
        enemy.AddEffectStacks(StatusEffectType.Invulnerable, 1);
        enemy.TakeDamage(5);
        enemy.LoseHP(3);
        Assert.AreEqual(30, enemy.CurrentHP);
        Assert.AreEqual(5, enemy.CurrentBlock, "блок не тратится");
        enemy.SetCurrentHP(20);
        enemy.Heal(4);
        Assert.AreEqual(24, enemy.CurrentHP, "лечение проходит");
        enemy.AddEffectStacks(StatusEffectType.Weak, 1);
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Weak), "эффекты накладываются");
        Advance(1);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Invulnerable));
        enemy.TakeDamage(5);
        Assert.AreEqual(0, enemy.CurrentBlock);
        enemy.TakeDamage(4);
        Assert.AreEqual(20, enemy.CurrentHP, "урон снова проходит");
    }

    [Test]
    public void E9b_Invulnerable_BlocksPoisonAndBleedingTicks()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.GainBlock(5);
        enemy.AddEffectStacks(StatusEffectType.Invulnerable, 4);
        enemy.AddEffectStacks(StatusEffectType.Poison, 2);
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 2);
        Advance(3);                       // Отравление в 1, 2; Кровотечение в 3; Неуязвимость ещё 1
        Assert.AreEqual(30, enemy.CurrentHP);
        Assert.AreEqual(5, enemy.CurrentBlock);
    }

    [Test]
    public void E10_StunOnEnemy_ShiftsNextAction()
    {
        var enemy = MakeEnemy(30, 5, 1);
        Spawn(enemy);
        Assert.AreEqual(5, enemy.NextTime);
        new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = 3, targetTag = EffectTargetTag.Enemy }.Apply(cm, player, enemy);
        Assert.AreEqual(8, enemy.NextTime);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Stun), "в словарь стаков не попадает");
    }

    // Враг: в 2 оглушает игрока на 3, в 4 бьёт на 5
    Enemy StunnerEnemy()
    {
        var enemy = Make<Enemy>();
        enemy.maxHP = 30; enemy.combatManager = cm; enemy.Init(); enemy.player = player;
        var pattern = ScriptableObject.CreateInstance<EnemyPatternData>();
        created.Add(pattern);
        pattern.steps.Add(new EnemyActionStep
        {
            stepNameKey = "enemy.test.attack_player", delaySeconds = 2, target = EnemyActionTarget.Player,
            appliedEffects = new List<AppliedEffectEntry> { new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = 3, targetTag = EffectTargetTag.Player } }
        });
        pattern.steps.Add(new EnemyActionStep
        {
            stepNameKey = "enemy.test.attack_player", delaySeconds = 2, target = EnemyActionTarget.Player,
            instantActions = new List<InstantActionEntry> { Act(InstantActionKind.Damage, 5, EffectTargetTag.Player) }
        });
        enemy.pattern = pattern;
        enemy.ScheduleFirstAction();
        return enemy;
    }

    [Test]
    public void E11_StunOnPlayer_TimeJumpsEnemiesActDrawRuns()
    {
        var enemy = StunnerEnemy();
        Spawn(enemy);
        var draws = new List<int>();
        AddDrawTimer(draws);
        MakeCard(2, 0).Play(null);        // каст 0→2, в 2 оглушение на 3
        Assert.AreEqual(5, cm.CurrentTime);
        Assert.AreEqual(25, player.CurrentHP, "удар врага в 4 прошёл");
        CollectionAssert.AreEqual(new[] { 2, 4 }, draws, "добор в 4 сработал");
    }

    [Test]
    public void E12a_Rewind_DotTicksUndoneAndReplayed_ZeroedTickReturns()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        enemy.AddEffectStacks(StatusEffectType.Poison, 2);
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 2);
        enemy.AddEffectStacks(StatusEffectType.Regen, 1);
        enemy.SetCurrentHP(40);
        Advance(3);                       // Отравление −2, −1; Регенерация +1; Кровотечение −2 (в 3 лечение раньше удара не мешает)
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Poison));
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Regen));
        int hpAfter = enemy.CurrentHP;

        RewindCard(3, 0, 0).Play(null);   // R=0: всё откатывается
        Assert.AreEqual(40, enemy.CurrentHP);
        Assert.AreEqual(2, enemy.GetStacks(StatusEffectType.Poison));
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Regen));
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Poison), "снятый при обнулении тик вернулся");
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Regen));
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Bleeding));

        Advance(3);
        Assert.AreEqual(hpAfter, enemy.CurrentHP, "повтор даёт то же самое");
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Poison));
    }

    [Test]
    public void E12b_Rewind_EnemyStunOnPlayerNotReplayed()
    {
        var enemy = StunnerEnemy();
        Spawn(enemy);
        MakeCard(2, 0).Play(null);        // время 5, HP 25
        RewindCard(4, 0, 0).Play(null);   // T=5, R=1
        Assert.AreEqual(1, cm.CurrentTime);
        Advance(4);                       // повтор действий врага в 2 и 4: на игрока не действует
        Assert.AreEqual(5, cm.CurrentTime);
        Assert.AreEqual(25, player.CurrentHP);
    }

    [Test]
    public void E13_NewCombat_NoEffectsOrTicksOfPreviousFight()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        player.AddEffectStacks(StatusEffectType.Poison, 5);
        player.AddEffectStacks(StatusEffectType.Weak, 2);
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 3);

        cm.ResetForNewCombat();
        var fresh = Make<Player>();
        fresh.maxHP = 30; fresh.combatManager = cm; fresh.Init();
        cm.SetPlayer(fresh);
        foreach (StatusEffectType t in System.Enum.GetValues(typeof(StatusEffectType)))
            Assert.AreEqual(0, fresh.GetStacks(t), t.ToString());
        Assert.IsFalse(cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Any(p => p.EffectType != null), "тиков прошлого боя нет");
        Advance(5);
        Assert.AreEqual(30, fresh.CurrentHP);
    }

    // ---------- Порядок событий одним ключом ----------

    static string Label(IScheduledEvent s)
    {
        if (s is Enemy e) return e.name;
        if (s is PeriodicEffectEvent p) return (p.Owner == null ? "timer" : p.Owner.name) + ":" + (p.EffectType?.ToString() ?? (p.Priority == 2 ? "decay" : "?"));
        return "card";
    }

    List<string> OrderAt(int time) => cm.Journal.Where(e => e.Time == time).Select(e => Label(e.Source)).Distinct().ToList();

    // Новый бой с новым менеджером и игроком (как SetUp)
    void FreshCombat()
    {
        cm = Make<CombatManager>();
        player = Make<Player>();
        player.maxHP = 30;
        player.combatManager = cm;
        player.Init();
        cm.ResetForNewCombat();
        cm.SetPlayer(player);
    }

    // Тройка из примера на одном враге в момент 1: R — Регенерация, наложенная врагом на себя (группа 1); P — Отравление от игрока (группа 0);
    // A — действие врага (группа 1, создано раньше R в порядке 0,1,2). order — порядок регистрации [A, R, P].
    Enemy RegisterTriple(int[] order)
    {
        var enemy = MakeEnemy(50, 1, 1);
        enemy.name = "E";
        foreach (int what in order)
        {
            if (what == 0) cm.RegisterScheduledEvent(enemy);
            else if (what == 1) enemy.AddEffectStacks(StatusEffectType.Regen, 5, enemy);
            else enemy.AddEffectStacks(StatusEffectType.Poison, 5, player);
        }
        enemy.OnDeath += () => cm.UnregisterScheduledEvent(enemy);
        cm.RegisterEnemies(new List<Enemy> { enemy });
        return enemy;
    }

    static IEnumerable<int[]> Permutations3()
    {
        int[][] all = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
        return all;
    }

    [Test]
    public void C1_RegenPoisonAction_SameOrderForAllRegistrationOrders()
    {
        foreach (var order in Permutations3())
        {
            FreshCombat();
            RegisterTriple(order);
            Advance(1);
            var fired = OrderAt(1).Where(l => l != "Player:decay" && l != "E:decay").ToList();
            CollectionAssert.AreEqual(new[] { "E:Regen", "E:Poison", "E" }, fired, "порядок регистрации " + string.Join(",", order));
        }
    }

    [Test]
    public void C2_RegenWithoutDamageOverTime_StaysAtA2Place()
    {
        var enemy = MakeEnemy(50, 99, 1);
        enemy.name = "E";
        Spawn(enemy);
        var log = new List<string>();
        enemy.AddEffectStacks(StatusEffectType.Regen, 3, enemy);              // группа 1
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 1, 0, _ => log.Add("card"))); // группа 0, создана позже
        Advance(1);
        CollectionAssert.AreEqual(new[] { "card", "E:Regen" }, OrderAt(1).Where(l => !l.EndsWith(":decay")).ToList());
    }

    [Test]
    public void C3_RegenRightBeforeBleeding_RestByA2()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 3, 1);
        enemy.name = "E";
        Spawn(enemy);                                                   // действие врага A в 3 — создано первым
        enemy.AddEffectStacks(StatusEffectType.Bleeding, 5, enemy);     // тик в 3
        enemy.AddEffectStacks(StatusEffectType.Regen, 5, enemy);        // тики в 1, 2, 3
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 3, 0, _ => { })); // карта игрока в 3
        Advance(3);
        var at3 = OrderAt(3).Where(l => !l.EndsWith(":decay")).ToList();
        CollectionAssert.AreEqual(new[] { "card", "E", "E:Regen", "E:Bleeding" }, at3);
    }

    [Test]
    public void C4_Replay_TripleKeepsOrder()
    {
        FreshCombat();
        RegisterTriple(new[] { 2, 0, 1 });
        Advance(1);
        var first = OrderAt(1).Where(l => !l.EndsWith(":decay")).ToList();
        CollectionAssert.AreEqual(new[] { "E:Regen", "E:Poison", "E" }, first);

        RewindCard(1, 0, 0).Play(null);   // T=1, R=0
        Assert.AreEqual(0, cm.CurrentTime);
        Advance(1);
        var replay = OrderAt(1).Where(l => !l.EndsWith(":decay")).ToList();
        CollectionAssert.AreEqual(first, replay);
    }

    [Test]
    public void C5_RepeatedRegistration_OneDecayTickPerFighter()
    {
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        cm.RegisterEnemies(new List<Enemy> { enemy });  // повторно
        cm.SetPlayer(player);                            // повторно
        Assert.AreEqual(1, cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Count(p => p.Owner == player && p.Priority == 2));
        Assert.AreEqual(1, cm.ScheduledEvents.OfType<PeriodicEffectEvent>().Count(p => p.Owner == enemy && p.Priority == 2));
        player.GainBlock(10);
        enemy.GainBlock(10);
        Advance(1);
        Assert.AreEqual(8, player.CurrentBlock);
        Assert.AreEqual(8, enemy.CurrentBlock);
    }

    [Test]
    public void C6_DeathByPoisonAndByDamage_OneDeathTicksRemovedRealDifferenceUndone()
    {
        // Отравление: HP 2, потеря 5 — в журнале реальная разница 2. Третий враг жив: бой не заканчивается, откат возможен.
        var poisoned = MakeEnemy(2, 99, 1);
        var hit = MakeEnemy(3, 99, 1);
        Spawn(poisoned, hit, MakeEnemy(50, 99, 1));
        int poisonDeaths = 0; poisoned.OnDeath += () => poisonDeaths++;
        int hitDeaths = 0; hit.OnDeath += () => hitDeaths++;
        poisoned.AddEffectStacks(StatusEffectType.Poison, 5);
        poisoned.AddEffectStacks(StatusEffectType.Weak, 1);
        hit.AddEffectStacks(StatusEffectType.Weak, 1);
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 1, 0, _ => hit.TakeDamage(10))); // урон: HP 3, урон 10
        Advance(1);
        Assert.AreEqual(0, poisoned.CurrentHP);
        Assert.AreEqual(1, poisonDeaths);
        Assert.IsFalse(HasTick(poisoned, StatusEffectType.Poison));
        Assert.IsFalse(HasTick(poisoned, StatusEffectType.Weak));
        Assert.AreEqual(0, hit.CurrentHP);
        Assert.AreEqual(1, hitDeaths);
        Assert.IsFalse(HasTick(hit, StatusEffectType.Weak));

        RewindCard(2, 0, 0).Play(null);   // T=1, R=0: оба оживают на реальную разницу
        Assert.AreEqual(2, poisoned.CurrentHP);
        Assert.AreEqual(3, hit.CurrentHP);
        Assert.AreEqual(1, poisonDeaths);
        Assert.AreEqual(1, hitDeaths);
    }

    [Test]
    public void C6b_Invulnerable_BlocksBothLossPaths()
    {
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        int deaths = 0; enemy.OnDeath += () => deaths++;
        enemy.AddEffectStacks(StatusEffectType.Invulnerable, 5);
        enemy.LoseHP(10);
        enemy.TakeDamage(10);
        Assert.AreEqual(5, enemy.CurrentHP);
        Assert.AreEqual(0, deaths);
    }

    // Смешанный момент 3: карта игрока, два врага, Регенерация, Отравление, Кровотечение, Слабость, затухание блока.
    // Порядок регистрации врагов (и их эффектов) меняется, порядок срабатывания — нет.
    List<string> MixedMoment(bool secondEnemyFirst)
    {
        FreshCombat();
        var e1 = MakeEnemy(50, 3, 1); e1.name = "E1";
        var e2 = MakeEnemy(50, 3, 1); e2.name = "E2";
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 3, 0, _ => { }));                // карта игрока в 3
        void Block1() { cm.RegisterScheduledEvent(e1); e1.AddEffectStacks(StatusEffectType.Regen, 5, e1); e1.AddEffectStacks(StatusEffectType.Poison, 5, player); e1.AddEffectStacks(StatusEffectType.Weak, 1, e1); e1.GainBlock(20); }
        void Block2() { cm.RegisterScheduledEvent(e2); e2.AddEffectStacks(StatusEffectType.Bleeding, 5, e2); e2.GainBlock(20); }
        if (secondEnemyFirst) { Block2(); Block1(); } else { Block1(); Block2(); }
        cm.RegisterEnemies(new List<Enemy> { e1, e2 });
        player.GainBlock(20);
        Advance(3);
        return OrderAt(3);
    }

    [Test]
    public void C7_MixedMoment_DeterministicForDifferentRegistrationOrders()
    {
        var a = MixedMoment(false);
        var b = MixedMoment(true);
        CollectionAssert.AreEqual(a, b);
        Assert.AreEqual("card", a[0]);
        Assert.AreEqual("E1:Regen", a[1], "Регенерация встаёт на место Отравления (группа 0, после карты)");
        Assert.AreEqual("E1:Poison", a[2]);
        Assert.AreEqual("Player:decay", a[a.Count - 3], "затухание блока — в конце момента");
    }

    [Test]
    public void C8_RepeatedRegistration_DeathHandlersSubscribedOnce()
    {
        int victories = 0, defeats = 0;
        cm.OnVictory += () => victories++;
        cm.OnDefeat += () => defeats++;

        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        cm.RegisterEnemies(new List<Enemy> { enemy });  // повторно
        enemy.TakeDamage(100);
        Assert.AreEqual(1, victories);

        cm.ResetForNewCombat();                          // следующий бой: второго исхода после победы в этом бою нет
        cm.SetPlayer(player);
        cm.SetPlayer(player);                            // повторно
        player.TakeDamage(100);
        Assert.AreEqual(1, defeats);
    }

    // ---------- Конец боя останавливает разбор ----------

    [Test]
    public void K1_VictoryMidCast_LaterPoisonTickDoesNotKillPlayerSecondOutcome()
    {
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        int victories = 0, defeats = 0;
        cm.OnVictory += () => victories++;
        cm.OnDefeat += () => defeats++;
        player.SetCurrentHP(3);
        player.AddEffectStacks(StatusEffectType.Poison, 5);   // тик в 1 был бы смертельным
        Dmg(10, 2, 0).Play(enemy);                             // победа в 0, каст до 2
        Assert.AreEqual(1, victories);
        Assert.AreEqual(0, defeats);
        Assert.AreEqual(3, player.CurrentHP);
        Assert.AreEqual(2, cm.CurrentTime);
    }

    [Test]
    public void K2_RewindAfterVictoryInSameCast_DoesNothing()
    {
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        var card = MakeCard(2, 2, Act(InstantActionKind.Damage, 10, EffectTargetTag.Enemy), Act(InstantActionKind.Rewind, 2, EffectTargetTag.Player));
        card.Play(enemy);                                      // в 2 урон убивает врага, затем Откат
        Assert.AreEqual(0, enemy.CurrentHP, "враг остаётся мёртвым");
        Assert.AreEqual(-1, cm.ReplayUntil);
        Assert.AreEqual(2, cm.CurrentTime);
        Assert.IsTrue(cm.Journal.Count > 0, "журнал не откатан");
    }

    [Test]
    public void K3_DrawTimerAfterVictoryInSameCast_DoesNotFire()
    {
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        var draws = new List<int>();
        AddDrawTimer(draws);                                   // тики в 2 и 4
        Dmg(10, 5, 0).Play(enemy);                             // победа в 0, каст до 5
        CollectionAssert.IsEmpty(draws);
    }

    [Test]
    public void K4_PlayerDeathAfterVictory_NoDefeat()
    {
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        int victories = 0, defeats = 0;
        cm.OnVictory += () => victories++;
        cm.OnDefeat += () => defeats++;
        enemy.TakeDamage(100);
        Assert.AreEqual(1, victories);
        player.TakeDamage(100);
        Assert.AreEqual(0, player.CurrentHP);
        Assert.AreEqual(0, defeats);
    }

    [Test]
    public void K5_NegativeAddToZero_RemovesTick_RewindReturnsTickAndStacks()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        enemy.AddEffectStacks(StatusEffectType.Weak, 2);
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Weak));
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 1, 0, _ => enemy.AddEffectStacks(StatusEffectType.Weak, -2)));
        Advance(1);
        Assert.AreEqual(0, enemy.GetStacks(StatusEffectType.Weak));
        Assert.IsFalse(HasTick(enemy, StatusEffectType.Weak), "тик снят");

        RewindCard(1, 0, 0).Play(null);   // T=1, R=0
        Assert.AreEqual(2, enemy.GetStacks(StatusEffectType.Weak));
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Weak), "тик вернулся");
    }

    [Test]
    public void K6_StunOnPlayerMidCast_AddsToCurrentTime()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        var stun = new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = 3, targetTag = EffectTargetTag.Player };
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 2, 0, _ => stun.Apply(cm, enemy, player)));
        MakeCard(5, 0).Play(null);        // каст 0→5, в 2 Оглушение 3
        Assert.AreEqual(8, cm.CurrentTime);
    }

    // ---------- Мёртвый враг не цель (группа X) ----------

    [Test]
    public void X1_IsValidTarget_FalseForDeadEnemy_TrueForAlive()
    {
        var dead = MakeEnemy(5, 99, 1);
        var alive = MakeEnemy(50, 99, 1);
        Spawn(dead, alive);
        var card = Dmg(3, 1, 0);
        Assert.IsTrue(card.IsValidTarget(dead));
        dead.TakeDamage(10);
        Assert.IsFalse(card.IsValidTarget(dead));
        Assert.IsTrue(card.IsValidTarget(alive));
    }

    [Test]
    public void X2_SelectTarget_OnDeadEnemy_PlaysNothing_CardStaysPending()
    {
        NoDecay();
        var dead = MakeEnemy(5, 99, 1);
        var alive = MakeEnemy(50, 99, 1);
        Spawn(dead, alive);
        dead.TakeDamage(10);
        var tsm = Make<TargetSelectionManager>();
        var card = Dmg(3, 1, 0);
        tsm.SelectCard(card);
        tsm.SetHoveredTarget(dead);
        Assert.IsNull(tsm.HoveredTarget, "превью не на мёртвого");
        tsm.SelectTarget(dead);
        Assert.AreEqual(0, cm.CurrentTime);
        Assert.AreEqual(50, alive.CurrentHP);
        Assert.AreEqual(0, dead.CurrentHP);
        Assert.AreSame(card, tsm.PendingCard, "карта продолжает ждать цель");
    }

    [Test]
    public void X3_RewindOverDeath_EnemyValidTargetAgain_UntilReplayKillsHimAgain()
    {
        NoDecay();
        var victim = MakeEnemy(5, 99, 1);
        Spawn(victim, MakeEnemy(50, 99, 1));
        MakeCard(3, 2, Act(InstantActionKind.Damage, 10, EffectTargetTag.Enemy)).Play(victim); // смерть в 2, время 3
        Assert.IsFalse(Dmg(1, 1, 0).IsValidTarget(victim));
        RewindCard(3, 1, 1).Play(null);                   // событие в 4, откат на 3 с: R=1, время после отката 1 — смерть в 2 ещё впереди
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.IsTrue(victim.CurrentHP > 0, "враг ожил в том же объекте");
        Assert.IsTrue(Dmg(1, 1, 0).IsValidTarget(victim));
        Advance(1);                                       // повтор удара в 2 убивает его снова
        Assert.IsFalse(Dmg(1, 1, 0).IsValidTarget(victim));
    }

    // ---------- События боя и «Подождать» (группа W) ----------

    [Test]
    public void W1_Wait_AdvancesOneSecond_EnemyActionAndDrawFireInTheirTime()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 1, 2);
        Spawn(enemy);
        var draws = new List<int>();
        AddDrawTimer(draws);
        cm.Wait();
        Assert.AreEqual(1, cm.CurrentTime);
        Assert.AreEqual(28, player.CurrentHP, "действие врага в 1 сработало");
        Assert.AreEqual(0, draws.Count);
        cm.Wait();
        Assert.AreEqual(2, cm.CurrentTime);
        CollectionAssert.AreEqual(new[] { 2 }, draws, "добор сработал в своё время");
    }

    [Test]
    public void W2_Wait_DoesNothingAfterCombatEnd_AndWhileCardAwaitsTarget()
    {
        NoDecay();
        var enemy = MakeEnemy(5, 99, 1);
        Spawn(enemy);
        var tsm = Make<TargetSelectionManager>();
        tsm.combatManager = cm;
        tsm.SelectCard(Dmg(3, 1, 0));            // нужна цель: карта ждёт её
        Assert.IsNotNull(tsm.PendingCard);
        tsm.Wait();
        Assert.AreEqual(0, cm.CurrentTime, "при выбранной карте Подождать игнорируется");
        tsm.CancelSelection();
        tsm.Wait();
        Assert.AreEqual(1, cm.CurrentTime, "без выбранной карты Подождать работает");
        Dmg(10, 1, 0).Play(enemy);               // победа
        int t = cm.CurrentTime;
        cm.Wait();
        Assert.AreEqual(t, cm.CurrentTime, "после конца боя Подождать ничего не делает");
    }

    [Test]
    public void W3_CardPlayedAndCastSeconds_OncePerPlay_WaitDoesNotRaiseThem()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        var played = new List<(CardInstance, Combatant)>();
        var seconds = new List<int>();
        int timeAtPlayed = -1;
        cm.OnCardPlayed += (c, t) => { played.Add((c, t)); timeAtPlayed = cm.CurrentTime; };
        cm.OnCastSeconds += seconds.Add;
        var card = Dmg(1, 2, 0);
        var instance = card.instance;
        card.Play(enemy);
        Assert.AreEqual(1, played.Count);
        Assert.AreSame(instance, played[0].Item1);
        Assert.AreSame(enemy, played[0].Item2);
        Assert.AreEqual(0, timeAtPlayed, "OnCardPlayed — до AdvanceTime");
        CollectionAssert.AreEqual(new[] { 2 }, seconds);
        cm.Wait();
        Assert.AreEqual(1, played.Count);
        Assert.AreEqual(1, seconds.Count);
    }

    [Test]
    public void W4_OnAttack_PerHit_ForBothSides_NotOnSkippedReplayHit()
    {
        NoDecay();
        var enemy = MakeEnemy(100, 2, 1);
        Spawn(enemy);
        var hits = new List<(Combatant src, Combatant dst, int dmg)>();
        cm.OnAttack += (s, t, d, r) => hits.Add((s, t, d));
        var twoHits = MakeCard(1, 0, new InstantActionEntry { kind = InstantActionKind.Damage, amount = 4, hitCount = 2, targetTag = EffectTargetTag.Enemy });
        twoHits.Play(enemy);                     // время 1, два удара
        Assert.AreEqual(2, hits.Count);
        Assert.IsTrue(hits.All(h => h.src == player && h.dst == enemy && h.dmg == 4));
        cm.Wait();                               // в 2 враг бьёт игрока
        Assert.AreEqual(3, hits.Count);
        Assert.AreSame(enemy, hits[2].src);
        Assert.AreSame(player, hits[2].dst);
        RewindCard(2, 1, 0).Play(null);          // откат на 2 с: действие врага в 2 вернётся
        cm.Wait();                               // повтор действия врага в 2: игрока он уже пережил
        Assert.AreEqual(1, hits.Count(h => h.src == enemy && h.dst == player), "повтор действия врага по игроку не вызывает OnAttack");
    }

    // Подписчик «каждая 3-я атака игрока — 2 урона первому живому врагу», счётчик пишется через Record(null, …)
    [Test]
    public void W5_Subscriber_EveryThirdAttack_RewindUndoesCounterAndReaction_ReplayRepeatsIt()
    {
        NoDecay();
        var enemy = MakeEnemy(100, 99, 1);
        Spawn(enemy);
        int attacks = 0;
        var seen = new List<(bool replay, int counterOnEntry, int enemyHp)>();
        cm.OnAttack += (src, dst, dmg, replay) =>
        {
            if (src != player) return;
            seen.Add((replay, attacks, enemy.CurrentHP));
            int prev = attacks;
            attacks++;
            cm.Record(null, () => attacks = prev);
            if (attacks % 3 == 0)
            {
                var first = cm.Enemies.FirstOrDefault(e => e.CurrentHP > 0);
                if (first != null) first.TakeDamage(2);
            }
        };
        Dmg(5, 1, 0).Play(enemy);
        Dmg(5, 1, 0).Play(enemy);
        Dmg(5, 1, 0).Play(enemy);                // третья атака в 2: реакция
        Assert.AreEqual(3, attacks);
        Assert.AreEqual(83, enemy.CurrentHP, "15 урона + реакция 2");
        RewindCard(1, 1, 0).Play(null);          // откат на интервал с третьей атакой
        var replayed = seen.Where(x => x.replay).ToList();
        Assert.AreEqual(1, replayed.Count, "третья атака повторена");
        Assert.AreEqual(2, replayed[0].counterOnEntry, "счётчик откатился");
        Assert.AreEqual(85, replayed[0].enemyHp, "урон третьей атаки и реакции откатились (повторный удар уже нанесён)");
        Assert.AreEqual(3, attacks, "реакция сработала снова");
        Assert.AreEqual(83, enemy.CurrentHP, "итоговое HP то же, что до отката");
    }

    [Test]
    public void W6_Subscriber_BlockOnNonReplayAttack_NotDoubledAfterRewindReplay()
    {
        NoDecay();
        var enemy = MakeEnemy(100, 99, 1);
        Spawn(enemy);
        cm.OnAttack += (src, dst, dmg, replay) => { if (src == player && !replay) player.GainBlock(3); };
        Dmg(1, 1, 0).Play(enemy);
        Assert.AreEqual(3, player.CurrentBlock);
        RewindCard(1, 1, 0).Play(null);
        Assert.AreEqual(99, enemy.CurrentHP, "атака повторена один раз");
        Assert.AreEqual(3, player.CurrentBlock, "блок игрока при повторе не удвоился");
    }

    [Test]
    public void W7_OnCombatStart_OncePerCombat_AfterPlayerAndEnemiesRegistered()
    {
        var em = Make<EncounterManager>();
        em.combatManager = cm;
        em.deckManager = Make<DeckManager>();
        var hand = Make<HandManager>();
        hand.combatManager = cm;
        hand.deckManager = em.deckManager;
        em.handManager = hand;
        var area = new GameObject("Area", typeof(RectTransform)); created.Add(area);
        em.enemiesArea = (RectTransform)area.transform;
        var spawn = new GameObject("Spawn", typeof(RectTransform)); created.Add(spawn);
        em.playerSpawnPoint = (RectTransform)spawn.transform;
        var playerPrefab = new GameObject("PlayerPrefab"); created.Add(playerPrefab);
        playerPrefab.AddComponent<Player>().maxHP = 30;
        em.playerPrefab = playerPrefab;
        var enemyPrefab = new GameObject("EnemyPrefab", typeof(RectTransform)); created.Add(enemyPrefab);
        var proto = enemyPrefab.AddComponent<Enemy>();
        proto.maxHP = 20;
        var pattern = ScriptableObject.CreateInstance<EnemyPatternData>(); created.Add(pattern);
        pattern.steps.Add(new EnemyActionStep { stepNameKey = "enemy.test.attack_player", delaySeconds = 99, target = EnemyActionTarget.Player,
            instantActions = new List<InstantActionEntry> { Act(InstantActionKind.Damage, 1, EffectTargetTag.Player) } });
        proto.pattern = pattern;
        var encounter = ScriptableObject.CreateInstance<EncounterData>(); created.Add(encounter);
        encounter.enemyPrefabs.Add(enemyPrefab);

        int starts = 0, enemiesAtStart = -1;
        bool playerAtStart = false;
        cm.OnCombatStart += () => { starts++; enemiesAtStart = cm.Enemies.Count; playerAtStart = cm.player != null && cm.player != player; };
        em.StartEncounter(encounter);
        Assert.AreEqual(1, starts);
        Assert.AreEqual(1, enemiesAtStart, "враги уже зарегистрированы");
        Assert.IsTrue(playerAtStart, "игрок боя уже назначен");
        cm.Wait();
        MakeCard(1, 0).Play(null);
        Assert.AreEqual(1, starts, "за бой — один раз");
    }

    // ---------- Парирование (группа Y) и откат с Неуязвимостью (Z) ----------

    // Карта: каст 1, эффект через 1 (эффект в t+1). main — обычное действие; parry добавляется отдельно
    Card ParryCard(InstantActionEntry main, AppliedEffectEntry parryEffect = null, InstantActionEntry parryAction = null)
    {
        var card = MakeCard(1, 1, main);
        if (parryEffect != null) card.data.parryEffects.Add(parryEffect);
        if (parryAction != null) card.data.parryActions.Add(parryAction);
        return card;
    }

    static AppliedEffectEntry Eff(StatusEffectType type, float stacks, EffectTargetTag tag)
        => new AppliedEffectEntry { statusType = type, stacks = stacks, targetTag = tag };

    List<(CardInstance, Enemy, bool)> WatchParry()
    {
        var list = new List<(CardInstance, Enemy, bool)>();
        cm.OnParry += (c, e, r) => list.Add((c, e, r));
        return list;
    }

    [Test]
    public void Y1_SelectedTarget_ParryOnlyFromSelectedEnemyAction()
    {
        NoDecay();
        var a = MakeEnemy(100, 1, 1);          // действует в 1, 2, 3…
        var b = MakeEnemy(100, 99, 1);
        Spawn(a, b);
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy)).Play(b);   // эффект в 1: действует A, не B
        Assert.AreEqual(0, b.GetStacks(StatusEffectType.Weak), "выбран B, а в 1 действует A: Парирования нет");
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy)).Play(a);   // эффект в 2: действует A
        Assert.AreEqual(1, a.GetStacks(StatusEffectType.Weak), "выбран A, он действует в 2: Парирование");
    }

    [Test]
    public void Y2_NoEnemyTarget_ParryFromAnyEnemy()
    {
        NoDecay();
        Spawn(MakeEnemy(100, 99, 1), MakeEnemy(100, 1, 0));
        ParryCard(Act(InstantActionKind.Block, 5, EffectTargetTag.Player), null, Act(InstantActionKind.Block, 5, EffectTargetTag.Player)).Play(null);
        Assert.AreEqual(10, player.CurrentBlock);
    }

    [Test]
    public void Y3_AllEnemies_ParryFromAnyEnemy()
    {
        NoDecay();
        var a = MakeEnemy(100, 99, 1);
        var b = MakeEnemy(100, 1, 1);
        Spawn(a, b);
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.AllEnemies), Eff(StatusEffectType.Weak, 2, EffectTargetTag.AllEnemies)).Play(null);
        Assert.AreEqual(2, a.GetStacks(StatusEffectType.Weak));
        Assert.AreEqual(2, b.GetStacks(StatusEffectType.Weak));
    }

    [Test]
    public void Y4_EnemyAndAllEnemies_OnlySelectedEnemyCounts()
    {
        NoDecay();
        var a = MakeEnemy(100, 99, 1);         // выбранный, не действует
        var b = MakeEnemy(100, 1, 1);          // невыбранный, действует
        Spawn(a, b);
        var card = ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Poison, 4, EffectTargetTag.AllEnemies));
        card.data.instantActions.Add(Act(InstantActionKind.Damage, 1, EffectTargetTag.AllEnemies));
        card.Play(a);
        Assert.AreEqual(0, a.GetStacks(StatusEffectType.Poison));
        Assert.AreEqual(0, b.GetStacks(StatusEffectType.Poison), "выбран A, B действует, но не выбран: Парирования нет");
    }

    [Test]
    public void Y5_CardStunsSelectedEnemy_ParryCountedByOriginalTime()
    {
        NoDecay();
        var a = MakeEnemy(100, 1, 3);
        Spawn(a);
        var card = ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy));
        card.data.appliedEffects.Add(Eff(StatusEffectType.Stun, 3, EffectTargetTag.Enemy));
        card.Play(a);
        Assert.AreEqual(1, a.GetStacks(StatusEffectType.Weak), "условие посчитано до Оглушения");
        Assert.AreEqual(30, player.CurrentHP, "действие врага сдвинуто Оглушением и в 1 не сработало");
        Assert.AreEqual(4, a.NextTime);
    }

    [Test]
    public void Y6_NoParry_ForDeadEnemy_OrEnemyWhoseActionWasStunnedAway()
    {
        NoDecay();
        var dead = MakeEnemy(5, 1, 1);
        var alive = MakeEnemy(100, 1, 1);
        Spawn(dead, alive);
        dead.TakeDamage(10);
        var log = WatchParry();
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy)).Play(dead);
        Assert.AreEqual(0, log.Count, "мёртвый враг не действует");
        // Оглушение (карта) уводит действие alive с 2 на 5: следующая карта в 3 не парирует
        var stun = MakeCard(1, 0, Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy));
        stun.data.appliedEffects.Add(Eff(StatusEffectType.Stun, 3, EffectTargetTag.Enemy));
        stun.Play(alive);                                       // время 2 (после 1+1), действие alive было в 2, теперь в 5
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy)).Play(alive);   // эффект в 3
        Assert.AreEqual(0, alive.GetStacks(StatusEffectType.Weak));
        Assert.AreEqual(0, log.Count);
    }

    [Test]
    public void Y7_EnemyStepWithoutAttack_StillParried()
    {
        NoDecay();
        var enemy = MakeEnemyWithSteps(100, (Act(InstantActionKind.Block, 5, EffectTargetTag.Player), EnemyActionTarget.Self, 1));
        Spawn(enemy);
        var log = WatchParry();
        ParryCard(Act(InstantActionKind.Damage, 1, EffectTargetTag.Enemy), Eff(StatusEffectType.Weak, 1, EffectTargetTag.Enemy)).Play(enemy);
        Assert.AreEqual(1, log.Count);
        Assert.AreEqual(1, enemy.GetStacks(StatusEffectType.Weak));
    }

    [Test]
    public void Y8_RewindAndReplay_ParryAgain_PlayerNotDoubled_FirstPassNumbers()
    {
        NoDecay();
        var a = MakeEnemy(100, 1, 0);
        Spawn(a);
        var log = WatchParry();
        var card = ParryCard(Act(InstantActionKind.Damage, 4, EffectTargetTag.Enemy), null, Act(InstantActionKind.Damage, 2, EffectTargetTag.Enemy));
        card.data.parryActions.Add(Act(InstantActionKind.Block, 3, EffectTargetTag.Player));
        card.Play(a);                                        // эффект в 1 совпал с действием A
        Assert.AreEqual(94, a.CurrentHP);
        Assert.AreEqual(3, player.CurrentBlock);
        Assert.AreEqual(1, log.Count);
        Assert.IsFalse(log[0].Item3);
        Assert.AreSame(a, log[0].Item2);
        player.AddEffectStacks(StatusEffectType.Strength, 5);   // игрок не откатывается: повтор обязан взять числа первого прохода
        RewindCard(2, 1, 1).Play(null);                      // событие в 2, откат на 2 с: время 0, A снова должен действовать в 1
        Assert.AreEqual(100, a.CurrentHP, "урон карты и Парирования откатился");
        Advance(1);                                          // повтор карты в 1, действие A возвращено откатом
        Assert.AreEqual(2, log.Count, "Парирование сработало снова");
        Assert.IsTrue(log[1].Item3, "isReplay");
        Assert.AreEqual(94, a.CurrentHP, "числа первого прохода, а не усиленные Силой");
        Assert.AreEqual(3, player.CurrentBlock, "на игрока при повторе не действует");
    }

    [Test]
    public void Y9_TwoEnemiesActing_OneParry_FirstInList()
    {
        NoDecay();
        var a = MakeEnemy(100, 1, 0);
        var b = MakeEnemy(100, 1, 0);
        Spawn(a, b);
        var log = WatchParry();
        ParryCard(Act(InstantActionKind.Block, 5, EffectTargetTag.Player), null, Act(InstantActionKind.Block, 5, EffectTargetTag.Player)).Play(null);
        Assert.AreEqual(1, log.Count);
        Assert.AreSame(a, log[0].Item2);
        Assert.AreEqual(10, player.CurrentBlock, "эффекты Парирования применены один раз");
    }

    [Test]
    public void Y10_RealAssets_FieldsAndBehaviour()
    {
        NoDecay();
        CardData Load(string file) => Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>("Assets/Data/Cards/" + file));
        var guard = Load("ParryGuard_data.asset");
        var wave = Load("ParryWave_data.asset");
        var strike = Load("ParryStrike_data.asset");
        created.AddRange(new Object[] { guard, wave, strike });
        foreach (var d in new[] { guard, wave, strike })
        {
            Assert.AreEqual(1, d.timeCostSeconds);
            Assert.AreEqual(1, d.effectDelaySeconds);
        }
        Assert.AreEqual(InstantActionKind.Block, guard.instantActions.Single().kind);
        Assert.AreEqual(10, guard.instantActions.Single().amount);
        Assert.AreEqual(EffectTargetTag.Player, guard.instantActions.Single().targetTag);
        Assert.AreEqual((StatusEffectType.Weak, 3f, EffectTargetTag.Enemy), (guard.parryEffects.Single().statusType, guard.parryEffects.Single().stacks, guard.parryEffects.Single().targetTag));
        Assert.AreEqual((InstantActionKind.Damage, 1, EffectTargetTag.AllEnemies), (wave.instantActions.Single().kind, wave.instantActions.Single().amount, wave.instantActions.Single().targetTag));
        Assert.AreEqual((StatusEffectType.Vulnerable, 1f, EffectTargetTag.AllEnemies), (wave.parryEffects.Single().statusType, wave.parryEffects.Single().stacks, wave.parryEffects.Single().targetTag));
        Assert.AreEqual((InstantActionKind.Damage, 13, EffectTargetTag.Enemy), (strike.instantActions.Single().kind, strike.instantActions.Single().amount, strike.instantActions.Single().targetTag));
        Assert.AreEqual((StatusEffectType.Poison, 4f, EffectTargetTag.AllEnemies), (strike.parryEffects.Single().statusType, strike.parryEffects.Single().stacks, strike.parryEffects.Single().targetTag));

        var guardCard = Make<Card>(); guardCard.instance = new CardInstance(guard); guardCard.combatManager = cm;
        Assert.IsTrue(guardCard.RequiresTarget, "цель-враг только в Парировании тоже требует выбора цели");

        var a = MakeEnemy(100, 1, 1);
        Spawn(a);
        Card Play(CardData d, Combatant target) { var c = Make<Card>(); c.instance = new CardInstance(d); c.combatManager = cm; c.Play(target); return c; }
        Play(strike, a);                                      // эффект в 1 совпал с действием A
        Assert.AreEqual(87, a.CurrentHP);
        Assert.IsTrue(a.GetStacks(StatusEffectType.Poison) > 0, "Парирование ядовитого выпада");
        Play(guard, a);                                       // эффект в 2 совпал с действием A
        Assert.AreEqual(3, a.GetStacks(StatusEffectType.Weak));
        Assert.AreEqual(10, player.CurrentBlock);
        Play(wave, null);                                     // эффект в 3 совпал
        Assert.AreEqual(1, a.GetStacks(StatusEffectType.Vulnerable));
    }

    [Test]
    public void Z1_RewindWithInvulnerable_StacksAndTickReturn_DamageStillBlockedOnReplay()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        var invuln = MakeCard(1, 0, Act(InstantActionKind.Damage, 0, EffectTargetTag.Enemy));
        invuln.data.appliedEffects.Add(Eff(StatusEffectType.Invulnerable, 5, EffectTargetTag.Enemy));
        invuln.Play(enemy);                                   // Неуязвимость 5 в 0, время 1
        Dmg(10, 1, 0).Play(enemy);                            // удар в 1 не проходит, время 2
        Assert.AreEqual(50, enemy.CurrentHP);
        RewindCard(3, 1, 1).Play(null);                       // событие в 3, откат на 3 с: время 0; повтор Неуязвимости в 0
        Assert.AreEqual(5, enemy.GetStacks(StatusEffectType.Invulnerable), "стаки откатились и наложены один раз");
        Assert.IsTrue(HasTick(enemy, StatusEffectType.Invulnerable));
        Advance(1);                                           // повтор удара в 1 — Неуязвимость на месте
        Assert.AreEqual(50, enemy.CurrentHP, "при повторе урон снова не проходит");
        Assert.AreEqual(4, enemy.GetStacks(StatusEffectType.Invulnerable));
    }

    // ---------- Версия состояния боя (для UI-кэша текстов) ----------

    [Test]
    public void V1_StateVersion_GrowsOnEveryStateChange()
    {
        NoDecay();
        var enemy = MakeEnemy(50, 99, 1);
        Spawn(enemy);
        int v = cm.StateVersion;
        void Grew(string what) { Assert.Greater(cm.StateVersion, v, what); v = cm.StateVersion; }
        player.GainBlock(3); Grew("блок вне события");
        player.AddEffectStacks(StatusEffectType.Strength, 1); Grew("эффект вне события");
        enemy.SetCurrentHP(40); Grew("SetCurrentHP");
        cm.AdvanceTime(1); Grew("время");
        Dmg(5, 1, 0).Play(enemy); Grew("розыгрыш карты");
        RewindCard(1, 1, 0).Play(null); Grew("откат");
        cm.ResetForNewCombat(); Grew("сброс боя");
    }

    // ---------- Исправления по полной проверке (группа R) ----------

    [Test]
    public void R1_StepDelayZero_ResolveUpToFinishes_EnemyActsEverySecond()
    {
        NoDecay();
        var enemy = MakeEnemy(30, 0, 1);                  // delaySeconds = 0 в данных
        Spawn(enemy);
        Advance(3);                                       // без Max(1, …) ResolveUpTo зациклился бы
        Assert.AreEqual(3, cm.CurrentTime);
        Assert.AreEqual(27, player.CurrentHP, "действия в 1, 2, 3");
    }

    [Test]
    public void R2_ClearingCallersList_DoesNotEmptyCombatManagerEnemies()
    {
        var list = new List<Enemy> { MakeEnemy(30, 99, 1) };
        cm.RegisterEnemies(list);
        list.Clear();
        Assert.AreEqual(1, cm.Enemies.Count);
    }

    [Test]
    public void R3_TwoStuns_DisplayShowsSum_ActionShiftedByBoth()
    {
        NoDecay();
        var enemy = MakeEnemy(100, 2, 1);                 // первое действие в 2
        Spawn(enemy);
        Card Stun(int seconds)
        {
            var card = MakeCard(1, 0, Act(InstantActionKind.Damage, 0, EffectTargetTag.Enemy));
            card.data.appliedEffects.Add(new AppliedEffectEntry { statusType = StatusEffectType.Stun, stacks = seconds, targetTag = EffectTargetTag.Enemy });
            return card;
        }
        int displayAt1 = -1;
        Stun(3).Play(enemy);                              // Оглушение 3 в 0: действие 2 → 5
        cm.RegisterScheduledEvent(new OneShotEvent(cm, 1, EventGroup.Environment, _ => displayAt1 = enemy.StunDisplay)); // читает значение в конце момента 1
        Stun(2).Play(enemy);                              // Оглушение 2 в 1: остаток 2 + 2
        Assert.AreEqual(4, displayAt1, "в момент 1 осталось 2 от первого + 2 нового");
        Assert.AreEqual(7, enemy.NextTime, "действие сдвинуто на 5 (2 → 7)");
    }

    [Test]
    public void R4_HoveringPlayer_DoesNotChangePreview_HoveringAliveEnemyDoes()
    {
        var enemy = MakeEnemy(30, 99, 1);
        Spawn(enemy);
        var tsm = Make<TargetSelectionManager>();
        tsm.SelectCard(Dmg(3, 1, 0));                     // нужна цель-враг: карта ждёт её
        Assert.IsNotNull(tsm.PendingCard);
        tsm.SetHoveredTarget(player);
        Assert.IsNull(tsm.HoveredTarget, "игрок не допустимая цель карты");
        tsm.SetHoveredTarget(enemy);
        Assert.AreSame(enemy, tsm.HoveredTarget);
    }
}
