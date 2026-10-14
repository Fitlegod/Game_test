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

    void Update()
    {
        UpdateEffectText();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TargetSelectionManager.Instance.SelectCard(this);
    }

    public bool RequiresTarget =>
        data.instantActions.Exists(a => a.targetTag == EffectTargetTag.Enemy) ||
        data.appliedEffects.Exists(e => e.targetTag == EffectTargetTag.Enemy);

    public bool IsValidTarget(Combatant target) => target is Enemy;

    public void Play(Combatant target)
    {
        // Событие не держит ссылку на this: объект карты уничтожается сразу, а эффект может сработать позже
        CardInstance playedInstance = instance;
        CombatManager cm = combatManager;
        int effectTime = cm.CurrentTime + playedInstance.data.effectDelaySeconds;
        var memo = new Dictionary<(int, Combatant), int>(); // числа первого срабатывания — для повтора после отката
        cm.RegisterScheduledEvent(new OneShotEvent(cm, effectTime, priority: 0, isReplay => ApplyAllEffects(playedInstance.data, cm, target, isReplay, memo)));

        if (HandManager.Instance != null)
            HandManager.Instance.RemoveFromHand(this); // слот свободен уже на время каста

        cm.AdvanceTime(playedInstance.data.timeCostSeconds);
        cm.ResolveUpTo();

        if (HandManager.Instance != null)
            HandManager.Instance.deckManager.Discard(playedInstance); // сброс или сжигание по EffectiveProperties
    }

    // isReplay: событие переигрывается после отката — на игрока и сам Откат не действует, на врагов действует
    // memo: итоговые («исходящие») числа по (номер действия, цель). Первое срабатывание их записывает, повтор применяет их как есть
    // и не пересчитывает формулы источника по текущему состоянию игрока (игрок при откате не откатывается). Эффекты-стаки — фиксированные числа, в memo не нужны.
    public static void ApplyAllEffects(CardData data, CombatManager combatManager, Combatant chosenTarget, bool isReplay = false, Dictionary<(int, Combatant), int> memo = null)
    {
        InstantActionEntry rewind = null; // Откат — один раз на карту и последним: после него у этой карты не остаётся записей журнала
        for (int i = 0; i < data.instantActions.Count; i++)
        {
            var action = data.instantActions[i];
            if (action.kind == InstantActionKind.Rewind)
            {
                if (rewind == null) rewind = action;
                continue;
            }
            foreach (var target in ResolveTargets(combatManager, action.targetTag, chosenTarget))
            {
                if (isReplay && target is Player) continue;
                if (isReplay && memo != null && memo.TryGetValue((i, target), out int remembered))
                    action.Apply(combatManager, combatManager.player, target, remembered);
                else
                {
                    int outgoing = action.Apply(combatManager, combatManager.player, target);
                    if (!isReplay && memo != null) memo[(i, target)] = outgoing;
                }
            }
        }

        foreach (var effect in data.appliedEffects)
            foreach (var target in ResolveTargets(combatManager, effect.targetTag, chosenTarget))
                if (!(isReplay && target is Player))
                    effect.Apply(combatManager, combatManager.player, target);

        if (!isReplay && data.drawCardsOnPlay > 0 && HandManager.Instance != null)
            for (int i = 0; i < data.drawCardsOnPlay; i++)
                HandManager.Instance.TryDrawToHand();

        if (rewind != null && !isReplay)
            rewind.Apply(combatManager, combatManager.player, combatManager.player);
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

        var lines = new List<string>();
        foreach (var action in data.instantActions)
        {
            Combatant recipient = action.targetTag switch
            {
                EffectTargetTag.Player => combatManager.player,
                EffectTargetTag.Enemy => previewTarget,
                _ => null
            };
            int value = action.ComputePreviewAmount(combatManager.player, recipient);
            lines.Add(action.Describe(value));
        }
        foreach (var effect in data.appliedEffects)
            lines.Add(effect.Describe());
        if (data.drawCardsOnPlay > 0)
            lines.Add(Loc.Format("card.draw", data.drawCardsOnPlay));

        effectLabel.text = string.Join("\n", lines);
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
