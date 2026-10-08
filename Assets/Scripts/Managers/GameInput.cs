using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Единственное место, где читаются горячие клавиши. Боевые действия (слоты, Wait) не доходят до подписчиков, пока открыт любой оверлей; Cancel, ToggleMap, ToggleDeck работают всегда. Остальной код подписывается на события и сам решает, уместны ли они в текущем контексте.
public class GameInput : MonoBehaviour
{
    public InputActionAsset actions;

    public static GameInput Instance { get; private set; }

    public event Action<int> OnSlot; // 0..6
    public event Action OnWait;
    public event Action OnCancel;
    public event Action OnToggleMap;
    public event Action OnToggleDeck;

    private InputActionMap map;

    void Awake()
    {
        Instance = this;
        actions = Instantiate(actions); // своя копия: состояние ассета переживает выход из Play Mode и ломает повторное включение карты
        map = actions.FindActionMap("Game", true);
        for (int i = 0; i < HandManager.MaxHandSize; i++)
        {
            int slot = i;
            map.FindAction("Slot" + (i + 1), true).performed += _ => HandleSlot(slot);
        }
        map.FindAction("Wait", true).performed += _ => HandleWait();
        map.FindAction("Cancel", true).performed += _ => OnCancel?.Invoke();
        map.FindAction("ToggleMap", true).performed += _ => OnToggleMap?.Invoke();
        map.FindAction("ToggleDeck", true).performed += _ => OnToggleDeck?.Invoke();
    }

    private void HandleSlot(int slot)
    {
        if (!ScreenManager.Instance.HasOverlay) OnSlot?.Invoke(slot);
    }

    private void HandleWait()
    {
        if (!ScreenManager.Instance.HasOverlay) OnWait?.Invoke();
    }

    void OnEnable() => map.Enable();
    void OnDisable() => map.Disable();
    void OnDestroy() => Destroy(actions);
}
