using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class OpenWorldPauseMenu : MonoBehaviour
{
    enum MenuSection
    {
        Party,
        Pokedex,
        Bag,
        Map,
        Save,
        Settings
    }

    static OpenWorldPauseMenu _instance;

    public static bool IsOpen => _instance != null && _instance._panelRoot != null && _instance._panelRoot.activeSelf;

    Canvas _canvas;
    GameObject _panelRoot;
    GameObject _textContentRoot;
    GameObject _pokedexContentRoot;
    Text _headerText;
    Text _bodyText;
    Text _footerText;
    Button _resumeButton;
    Button[] _sectionButtons;
    Image _pokedexPortraitImage;
    Text _pokedexNameText;
    Text _pokedexDataText;
    Text _pokedexStatusText;
    Text _pokedexSliderLabel;
    Text _pokedexHintText;
    Slider _pokedexSlider;
    MenuSection _selectedSection = MenuSection.Party;
    float _timeScaleBeforePause = 1f;
    readonly List<PokedexSpeciesEntry> _pokedexEntries = new List<PokedexSpeciesEntry>();
    readonly Dictionary<string, Sprite> _portraitSpriteCache = new Dictionary<string, Sprite>();
    PokedexDatabase _pokedexDatabase;
    int _selectedPokedexIndex;
    bool _isUpdatingPokedexSlider;

    public static void EnsureExists()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject("OpenWorldPauseMenu");
        _instance = go.AddComponent<OpenWorldPauseMenu>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        BuildUi();
        HideImmediate();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;

        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    void Update()
    {
        if (OpenWorldEncounterPromptUI.IsOpen)
        {
            if (IsOpen)
                Hide();
            return;
        }

        if (WasPausePressed())
        {
            if (IsOpen)
                Hide();
            else
                Show();
        }

        if (IsOpen && _selectedSection == MenuSection.Pokedex)
            HandlePokedexSelectionInput();
    }

    void BuildUi()
    {
        EnsureEventSystem();

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;
        gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _panelRoot = CreateUiObject("Pause Root", transform, typeof(Image));
        RectTransform rootRt = _panelRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        _panelRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        GameObject window = CreateUiObject("Window", _panelRoot.transform, typeof(Image));
        RectTransform windowRt = window.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.pivot = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(1160f, 680f);
        window.GetComponent<Image>().color = new Color(0.07f, 0.10f, 0.14f, 0.97f);

        GameObject sidebar = CreateUiObject("Sidebar", window.transform, typeof(Image));
        RectTransform sidebarRt = sidebar.GetComponent<RectTransform>();
        sidebarRt.anchorMin = new Vector2(0f, 0f);
        sidebarRt.anchorMax = new Vector2(0f, 1f);
        sidebarRt.pivot = new Vector2(0f, 0.5f);
        sidebarRt.sizeDelta = new Vector2(290f, 0f);
        sidebar.GetComponent<Image>().color = new Color(0.11f, 0.16f, 0.20f, 1f);

        GameObject content = CreateUiObject("Content", window.transform, typeof(Image));
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 0f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.offsetMin = new Vector2(290f, 0f);
        contentRt.offsetMax = Vector2.zero;
        content.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.17f, 1f);

        _headerText = CreateText("Header", content.transform, 36, FontStyle.Bold, TextAnchor.UpperLeft);
        RectTransform headerRt = _headerText.rectTransform;
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0f, 1f);
        headerRt.offsetMin = new Vector2(44f, -84f);
        headerRt.offsetMax = new Vector2(-44f, -24f);

        _textContentRoot = CreateUiObject("Text Content", content.transform);
        RectTransform textRootRt = _textContentRoot.GetComponent<RectTransform>();
        textRootRt.anchorMin = Vector2.zero;
        textRootRt.anchorMax = Vector2.one;
        textRootRt.offsetMin = Vector2.zero;
        textRootRt.offsetMax = Vector2.zero;

        _bodyText = CreateText("Body", _textContentRoot.transform, 25, FontStyle.Normal, TextAnchor.UpperLeft);
        RectTransform bodyRt = _bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.offsetMin = new Vector2(44f, 82f);
        bodyRt.offsetMax = new Vector2(-44f, -110f);
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        _footerText = CreateText("Footer", _textContentRoot.transform, 20, FontStyle.Italic, TextAnchor.LowerLeft);
        RectTransform footerRt = _footerText.rectTransform;
        footerRt.anchorMin = new Vector2(0f, 0f);
        footerRt.anchorMax = new Vector2(1f, 0f);
        footerRt.offsetMin = new Vector2(44f, 26f);
        footerRt.offsetMax = new Vector2(-44f, 66f);

        BuildPokedexUi(content.transform);

        _resumeButton = CreateButton("Resume", sidebar.transform, "Resume", new Vector2(0f, -38f));
        _resumeButton.onClick.AddListener(Hide);

        string[] sectionLabels = { "Party", "Pokedex", "Bag", "Map", "Save", "Settings" };
        _sectionButtons = new Button[sectionLabels.Length];
        for (int i = 0; i < sectionLabels.Length; i++)
        {
            float y = -122f - (i * 86f);
            Button button = CreateButton(sectionLabels[i] + " Button", sidebar.transform, sectionLabels[i], new Vector2(0f, y));
            int index = i;
            button.onClick.AddListener(() => ShowSection((MenuSection)index));
            _sectionButtons[i] = button;
        }

        ShowSection(MenuSection.Party);
    }

    void BuildPokedexUi(Transform parent)
    {
        _pokedexContentRoot = CreateUiObject("Pokedex Content", parent);
        RectTransform rootRt = _pokedexContentRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        GameObject portraitCard = CreateUiObject("Portrait Card", _pokedexContentRoot.transform, typeof(Image));
        RectTransform portraitCardRt = portraitCard.GetComponent<RectTransform>();
        portraitCardRt.anchorMin = new Vector2(0f, 1f);
        portraitCardRt.anchorMax = new Vector2(0f, 1f);
        portraitCardRt.pivot = new Vector2(0f, 1f);
        portraitCardRt.sizeDelta = new Vector2(290f, 360f);
        portraitCardRt.anchoredPosition = new Vector2(44f, -122f);
        portraitCard.GetComponent<Image>().color = new Color(0.14f, 0.19f, 0.24f, 1f);

        _pokedexPortraitImage = CreateImage("Portrait", portraitCard.transform, new Color(1f, 1f, 1f, 1f));
        RectTransform portraitRt = _pokedexPortraitImage.rectTransform;
        portraitRt.anchorMin = new Vector2(0.5f, 0.5f);
        portraitRt.anchorMax = new Vector2(0.5f, 0.5f);
        portraitRt.pivot = new Vector2(0.5f, 0.5f);
        portraitRt.sizeDelta = new Vector2(240f, 240f);
        portraitRt.anchoredPosition = new Vector2(0f, -12f);
        _pokedexPortraitImage.preserveAspect = true;

        _pokedexNameText = CreateText("Pokedex Name", _pokedexContentRoot.transform, 30, FontStyle.Bold, TextAnchor.UpperLeft);
        RectTransform nameRt = _pokedexNameText.rectTransform;
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.offsetMin = new Vector2(370f, -138f);
        nameRt.offsetMax = new Vector2(-44f, -92f);

        _pokedexStatusText = CreateText("Pokedex Status", _pokedexContentRoot.transform, 20, FontStyle.Italic, TextAnchor.UpperLeft);
        RectTransform statusRt = _pokedexStatusText.rectTransform;
        statusRt.anchorMin = new Vector2(0f, 1f);
        statusRt.anchorMax = new Vector2(1f, 1f);
        statusRt.pivot = new Vector2(0f, 1f);
        statusRt.offsetMin = new Vector2(370f, -182f);
        statusRt.offsetMax = new Vector2(-44f, -140f);
        _pokedexStatusText.color = new Color(0.82f, 0.90f, 0.97f, 1f);

        GameObject detailsCard = CreateUiObject("Details Card", _pokedexContentRoot.transform, typeof(Image));
        RectTransform detailsCardRt = detailsCard.GetComponent<RectTransform>();
        detailsCardRt.anchorMin = new Vector2(0f, 0f);
        detailsCardRt.anchorMax = new Vector2(1f, 1f);
        detailsCardRt.offsetMin = new Vector2(348f, 118f);
        detailsCardRt.offsetMax = new Vector2(-44f, -190f);
        detailsCard.GetComponent<Image>().color = new Color(0.11f, 0.16f, 0.21f, 1f);

        _pokedexDataText = CreateText("Pokedex Data", detailsCard.transform, 22, FontStyle.Normal, TextAnchor.UpperLeft);
        RectTransform dataRt = _pokedexDataText.rectTransform;
        dataRt.anchorMin = Vector2.zero;
        dataRt.anchorMax = Vector2.one;
        dataRt.offsetMin = new Vector2(24f, 24f);
        dataRt.offsetMax = new Vector2(-24f, -24f);
        _pokedexDataText.verticalOverflow = VerticalWrapMode.Overflow;

        _pokedexSliderLabel = CreateText("Pokedex Slider Label", _pokedexContentRoot.transform, 22, FontStyle.Bold, TextAnchor.LowerLeft);
        RectTransform sliderLabelRt = _pokedexSliderLabel.rectTransform;
        sliderLabelRt.anchorMin = new Vector2(0f, 0f);
        sliderLabelRt.anchorMax = new Vector2(1f, 0f);
        sliderLabelRt.offsetMin = new Vector2(44f, 110f);
        sliderLabelRt.offsetMax = new Vector2(-44f, 148f);

        _pokedexHintText = CreateText("Pokedex Hint", _pokedexContentRoot.transform, 18, FontStyle.Italic, TextAnchor.LowerLeft);
        RectTransform hintRt = _pokedexHintText.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.offsetMin = new Vector2(44f, 16f);
        hintRt.offsetMax = new Vector2(-44f, 46f);
        _pokedexHintText.color = new Color(0.84f, 0.91f, 0.96f, 1f);

        _pokedexSlider = CreateSlider("Pokedex Slider", _pokedexContentRoot.transform);
        RectTransform sliderRt = _pokedexSlider.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 0f);
        sliderRt.anchorMax = new Vector2(1f, 0f);
        sliderRt.offsetMin = new Vector2(44f, 54f);
        sliderRt.offsetMax = new Vector2(-44f, 92f);
        _pokedexSlider.onValueChanged.AddListener(OnPokedexSliderChanged);
    }

    void Show()
    {
        _timeScaleBeforePause = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        ShowSection(_selectedSection);
        _panelRoot.SetActive(true);
        SetCursorForMenu(true);
    }

    void Hide()
    {
        Time.timeScale = Mathf.Approximately(_timeScaleBeforePause, 0f) ? 1f : _timeScaleBeforePause;
        AudioListener.pause = false;
        _panelRoot.SetActive(false);
        SetCursorForMenu(false);
    }

    void HideImmediate()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (_panelRoot != null)
            _panelRoot.SetActive(false);
        SetCursorForMenu(false);
    }

    void ShowSection(MenuSection section)
    {
        _selectedSection = section;
        RefreshButtonStyles();

        switch (section)
        {
            case MenuSection.Party:
                ShowTextPage();
                _headerText.text = "Party";
                _bodyText.text = BuildPartyText();
                _footerText.text = "Review your current team. Party lead can be managed next if you want.";
                break;
            case MenuSection.Pokedex:
                ShowPokedexPage();
                break;
            case MenuSection.Bag:
                ShowTextPage();
                _headerText.text = "Bag";
                _bodyText.text = BuildBagText();
                _footerText.text = "Pokeballs are now consumed in battle. Healing items and key items can plug into this bag next.";
                break;
            case MenuSection.Map:
                ShowTextPage();
                _headerText.text = "Map";
                _bodyText.text = "Map menu placeholder.\n\nThis can become your region map, fast-travel, quest markers, or minimap detail screen.";
                _footerText.text = "Next step: hook this to your world/region data.";
                break;
            case MenuSection.Save:
                ShowTextPage();
                _headerText.text = "Save";
                _bodyText.text = "Save menu placeholder.\n\nNo save system is connected yet, but this is the slot where manual save or save-slot UI should open.";
                _footerText.text = "Next step: connect serialization and save slots.";
                break;
            case MenuSection.Settings:
                ShowTextPage();
                _headerText.text = "Settings";
                _bodyText.text = "Settings menu placeholder.\n\nUse this for audio, controls, graphics, camera sensitivity, and accessibility options.";
                _footerText.text = "Press Esc or Resume to return to the game.";
                break;
        }
    }

    string BuildPartyText()
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        if (manager == null || manager.PartyCount == 0)
            return "No Pokemon in party.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Trainer: {manager.PlayerTrainerName}");
        sb.AppendLine($"Party: {manager.PartyCount}/{OpenWorldEncounterManager.MaxPartySize}");
        sb.AppendLine($"Storage: {manager.StorageCount}");
        sb.AppendLine();

        for (int i = 0; i < manager.Party.Count; i++)
        {
            PokemonInstance pokemon = manager.Party[i];
            if (pokemon == null)
                continue;

            string leadTag = i == 0 ? " [Lead]" : "";
            sb.AppendLine($"{i + 1}. {BattleUI.GetName(pokemon)}{leadTag}");
            sb.AppendLine($"   Lv.{pokemon.Level}  HP {pokemon.CurrentHP}/{pokemon.MaxHP}  TP {pokemon.CurrentTP}/{pokemon.MaxTP}");
            if (pokemon.SpeciesData != null)
                sb.AppendLine($"   Type: {pokemon.SpeciesData.PrimaryType} / {pokemon.SpeciesData.SecondaryType}");
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    string BuildBagText()
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        if (manager == null)
            return "Bag is unavailable.";

        IReadOnlyList<OpenWorldEncounterManager.BagItemStack> bagItems = manager.BagItems;
        if (bagItems == null || bagItems.Count == 0)
            return "Your bag is empty.";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Trainer: {manager.PlayerTrainerName}");
        sb.AppendLine("Items:");
        sb.AppendLine();
        bool foundAny = false;

        for (int i = 0; i < bagItems.Count; i++)
        {
            OpenWorldEncounterManager.BagItemStack stack = bagItems[i];
            if (stack == null || stack.Count <= 0)
                continue;

            string displayName = string.IsNullOrWhiteSpace(stack.DisplayName)
                ? stack.ItemId
                : stack.DisplayName;
            sb.AppendLine($"{i + 1}. {displayName} x{stack.Count}");
            foundAny = true;
        }

        if (!foundAny)
            return "Your bag is empty.";

        return sb.ToString().TrimEnd();
    }

    void ShowTextPage()
    {
        _textContentRoot.SetActive(true);
        _pokedexContentRoot.SetActive(false);
    }

    void ShowPokedexPage()
    {
        _headerText.text = "Pokedex";
        ShowPokedexContent();
        RefreshPokedexData();
    }

    void ShowPokedexContent()
    {
        _textContentRoot.SetActive(false);
        _pokedexContentRoot.SetActive(true);
    }

    void RefreshPokedexData()
    {
        EnsurePokedexEntries();

        if (_pokedexEntries.Count == 0)
        {
            _pokedexNameText.text = "No Pokedex Data";
            _pokedexStatusText.text =
                $"Add species entries via a PokedexDatabase at Resources/{PokemonContentPaths.ResourcesPokedexDatabase} " +
                $"(place source assets under {PokemonContentPaths.AuthoringPokedex}).";
            _pokedexDataText.text = "The Pokedex database is empty right now, so there is nothing to scroll yet.";
            _pokedexPortraitImage.sprite = null;
            _pokedexPortraitImage.color = new Color(0f, 0f, 0f, 0f);
            _pokedexSliderLabel.text = "No species available";
            _pokedexHintText.text = "Add entries to the Pokedex database to populate this screen.";
            UpdateSliderBounds(0);
            return;
        }

        _selectedPokedexIndex = Mathf.Clamp(_selectedPokedexIndex, 0, _pokedexEntries.Count - 1);
        UpdateSliderBounds(_pokedexEntries.Count);

        PokedexSpeciesEntry entry = _pokedexEntries[_selectedPokedexIndex];
        PokemonData species = entry != null ? entry.Species : null;
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        bool hasSeen = manager != null && manager.HasSeenSpecies(species);
        bool hasCaught = manager != null && manager.HasCaughtSpecies(species);
        bool canReveal = hasSeen || hasCaught;

        int dexNumber = species != null ? species.DexNumber : _selectedPokedexIndex + 1;
        _pokedexSliderLabel.text = $"Dex {dexNumber:000}   {_selectedPokedexIndex + 1}/{_pokedexEntries.Count}";
        _pokedexNameText.text = canReveal && species != null
            ? $"#{dexNumber:000} {species.PokemonName}"
            : $"#{dexNumber:000} ???";
        _pokedexStatusText.text = hasCaught
            ? "Caught and fully registered"
            : hasSeen
                ? "Encountered and registered"
                : "Unseen silhouette";
        _pokedexDataText.text = canReveal
            ? BuildKnownPokedexText(species, hasCaught)
            : BuildUnknownPokedexText();
        _pokedexPortraitImage.sprite = LoadPokedexPortrait(entry);
        _pokedexPortraitImage.color = canReveal
            ? Color.white
            : new Color(0f, 0f, 0f, 0.95f);
        _pokedexHintText.text = "Use the slider or Left/Right to browse. Unseen Pokemon stay hidden until you meet them.";
    }

    void EnsurePokedexEntries()
    {
        if (_pokedexEntries.Count > 0)
            return;

        _pokedexDatabase = Resources.Load<PokedexDatabase>(PokemonContentPaths.ResourcesPokedexDatabase);
        if (_pokedexDatabase == null || _pokedexDatabase.SpeciesEntries == null)
            return;

        for (int i = 0; i < _pokedexDatabase.SpeciesEntries.Length; i++)
        {
            PokedexSpeciesEntry entry = _pokedexDatabase.SpeciesEntries[i];
            if (entry == null || entry.Species == null)
                continue;

            _pokedexEntries.Add(entry);
        }

        _pokedexEntries.Sort(ComparePokedexEntries);
    }

    static int ComparePokedexEntries(PokedexSpeciesEntry a, PokedexSpeciesEntry b)
    {
        int leftDex = a != null && a.Species != null ? a.Species.DexNumber : int.MaxValue;
        int rightDex = b != null && b.Species != null ? b.Species.DexNumber : int.MaxValue;
        if (leftDex != rightDex)
            return leftDex.CompareTo(rightDex);

        string leftName = a != null && a.Species != null ? a.Species.PokemonName : string.Empty;
        string rightName = b != null && b.Species != null ? b.Species.PokemonName : string.Empty;
        return string.Compare(leftName, rightName, System.StringComparison.OrdinalIgnoreCase);
    }

    void HandlePokedexSelectionInput()
    {
        if (_pokedexEntries.Count <= 1)
            return;

        int direction = 0;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                direction = -1;
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                direction = 1;
        }

        Mouse mouse = Mouse.current;
        if (direction == 0 && mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0.01f)
                direction = 1;
            else if (scrollY < -0.01f)
                direction = -1;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (direction == 0)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                direction = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                direction = 1;
        }
