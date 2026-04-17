using UnityEngine;
using System;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  STAT BLOCK — Reusable 6-stat container
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Holds the 6 core stats. Reused for base stats, IVs, EVs, and battle stages.
/// </summary>
[Serializable]
public struct StatBlock
{
    public int HP;
    public int Attack;
    public int Defense;
    public int SpAttack;
    public int SpDefense;
    public int Speed;

    public StatBlock(int hp, int atk, int def, int spa, int spd, int spe)
    {
        HP = hp; Attack = atk; Defense = def;
        SpAttack = spa; SpDefense = spd; Speed = spe;
    }

    /// <summary>Total sum of all stats (used for EV cap validation: max 510).</summary>
    public int Total => HP + Attack + Defense + SpAttack + SpDefense + Speed;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  NATURE
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// 25 natures. 5 are neutral (no stat change), 20 boost one stat by 10% and reduce another by 10%.
///
/// Nature stat table (partial):
///   Adamant   +ATK    -SPATK
///   Brave     +ATK    -SPD
///   Jolly     +SPD    -SPATK
///   Modest    +SPATK  -ATK
///   Timid     +SPD    -ATK
///   Bold      +DEF    -ATK
///   Impish    +DEF    -SPATK
///   Calm      +SPDEF  -ATK
///   Careful   +SPDEF  -SPATK
///   Neutral: Hardy, Docile, Bashful, Quirky, Serious (no change)
/// </summary>
public enum PokemonNature
{
    Hardy, Lonely, Brave, Adamant, Naughty,
    Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive,
    Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky
}

/// <summary>
/// Pokemon gender. Affects breeding and some moves/abilities (Attract, Rivalry).
/// </summary>
public enum PokemonGender { Male, Female, Genderless }

/// <summary>
/// EXP growth curve. Determines total EXP needed per level.
///
///   Erratic      — fastest to 50, slowest to 100
///   Fast         — consistently fast
///   MediumFast   — standard (most Pokemon)
///   MediumSlow   — slightly slower
///   Slow         — requires most total EXP
///   Fluctuating  — variable, fastest to 100
/// </summary>
public enum GrowthRate
{
    Erratic, Fast, MediumFast, MediumSlow, Slow, Fluctuating
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  ABILITY DATA — ScriptableObject
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Defines a Pokemon ability. Each ability triggers different battle effects
/// handled by the battle system.
///
/// Examples:
///   Intimidate  — lowers opponent's ATK by 1 stage on switch-in
///   Levitate    — grants Ground-type immunity
///   Overgrow    — boosts Grass moves by 50% when HP below 1/3
///   Static      — 30% chance to paralyze on contact
///   Pressure    — opponent's moves cost +1 TP per use
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "Battle/Ability Data")]
public class AbilityData : ScriptableObject
{
    public string AbilityName;
    [TextArea(2, 5)]
    public string Description;

    [Tooltip("Unique ID for the battle system to look up this ability's logic.")]
    public string AbilityID;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  EVOLUTION ENTRY
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Method by which a Pokemon evolves.
/// </summary>
public enum EvolutionMethod
{
    LevelUp,
    UseItem,
    Trade,
    Friendship,
    LevelUpWithMove
}

/// <summary>
/// Defines a single evolution path for a species.
/// A Pokemon can have multiple (e.g., Eevee has 7+ evolution entries).
/// </summary>
[Serializable]
public class EvolutionEntry
{
    public PokemonData EvolvesInto;
    public EvolutionMethod Method;

    [Tooltip("Required level for LevelUp method. Ignored for other methods.")]
    [Range(0, 100)]
    public int RequiredLevel = 0;

    [Tooltip("Required item name for UseItem method.")]
    public string RequiredItemID;

    [Tooltip("Required move name for LevelUpWithMove method.")]
    public string RequiredMoveName;

    [Tooltip("Required friendship threshold for Friendship method. Standard = 220.")]
    [Range(0, 255)]
    public int RequiredFriendship = 220;

