using UnityEngine;
using System.Collections;

/// <summary>
/// Battle controller -- orchestrates flow between extracted subsystems.
///
/// Battle flow:
///   BattleStart -> PlayerTurn -> (player picks move) ->
///   ResolveTurn (speed comparison, both sides execute) ->
///   TurnEnd (status chip, weather chip) ->
///   PlayerTurn ... or BattleOver when one side faints.
/// </summary>
public class BattleSimple : MonoBehaviour
{
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
    SkillData _playerChosenSkill;
    SkillData _enemyChosenSkill;
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  INITIALIZATION
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    void Start()
    {
        InitializeBattle();
    }

    public void InitializeBattle()
    {
        if (_battleCoroutine != null)
            StopAllCoroutines();

        _turnCount = 0;
        _badPoisonCounterPlayer = 0;
        _badPoisonCounterEnemy = 0;
        _waitingForInput = false;

        BattleEvents.Clear();

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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  MAIN BATTLE COROUTINE
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    IEnumerator BattleRoutine()
    {
        _state = BattleState.BattleStart;
        yield return new WaitForSeconds(0.3f);

        yield return UI.ShowMessage($"A wild {GetName(EnemyPokemon)} appeared!");
        yield return UI.ShowMessage($"Go, {GetName(PlayerPokemon)}!");

        while (true)
        {
            _state = BattleState.PlayerTurn;
            _turnCount++;

            yield return WaitForPlayerInput();

            _state = BattleState.EnemyTurn;
            _enemyChosenSkill = EnemyAI.PickMove(EnemyPokemon);

            _state = BattleState.ResolveTurn;
            yield return ResolveTurnRoutine();

            if (PlayerPokemon.IsFainted || EnemyPokemon.IsFainted)
                break;

            _state = BattleState.TurnEnd;
            yield return TurnEndRoutine();

            if (PlayerPokemon.IsFainted || EnemyPokemon.IsFainted)
                break;
        }

        _state = BattleState.BattleOver;

        if (PlayerPokemon.IsFainted)
        {
            yield return UI.ShowMessage($"{GetName(PlayerPokemon)} fainted!");
            BattleEvents.OnFainted?.Invoke(PlayerPokemon);
        }
        if (EnemyPokemon.IsFainted)
        {
            yield return UI.ShowMessage($"{GetName(EnemyPokemon)} fainted! You win!");
            BattleEvents.OnFainted?.Invoke(EnemyPokemon);
        }

        UI.DisableAllButtons();
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);
        BattleEvents.OnBattleEnd?.Invoke(!PlayerPokemon.IsFainted);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  PLAYER INPUT
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    IEnumerator WaitForPlayerInput()
    {
        _waitingForInput = true;

        bool anyUsable = UI.SetupSkillButtons(PlayerPokemon, OnPlayerSkillSelected);
        UI.RefreshUI(PlayerPokemon, EnemyPokemon);

        if (!anyUsable)
        {
            _waitingForInput = false;
            yield return UI.ShowMessage($"{GetName(PlayerPokemon)} has no TP left! It must use Struggle!");
            _playerChosenSkill = null;
            yield break;
        }

        UI.SetPrompt($"What will {GetName(PlayerPokemon)} do?");

        while (_waitingForInput)
            yield return null;
    }

    void OnPlayerSkillSelected(int slotIndex)
    {
        EquippedSkill slot = PlayerPokemon.EquippedSkills[slotIndex];
        _playerChosenSkill = slot.Data;
        UI.DisableAllButtons();
        _waitingForInput = false;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  TURN RESOLUTION
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    IEnumerator ResolveTurnRoutine()
    {
        bool playerFirst = DetermineWhoGoesFirst();

        PokemonInstance firstAttacker  = playerFirst ? PlayerPokemon : EnemyPokemon;
        PokemonInstance firstDefender  = playerFirst ? EnemyPokemon  : PlayerPokemon;
        SkillData       firstSkill     = playerFirst ? _playerChosenSkill : _enemyChosenSkill;

        PokemonInstance secondAttacker = playerFirst ? EnemyPokemon  : PlayerPokemon;
        PokemonInstance secondDefender = playerFirst ? PlayerPokemon : EnemyPokemon;
        SkillData       secondSkill    = playerFirst ? _enemyChosenSkill : _playerChosenSkill;

        yield return ExecuteMoveRoutine(firstAttacker, firstDefender, firstSkill);

        if (firstDefender.IsFainted || firstAttacker.IsFainted)
            yield break;

        yield return Animator.TurnTransitionPunch();

        yield return ExecuteMoveRoutine(secondAttacker, secondDefender, secondSkill);
    }

    bool DetermineWhoGoesFirst()
    {
        int playerPriority = _playerChosenSkill != null ? _playerChosenSkill.Priority : 0;
        int enemyPriority  = _enemyChosenSkill  != null ? _enemyChosenSkill.Priority  : 0;

        if (playerPriority != enemyPriority)
            return playerPriority > enemyPriority;

        float playerSpeed = DamageCalculator.GetEffectiveSpeed(PlayerPokemon);
        float enemySpeed  = DamageCalculator.GetEffectiveSpeed(EnemyPokemon);

        if (Mathf.Abs(playerSpeed - enemySpeed) > 0.01f)
            return playerSpeed > enemySpeed;

        return Random.value > 0.5f;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  MOVE EXECUTION
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

        // Struggle
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

        // Status moves
        if (skill.Category == MoveCategory.Status)
        {
            yield return ApplyHealingRoutine(attacker, skill, atkIsPlayer);
            yield return ApplySecondaryEffectsRoutine(attacker, defender, skill);
            UI.RefreshUI(PlayerPokemon, EnemyPokemon);
            yield break;
        }

        // Accuracy check
        if (!DamageCalculator.RollAccuracy(attacker, defender, skill))
        {
            yield return UI.ShowMessage($"{attackerName}'s attack missed!");
            yield break;
        }

        // Multi-hit loop
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

        // Drain
        if (skill.DrainPercent > 0f && totalDealt > 0)
        {
            int healed = Mathf.Max(1, Mathf.RoundToInt(totalDealt * skill.DrainPercent));
            int hpBefore = attacker.CurrentHP;
            attacker.CurrentHP = Mathf.Min(attacker.MaxHP, attacker.CurrentHP + healed);
            yield return UI.ShowMessage($"{attackerName} restored {healed} HP!");
            yield return UI.AnimateHPChange(attacker, hpBefore, attacker.CurrentHP, atkIsPlayer);
        }

        // Recoil
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

        // Secondary effects
        if (!defender.IsFainted)
            yield return ApplySecondaryEffectsRoutine(attacker, defender, skill);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  STATUS CHECK
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  SECONDARY EFFECTS
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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
            {
                yield return ApplyStatChangeRoutine(target, fx.StatAffected, fx.StageChange);
            }

            if (fx.FlinchChance > 0 && Random.Range(1, 101) <= fx.FlinchChance)
            {
                yield return UI.ShowMessage($"{GetName(defender)} flinched!");
            }

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
            case TargetStat.Attack:    s.Attack    = Mathf.Clamp(s.Attack + stages, -6, 6); break;
            case TargetStat.Defense:   s.Defense   = Mathf.Clamp(s.Defense + stages, -6, 6); break;
            case TargetStat.SpAttack:  s.SpAttack  = Mathf.Clamp(s.SpAttack + stages, -6, 6); break;
            case TargetStat.SpDefense: s.SpDefense = Mathf.Clamp(s.SpDefense + stages, -6, 6); break;
            case TargetStat.Speed:     s.Speed     = Mathf.Clamp(s.Speed + stages, -6, 6); break;
            case TargetStat.Accuracy:  pokemon.AccuracyStage = Mathf.Clamp(pokemon.AccuracyStage + stages, -6, 6); break;
            case TargetStat.Evasion:   pokemon.EvasionStage = Mathf.Clamp(pokemon.EvasionStage + stages, -6, 6); break;
        }
        pokemon.StatStages = s;

        yield return UI.ShowMessage($"{name}'s {stat} {sharply}{direction}!");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  HEALING
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    IEnumerator ApplyHealingRoutine(PokemonInstance user, SkillData skill, bool isPlayer)
    {
        if (skill.FlatHealAmount == 0) yield break;

        int healAmt;
        if (skill.HealIsPercentage)
            healAmt = Mathf.RoundToInt(user.MaxHP * skill.FlatHealAmount / 100f);
        else
            healAmt = skill.FlatHealAmount;

        if (healAmt <= 0) yield break;

        int hpBefore = user.CurrentHP;
        user.CurrentHP = Mathf.Min(user.MaxHP, user.CurrentHP + healAmt);
        yield return UI.ShowMessage($"{GetName(user)} restored {healAmt} HP!");
        yield return UI.AnimateHPChange(user, hpBefore, user.CurrentHP, isPlayer);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  TURN END
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    IEnumerator TurnEndRoutine()
    {
        bool pFaint = false, eFaint = false;

        yield return ProcessStatusChipRoutine(PlayerPokemon, true);
        if (PlayerPokemon.IsFainted && !pFaint)
        {
            pFaint = true;
            yield return Animator.FaintAnimation(true);
            BattleEvents.OnFainted?.Invoke(PlayerPokemon);
        }

        yield return ProcessStatusChipRoutine(EnemyPokemon, false);
        if (EnemyPokemon.IsFainted && !eFaint)
        {
            eFaint = true;
            yield return Animator.FaintAnimation(false);
            BattleEvents.OnFainted?.Invoke(EnemyPokemon);
        }

        yield return ProcessWeatherChipRoutine(PlayerPokemon, true);
        if (PlayerPokemon.IsFainted && !pFaint)
        {
            pFaint = true;
            yield return Animator.FaintAnimation(true);
            BattleEvents.OnFainted?.Invoke(PlayerPokemon);
        }

        yield return ProcessWeatherChipRoutine(EnemyPokemon, false);
        if (EnemyPokemon.IsFainted && !eFaint)
        {
            eFaint = true;
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    //  DAMAGE APPLICATION
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    void ApplyDamage(PokemonInstance pokemon, int damage)
    {
        pokemon.CurrentHP = Mathf.Max(0, pokemon.CurrentHP - damage);
    }
}
