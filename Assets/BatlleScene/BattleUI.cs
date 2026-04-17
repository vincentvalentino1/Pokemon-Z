using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class BattleUI : MonoBehaviour
{
    [Header("─── UI: Skill Buttons ───")]
    public Button[] SkillButtons = new Button[4];
    public TextMeshProUGUI[] SkillButtonTexts = new TextMeshProUGUI[4];

    [Header("─── UI: HP Bars ───")]
    public Slider PlayerHPBar;
    public Slider EnemyHPBar;
    public TextMeshProUGUI PlayerHPText;
    public TextMeshProUGUI EnemyHPText;

    [Header("─── UI: TP Bar ───")]
    public Slider PlayerTPBar;
    public TextMeshProUGUI PlayerTPText;

    [Header("─── UI: Info Display ───")]
    public TextMeshProUGUI PlayerNameText;
    public TextMeshProUGUI EnemyNameText;
    public TextMeshProUGUI PlayerLevelText;
    public TextMeshProUGUI EnemyLevelText;
    public TextMeshProUGUI PlayerStatusText;
    public TextMeshProUGUI EnemyStatusText;

    [Header("─── UI: Battle Log ───")]
    public TextMeshProUGUI BattleLogText;

    [Header("─── Animation Timing ───")]
    [Range(0.005f, 0.08f)] public float TextSpeed = 0.025f;
    [Range(0.1f, 1.5f)] public float HPBarAnimDuration = 0.5f;
    [Range(0.1f, 1f)] public float MessagePause = 0.35f;

    Coroutine _playerHPAnim;
    Coroutine _enemyHPAnim;
    Coroutine _playerTPAnim;

    public IEnumerator ShowMessage(string message)
    {
        Debug.Log($"[Battle] {message}");
        BattleEvents.OnMessageShown?.Invoke(message);

        if (BattleLogText == null)
        {
            yield return new WaitForSeconds(MessagePause);
            yield break;
        }

        BattleLogText.text = "";

        for (int i = 1; i <= message.Length; i++)
        {
            BattleLogText.text = message.Substring(0, i);
            yield return new WaitForSeconds(TextSpeed);
        }

        yield return new WaitForSeconds(MessagePause);
    }

    public void SetPrompt(string text)
    {
        if (BattleLogText != null)
            BattleLogText.text = text;
    }

    public IEnumerator AnimateHPChange(PokemonInstance pokemon, int fromHP, int toHP, bool isPlayer)
    {
        Slider bar = isPlayer ? PlayerHPBar : EnemyHPBar;
        TextMeshProUGUI text = isPlayer ? PlayerHPText : EnemyHPText;
        int maxHP = pokemon.MaxHP;

        if (bar == null) yield break;
        bar.maxValue = maxHP;

        if (isPlayer)
        {
            if (_playerHPAnim != null) StopCoroutine(_playerHPAnim);
            _playerHPAnim = StartCoroutine(RunHPBarLerp(bar, text, fromHP, toHP, maxHP));
            yield return _playerHPAnim;
            _playerHPAnim = null;
        }
        else
        {
            if (_enemyHPAnim != null) StopCoroutine(_enemyHPAnim);
            _enemyHPAnim = StartCoroutine(RunHPBarLerp(bar, text, fromHP, toHP, maxHP));
            yield return _enemyHPAnim;
            _enemyHPAnim = null;
        }
    }

    IEnumerator RunHPBarLerp(Slider bar, TextMeshProUGUI text, int fromHP, int toHP, int maxHP)
    {
        float elapsed = 0f;
        while (elapsed < HPBarAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / HPBarAnimDuration);
            float current = Mathf.Lerp(fromHP, toHP, t);
            bar.value = current;
            if (text != null)
                text.text = $"{Mathf.RoundToInt(current)} / {maxHP}";
            UpdateBarColor(bar);
            yield return null;
        }

        bar.value = toHP;
        if (text != null)
            text.text = $"{toHP} / {maxHP}";
        UpdateBarColor(bar);
    }

    public IEnumerator AnimateTPChange(int fromTP, int toTP, int maxTP)
    {
        if (PlayerTPBar == null) yield break;
        PlayerTPBar.maxValue = maxTP;

        if (_playerTPAnim != null) StopCoroutine(_playerTPAnim);
        _playerTPAnim = StartCoroutine(RunTPBarLerp(fromTP, toTP, maxTP));
        yield return _playerTPAnim;
        _playerTPAnim = null;
    }

    IEnumerator RunTPBarLerp(int fromTP, int toTP, int maxTP)
    {
        float duration = HPBarAnimDuration * 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float current = Mathf.Lerp(fromTP, toTP, t);
            PlayerTPBar.value = current;
            if (PlayerTPText != null)
                PlayerTPText.text = $"TP: {Mathf.RoundToInt(current)} / {maxTP}";
            yield return null;
        }

        PlayerTPBar.value = toTP;
        if (PlayerTPText != null)
            PlayerTPText.text = $"TP: {toTP} / {maxTP}";
    }

    public bool SetupSkillButtons(PokemonInstance player, Action<int> onSelected)
    {
        bool anyUsable = false;

        for (int i = 0; i < SkillButtons.Length; i++)
        {
            if (i < player.EquippedSkills.Length && player.EquippedSkills[i] != null
                && player.EquippedSkills[i].Data != null)
            {
                EquippedSkill slot = player.EquippedSkills[i];
                SkillData skill = slot.Data;
                bool usable = slot.IsUsable(player.CurrentTP);

                SkillButtons[i].gameObject.SetActive(true);
                SkillButtons[i].interactable = usable;
                StyleMoveButton(SkillButtons[i], SkillButtonTexts[i], skill, usable);
                if (usable) anyUsable = true;

                int index = i;
                SkillButtons[i].onClick.RemoveAllListeners();
                SkillButtons[i].onClick.AddListener(() => onSelected(index));
            }
            else
            {
                SkillButtons[i].gameObject.SetActive(false);
            }
        }

        return anyUsable;
    }

    public void DisableAllButtons()
    {
        for (int i = 0; i < SkillButtons.Length; i++)
            SkillButtons[i].interactable = false;
    }

    public void RefreshUI(PokemonInstance player, PokemonInstance enemy)
    {
        if (PlayerHPBar != null && _playerHPAnim == null)
        {
            PlayerHPBar.maxValue = player.MaxHP;
            PlayerHPBar.value = player.CurrentHP;
            UpdateBarColor(PlayerHPBar);
        }
        if (EnemyHPBar != null && _enemyHPAnim == null)
        {
            EnemyHPBar.maxValue = enemy.MaxHP;
            EnemyHPBar.value = enemy.CurrentHP;
            UpdateBarColor(EnemyHPBar);
        }

        if (PlayerHPText != null && _playerHPAnim == null)
            PlayerHPText.text = $"{player.CurrentHP} / {player.MaxHP}";
        if (EnemyHPText != null && _enemyHPAnim == null)
            EnemyHPText.text = $"{enemy.CurrentHP} / {enemy.MaxHP}";

        if (PlayerTPBar != null && _playerTPAnim == null)
        {
            PlayerTPBar.maxValue = player.MaxTP;
            PlayerTPBar.value = player.CurrentTP;
        }
        if (PlayerTPText != null && _playerTPAnim == null)
            PlayerTPText.text = $"TP: {player.CurrentTP} / {player.MaxTP}";

        if (PlayerNameText != null) PlayerNameText.text = GetName(player);
        if (EnemyNameText  != null) EnemyNameText.text  = GetName(enemy);

        if (PlayerLevelText != null) PlayerLevelText.text = $"Lv.{player.Level}";
        if (EnemyLevelText  != null) EnemyLevelText.text  = $"Lv.{enemy.Level}";

        if (PlayerStatusText != null) PlayerStatusText.text = FormatStatus(player.CurrentStatus);
        if (EnemyStatusText  != null) EnemyStatusText.text  = FormatStatus(enemy.CurrentStatus);
    }

    public void ClearAnimTrackers()
    {
        _playerHPAnim = null;
        _enemyHPAnim = null;
        _playerTPAnim = null;
    }

    void UpdateBarColor(Slider bar)
    {
        if (bar == null || bar.fillRect == null) return;
        Image fill = bar.fillRect.GetComponent<Image>();
        if (fill == null) return;

        float ratio = bar.maxValue > 0 ? bar.value / bar.maxValue : 1f;

        Color green  = new Color(0.29f, 0.87f, 0.50f);
        Color teal   = new Color(0.18f, 0.83f, 0.75f);
        Color amber  = new Color(0.98f, 0.75f, 0.14f);
        Color red    = new Color(0.90f, 0.20f, 0.20f);

        if (ratio > 0.75f)
            fill.color = green;
        else if (ratio > 0.50f)
            fill.color = Color.Lerp(teal, green, (ratio - 0.50f) / 0.25f);
        else if (ratio > 0.25f)
            fill.color = Color.Lerp(amber, teal, (ratio - 0.25f) / 0.25f);
        else
        {
            float pulse = Mathf.Lerp(0.8f, 1f, (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f);
            fill.color = red * pulse;
            fill.color = new Color(fill.color.r, fill.color.g, fill.color.b, 1f);
        }
    }

    void StyleMoveButton(Button btn, TextMeshProUGUI label, SkillData skill, bool usable)
    {
        Image btnImage = btn.GetComponent<Image>();
        if (btnImage != null)
        {
            Color typeColor = GetTypeColor(skill.SkillElement);
            if (usable)
            {
                Color darkened = typeColor * 0.85f;
                darkened.a = 1f;
                btnImage.color = darkened;
            }
            else
            {
                btnImage.color = typeColor * 0.25f + Color.gray * 0.2f;
                btnImage.color = new Color(btnImage.color.r, btnImage.color.g, btnImage.color.b, 1f);
            }
        }

        if (label != null)
        {
            string catIcon = skill.Category switch
            {
                MoveCategory.Physical => "[PHY]",
                MoveCategory.Special  => "[SPC]",
                MoveCategory.Status   => "[STS]",
                _ => ""
            };
            string typeName = skill.SkillElement.ToString();
            label.text = $"<size=120%><b>{skill.SkillName}</b></size>\n<size=80%>{typeName} {catIcon}  TP:{skill.TPCost}</size>";
        }
    }

    public static string GetName(PokemonInstance pokemon)
    {
        if (!string.IsNullOrEmpty(pokemon.Nickname)) return pokemon.Nickname;
        if (pokemon.SpeciesData != null) return pokemon.SpeciesData.PokemonName;
        return "???";
    }

    static string FormatStatus(PrimaryStatus status)
    {
        float pulse = Mathf.Lerp(0.6f, 1f, (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f);
        string a = $"{Mathf.RoundToInt(pulse * 255):X2}";

        return status switch
        {
            PrimaryStatus.None          => "",
            PrimaryStatus.Burn          => $"<color=#FF6600{a}>BRN</color>",
            PrimaryStatus.Poison        => $"<color=#AA44CC{a}>PSN</color>",
            PrimaryStatus.BadlyPoisoned => $"<color=#8822AA{a}>TOX</color>",
            PrimaryStatus.Paralysis     => $"<color=#CCAA00{a}>PAR</color>",
            PrimaryStatus.Sleep         => $"<color=#888888{a}>SLP</color>",
            PrimaryStatus.Freeze        => $"<color=#44CCDD{a}>FRZ</color>",
            _ => ""
        };
    }

    static Color GetTypeColor(ElementType type) => type switch
    {
        ElementType.Normal   => new Color(0.66f, 0.65f, 0.48f),
        ElementType.Fire     => new Color(0.93f, 0.51f, 0.19f),
        ElementType.Water    => new Color(0.39f, 0.56f, 0.94f),
        ElementType.Grass    => new Color(0.48f, 0.78f, 0.30f),
        ElementType.Electric => new Color(0.97f, 0.82f, 0.17f),
        ElementType.Ice      => new Color(0.59f, 0.85f, 0.84f),
        ElementType.Fighting => new Color(0.76f, 0.18f, 0.16f),
        ElementType.Poison   => new Color(0.64f, 0.24f, 0.63f),
        ElementType.Ground   => new Color(0.89f, 0.75f, 0.40f),
        ElementType.Flying   => new Color(0.66f, 0.56f, 0.95f),
        ElementType.Psychic  => new Color(0.98f, 0.33f, 0.53f),
        ElementType.Bug      => new Color(0.65f, 0.73f, 0.10f),
        ElementType.Rock     => new Color(0.72f, 0.63f, 0.21f),
        ElementType.Ghost    => new Color(0.44f, 0.34f, 0.58f),
        ElementType.Dragon   => new Color(0.44f, 0.21f, 0.99f),
        ElementType.Dark     => new Color(0.44f, 0.34f, 0.27f),
        ElementType.Steel    => new Color(0.72f, 0.72f, 0.78f),
        ElementType.Fairy    => new Color(0.84f, 0.52f, 0.68f),
        _ => Color.gray
    };
}