    [Tooltip("Time of day restriction. Empty = any time.")]
    public string TimeOfDay;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  LEARNABLE MOVE ENTRY
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Pairs a SkillData with the level at which a species learns it naturally.
/// </summary>
[Serializable]
public class LearnableMove
{
    public SkillData Skill;
    [Range(1, 100)]
    public int LevelLearned = 1;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  POKEMON DATA — ScriptableObject (Species Template)
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Species-level template. Shared by all individuals of the same species.
/// This is the "Pokedex entry" that never changes at runtime.
///
/// BaseStats: Raw stats at level 1 before IVs, EVs, or Nature.
/// EV Yield: What EVs are awarded when this species is defeated.
///   Example: Defeating a Gyarados yields +2 ATK EVs.
/// Abilities: Most species have 2 regular + 1 hidden. A specific instance
///   is assigned one of these at creation.
/// MaxTP: The species' total Technique Point pool. All equipped moves
///   draw from this shared pool.
/// GenderRatio: Percentage chance of being male (0-100). Set to -1 for genderless.
/// </summary>
[CreateAssetMenu(fileName = "New Pokemon Data", menuName = "Battle/Pokemon Data")]
public class PokemonData : ScriptableObject
{
    [Header("─── Identity ───")]
    public string PokemonName;
    public int DexNumber;
    [TextArea(2, 5)]
    public string PokedexEntry;
    public Sprite Portrait;
    public Sprite BattleSpriteFront;
    public Sprite BattleSpriteBack;

    [Header("─── Typing ───")]
    public ElementType PrimaryType;
    [Tooltip("Set equal to PrimaryType for mono-typed Pokemon.")]
    public ElementType SecondaryType;

    [Header("─── Base Stats ───")]
    public StatBlock BaseStats;

    [Header("─── TP Pool ───")]
    [Tooltip("Maximum Technique Points. Shared across all 4 equipped moves.")]
    [Range(10, 120)]
    public int MaxTP = 40;

    [Header("─── Growth & Yield ───")]
    public GrowthRate ExpGrowthRate = GrowthRate.MediumFast;

    [Tooltip("Base EXP awarded to the winner when this species is defeated.")]
    public int BaseExpYield = 50;

    [Tooltip("EVs awarded to the winner when this species is defeated.")]
    public StatBlock EVYield;

    [Header("─── Gender ───")]
    [Tooltip("% chance of being male. -1 = genderless species.")]
    [Range(-1, 100)]
    public int MaleRatio = 50;

    [Header("─── Catch & Friendship ───")]
    [Tooltip("Catch rate 1-255. Higher = easier to catch.")]
    [Range(1, 255)]
    public int CatchRate = 45;

    [Tooltip("Starting friendship when caught. Max = 255. Evolves at 220 for friendship evolutions.")]
    [Range(0, 255)]
    public int BaseFriendship = 70;

    [Header("─── Physical Traits ───")]
    [Tooltip("Weight in kg. Affects Low Kick, Grass Knot, Heavy Slam damage.")]
    public float WeightKg;
    public float HeightM;

    [Header("─── Abilities ───")]
    public AbilityData PrimaryAbility;
    public AbilityData SecondaryAbility;
    public AbilityData HiddenAbility;

    [Header("─── Learnable Moves ───")]
    [Tooltip("Moves learned by leveling up, ordered by level.")]
    public LearnableMove[] LevelUpMoves;

    [Tooltip("Moves teachable via TM/HM items.")]
    public SkillData[] TMMoves;

    [Tooltip("Moves learned through special tutors.")]
    public SkillData[] TutorMoves;

    [Tooltip("Moves known at level 1 / upon hatching.")]
    public SkillData[] EggMoves;

    [Header("─── Evolution ───")]
    [Tooltip("All possible evolution paths. Empty = final stage.")]
    public EvolutionEntry[] Evolutions;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  EQUIPPED SKILL — Runtime state per move slot
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Runtime wrapper for a single move in one of the 4 equip slots.
/// Tracks per-move state like Disable turns and Choice item locks.
/// </summary>
[Serializable]
public class EquippedSkill
{
    public SkillData Data;

    [Tooltip("If > 0, this move is Disabled and cannot be selected for this many turns.")]
    public int DisabledTurns = 0;