#endif
        if (direction != 0)
            SetPokedexIndex(_selectedPokedexIndex + direction);
    }

    void SetPokedexIndex(int index)
    {
        if (_pokedexEntries.Count == 0)
            return;

        _selectedPokedexIndex = Mathf.Clamp(index, 0, _pokedexEntries.Count - 1);
        UpdateSliderWithoutNotify(_selectedPokedexIndex);
        RefreshPokedexData();
    }

    void OnPokedexSliderChanged(float value)
    {
        if (_isUpdatingPokedexSlider)
            return;

        SetPokedexIndex(Mathf.RoundToInt(value));
    }

    void UpdateSliderBounds(int entryCount)
    {
        _isUpdatingPokedexSlider = true;
        _pokedexSlider.wholeNumbers = true;
        _pokedexSlider.minValue = 0f;
        _pokedexSlider.maxValue = Mathf.Max(0, entryCount - 1);
        _pokedexSlider.value = Mathf.Clamp(_selectedPokedexIndex, 0, Mathf.Max(0, entryCount - 1));
        _pokedexSlider.interactable = entryCount > 1;
        _isUpdatingPokedexSlider = false;
    }

    void UpdateSliderWithoutNotify(int value)
    {
        _isUpdatingPokedexSlider = true;
        _pokedexSlider.value = value;
        _isUpdatingPokedexSlider = false;
    }

    string BuildKnownPokedexText(PokemonData species, bool hasCaught)
    {
        if (species == null)
            return "Species data is missing.";

        string primaryType = species.PrimaryType.ToString();
        string secondaryType = species.SecondaryType == species.PrimaryType
            ? "-"
            : species.SecondaryType.ToString();
        string abilityList = BuildAbilityList(species);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Type: {primaryType} / {secondaryType}");
        sb.AppendLine($"Body Type: {species.BodyType}");
        sb.AppendLine($"Height: {species.HeightM:0.0} m");
        sb.AppendLine($"Weight: {species.WeightKg:0.0} kg");
        sb.AppendLine($"Catch Rate: {species.CatchRate}");
        sb.AppendLine($"Base Friendship: {species.BaseFriendship}");
        sb.AppendLine($"Growth Rate: {species.ExpGrowthRate}");
        sb.AppendLine($"Base EXP Yield: {species.BaseExpYield}");
        sb.AppendLine($"Ability Pool: {abilityList}");
        sb.AppendLine();
        sb.AppendLine("Base Stats");
        sb.AppendLine($"HP {species.BaseStats.HP}  ATK {species.BaseStats.Attack}  DEF {species.BaseStats.Defense}");
        sb.AppendLine($"SPA {species.BaseStats.SpAttack}  SPD {species.BaseStats.SpDefense}  SPE {species.BaseStats.Speed}");
        sb.AppendLine();
        sb.AppendLine("Pokedex Entry");
        sb.AppendLine(string.IsNullOrWhiteSpace(species.PokedexEntry) ? "No entry written yet." : species.PokedexEntry);
        sb.AppendLine();
        sb.Append(hasCaught ? "Ownership: Captured" : "Ownership: Encountered");
        return sb.ToString();
    }

    static string BuildAbilityList(PokemonData species)
    {
        if (species == null)
            return "Unknown";

        List<string> names = new List<string>(3);
        if (species.PrimaryAbility != null && !string.IsNullOrWhiteSpace(species.PrimaryAbility.AbilityName))
            names.Add(species.PrimaryAbility.AbilityName);
        if (species.SecondaryAbility != null && !string.IsNullOrWhiteSpace(species.SecondaryAbility.AbilityName))
            names.Add(species.SecondaryAbility.AbilityName);
        if (species.HiddenAbility != null && !string.IsNullOrWhiteSpace(species.HiddenAbility.AbilityName))
            names.Add(species.HiddenAbility.AbilityName + " (Hidden)");
        return names.Count > 0 ? string.Join(", ", names) : "None assigned";
    }

    static string BuildUnknownPokedexText()
    {
        return "This Pokemon has not been encountered yet.\n\n" +
               "Its portrait stays black until you meet it in the world.\n\n" +
               "Name, typing, stats, abilities, and entry text will appear after the first encounter.";
    }

    Sprite LoadPokedexPortrait(PokedexSpeciesEntry entry)
    {
        if (entry == null)
            return null;

        if (entry.Species != null && entry.Species.Portrait != null)
            return entry.Species.Portrait;

        if (string.IsNullOrWhiteSpace(entry.PortraitResourcePath))
            return null;

        if (_portraitSpriteCache.TryGetValue(entry.PortraitResourcePath, out Sprite cached))
            return cached;

        Sprite sprite = Resources.Load<Sprite>(entry.PortraitResourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(entry.PortraitResourcePath);
            if (texture != null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
        }

        _portraitSpriteCache[entry.PortraitResourcePath] = sprite;
        return sprite;
    }

    void RefreshButtonStyles()
    {
        if (_sectionButtons == null)
            return;

        for (int i = 0; i < _sectionButtons.Length; i++)
        {
            Image image = _sectionButtons[i] != null ? _sectionButtons[i].GetComponent<Image>() : null;
            if (image == null)
                continue;

            bool selected = i == (int)_selectedSection;
            image.color = selected
                ? new Color(0.27f, 0.44f, 0.56f, 1f)
                : new Color(0.18f, 0.23f, 0.29f, 1f);
        }
    }

    static bool WasPausePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            return true;

        Gamepad pad = Gamepad.current;
        if (pad != null && pad.startButton.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            return true;
#endif
        return false;
    }

    static GameObject CreateUiObject(string name, Transform parent, params System.Type[] extraComponents)
    {
        GameObject go = new GameObject(name, extraComponents);
        go.transform.SetParent(parent, false);
        if (go.GetComponent<RectTransform>() == null)
            go.AddComponent<RectTransform>();
        return go;
    }

    static Text CreateText(string name, Transform parent, int fontSize, FontStyle style, TextAnchor anchor)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Text));
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image));
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition)
    {
        GameObject go = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(220f, 62f);
        rt.anchoredPosition = anchoredPosition;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.18f, 0.23f, 0.29f, 1f);

        Button button = go.GetComponent<Button>();
        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.88f, 0.95f, 1f, 1f);
        cb.pressedColor = new Color(0.76f, 0.84f, 0.92f, 1f);
        button.colors = cb;

        Text text = CreateText("Label", go.transform, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        text.text = label;

        return button;
    }

    static Slider CreateSlider(string name, Transform parent)
    {
        GameObject root = CreateUiObject(name, parent);
        Slider slider = root.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;

        GameObject background = CreateUiObject("Background", root.transform, typeof(Image));
        RectTransform backgroundRt = background.GetComponent<RectTransform>();
        backgroundRt.anchorMin = new Vector2(0f, 0.5f);
        backgroundRt.anchorMax = new Vector2(1f, 0.5f);
        backgroundRt.sizeDelta = new Vector2(0f, 18f);
        background.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.28f, 1f);

        GameObject fillArea = CreateUiObject("Fill Area", root.transform);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0f);
        fillAreaRt.anchorMax = new Vector2(1f, 1f);
        fillAreaRt.offsetMin = new Vector2(12f, 0f);
        fillAreaRt.offsetMax = new Vector2(-12f, 0f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform, typeof(Image));
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.40f, 0.72f, 0.58f, 1f);

        GameObject handleArea = CreateUiObject("Handle Slide Area", root.transform);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(12f, 0f);
        handleAreaRt.offsetMax = new Vector2(-12f, 0f);

        GameObject handle = CreateUiObject("Handle", handleArea.transform, typeof(Image));
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(28f, 40f);
        handle.GetComponent<Image>().color = new Color(0.94f, 0.96f, 0.98f, 1f);

        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;

        return slider;
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

    static void SetCursorForMenu(bool menuOpen)
    {
        Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = menuOpen;
    }
}
