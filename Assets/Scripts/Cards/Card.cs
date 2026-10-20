using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class Card : MonoBehaviour, IPointerClickHandler
{
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
    private static Enemy FindParriedEnemy(CardData d, CombatManager cm, Combatant chosenTarget)
    {
        if (d.parryActions.Count == 0 && d.parryEffects.Count == 0) return null;
        bool onlyChosen = chosenTarget != null && HasEnemyTarget(d);
        int t = cm.CurrentEventTime;
        foreach (var e in cm.Enemies)
            if (e.CurrentHP > 0 && e.IsPending && e.NextTime == t && (!onlyChosen || e == chosenTarget))
                return e;
        return null;
    }

    public bool IsValidTarget(Combatant target) => target is Enemy && target.CurrentHP > 0; // мёртвый враг остаётся в списке (оживает при откате), но целью не бывает

    public void Play(Combatant target)
    {
        // Событие не держит ссылку на this: объект карты уничтожается сразу, а эффект может сработать позже
        CardInstance playedInstance = instance;
        CombatManager cm = combatManager;
        int effectTime = cm.CurrentTime + playedInstance.data.effectDelaySeconds;
        var memo = new Dictionary<(int, Combatant), float>(); // числа первого срабатывания — для повтора после отката
        cm.RegisterScheduledEvent(new OneShotEvent(cm, effectTime, priority: EventGroup.Player, isReplay => ApplyAllEffects(playedInstance.data, cm, target, isReplay, memo, playedInstance)));

        if (HandManager.Instance != null)
            HandManager.Instance.RemoveFromHand(this); // слот свободен уже на время каста

        cm.RaiseCardPlayed(playedInstance, target);
        cm.AdvanceTime(playedInstance.data.timeCostSeconds);
        cm.RaiseCastSeconds(playedInstance.data.timeCostSeconds);
        cm.ResolveUpTo();

        if (HandManager.Instance != null)
            HandManager.Instance.deckManager.Discard(playedInstance); // сброс или сжигание по EffectiveProperties
    }

    // isReplay: событие переигрывается после отката — на игрока и сам Откат не действует, на врагов действует
    // memo: итоговые («исходящие») числа по (номер действия, цель). Первое срабатывание их записывает, повтор применяет их как есть
    // и не пересчитывает формулы источника по текущему состоянию игрока (игрок при откате не откатывается). Эффекты-стаки — фиксированные числа, в memo не нужны.
    public static void ApplyAllEffects(CardData data, CombatManager combatManager, Combatant chosenTarget, bool isReplay = false, Dictionary<(int, Combatant), float> memo = null, CardInstance card = null)
    {
        Enemy parried = FindParriedEnemy(data, combatManager, chosenTarget); // до любых эффектов: Оглушение этой же карты Парирование не отменяет
        InstantActionEntry rewind = null; // Откат — один раз на карту и последним: после него у этой карты не остаётся записей журнала
        for (int i = 0; i < data.instantActions.Count; i++)
        {
            if (data.instantActions[i].kind == InstantActionKind.Rewind)
            {
                if (rewind == null) rewind = data.instantActions[i];
                continue;
            }
            ApplyAction(data.instantActions[i], i, combatManager, chosenTarget, isReplay, memo);
        }

        foreach (var effect in data.appliedEffects)
            ApplyEffect(effect, combatManager, chosenTarget, isReplay);

        if (!isReplay && data.drawCardsOnPlay > 0 && HandManager.Instance != null)
            for (int i = 0; i < data.drawCardsOnPlay; i++)
                HandManager.Instance.TryDrawToHand();

        if (parried != null)
        {
            combatManager.RaiseParry(card, parried);
            for (int i = 0; i < data.parryActions.Count; i++)
                if (data.parryActions[i].kind != InstantActionKind.Rewind)
                    ApplyAction(data.parryActions[i], -1 - i, combatManager, chosenTarget, isReplay, memo); // ключи memo < 0 не пересекаются с обычными
            foreach (var effect in data.parryEffects)
                ApplyEffect(effect, combatManager, chosenTarget, isReplay);
        }

        if (rewind != null && !isReplay)
            rewind.Apply(combatManager, combatManager.player, combatManager.player);
    }

    private static void ApplyAction(InstantActionEntry action, int memoIndex, CombatManager combatManager, Combatant chosenTarget, bool isReplay, Dictionary<(int, Combatant), float> memo)
    {
        foreach (var target in ResolveTargets(combatManager, action.targetTag, chosenTarget))
        {
            if (isReplay && target is Player) continue;
            if (isReplay && memo != null && memo.TryGetValue((memoIndex, target), out float remembered))
                action.Apply(combatManager, combatManager.player, target, remembered);
            else
            {
                float outgoing = action.Apply(combatManager, combatManager.player, target);
                if (!isReplay && memo != null) memo[(memoIndex, target)] = outgoing;
            }
        }
    }

    private static void ApplyEffect(AppliedEffectEntry effect, CombatManager combatManager, Combatant chosenTarget, bool isReplay)
    {
        foreach (var target in ResolveTargets(combatManager, effect.targetTag, chosenTarget))
            if (!(isReplay && target is Player))
                effect.Apply(combatManager, combatManager.player, target);
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
