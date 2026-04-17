using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

public class BattleUIBuilder : EditorWindow
{
    static readonly Color Black = new Color(0.05f, 0.05f, 0.08f, 1f);
    static readonly Color DarkGray = new Color(0.12f, 0.12f, 0.15f, 1f);
    static readonly Color MedGray = new Color(0.22f, 0.22f, 0.28f, 1f);
    static readonly Color PanelBg = new Color(0.08f, 0.08f, 0.12f, 0.95f);
    static readonly Color HPGreen = new Color(0.29f, 0.87f, 0.50f);
    static readonly Color TPBlue = new Color(0.35f, 0.55f, 0.95f);
    static readonly Color BarBg = new Color(0.18f, 0.18f, 0.22f, 1f);
    static readonly Color BorderColor = new Color(0.35f, 0.35f, 0.42f, 1f);

    [MenuItem("Tools/Battle/Build Battle UI")]
    static void Build()
    {
        BattleUI battleUI = Object.FindObjectOfType<BattleUI>();
        if (battleUI == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No BattleUI component found in the scene.\nAdd a BattleUI script to a GameObject under Canvas first.", "OK");
            return;
        }

        Canvas canvas = battleUI.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in the scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Battle UI");
        Undo.RecordObject(battleUI, "Build Battle UI");

        Transform root = battleUI.transform;
        Debug.Log($"[BattleUIBuilder] Root: {root.name}, parent: {root.parent?.name}, childCount before clear: {root.childCount}");

        RectTransform rootRect = root.GetComponent<RectTransform>();
        if (rootRect == null) rootRect = root.gameObject.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.localScale = Vector3.one;
        rootRect.localPosition = Vector3.zero;

        ClearChildren(root);
        Debug.Log($"[BattleUIBuilder] After clear: childCount = {root.childCount}");

        // ════════════════════════════════════════════
        //  ENEMY INFO PANEL — top-right
        // ════════════════════════════════════════════
        RectTransform enemyPanel = CreateRect(root, "Enemy Panel");
        enemyPanel.anchorMin = new Vector2(1, 1);
        enemyPanel.anchorMax = new Vector2(1, 1);
        enemyPanel.pivot = new Vector2(1, 1);
        enemyPanel.anchoredPosition = new Vector2(-30, -20);
        enemyPanel.sizeDelta = new Vector2(380, 110);
        AddPanelBg(enemyPanel, PanelBg, BorderColor);

        float ey = -10;
        TextMeshProUGUI enemyName = CreateTMP(enemyPanel, "Enemy Name", "Wild Pokemon", 26,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, ey), new Vector2(250, 32));
        TextMeshProUGUI enemyLevel = CreateTMP(enemyPanel, "Enemy Level", "Lv.5", 22,
            TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(270, ey), new Vector2(96, 32));

