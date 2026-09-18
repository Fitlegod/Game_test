using UnityEngine;
using TMPro;

public class EffectStacksDisplay : MonoBehaviour
{
    public Combatant target;
    public TMP_Text label;

    void Update()
    {
        string text = "";
        AppendIfPresent(ref text, "Сила", StatusEffectType.Strength);
        AppendIfPresent(ref text, "Слабость", StatusEffectType.Weak);
        AppendIfPresent(ref text, "Лечение", StatusEffectType.Regen);
        AppendIfPresent(ref text, "Крепкость", StatusEffectType.Toughness);
        AppendIfPresent(ref text, "Хрупкость", StatusEffectType.Frailty);
        AppendIfPresent(ref text, "Уязвимость", StatusEffectType.Vulnerable);

        if (target is Enemy enemy && enemy.StaggerDisplay > 0f)
        {
            if (text.Length > 0) text += " | ";
            text += "Пошатывание x" + enemy.StaggerDisplay.ToString("0.##");
        }

        label.text = text;
    }

    private void AppendIfPresent(ref string text, string name, StatusEffectType type)
    {
        float stacks = target.GetStacks(type);
        if (stacks <= 0) return;
        if (text.Length > 0) text += " | ";
        text += name + " x" + stacks.ToString("0.##");
    }
}