    [Tooltip("If true, this move is locked by Choice Band/Scarf/Specs.")]
    public bool IsChoiceLocked = false;

    public bool IsUsable(int currentTP)
    {
        if (Data == null) return false;
        if (DisabledTurns > 0) return false;
        return currentTP >= Data.TPCost;
    }
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  POKEMON INSTANCE — Runtime state for a living Pokemon
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// One specific Pokemon in the player's party or storage.
/// All mutable game state lives here; PokemonData is the immutable template.
///
/// IVs (Individual Values):
///   Range: 0-31 per stat.
///   Set at creation, never change.
///   Represent genetic potential / natural talent.
///
/// EVs (Effort Values):
///   Range: 0-252 per stat, 510 total across all stats.
///   Gained by defeating Pokemon (see PokemonData.EVYield).
///   Every 4 EVs = +1 to the final stat at level 100.
///
/// STAT FORMULA (Gen IV / Platinum):
///   HP    = ((2 * Base + IV + EV/4) * Level / 100) + Level + 10
///   Other = (((2 * Base + IV + EV/4) * Level / 100) + 5) * NatureModifier
///   NatureModifier: 1.1 (boosted), 0.9 (hindered), or 1.0 (neutral).
///
/// HP:
///   Standard hit points. Pokemon faints at 0.
///
/// TP (Technique Points):
///   Shared pool for all equipped moves.
///   Restored by items (Ether restores 10 TP, Elixir fully restores).
///   Resting at a Pokemon Center restores both HP and TP to full.
///
/// BATTLE STAT STAGES:
///   Range: -6 to +6 per stat.
///     +6 = 4.0x  |  +3 = 2.5x  |   0 = 1.0x  |  -3 = 0.4x  |  -6 = 0.25x
///     +5 = 3.5x  |  +2 = 2.0x  |  -1 = 0.67x |  -4 = 0.33x
///     +4 = 3.0x  |  +1 = 1.5x  |  -2 = 0.5x  |  -5 = 0.29x
///   Cleared on switch-out or battle end.
///
/// FRIENDSHIP:
///   0-255. Increases by walking, leveling, vitamins.
///   Decreases by fainting, using bitter items.
///   Triggers evolution at >= 220 for Friendship-type evolutions.
///   Affects Return (more = more power) and Frustration (inverse).
/// </summary>
[Serializable]
public class PokemonInstance
{
    [Header("─── Species & Identity ───")]
    public PokemonData SpeciesData;
    public string Nickname;
    public PokemonGender Gender;
    public PokemonNature Nature;
    public bool IsShiny;

    [Header("─── Level & Experience ───")]
    [Range(1, 100)]
    public int Level = 1;
    public int CurrentExp = 0;

    [Header("─── HP (Hit Points) ───")]
    public int CurrentHP;
    public int MaxHP;

    [Header("─── TP (Technique Points) ───")]
    [Tooltip("Shared across all 4 moves. Consumed on every move use.")]
    public int CurrentTP;
    public int MaxTP;

    [Header("─── IVs (0-31, set at birth/catch, never change) ───")]
    public StatBlock IVs;

    [Header("─── EVs (0-252 each, 510 total cap) ───")]
    public StatBlock EVs;

    [Header("─── Ability ───")]
    public AbilityData Ability;

    [Header("─── Held Item ───")]
    [Tooltip("ID of the held item. Empty = no item.")]
    public string HeldItemID;

    [Header("─── Equipped Moves (4 slots) ───")]
    public EquippedSkill[] EquippedSkills = new EquippedSkill[4];

    [Header("─── Status ───")]
    public PrimaryStatus CurrentStatus = PrimaryStatus.None;
    [Tooltip("Turns remaining for Sleep/Freeze. 0 = use default mechanics.")]
    public int StatusTurnsRemaining = 0;

    [Header("─── Battle-Only (reset on switch/battle end) ───")]
    public StatBlock StatStages;
    [Range(-6, 6)]
    public int AccuracyStage = 0;
    [Range(-6, 6)]
    public int EvasionStage = 0;

    [Header("─── Friendship ───")]
    [Range(0, 255)]
    public int Friendship;