        ey -= 34;
        TextMeshProUGUI enemyStatus = CreateTMP(enemyPanel, "Enemy Status", "", 16,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, ey), new Vector2(80, 22));

        ey -= 24;
        CreateTMP(enemyPanel, "Enemy HP Label", "HP", 14,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, ey), new Vector2(30, 20));
        Slider enemyHP = CreateSlider(enemyPanel, "Enemy HP Bar", HPGreen,
            new Vector2(48, ey), new Vector2(266, 16));
        TextMeshProUGUI enemyHPText = CreateTMP(enemyPanel, "Enemy HP Text", "50 / 50", 13,
            TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(200, ey), new Vector2(166, 16));

        // ════════════════════════════════════════════
        //  PLAYER INFO PANEL — bottom-left, above bottom bar
        // ════════════════════════════════════════════
        RectTransform playerPanel = CreateRect(root, "Player Panel");
        playerPanel.anchorMin = new Vector2(0, 0);
        playerPanel.anchorMax = new Vector2(0, 0);
        playerPanel.pivot = new Vector2(0, 0);
        playerPanel.anchoredPosition = new Vector2(30, 240);
        playerPanel.sizeDelta = new Vector2(380, 136);
        AddPanelBg(playerPanel, PanelBg, BorderColor);

        float py = -10;
        TextMeshProUGUI playerName = CreateTMP(playerPanel, "Player Name", "Pokemon", 26,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, py), new Vector2(250, 32));
        TextMeshProUGUI playerLevel = CreateTMP(playerPanel, "Player Level", "Lv.5", 22,
            TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(270, py), new Vector2(96, 32));

        py -= 34;
        TextMeshProUGUI playerStatus = CreateTMP(playerPanel, "Player Status", "", 16,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, py), new Vector2(80, 22));

        py -= 24;
        CreateTMP(playerPanel, "Player HP Label", "HP", 14,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, py), new Vector2(30, 20));
        Slider playerHP = CreateSlider(playerPanel, "Player HP Bar", HPGreen,
            new Vector2(48, py), new Vector2(266, 16));
        TextMeshProUGUI playerHPText = CreateTMP(playerPanel, "Player HP Text", "100 / 100", 13,
            TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(200, py), new Vector2(166, 16));

        py -= 22;
        CreateTMP(playerPanel, "Player TP Label", "TP", 12,
            TextAlignmentOptions.Left, FontStyles.Bold, new Vector2(14, py), new Vector2(30, 16));
        Slider playerTP = CreateSlider(playerPanel, "Player TP Bar", TPBlue,
            new Vector2(48, py), new Vector2(266, 12));
        TextMeshProUGUI playerTPText = CreateTMP(playerPanel, "Player TP Text", "TP: 40 / 40", 12,
            TextAlignmentOptions.Right, FontStyles.Normal, new Vector2(200, py), new Vector2(166, 12));

        // ════════════════════════════════════════════
        //  BOTTOM BAR — classic black panel
        // ════════════════════════════════════════════
        RectTransform bottomBar = CreateRect(root, "Bottom Bar");
        bottomBar.anchorMin = new Vector2(0, 0);
        bottomBar.anchorMax = new Vector2(1, 0);
        bottomBar.pivot = new Vector2(0.5f, 0);
        bottomBar.anchoredPosition = Vector2.zero;
        bottomBar.sizeDelta = new Vector2(0, 230);

        Image bottomBg = bottomBar.gameObject.AddComponent<Image>();
        bottomBg.color = Black;

        RectTransform borderLine = CreateRect(bottomBar, "Top Border");
        borderLine.anchorMin = new Vector2(0, 1);
        borderLine.anchorMax = new Vector2(1, 1);
        borderLine.pivot = new Vector2(0.5f, 1);
        borderLine.anchoredPosition = Vector2.zero;
        borderLine.sizeDelta = new Vector2(0, 3);
        Image borderImg = borderLine.gameObject.AddComponent<Image>();
        borderImg.color = BorderColor;

        // ── Battle Log — left half of bottom bar ──
        RectTransform battleLog = CreateRect(bottomBar, "Battle Log");
        battleLog.anchorMin = new Vector2(0, 0);
        battleLog.anchorMax = new Vector2(0.48f, 1);
        battleLog.offsetMin = new Vector2(24, 16);
        battleLog.offsetMax = new Vector2(-8, -16);

        TextMeshProUGUI logText = CreateTMPStretched(battleLog, "Log Text", "", 24,
            TextAlignmentOptions.TopLeft, FontStyles.Normal);
        logText.textWrappingMode = TextWrappingModes.Normal;
        logText.overflowMode = TextOverflowModes.Ellipsis;

        // ── Skill Panel — right half of bottom bar ──
        RectTransform skillPanel = CreateRect(bottomBar, "Skill Panel");
        skillPanel.anchorMin = new Vector2(0.48f, 0);
        skillPanel.anchorMax = new Vector2(1, 1);
        skillPanel.offsetMin = new Vector2(8, 12);
        skillPanel.offsetMax = new Vector2(-16, -12);

        GridLayoutGroup grid = skillPanel.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240, 90);
        grid.spacing = new Vector2(10, 10);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.MiddleCenter;

        Button[] skillButtons = new Button[4];
        TextMeshProUGUI[] skillTexts = new TextMeshProUGUI[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject btnObj = new GameObject($"Skill Button {i + 1}",
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnObj.transform.SetParent(skillPanel, false);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = MedGray;

            Outline outline = btnObj.GetComponent<Outline>();
            outline.effectColor = BorderColor;
            outline.effectDistance = new Vector2(2, -2);

            Button btn = btnObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            cb.disabledColor = new Color(0.35f, 0.35f, 0.35f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            GameObject textObj = new GameObject("Text (TMP)", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10, 6);
            tr.offsetMax = new Vector2(-10, -6);

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            tmp.text = $"Skill {i + 1}";
            tmp.fontSize = 20;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.richText = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            skillButtons[i] = btn;
            skillTexts[i] = tmp;
        }

        // ════════════════════════════════════════════
        //  WIRE REFERENCES
        // ════════════════════════════════════════════
        battleUI.SkillButtons = skillButtons;
        battleUI.SkillButtonTexts = skillTexts;
        battleUI.PlayerHPBar = playerHP;
        battleUI.EnemyHPBar = enemyHP;
        battleUI.PlayerHPText = playerHPText;
        battleUI.EnemyHPText = enemyHPText;
        battleUI.PlayerTPBar = playerTP;
        battleUI.PlayerTPText = playerTPText;
        battleUI.PlayerNameText = playerName;
        battleUI.EnemyNameText = enemyName;
        battleUI.PlayerLevelText = playerLevel;
        battleUI.EnemyLevelText = enemyLevel;
        battleUI.PlayerStatusText = playerStatus;
        battleUI.EnemyStatusText = enemyStatus;
        battleUI.BattleLogText = logText;

        EditorUtility.SetDirty(battleUI);
        EditorUtility.SetDirty(canvas.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"[BattleUIBuilder] Final childCount = {root.childCount}");
        for (int i = 0; i < root.childCount; i++)
            Debug.Log($"[BattleUIBuilder]   child[{i}]: {root.GetChild(i).name}");
        Debug.Log("[BattleUIBuilder] Classic battle UI built and wired successfully!");
        EditorUtility.DisplayDialog("Done",
            "Classic Battle UI built!\n\n" +
            "- Enemy info panel (top-left)\n" +
            "- Player info panel (right, above bar)\n" +
            "- Black bottom bar with log + skills\n\n" +
            "Save the scene (Cmd+S / Ctrl+S).", "OK");
    }

    // ─── Helpers ──────────────────────────────────────

    static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.GetComponent<RectTransform>();
    }

    static void AddPanelBg(RectTransform panel, Color bgColor, Color borderColor)
    {
        Image bg = panel.gameObject.AddComponent<Image>();
        bg.color = bgColor;

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = borderColor;
        outline.effectDistance = new Vector2(2, -2);
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions align, FontStyles style,
        Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.richText = true;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    static TextMeshProUGUI CreateTMPStretched(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions align, FontStyles style)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4, 4);
        rt.offsetMax = new Vector2(-4, -4);

        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.richText = true;
        return tmp;
    }

    static Slider CreateSlider(Transform parent, string name, Color fillColor,
        Vector2 pos, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Slider));
        obj.transform.SetParent(parent, false);

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(obj.transform, false);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bgObj.GetComponent<Image>().color = BarBg;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(obj.transform, false);
        RectTransform faRt = fillArea.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fRt = fill.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = fillColor;

        Slider slider = obj.GetComponent<Slider>();
        slider.fillRect = fRt;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.value = 100;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    static void ClearChildren(Transform parent)
    {
        var children = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < parent.childCount; i++)
            children.Add(parent.GetChild(i).gameObject);
        foreach (var child in children)
            Object.DestroyImmediate(child);
    }
}
