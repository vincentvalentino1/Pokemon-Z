using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class OpenWorldEncounterPromptUI : MonoBehaviour
{
    static OpenWorldEncounterPromptUI _instance;
    public static bool IsOpen => _instance != null && _instance._panel != null && _instance._panel.activeSelf;

    Canvas _canvas;
    GameObject _panel;
    Text _titleText;
    Text _bodyText;
    Button _battleButton;
    Button _cancelButton;
    WorldPokemonEncounter _pendingEncounter;

    public static void Show(WorldPokemonEncounter encounter)
    {
        if (encounter == null)
            return;

        if (_instance == null)
            CreateInstance();

        _instance.ShowInternal(encounter);
    }

    static void CreateInstance()
    {
        GameObject go = new GameObject("OpenWorldEncounterPromptUI");
        _instance = go.AddComponent<OpenWorldEncounterPromptUI>();
        _instance.BuildUi();
    }

    void BuildUi()
    {
        EnsureEventSystem();

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameObject.AddComponent<GraphicRaycaster>();
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        _panel = CreateUiObject("Panel", transform, typeof(Image));
        RectTransform panelRt = _panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(480f, 220f);
        panelRt.anchoredPosition = new Vector2(0f, 30f);
        _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        _titleText = CreateText("Title", _panel.transform, 28, FontStyle.Bold);
        RectTransform titleRt = _titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-30f, 42f);
        titleRt.anchoredPosition = new Vector2(0f, -12f);
        _titleText.alignment = TextAnchor.MiddleCenter;
        _titleText.text = "Wild Encounter";

        _bodyText = CreateText("Body", _panel.transform, 22, FontStyle.Normal);
        RectTransform bodyRt = _bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0.4f);
        bodyRt.anchorMax = new Vector2(1f, 0.9f);
        bodyRt.offsetMin = new Vector2(20f, 0f);
        bodyRt.offsetMax = new Vector2(-20f, 0f);
        _bodyText.alignment = TextAnchor.MiddleCenter;

        _battleButton = CreateButton("BattleButton", _panel.transform, "Battle", new Vector2(-95f, 26f));
        _cancelButton = CreateButton("CancelButton", _panel.transform, "Cancel", new Vector2(95f, 26f));

        _battleButton.onClick.AddListener(ConfirmBattle);
        _cancelButton.onClick.AddListener(Hide);

        Hide();
    }

    void ShowInternal(WorldPokemonEncounter encounter)
    {
        _pendingEncounter = encounter;
        _bodyText.text = encounter.RuntimeData?.Instance?.SpeciesData != null
            ? "A wild " + encounter.RuntimeData.Instance.SpeciesData.PokemonName + " appeared."
            : "A wild Pokemon appeared.";
        SetCursorForUi(true);
        _panel.SetActive(true);
    }

    void ConfirmBattle()
    {
        if (_pendingEncounter != null)
            _pendingEncounter.BeginEncounterFromPrompt();
        Hide();
    }

    void Hide()
    {
        _pendingEncounter = null;
        SetCursorForUi(false);
        if (_panel != null)
            _panel.SetActive(false);
    }

    static GameObject CreateUiObject(string name, Transform parent, params System.Type[] extraComponents)
    {
        GameObject go = new GameObject(name, extraComponents);
        go.transform.SetParent(parent, false);
        if (go.GetComponent<RectTransform>() == null)
            go.AddComponent<RectTransform>();
        return go;
    }

    static Text CreateText(string name, Transform parent, int fontSize, FontStyle style)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Text));
        Text txt = go.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = Color.white;
        return txt;
    }

    static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(160f, 54f);
        rt.anchoredPosition = anchoredPos;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.2f, 0.3f, 1f);

        Button btn = go.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.25f, 0.32f, 0.46f, 1f);
        cb.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
        btn.colors = cb;

        Text t = CreateText("Label", go.transform, 24, FontStyle.Bold);
        RectTransform tr = t.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        t.alignment = TextAnchor.MiddleCenter;
        t.text = label;

        return btn;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    static void SetCursorForUi(bool uiOpen)
    {
        Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = uiOpen;
    }
}