    [Header("─── Trainer Info ───")]
    public string OriginalTrainerName;
    public int OriginalTrainerID;

    // ── Derived Properties ──

    public bool IsFainted => CurrentHP <= 0;
    public bool HasFullHP => CurrentHP >= MaxHP;
    public bool HasFullTP => CurrentTP >= MaxTP;

    // ── Stat Calculation (Gen IV Formula) ──

    /// <summary>
    /// HP = ((2 * Base + IV + EV/4) * Level / 100) + Level + 10
    /// </summary>
    public int CalculateMaxHP()
    {
        int b = SpeciesData.BaseStats.HP;
        int iv = Mathf.Clamp(IVs.HP, 0, 31);
        int ev = Mathf.Clamp(EVs.HP, 0, 252);
        return Mathf.FloorToInt((2 * b + iv + ev / 4f) * Level / 100f) + Level + 10;
    }

    /// <summary>
    /// Stat = (((2 * Base + IV + EV/4) * Level / 100) + 5) * NatureMod
    /// </summary>
    public int CalculateStat(int baseStat, int iv, int ev, float natureMod)
    {
        iv = Mathf.Clamp(iv, 0, 31);
        ev = Mathf.Clamp(ev, 0, 252);
        float raw = ((2 * baseStat + iv + ev / 4f) * Level / 100f) + 5f;
        return Mathf.FloorToInt(raw * natureMod);
    }

    /// <summary>
    /// Recalculates MaxHP and MaxTP. Call after level up, EV gain, or nature change.
    /// </summary>
    public void RecalculateMaxValues()
    {
        MaxHP = CalculateMaxHP();
        MaxTP = SpeciesData.MaxTP;
    }

    /// <summary>
    /// Full heal: restores HP, TP, clears status and stat stages (Pokemon Center).
    /// </summary>
    public void FullRestore()
    {
        RecalculateMaxValues();
        CurrentHP = MaxHP;
        CurrentTP = MaxTP;
        CurrentStatus = PrimaryStatus.None;
        StatusTurnsRemaining = 0;
        ResetBattleStages();
    }

    /// <summary>
    /// Resets all in-battle stat stages to 0. Called on switch-out or battle end.
    /// </summary>
    public void ResetBattleStages()
    {
        StatStages = new StatBlock(0, 0, 0, 0, 0, 0);
        AccuracyStage = 0;
        EvasionStage = 0;
    }

    /// <summary>
    /// Returns the stat stage multiplier for a given stage value (-6 to +6).
    /// Uses the standard Gen IV table: max(2, 2+N) / max(2, 2-N).
    /// </summary>
    public static float GetStageMultiplier(int stage)
    {
        stage = Mathf.Clamp(stage, -6, 6);
        float numerator = Mathf.Max(2, 2 + stage);
        float denominator = Mathf.Max(2, 2 - stage);
        return numerator / denominator;
    }

    /// <summary>
    /// Adds EVs from a defeated Pokemon, respecting per-stat (252) and total (510) caps.
    /// </summary>
    public void GainEVs(StatBlock yield)
    {
        int remaining = 510 - EVs.Total;
        if (remaining <= 0) return;

        EVs.HP = Mathf.Min(EVs.HP + Mathf.Min(yield.HP, remaining), 252);
        remaining = 510 - EVs.Total;
        EVs.Attack = Mathf.Min(EVs.Attack + Mathf.Min(yield.Attack, remaining), 252);
        remaining = 510 - EVs.Total;
        EVs.Defense = Mathf.Min(EVs.Defense + Mathf.Min(yield.Defense, remaining), 252);
        remaining = 510 - EVs.Total;
        EVs.SpAttack = Mathf.Min(EVs.SpAttack + Mathf.Min(yield.SpAttack, remaining), 252);
        remaining = 510 - EVs.Total;
        EVs.SpDefense = Mathf.Min(EVs.SpDefense + Mathf.Min(yield.SpDefense, remaining), 252);
        remaining = 510 - EVs.Total;
        EVs.Speed = Mathf.Min(EVs.Speed + Mathf.Min(yield.Speed, remaining), 252);
    }
}
