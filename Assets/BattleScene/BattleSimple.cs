using UnityEngine;
using System.Collections;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Battle controller -- orchestrates flow between extracted subsystems.
/// </summary>
public class BattleSimple : MonoBehaviour
{
    enum PlayerActionType
    {
        None,
        Move,
        Capture,
        Switch,
        Run
    }

    enum PlayerMenuMode
    {
        MainActions,
        SkillSelect,
        PartySelect
    }

    const int PartyMenuPageSize = 2;

    [Header("─── Pokemon ───")]
    public PokemonInstance PlayerPokemon;
    public PokemonInstance EnemyPokemon;

    [Header("─── Subsystems ───")]
    public BattleUI UI;
    public BattleAnimator Animator;
    public PokemonSpawner Spawner;

    [Header("─── Weather ───")]
    public WeatherType CurrentWeather = WeatherType.None;
    public int WeatherTurnsRemaining = 0;

    BattleState _state;
    BattleResolution _resolution = BattleResolution.None;
    SkillData _playerChosenSkill;
    SkillData _enemyChosenSkill;
    PlayerActionType _playerAction = PlayerActionType.None;
    PlayerMenuMode _menuMode = PlayerMenuMode.MainActions;
    int _selectedPartyIndex = -1;
    int _partyMenuPage = 0;
    int _turnCount;
    int _badPoisonCounterPlayer;
    int _badPoisonCounterEnemy;
    bool _waitingForInput;

    // Written by CheckCanAct, read by caller immediately after yield -- safe only because
    // the battle coroutine is strictly sequential.
    bool _canActResult;

    Coroutine _battleCoroutine;

    bool IsPlayer(PokemonInstance p) => p == PlayerPokemon;
    string GetName(PokemonInstance p) => BattleUI.GetName(p);

    void Start()
    {
        if (OpenWorldEncounterManager.Instance != null &&
            OpenWorldEncounterManager.Instance.CurrentEncounter != null &&
            OpenWorldEncounterManager.Instance.CurrentEncounter.WildPokemon != null)
            return;

        if (PlayerPokemon != null && EnemyPokemon != null)
            InitializeBattle();
    }

    public void SetCombatants(PokemonInstance playerPokemon, PokemonInstance enemyPokemon)
    {
        PlayerPokemon = playerPokemon;
        EnemyPokemon = enemyPokemon;
    }

    public void InitializeBattle()
    {
        if (PlayerPokemon == null || EnemyPokemon == null)
        {
            Debug.LogWarning("[BattleSimple] Cannot initialize battle: combatants are missing.");
            return;
        }

        if (_battleCoroutine != null)
            StopAllCoroutines();

        _resolution = BattleResolution.None;
        _turnCount = 0;
        _badPoisonCounterPlayer = 0;
        _badPoisonCounterEnemy = 0;
        _waitingForInput = false;
        _playerAction = PlayerActionType.None;
        _playerChosenSkill = null;
        _enemyChosenSkill = null;
        _selectedPartyIndex = -1;
        _partyMenuPage = 0;

        Spawner.SpawnBoth(PlayerPokemon, EnemyPokemon);
        Animator.Init(Spawner);
        UI.ClearAnimTrackers();

        PlayerPokemon.RecalculateMaxValues();
        EnemyPokemon.RecalculateMaxValues();
        PlayerPokemon.ResetBattleStages();
        EnemyPokemon.ResetBattleStages();

        if (PlayerPokemon.CurrentHP <= 0) PlayerPokemon.CurrentHP = PlayerPokemon.MaxHP;
        if (PlayerPokemon.CurrentTP <= 0) PlayerPokemon.CurrentTP = PlayerPokemon.MaxTP;
        if (EnemyPokemon.CurrentHP <= 0) EnemyPokemon.CurrentHP = EnemyPokemon.MaxHP;
        if (EnemyPokemon.CurrentTP <= 0) EnemyPokemon.CurrentTP = EnemyPokemon.MaxTP;

        Spawner.ResetVisuals();
        UI.DisableAllButtons();
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);

