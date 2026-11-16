using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class Card : MonoBehaviour, IPointerClickHandler
{
    private const int SectionActions = 0;
    private const int SectionEffects = 1;
    private const int SectionParryActions = 2;
    private const int SectionParryEffects = 3;
    public CardInstance instance;
    public CombatManager combatManager;
    public TMP_Text nameLabel;
    public TMP_Text effectLabel;
    public TMP_Text propertiesLabel;

    public CardData data => instance.data;

    void Awake()
    {
        if (instance != null && instance.data != null && nameLabel != null)
            nameLabel.text = data.DisplayName;
        UpdatePropertiesText();
    }

    private int shownVersion = -1;
    private Combatant shownPreviewTarget;

    void Update()
    {
        UpdateEffectText();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectCard(this);
    }

    public bool RequiresTarget => HasEnemyTarget(data);

    // Цель-враг (выбранный) в любом из четырёх списков, включая Парирование
    private static bool HasEnemyTarget(CardData d) =>
        d.instantActions.Exists(a => a.targetTag == EffectTargetTag.Enemy) ||
        d.appliedEffects.Exists(e => e.targetTag == EffectTargetTag.Enemy) ||
        d.parryActions.Exists(a => a.targetTag == EffectTargetTag.Enemy) ||
        d.parryEffects.Exists(e => e.targetTag == EffectTargetTag.Enemy);

    // Парирование (дизайн-документ §5.5): считается один раз в начале срабатывания карты, до любых её эффектов.
    // Враг «действует в t» — жив, ждёт срабатывания и его время ровно t. Есть выбранная цель-враг — учитывается только она, иначе любой живой.
    // Возвращает парированного врага (первый по списку) или null. При цели «случайный враг» (в игре пока нет) сначала выбирается цель, затем проверка от неё.
    private static Enemy FindParriedEnemy(
        CardData data,
        List<Enemy> actingEnemies,
        Combatant chosenTarget,
        Dictionary<(int section, int entry, int hit), Enemy> randomTargets)
    {
        if (data.parryActions.Count == 0 && data.parryEffects.Count == 0)
            return null;

        // Приоритет 1: явно выбранный враг.
        if (HasEnemyTarget(data))
            return actingEnemies.FirstOrDefault(e => e == chosenTarget);

        // Приоритет 2: AllEnemies — достаточно любого действующего врага.
        bool hasAllEnemies =
            data.instantActions.Any(a => a.targetTag == EffectTargetTag.AllEnemies) ||
            data.appliedEffects.Any(e => e.targetTag == EffectTargetTag.AllEnemies) ||
            data.parryActions.Any(a => a.targetTag == EffectTargetTag.AllEnemies) ||
            data.parryEffects.Any(e => e.targetTag == EffectTargetTag.AllEnemies);

        if (hasAllEnemies)
            return actingEnemies.FirstOrDefault();

        // Приоритет 3: RandomEnemy — только если случайный выбор карты
        // действительно затронул хотя бы одного действующего врага.
        bool hasRandomEnemy =
            data.instantActions.Any(a => a.targetTag == EffectTargetTag.RandomEnemy) ||
            data.appliedEffects.Any(e => e.targetTag == EffectTargetTag.RandomEnemy);

        if (hasRandomEnemy)
        {
            var selectedTargets = new HashSet<Enemy>();

            if (randomTargets != null)
            {
                foreach (var pair in randomTargets)
                    if (pair.Key.section == SectionActions || pair.Key.section == SectionEffects)
                        if (pair.Value != null)
                            selectedTargets.Add(pair.Value);
            }

            return actingEnemies.FirstOrDefault(e => selectedTargets.Contains(e));
        }

        // Нет специальных тегов целей — достаточно любого действующего врага.
        return actingEnemies.FirstOrDefault();
    }

    public bool IsValidTarget(Combatant target) => target is Enemy && target.CurrentHP > 0; // мёртвый враг остаётся в списке (оживает при откате), но целью не бывает

    public void Play(Combatant target)
    {
        // Событие не держит ссылку на this: объект карты уничтожается сразу,
        // а эффект может сработать позже.
        CardInstance playedInstance = instance;
        CombatManager cm = combatManager;
        int effectTime = cm.CurrentTime + playedInstance.data.effectDelaySeconds;

        // Числа первого срабатывания сохраняются для повтора после отката.
        var memo = new Dictionary<(int section, int entry, int hit, Combatant target), float>();

        // Случайные цели сохраняются отдельно от чисел.
        // Наличие ключа означает, что выбор уже состоялся, даже если значение null.
        var randomTargets = new Dictionary<(int section, int entry, int hit), Enemy>();

        cm.RegisterScheduledEvent(new OneShotEvent(
            cm,
            effectTime,
            priority: EventGroup.Player,
            isReplay => ApplyAllEffects(
                playedInstance.data,
                cm,
                target,
                isReplay,
                memo,
                playedInstance,
                randomTargets)));

        if (HandManager.Instance != null)
            HandManager.Instance.RemoveFromHand(this);

        cm.RaiseCardPlayed(playedInstance, target);
        cm.AdvanceTime(playedInstance.data.timeCostSeconds);
        cm.RaiseCastSeconds(playedInstance.data.timeCostSeconds);
        cm.ResolveUpTo();

        if (HandManager.Instance != null)
            HandManager.Instance.deckManager.Discard(playedInstance);
    }

    public static void ApplyAllEffects(
        CardData data,
        CombatManager combatManager,
        Combatant chosenTarget,
        bool isReplay = false,
        Dictionary<(int section, int entry, int hit, Combatant target), float> memo = null,
        CardInstance card = null,
        Dictionary<(int section, int entry, int hit), Enemy> randomTargets = null)
    {
        // Снимок действующих врагов делается до любых эффектов карты.
        int t = combatManager.CurrentEventTime;
        var actingEnemies = combatManager.Enemies
            .Where(e => e.CurrentHP > 0 && e.IsPending && e.NextTime == t)
            .ToList();

        InstantActionEntry rewind = null;

        // Сначала обычные разовые действия.
        for (int i = 0; i < data.instantActions.Count; i++)
        {
            var action = data.instantActions[i];

            if (action.kind == InstantActionKind.Rewind)
            {
                if (rewind == null)
                    rewind = action;
                continue;
            }

            ApplyAction(
                action,
                SectionActions,
                i,
                combatManager,
                chosenTarget,
                isReplay,
                memo,
                randomTargets);
        }

        // Затем обычные накладываемые эффекты.
        for (int i = 0; i < data.appliedEffects.Count; i++)
        {
            ApplyEffect(
                data.appliedEffects[i],
                SectionEffects,
                i,
                combatManager,
                chosenTarget,
                isReplay,
                randomTargets);
        }

        if (!isReplay && data.drawCardsOnPlay > 0 && HandManager.Instance != null)
            for (int i = 0; i < data.drawCardsOnPlay; i++)
                HandManager.Instance.TryDrawToHand();

        // Проверка Парирования использует снимок врагов и уже сделанные случайные выборы.
        Enemy parried = FindParriedEnemy(data, actingEnemies, chosenTarget, randomTargets);

        if (parried != null)
        {
            combatManager.RaiseParry(card, parried);

            for (int i = 0; i < data.parryActions.Count; i++)
            {
                var action = data.parryActions[i];

                if (action.kind == InstantActionKind.Rewind)
                    continue;

                ApplyAction(
                    action,
                    SectionParryActions,
                    i,
                    combatManager,
                    chosenTarget,
                    isReplay,
                    memo,
                    randomTargets);
            }

            for (int i = 0; i < data.parryEffects.Count; i++)
            {
                ApplyEffect(
                    data.parryEffects[i],
                    SectionParryEffects,
                    i,
                    combatManager,
                    chosenTarget,
                    isReplay,
                    randomTargets);
            }
        }

        // Откат остаётся последним действием карты и не повторяется.
        if (rewind != null && !isReplay)
            rewind.Apply(combatManager, combatManager.player, combatManager.player);
    }

    private static void ApplyAction(
        InstantActionEntry action,
        int section,
        int entryIndex,
        CombatManager combatManager,
        Combatant chosenTarget,
        bool isReplay,
        Dictionary<(int section, int entry, int hit, Combatant target), float> memo,
        Dictionary<(int section, int entry, int hit), Enemy> randomTargets)
    {
        if (action.targetTag == EffectTargetTag.RandomEnemy)
        {
            // Для RandomEnemy новая цель выбирается перед каждым попаданием.
            for (int hit = 0; hit < action.hitCount; hit++)
            {
                Enemy target = GetRandomTarget(
                    combatManager,
                    randomTargets,
                    (section, entryIndex, hit));

                if (target == null)
                    continue;

                var memoKey = (section, entryIndex, hit, (Combatant)target);

                if (isReplay && memo != null && memo.TryGetValue(memoKey, out float remembered))
                {
                    action.ApplySingleHit(
                        combatManager,
                        combatManager.player,
                        target,
                        remembered);
                }
                else
                {
                    float outgoing = action.ApplySingleHit(
                        combatManager,
                        combatManager.player,
                        target);

                    if (!isReplay && memo != null)
                        memo[memoKey] = outgoing;
                }
            }

            return;
        }

        // Для остальных целей сохраняется прежнее поведение Apply(),
        // включая обработку hitCount и действий врагов.
        foreach (var target in ResolveTargets(combatManager, action.targetTag, chosenTarget))
        {
            if (isReplay && target is Player)
                continue;

            var memoKey = (section, entryIndex, 0, target);

            if (isReplay && memo != null && memo.TryGetValue(memoKey, out float remembered))
            {
                action.Apply(combatManager, combatManager.player, target, remembered);
            }
            else
            {
                float outgoing = action.Apply(combatManager, combatManager.player, target);

                if (!isReplay && memo != null)
                    memo[memoKey] = outgoing;
            }
        }
    }

    private static void ApplyEffect(
        AppliedEffectEntry effect,
        int section,
        int entryIndex,
        CombatManager combatManager,
        Combatant chosenTarget,
        bool isReplay,
        Dictionary<(int section, int entry, int hit), Enemy> randomTargets)
    {
        if (effect.targetTag == EffectTargetTag.RandomEnemy)
        {
            Enemy target = GetRandomTarget(
                combatManager,
                randomTargets,
                (section, entryIndex, 0));

            if (target != null)
                effect.Apply(combatManager, combatManager.player, target);

            return;
        }

        foreach (var target in ResolveTargets(combatManager, effect.targetTag, chosenTarget))
            if (!(isReplay && target is Player))
                effect.Apply(combatManager, combatManager.player, target);
    }

    private static Enemy GetRandomTarget(
    CombatManager combatManager,
    Dictionary<(int section, int entry, int hit), Enemy> randomTargets,
    (int section, int entry, int hit) key)
    {
        if (randomTargets != null && randomTargets.TryGetValue(key, out Enemy remembered))
            return remembered;

        var livingEnemies = combatManager.Enemies
            .Where(e => e.CurrentHP > 0)
            .ToList();

        Enemy selected = livingEnemies.Count > 0
            ? livingEnemies[Random.Range(0, livingEnemies.Count)]
            : null;

        if (randomTargets != null)
            randomTargets[key] = selected;

        return selected;
    }

    private static List<Combatant> ResolveTargets(CombatManager combatManager, EffectTargetTag tag, Combatant chosenTarget)
    {
        switch (tag)
        {
            case EffectTargetTag.Player:
                return new List<Combatant> { combatManager.player };
            case EffectTargetTag.Enemy:
                return chosenTarget != null ? new List<Combatant> { chosenTarget } : new List<Combatant>();
            case EffectTargetTag.AllEnemies:
                return combatManager.Enemies.Where(e => e.CurrentHP > 0).Cast<Combatant>().ToList();
            case EffectTargetTag.RandomEnemy:
                return new List<Combatant>();
            default:
                return new List<Combatant>();
        }
    }

    private void UpdateEffectText()
    {
        if (effectLabel == null || instance == null || instance.data == null) return;

        Combatant previewTarget = null;
        bool isPending = TargetSelectionManager.Instance != null && TargetSelectionManager.Instance.PendingCard == this;
        if (isPending)
            previewTarget = TargetSelectionManager.Instance.HoveredTarget;

        int version = combatManager.StateVersion;
        if (version == shownVersion && previewTarget == shownPreviewTarget) return; // ни бой, ни цель наведения не менялись
        shownVersion = version;
        shownPreviewTarget = previewTarget;

        var lines = new List<string>();
        foreach (var action in data.instantActions)
            lines.Add(DescribeAction(action, previewTarget));
        foreach (var effect in data.appliedEffects)
            lines.Add(effect.Describe());
        if (data.drawCardsOnPlay > 0)
            lines.Add(Loc.Format("card.draw", data.drawCardsOnPlay));
        foreach (var action in data.parryActions)
            lines.Add(Loc.Format("card.parry", DescribeAction(action, previewTarget)));
        foreach (var effect in data.parryEffects)
            lines.Add(Loc.Format("card.parry", effect.Describe()));

        effectLabel.text = string.Join("\n", lines);
    }

    private string DescribeAction(InstantActionEntry action, Combatant previewTarget)
    {
        Combatant recipient = action.targetTag switch
        {
            EffectTargetTag.Player => combatManager.player,
            EffectTargetTag.Enemy => previewTarget,
            _ => null
        };
        return action.Describe(action.ComputePreviewAmount(combatManager.player, recipient));
    }

    private void UpdatePropertiesText()
    {
        if (propertiesLabel == null || instance == null || instance.data == null) return;
        var props = new List<string>();
        var effective = instance.EffectiveProperties;
        if ((effective & CardPropertyFlags.Exhaust) != 0) props.Add(Loc.Get("card.property.exhaust"));
        if ((effective & CardPropertyFlags.FrontOfDraw) != 0) props.Add(Loc.Get("card.property.frontOfDraw"));
        propertiesLabel.text = string.Join(", ", props);
    }
}