        BattleEvents.OnBattleStart?.Invoke();
        _battleCoroutine = StartCoroutine(BattleRoutine());
    }

    IEnumerator BattleRoutine()
    {
        _state = BattleState.BattleStart;
        yield return new WaitForSeconds(0.3f);

        yield return UI.ShowMessage($"A wild {GetName(EnemyPokemon)} appeared!");
        yield return UI.ShowMessage($"Go, {GetName(PlayerPokemon)}!");

        while (_resolution == BattleResolution.None)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;

            yield return WaitForPlayerInput();
            if (_resolution != BattleResolution.None)
                break;

            _state = BattleState.EnemyTurn;
            _enemyChosenSkill = EnemyAI.PickMove(EnemyPokemon);

            _state = BattleState.ResolveTurn;
            yield return ResolveTurnRoutine();
            if (_resolution != BattleResolution.None)
                break;

            yield return CheckBattleContinuationRoutine();
            if (_resolution != BattleResolution.None)
                break;

            _state = BattleState.TurnEnd;
            yield return TurnEndRoutine();

            yield return CheckBattleContinuationRoutine();
        }

        _state = BattleState.BattleOver;
        UI.DisableAllButtons();
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);
        BattleEvents.OnBattleEnd?.Invoke(_resolution);
    }

    IEnumerator WaitForPlayerInput()
    {
        _waitingForInput = true;
        _playerAction = PlayerActionType.None;
        _playerChosenSkill = null;
        _selectedPartyIndex = -1;
        _partyMenuPage = 0;

        ShowMainActionMenu();

        while (_waitingForInput)
        {
            if (WasCancelPressed())
                HandleCancelInput();

            yield return null;
        }
    }

    void ShowMainActionMenu()
    {
        _menuMode = PlayerMenuMode.MainActions;

        string[] labels =
        {
            "Fight",
            GetCatchActionLabel(),
            "Switch",
            "Run"
        };

        bool[] enabled =
        {
            true,
            CanAttemptCapture(),
            CanSwitchPokemon(),
            true
        };

        UI.SetupOptionButtons(labels, enabled, OnMenuButtonSelected);
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);
        UI.SetPrompt($"What will {GetName(PlayerPokemon)} do?");
    }

    void ShowSkillMenu()
    {
        _menuMode = PlayerMenuMode.SkillSelect;
        bool anyUsable = UI.SetupSkillButtons(PlayerPokemon, OnSkillButtonSelected);
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);

        if (!anyUsable)
        {
            _playerAction = PlayerActionType.Move;
            _playerChosenSkill = null;
            UI.DisableAllButtons();
            UI.SetPrompt($"{GetName(PlayerPokemon)} has no TP left. It will use Struggle.");
            _waitingForInput = false;
            return;
        }

        UI.SetPrompt($"Choose a move. Press Esc / Right Click to go back.");
    }

    void ShowPartyMenu()
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        List<int> switchable = GetSwitchablePartyIndices();
        if (manager == null || switchable.Count == 0)
        {
            ShowMainActionMenu();
            return;
        }

        _menuMode = PlayerMenuMode.PartySelect;
        int start = _partyMenuPage * PartyMenuPageSize;
        int totalPages = Mathf.CeilToInt(switchable.Count / (float)PartyMenuPageSize);

        string[] labels = new string[4];
        bool[] enabled = new bool[4];

        for (int i = 0; i < PartyMenuPageSize; i++)
        {
            int switchableIndex = start + i;
            if (switchableIndex >= switchable.Count)
                continue;

            PokemonInstance option = manager.Party[switchable[switchableIndex]];
            labels[i] = FormatPartyOption(option);
            enabled[i] = option != null && !option.IsFainted;
        }

        if (totalPages > 1)
        {
            labels[2] = start + PartyMenuPageSize < switchable.Count ? "Next" : "Back To Start";
            enabled[2] = true;
        }

        labels[3] = "Back";
        enabled[3] = true;

        UI.SetupOptionButtons(labels, enabled, OnMenuButtonSelected);
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);
        UI.SetPrompt("Choose a Pokemon. Press Esc / Right Click to go back.");
    }

    void OnMenuButtonSelected(int buttonIndex)
    {
        switch (_menuMode)
        {
            case PlayerMenuMode.MainActions:
                HandleMainMenuSelection(buttonIndex);
                break;
            case PlayerMenuMode.PartySelect:
                HandlePartyMenuSelection(buttonIndex);
                break;
        }
    }

    void HandleMainMenuSelection(int buttonIndex)
    {
        switch (buttonIndex)
        {
            case 0:
                ShowSkillMenu();
                break;
            case 1:
                _playerAction = PlayerActionType.Capture;
                UI.DisableAllButtons();
                _waitingForInput = false;
                break;
            case 2:
                ShowPartyMenu();
                break;
            case 3:
                _playerAction = PlayerActionType.Run;
                UI.DisableAllButtons();
                _waitingForInput = false;
                break;
        }
    }

    void HandlePartyMenuSelection(int buttonIndex)
    {
        List<int> switchable = GetSwitchablePartyIndices();
        int start = _partyMenuPage * PartyMenuPageSize;

        if (buttonIndex < PartyMenuPageSize)
        {
            int switchableIndex = start + buttonIndex;
            if (switchableIndex >= switchable.Count)
                return;

            _selectedPartyIndex = switchable[switchableIndex];
            _playerAction = PlayerActionType.Switch;
            UI.DisableAllButtons();
            _waitingForInput = false;
            return;
        }

        if (buttonIndex == 2)
        {
            if (switchable.Count > PartyMenuPageSize)
            {
                int totalPages = Mathf.CeilToInt(switchable.Count / (float)PartyMenuPageSize);
                _partyMenuPage = (_partyMenuPage + 1) % totalPages;
                ShowPartyMenu();
            }
            return;
        }

        ShowMainActionMenu();
    }

    void OnSkillButtonSelected(int slotIndex)
    {
        EquippedSkill slot = PlayerPokemon.EquippedSkills[slotIndex];
        if (slot == null || slot.Data == null || !slot.IsUsable(PlayerPokemon.CurrentTP))
            return;

        _playerChosenSkill = slot.Data;
        _playerAction = PlayerActionType.Move;
        UI.DisableAllButtons();
        _waitingForInput = false;
    }

    void HandleCancelInput()
    {
        if (!_waitingForInput)
            return;

        if (_menuMode == PlayerMenuMode.SkillSelect || _menuMode == PlayerMenuMode.PartySelect)
            ShowMainActionMenu();
    }

    IEnumerator ResolveTurnRoutine()
    {
        switch (_playerAction)
        {
            case PlayerActionType.Capture:
                yield return AttemptCaptureRoutine();
                if (_resolution == BattleResolution.None && !EnemyPokemon.IsFainted && !PlayerPokemon.IsFainted)
                {
                    yield return Animator.TurnTransitionPunch();
                    yield return ExecuteMoveRoutine(EnemyPokemon, PlayerPokemon, _enemyChosenSkill);
                }
                yield break;

            case PlayerActionType.Switch:
                yield return SwitchToPartyMemberRoutine(_selectedPartyIndex, false);
                if (_resolution == BattleResolution.None && !EnemyPokemon.IsFainted && !PlayerPokemon.IsFainted)
                {
                    yield return Animator.TurnTransitionPunch();
                    yield return ExecuteMoveRoutine(EnemyPokemon, PlayerPokemon, _enemyChosenSkill);
                }
                yield break;

            case PlayerActionType.Run:
                yield return AttemptRunRoutine();
                yield break;
        }

        bool playerFirst = DetermineWhoGoesFirst();

        PokemonInstance firstAttacker = playerFirst ? PlayerPokemon : EnemyPokemon;
        PokemonInstance firstDefender = playerFirst ? EnemyPokemon : PlayerPokemon;
        SkillData firstSkill = playerFirst ? _playerChosenSkill : _enemyChosenSkill;

        PokemonInstance secondAttacker = playerFirst ? EnemyPokemon : PlayerPokemon;
        PokemonInstance secondDefender = playerFirst ? PlayerPokemon : EnemyPokemon;
        SkillData secondSkill = playerFirst ? _enemyChosenSkill : _playerChosenSkill;

        yield return ExecuteMoveRoutine(firstAttacker, firstDefender, firstSkill);

        if (firstDefender.IsFainted || firstAttacker.IsFainted || _resolution != BattleResolution.None)
            yield break;

        yield return Animator.TurnTransitionPunch();
        yield return ExecuteMoveRoutine(secondAttacker, secondDefender, secondSkill);
    }

    bool DetermineWhoGoesFirst()
    {
        int playerPriority = _playerChosenSkill != null ? _playerChosenSkill.Priority : 0;
        int enemyPriority = _enemyChosenSkill != null ? _enemyChosenSkill.Priority : 0;

        if (playerPriority != enemyPriority)
            return playerPriority > enemyPriority;

        float playerSpeed = DamageCalculator.GetEffectiveSpeed(PlayerPokemon);
        float enemySpeed = DamageCalculator.GetEffectiveSpeed(EnemyPokemon);

        if (Mathf.Abs(playerSpeed - enemySpeed) > 0.01f)
            return playerSpeed > enemySpeed;

        return Random.value > 0.5f;
    }

    IEnumerator ExecuteMoveRoutine(PokemonInstance attacker, PokemonInstance defender, SkillData skill)
    {
        bool isStruggle = skill == null;
        string moveName = isStruggle ? "Struggle" : skill.SkillName;
        string attackerName = GetName(attacker);
        bool atkIsPlayer = IsPlayer(attacker);
        bool defIsPlayer = IsPlayer(defender);

        yield return CheckCanAct(attacker);
        if (!_canActResult) yield break;

        yield return UI.ShowMessage($"{attackerName} used {moveName}!");
        BattleEvents.OnMoveUsed?.Invoke(moveName);

        if (!isStruggle)
        {
            int tpBefore = attacker.CurrentTP;
            attacker.CurrentTP = Mathf.Max(0, attacker.CurrentTP - skill.TPCost);
            if (atkIsPlayer)
                yield return UI.AnimateTPChange(tpBefore, attacker.CurrentTP, attacker.MaxTP);
        }

        if (isStruggle)
        {
            yield return Animator.AttackLunge(atkIsPlayer);

            int strgDmg = Mathf.Max(1, defender.MaxHP / 4);
            int hpBefore = defender.CurrentHP;
            ApplyDamage(defender, strgDmg);
            BattleEvents.OnDamageDealt?.Invoke(defender, strgDmg, defIsPlayer);
            yield return Animator.HitFlash(defIsPlayer);
            yield return UI.AnimateHPChange(defender, hpBefore, defender.CurrentHP, defIsPlayer);

            int recoil = Mathf.Max(1, attacker.MaxHP / 4);
            hpBefore = attacker.CurrentHP;
            ApplyDamage(attacker, recoil);
            yield return UI.ShowMessage($"{attackerName} is hit with recoil!");
            yield return UI.AnimateHPChange(attacker, hpBefore, attacker.CurrentHP, atkIsPlayer);

            if (attacker.IsFainted)
            {
                yield return Animator.FaintAnimation(atkIsPlayer);
                BattleEvents.OnFainted?.Invoke(attacker);
            }
            if (defender.IsFainted)
            {
                yield return Animator.FaintAnimation(defIsPlayer);
                BattleEvents.OnFainted?.Invoke(defender);
            }

            yield break;
        }

        if (skill.Category == MoveCategory.Status)
        {
            yield return ApplyHealingRoutine(attacker, skill, atkIsPlayer);
            yield return ApplySecondaryEffectsRoutine(attacker, defender, skill);
            UI.RefreshUI(PlayerPokemon, EnemyPokemon);
            yield break;
        }

        if (!DamageCalculator.RollAccuracy(attacker, defender, skill))
        {
            yield return UI.ShowMessage($"{attackerName}'s attack missed!", 0.8f);
            UI.SetPrompt($"{attackerName}'s attack missed!");
            yield break;
        }

        int hits = Random.Range(skill.MinHits, skill.MaxHits + 1);
        int totalDealt = 0;

        for (int h = 0; h < hits; h++)
        {
            if (defender.IsFainted) break;

            bool isCrit = DamageCalculator.RollCritical(attacker, skill);
            int damage = DamageCalculator.Calculate(attacker, defender, skill, isCrit, CurrentWeather);
            damage = Mathf.Max(1, damage);

            yield return Animator.AttackLunge(atkIsPlayer);

            int hpBefore = defender.CurrentHP;
            ApplyDamage(defender, damage);
            totalDealt += damage;

            BattleEvents.OnDamageDealt?.Invoke(defender, damage, defIsPlayer);
            yield return Animator.HitFlash(defIsPlayer);

            float typeEff = TypeChart.GetDualEffectiveness(
                skill.SkillElement,
                defender.SpeciesData.PrimaryType,
                defender.SpeciesData.SecondaryType
            );

            float shakeIntensity = 0f;
            if (typeEff > 1.5f) shakeIntensity += 0.08f;
            if (isCrit) shakeIntensity += 0.06f;
            float damageRatio = defender.MaxHP > 0 ? (float)damage / defender.MaxHP : 0f;
            shakeIntensity += damageRatio * 0.1f;
            if (shakeIntensity > 0.02f)
                yield return Animator.CameraShake(shakeIntensity, 0.2f);

            yield return UI.AnimateHPChange(defender, hpBefore, defender.CurrentHP, defIsPlayer);

            if (isCrit)
            {
                yield return UI.ShowMessage("A critical hit!");
                BattleEvents.OnCriticalHit?.Invoke(true);
            }

            if (typeEff > 1.5f)
            {
                yield return UI.ShowMessage("It's super effective!");
                BattleEvents.OnSuperEffective?.Invoke(typeEff);
            }
            else if (typeEff < 0.01f)
                yield return UI.ShowMessage("It doesn't affect the target...");
            else if (typeEff < 0.9f)
                yield return UI.ShowMessage("It's not very effective...");

            if (defender.IsFainted)
            {
                yield return Animator.FaintAnimation(defIsPlayer);
                BattleEvents.OnFainted?.Invoke(defender);
            }
        }

        if (hits > 1)
            yield return UI.ShowMessage($"Hit {hits} time(s)!");

        if (skill.DrainPercent > 0f && totalDealt > 0)
        {
            int healed = Mathf.Max(1, Mathf.RoundToInt(totalDealt * skill.DrainPercent));
            int hpBefore = attacker.CurrentHP;
            attacker.CurrentHP = Mathf.Min(attacker.MaxHP, attacker.CurrentHP + healed);
            yield return UI.ShowMessage($"{attackerName} restored {healed} HP!");
            yield return UI.AnimateHPChange(attacker, hpBefore, attacker.CurrentHP, atkIsPlayer);
        }

        if (skill.RecoilPercent > 0f && totalDealt > 0)
        {
            int recoilDmg = Mathf.Max(1, Mathf.RoundToInt(totalDealt * skill.RecoilPercent));
            int hpBefore = attacker.CurrentHP;
            ApplyDamage(attacker, recoilDmg);
            yield return UI.ShowMessage($"{attackerName} is hit with recoil!");
            yield return UI.AnimateHPChange(attacker, hpBefore, attacker.CurrentHP, atkIsPlayer);

            if (attacker.IsFainted)
            {
                yield return Animator.FaintAnimation(atkIsPlayer);
                BattleEvents.OnFainted?.Invoke(attacker);
            }
        }

        if (!defender.IsFainted)
            yield return ApplySecondaryEffectsRoutine(attacker, defender, skill);
    }

    IEnumerator CheckCanAct(PokemonInstance pokemon)
    {
        _canActResult = true;
        string name = GetName(pokemon);

        switch (pokemon.CurrentStatus)
        {
            case PrimaryStatus.Sleep:
                if (pokemon.StatusTurnsRemaining > 0)
                {
                    pokemon.StatusTurnsRemaining--;
                    yield return UI.ShowMessage($"{name} is fast asleep.");
                    _canActResult = false;
                    yield break;
                }
                pokemon.CurrentStatus = PrimaryStatus.None;
                yield return UI.ShowMessage($"{name} woke up!");
                UI.RefreshUI(PlayerPokemon, EnemyPokemon);
                break;

            case PrimaryStatus.Freeze:
                if (Random.value > 0.2f)
                {
                    yield return UI.ShowMessage($"{name} is frozen solid.");
                    _canActResult = false;
                    yield break;
                }
                pokemon.CurrentStatus = PrimaryStatus.None;
                yield return UI.ShowMessage($"{name} thawed out!");
                UI.RefreshUI(PlayerPokemon, EnemyPokemon);
                break;

            case PrimaryStatus.Paralysis:
                if (Random.value < 0.25f)
                {
                    yield return UI.ShowMessage($"{name} is fully paralyzed! It can't move!");
                    _canActResult = false;
                    yield break;
                }
                break;
        }
    }

    IEnumerator ApplySecondaryEffectsRoutine(PokemonInstance attacker, PokemonInstance defender, SkillData skill)
    {
        if (skill.SecondaryEffects == null) yield break;

        for (int i = 0; i < skill.SecondaryEffects.Length; i++)
        {
            MoveSecondaryEffect fx = skill.SecondaryEffects[i];
            bool proc = fx.Chance == 0 || Random.Range(1, 101) <= fx.Chance;
            if (!proc) continue;

            PokemonInstance target = fx.TargetsSelf ? attacker : defender;

            if (fx.InflictStatus != PrimaryStatus.None && target.CurrentStatus == PrimaryStatus.None)
            {
                target.CurrentStatus = fx.InflictStatus;
                if (fx.InflictStatus == PrimaryStatus.Sleep)
                    target.StatusTurnsRemaining = Random.Range(1, 4);

                yield return UI.ShowMessage($"{GetName(target)} is now {fx.InflictStatus}!");
                BattleEvents.OnStatusInflicted?.Invoke(fx.InflictStatus.ToString());
                UI.RefreshUI(PlayerPokemon, EnemyPokemon);
            }

            if (fx.StatAffected != TargetStat.None && fx.StageChange != 0)
                yield return ApplyStatChangeRoutine(target, fx.StatAffected, fx.StageChange);

            if (fx.FlinchChance > 0 && Random.Range(1, 101) <= fx.FlinchChance)
                yield return UI.ShowMessage($"{GetName(defender)} flinched!");

            if (fx.SetWeather != WeatherType.None)
            {
                CurrentWeather = fx.SetWeather;
                WeatherTurnsRemaining = fx.WeatherDuration;
                yield return UI.ShowMessage($"The weather changed to {fx.SetWeather}!");
                BattleEvents.OnWeatherChanged?.Invoke(fx.SetWeather);
            }
        }
    }

    IEnumerator ApplyStatChangeRoutine(PokemonInstance pokemon, TargetStat stat, int stages)
    {
        string name = GetName(pokemon);
        string direction = stages > 0 ? "rose" : "fell";
        string sharply = Mathf.Abs(stages) >= 2 ? "sharply " : "";

        StatBlock s = pokemon.StatStages;
        switch (stat)
        {
            case TargetStat.Attack: s.Attack = Mathf.Clamp(s.Attack + stages, -6, 6); break;
            case TargetStat.Defense: s.Defense = Mathf.Clamp(s.Defense + stages, -6, 6); break;
            case TargetStat.SpAttack: s.SpAttack = Mathf.Clamp(s.SpAttack + stages, -6, 6); break;
            case TargetStat.SpDefense: s.SpDefense = Mathf.Clamp(s.SpDefense + stages, -6, 6); break;
            case TargetStat.Speed: s.Speed = Mathf.Clamp(s.Speed + stages, -6, 6); break;
            case TargetStat.Accuracy: pokemon.AccuracyStage = Mathf.Clamp(pokemon.AccuracyStage + stages, -6, 6); break;
            case TargetStat.Evasion: pokemon.EvasionStage = Mathf.Clamp(pokemon.EvasionStage + stages, -6, 6); break;
        }
        pokemon.StatStages = s;

        yield return UI.ShowMessage($"{name}'s {stat} {sharply}{direction}!");
    }

    IEnumerator ApplyHealingRoutine(PokemonInstance user, SkillData skill, bool isPlayer)
    {
        if (skill.FlatHealAmount == 0) yield break;

        int healAmt = skill.HealIsPercentage
            ? Mathf.RoundToInt(user.MaxHP * skill.FlatHealAmount / 100f)
            : skill.FlatHealAmount;

        if (healAmt <= 0) yield break;

        int hpBefore = user.CurrentHP;
        user.CurrentHP = Mathf.Min(user.MaxHP, user.CurrentHP + healAmt);
        yield return UI.ShowMessage($"{GetName(user)} restored {healAmt} HP!");
        yield return UI.AnimateHPChange(user, hpBefore, user.CurrentHP, isPlayer);
    }

    IEnumerator TurnEndRoutine()
    {
        bool playerFaintedBeforeWeather = PlayerPokemon.IsFainted;
        bool enemyFaintedBeforeWeather = EnemyPokemon.IsFainted;

        yield return ProcessStatusChipRoutine(PlayerPokemon, true);
        if (PlayerPokemon.IsFainted && !playerFaintedBeforeWeather)
        {
            yield return Animator.FaintAnimation(true);
            BattleEvents.OnFainted?.Invoke(PlayerPokemon);
        }

        yield return ProcessStatusChipRoutine(EnemyPokemon, false);
        if (EnemyPokemon.IsFainted && !enemyFaintedBeforeWeather)
        {
            yield return Animator.FaintAnimation(false);
            BattleEvents.OnFainted?.Invoke(EnemyPokemon);
        }

        bool playerFaintedBeforeHail = PlayerPokemon.IsFainted;
        bool enemyFaintedBeforeHail = EnemyPokemon.IsFainted;

        yield return ProcessWeatherChipRoutine(PlayerPokemon, true);
        if (PlayerPokemon.IsFainted && !playerFaintedBeforeHail)
        {
            yield return Animator.FaintAnimation(true);
            BattleEvents.OnFainted?.Invoke(PlayerPokemon);
        }

        yield return ProcessWeatherChipRoutine(EnemyPokemon, false);
        if (EnemyPokemon.IsFainted && !enemyFaintedBeforeHail)
        {
            yield return Animator.FaintAnimation(false);
            BattleEvents.OnFainted?.Invoke(EnemyPokemon);
        }

        if (WeatherTurnsRemaining > 0)
        {
            WeatherTurnsRemaining--;
            if (WeatherTurnsRemaining == 0)
            {
                yield return UI.ShowMessage("The weather returned to normal.");
                CurrentWeather = WeatherType.None;
                BattleEvents.OnWeatherChanged?.Invoke(WeatherType.None);
            }
        }

        UI.RefreshUI(PlayerPokemon, EnemyPokemon);
    }

    IEnumerator ProcessStatusChipRoutine(PokemonInstance pokemon, bool isPlayer)
    {
        if (pokemon.IsFainted) yield break;

        string name = GetName(pokemon);

        switch (pokemon.CurrentStatus)
        {
            case PrimaryStatus.Burn:
            {
                int chip = Mathf.Max(1, pokemon.MaxHP / 8);
                int hpBefore = pokemon.CurrentHP;
                ApplyDamage(pokemon, chip);
                yield return UI.ShowMessage($"{name} is hurt by its burn! (-{chip} HP)");
                yield return UI.AnimateHPChange(pokemon, hpBefore, pokemon.CurrentHP, isPlayer);
                break;
            }
            case PrimaryStatus.Poison:
            {
                int chip = Mathf.Max(1, pokemon.MaxHP / 8);
                int hpBefore = pokemon.CurrentHP;
                ApplyDamage(pokemon, chip);
                yield return UI.ShowMessage($"{name} is hurt by poison! (-{chip} HP)");
                yield return UI.AnimateHPChange(pokemon, hpBefore, pokemon.CurrentHP, isPlayer);
                break;
            }
            case PrimaryStatus.BadlyPoisoned:
            {
                if (isPlayer) _badPoisonCounterPlayer++;
                else _badPoisonCounterEnemy++;
                int counter = isPlayer ? _badPoisonCounterPlayer : _badPoisonCounterEnemy;
                int chip = Mathf.Max(1, pokemon.MaxHP * counter / 16);
                int hpBefore = pokemon.CurrentHP;
                ApplyDamage(pokemon, chip);
                yield return UI.ShowMessage($"{name} is hurt by toxic poison! (-{chip} HP)");
                yield return UI.AnimateHPChange(pokemon, hpBefore, pokemon.CurrentHP, isPlayer);
                break;
            }
        }
    }

    IEnumerator ProcessWeatherChipRoutine(PokemonInstance pokemon, bool isPlayer)
    {
        if (pokemon.IsFainted || CurrentWeather == WeatherType.None) yield break;

        ElementType t1 = pokemon.SpeciesData.PrimaryType;
        ElementType t2 = pokemon.SpeciesData.SecondaryType;

        if (CurrentWeather == WeatherType.Sandstorm)
        {
            bool immune = t1 == ElementType.Rock || t1 == ElementType.Steel || t1 == ElementType.Ground
                       || t2 == ElementType.Rock || t2 == ElementType.Steel || t2 == ElementType.Ground;
            if (!immune)
            {
                int chip = Mathf.Max(1, pokemon.MaxHP / 16);
                int hpBefore = pokemon.CurrentHP;
                ApplyDamage(pokemon, chip);
                yield return UI.ShowMessage($"{GetName(pokemon)} is buffeted by the sandstorm! (-{chip} HP)");
                yield return UI.AnimateHPChange(pokemon, hpBefore, pokemon.CurrentHP, isPlayer);
            }
        }
        else if (CurrentWeather == WeatherType.Hail)
        {
            bool immune = t1 == ElementType.Ice || t2 == ElementType.Ice;
            if (!immune)
            {
                int chip = Mathf.Max(1, pokemon.MaxHP / 16);
                int hpBefore = pokemon.CurrentHP;
                ApplyDamage(pokemon, chip);
                yield return UI.ShowMessage($"{GetName(pokemon)} is pelted by hail! (-{chip} HP)");
                yield return UI.AnimateHPChange(pokemon, hpBefore, pokemon.CurrentHP, isPlayer);
            }
        }
    }

    IEnumerator CheckBattleContinuationRoutine()
    {
        if (EnemyPokemon != null && EnemyPokemon.IsFainted)
        {
            yield return UI.ShowMessage($"{GetName(EnemyPokemon)} fainted! You win!");
            _resolution = BattleResolution.PlayerWon;
            yield break;
        }

        if (PlayerPokemon != null && PlayerPokemon.IsFainted)
        {
            yield return UI.ShowMessage($"{GetName(PlayerPokemon)} fainted!");

            OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
            if (manager != null && manager.TryGetNextUsablePartyMember(PlayerPokemon, out PokemonInstance _, out int nextIndex))
            {
                yield return SwitchToPartyMemberRoutine(nextIndex, true);
                yield break;
            }

            _resolution = BattleResolution.PlayerLost;
        }
    }

    IEnumerator AttemptCaptureRoutine()
    {
        if (!CanAttemptCapture())
        {
            if (GetPokeBallCount() <= 0)
                yield return UI.ShowMessage("You do not have any Poke Balls left.", 0.7f);
            else
                yield return UI.ShowMessage("You can only catch a living wild Pokemon.", 0.7f);
            yield break;
        }

        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        if (manager == null || !manager.TryConsumePokeBall())
        {
            yield return UI.ShowMessage("You reached for a Poke Ball, but the bag is empty.", 0.7f);
            yield break;
        }

        yield return UI.ShowMessage($"You threw a Poke Ball at {GetName(EnemyPokemon)}!", 0.55f);

        CatchAttemptResult result = CatchCalculator.TryCatch(EnemyPokemon);
        yield return PlayCaptureSequence(result);
        if (result.Success)
        {
            yield return UI.ShowMessage($"Gotcha! {GetName(EnemyPokemon)} was caught!", 0.9f);

            if (manager != null)
            {
                OpenWorldEncounterManager.PokemonCollectionDestination destination = manager.CaptureWildPokemon(EnemyPokemon);
                if (destination == OpenWorldEncounterManager.PokemonCollectionDestination.Party)
                    yield return UI.ShowMessage($"{GetName(EnemyPokemon)} was added to your party.", 0.65f);
                else
                    yield return UI.ShowMessage($"{GetName(EnemyPokemon)} was sent to storage.", 0.65f);
            }

            _resolution = BattleResolution.Captured;
            yield break;
        }

        yield return UI.ShowMessage(BuildFailedCaptureMessage(result.SuccessfulShakes), 0.75f);
    }

    IEnumerator AttemptRunRoutine()
    {
        yield return UI.ShowMessage("You got away safely!");
        _resolution = BattleResolution.Escaped;
    }

    IEnumerator SwitchToPartyMemberRoutine(int partyIndex, bool autoSwitch)
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        if (manager == null || !manager.SetLeadPokemon(partyIndex))
            yield break;

        if (PlayerPokemon != null)
            PlayerPokemon.ResetBattleStages();

        PlayerPokemon = manager.PlayerLeadPokemon;
        if (PlayerPokemon == null)
            yield break;

        PlayerPokemon.RecalculateMaxValues();
        PlayerPokemon.ResetBattleStages();
        Spawner.ReplacePlayer(PlayerPokemon);
        Spawner.ResetVisuals();
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);

        string message = autoSwitch
            ? $"{manager.PlayerTrainerName} sent out {GetName(PlayerPokemon)}!"
            : $"Go, {GetName(PlayerPokemon)}!";
        yield return UI.ShowMessage(message);
    }

    bool CanAttemptCapture()
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        return EnemyPokemon != null &&
               !EnemyPokemon.IsFainted &&
               manager != null &&
               manager.CurrentEncounter != null &&
               manager.CurrentEncounter.WildPokemon == EnemyPokemon &&
               manager.PokeBallCount > 0;
    }

    bool CanSwitchPokemon()
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        return manager != null && manager.HasOtherUsablePartyMember(PlayerPokemon);
    }

    List<int> GetSwitchablePartyIndices()
    {
        List<int> indices = new List<int>();
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        if (manager == null)
            return indices;

        for (int i = 0; i < manager.Party.Count; i++)
        {
            PokemonInstance candidate = manager.Party[i];
            if (candidate == null || candidate == PlayerPokemon || candidate.IsFainted)
                continue;

            indices.Add(i);
        }

        return indices;
    }

    static string FormatPartyOption(PokemonInstance pokemon)
    {
        if (pokemon == null)
            return "Empty";

        return $"{BattleUI.GetName(pokemon)} Lv.{pokemon.Level}\nHP {pokemon.CurrentHP}/{pokemon.MaxHP}";
    }

    string GetCatchActionLabel()
    {
        return $"Ball x{GetPokeBallCount()}";
    }

    int GetPokeBallCount()
    {
        return OpenWorldEncounterManager.Instance != null
            ? OpenWorldEncounterManager.Instance.PokeBallCount
            : 0;
    }

    IEnumerator PlayCaptureSequence(CatchAttemptResult result)
    {
        int shownShakes = Mathf.Min(3, result.Success ? 3 : result.SuccessfulShakes);
        for (int i = 0; i < shownShakes; i++)
        {
            yield return Animator.CaptureShake(false);
            yield return UI.ShowMessage($"{i + 1}...", 0.38f);
        }

        if (result.Success)
        {
            yield return Animator.CapturePop(false, false);
            yield return UI.ShowMessage("Click!", 0.55f);
            yield break;
        }

        yield return Animator.CapturePop(false, true);
        yield return UI.ShowMessage("Pop!", 0.45f);
    }

    static string BuildFailedCaptureMessage(int successfulShakes)
    {
        return successfulShakes switch
        {
            0 => "Oh no! The Pokemon broke free immediately!",
            1 => "Aww! It appeared to be caught!",
            2 => "Aargh! Almost had it!",
            3 => "So close! It broke free!",
            _ => "The Pokemon broke free!"
        };
    }

    void ApplyDamage(PokemonInstance pokemon, int damage)
    {
        pokemon.CurrentHP = Mathf.Max(0, pokemon.CurrentHP - damage);
    }

    static bool WasCancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            return true;
#endif
        return false;
    }
}
