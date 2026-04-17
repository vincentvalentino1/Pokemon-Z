using UnityEngine;
using System;

/// <summary>
/// All 18 elemental types used for type effectiveness and move categorization.
/// </summary>
public enum ElementType
{
    Normal, Fire, Water, Grass, Electric, Ice,
    Fighting, Poison, Ground, Flying, Psychic,
    Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy
}

/// <summary>
/// Determines which stats are used in damage calculation.
/// Physical: ATK vs DEF | Special: SPATK vs SPDEF | Status: no damage.
/// </summary>
public enum MoveCategory { Physical, Special, Status }

/// <summary>
/// Who the move targets in battle. Platinum supports Singles and Doubles.
/// </summary>
public enum MoveTarget
{
    SingleEnemy,
    AllEnemies,
    Self,
    SingleAlly,
    AllAllies,
    SelfAndAllies,
    AllOnField,
    RandomEnemy
}

/// <summary>
/// Primary status ailments — only one can be active at a time per Pokemon.
/// These persist outside of battle (except Freeze which can thaw).
/// </summary>
public enum PrimaryStatus
{
    None,
    Burn,           // Halves ATK, 1/8 max HP chip per turn
    Poison,         // 1/8 max HP per turn
    BadlyPoisoned,  // Escalates: 1/16, 2/16, 3/16... per turn
    Paralysis,      // Speed quartered, 25% chance to skip turn
    Sleep,          // Cannot act for 1-3 turns
    Freeze          // Cannot act; 20% thaw chance per turn, Fire moves thaw
}

/// <summary>
/// Volatile statuses — can stack, cleared on switch-out.
/// </summary>
public enum VolatileStatus
{
    None,
    Confusion,      // 50% chance to hit self for 1-4 turns
    Flinch,         // Skips next action (only works if user moves first)
    Infatuation,    // 50% chance to skip turn (opposite gender)
    Trapped,        // Cannot switch (Mean Look, Block)
    Cursed,         // Loses 1/4 HP per turn (Ghost-type Curse)
    LeechSeed,      // Drains 1/8 HP to opponent each turn
    Encore,         // Forced to repeat last move for 3 turns
    Taunt,          // Cannot use Status moves for 3 turns
    Torment,        // Cannot use the same move twice in a row
    Substitute,     // Blocks damage until substitute HP is depleted
    Drowsy,         // Will fall asleep next turn (Yawn effect)
    PerishCount     // Faints in 3 turns unless switched
}

/// <summary>
/// Which stat a move's secondary effect modifies.
/// </summary>
public enum TargetStat
{
    None, Attack, Defense, SpAttack, SpDefense, Speed,
    Accuracy, Evasion
}

/// <summary>
/// Weather conditions that affect battle globally.
/// </summary>
public enum WeatherType
{
    None,
    Sun,        // Boosts Fire 1.5x, weakens Water 0.5x, enables SolarBeam instant
    Rain,       // Boosts Water 1.5x, weakens Fire 0.5x, Thunder 100% accuracy
    Sandstorm,  // 1/16 chip to non-Rock/Steel/Ground, +50% SPDEF for Rock types
    Hail,       // 1/16 chip to non-Ice types, Blizzard 100% accuracy
    Fog         // Lowers accuracy of all moves (Platinum-specific)
}

/// <summary>
/// Entry hazards placed on the opponent's field.
/// </summary>
public enum EntryHazard
{
    None,
    StealthRock,    // Deals type-effective damage on switch-in
    Spikes,         // Stackable ground damage (1/8, 1/6, 1/4 per layer)
    ToxicSpikes     // Poisons on switch-in; 2 layers = badly poisoned
}

/// <summary>
/// Move property flags for ability/item interaction.
/// Uses [Flags] bitfield so multiple flags can be combined.
/// </summary>
[Flags]
public enum MoveFlags
{
    None         = 0,
    Contact      = 1 << 0,   // Triggers contact abilities (Static, Rough Skin)
    Sound        = 1 << 1,   // Bypasses Substitute (Hyper Voice, Bug Buzz)
    Punch        = 1 << 2,   // Boosted by Iron Fist ability
    Bite         = 1 << 3,   // Boosted by Strong Jaw ability
    Pulse        = 1 << 4,   // Boosted by Mega Launcher (Aura Sphere)
    Powder       = 1 << 5,   // Blocked by Grass types and Overcoat
    Recharge     = 1 << 6,   // User must skip next turn (Hyper Beam)
    Charge       = 1 << 7,   // Requires a charge turn (Solar Beam, Fly)
    Reflectable  = 1 << 8,   // Bounced by Magic Coat / Magic Bounce
    Snatchable   = 1 << 9,   // Can be stolen by Snatch
    Ballistic    = 1 << 10,  // Blocked by Bulletproof (Shadow Ball)
    Protection   = 1 << 11   // Protect / Detect style move
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  SECONDARY EFFECT
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Defines a single secondary effect a move can have.
/// A move can carry multiple (e.g., Blizzard: damage + 10% freeze).
///
/// Chance = 0 means guaranteed (used for self-buffs like Swords Dance).
/// Chance = 30 means 30% proc rate.
///
/// For stat changes: positive StageChange = buff, negative = debuff.
/// TargetsSelf = true means the effect hits the user (e.g., Close Combat lowering own DEF/SPDEF).
/// </summary>
[Serializable]
public class MoveSecondaryEffect
{
    [Tooltip("Proc chance 0-100. 0 means always applies (guaranteed effects).")]
    [Range(0, 100)]
    public int Chance = 0;

    [Header("Status")]
    public PrimaryStatus InflictStatus = PrimaryStatus.None;
    public VolatileStatus InflictVolatile = VolatileStatus.None;

    [Header("Stat Modification")]
    public TargetStat StatAffected = TargetStat.None;
    [Range(-3, 3)]
    public int StageChange = 0;
    public bool TargetsSelf = false;

    [Header("Flinch")]
    [Tooltip("Chance to flinch the target, independent of the main Chance field.")]
    [Range(0, 100)]
    public int FlinchChance = 0;

    [Header("Weather / Hazard")]
    public WeatherType SetWeather = WeatherType.None;
    [Tooltip("Duration in turns. 5 default, 8 with extending item (Heat Rock, etc).")]
    [Range(0, 8)]
    public int WeatherDuration = 5;

    public EntryHazard PlaceHazard = EntryHazard.None;
}

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  SKILL DATA — ScriptableObject
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

/// <summary>
/// Immutable template for a single move/skill.
/// Created as a ScriptableObject asset in Unity (Right-click -> Create -> Battle -> Skill Data).
///
/// HP and TP SYSTEM:
///   Every move has a TP cost (replaces PP in standard Pokemon).
///   TP is a shared pool per Pokemon — all 4 moves draw from the same TP bar.
///   When TP reaches 0, the Pokemon can only use Struggle.
///   TP is restored at Pokemon Centers, via Ether/Elixir items, or camp rest.
///
/// DAMAGE FORMULA (Gen IV / Platinum):
///   Damage = ((2 * Level / 5 + 2) * Power * A / D) / 50 + 2
///   Where A = ATK or SPATK, D = DEF or SPDEF depending on MoveCategory.
///   Then multiply by: Random(0.85-1.0) * CritMod * TypeEffectiveness * StatusMod
///
/// PRIORITY BRACKETS:
///   +5  Helping Hand
///   +4  Protect, Detect
///   +3  Fake Out, Follow Me
///   +2  ExtremeSpeed
///   +1  Quick Attack, Mach Punch, Aqua Jet, Sucker Punch
///    0  (most moves)
///   -1  Vital Throw
///   -3  Focus Punch
///   -5  Trick Room
///   -6  Counter, Mirror Coat
///   -7  Roar, Whirlwind
/// </summary>
[CreateAssetMenu(fileName = "New Skill Data", menuName = "Battle/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("─── Identity ───")]
    public string SkillName;
    [TextArea(2, 5)]
    public string Description;
    public Sprite Icon;

    [Header("─── Typing & Category ───")]
    public ElementType SkillElement;
    public MoveCategory Category;

    [Header("─── Power & Accuracy ───")]
    [Tooltip("Base power. 0 for pure Status moves. Variable-power moves use code overrides.")]
    [Range(0, 300)]
    public int BasePower;

    [Tooltip("Accuracy 1-100. Set 0 for moves that bypass accuracy checks (Swift, Aerial Ace).")]
    [Range(0, 100)]
    public int Accuracy = 100;

    [Header("─── TP Cost ───")]
    [Tooltip("Technique Points consumed per use. Equivalent to PP cost in standard games.")]
    [Range(1, 30)]
    public int TPCost = 5;

    [Header("─── Hit Mechanics ───")]
    [Tooltip("Minimum hits per use. Most moves = 1. Multi-hit moves: min 2.")]
    [Range(1, 5)]
    public int MinHits = 1;

    [Tooltip("Maximum hits per use. Equal to MinHits for single-strike moves.")]
    [Range(1, 5)]
    public int MaxHits = 1;

    [Header("─── Priority & Targeting ───")]
    [Tooltip("Priority bracket. Higher = acts first regardless of Speed.")]
    [Range(-7, 5)]
    public int Priority = 0;

    public MoveTarget Target = MoveTarget.SingleEnemy;

    [Header("─── Critical Hit ───")]
    [Tooltip("Crit stage bonus. 0 = normal (6.25%), 1 = high (12.5%), 2 = 50%, 3+ = guaranteed.")]
    [Range(0, 4)]
    public int CritStageBonus = 0;

    [Header("─── HP Manipulation ───")]
    [Tooltip("% of damage dealt restored as HP. 0.5 = half drain (Giga Drain). 0 = none.")]
    [Range(0f, 1f)]
    public float DrainPercent = 0f;

    [Tooltip("Recoil as % of damage dealt. 0.33 = 1/3 recoil (Brave Bird). 0 = none.")]
    [Range(0f, 1f)]
    public float RecoilPercent = 0f;

    [Tooltip("Flat HP healed on user. For moves like Recover (50% max HP, use -1 as flag) or Rest.")]
    public int FlatHealAmount = 0;

    [Tooltip("If true, heals a percentage of max HP instead of flat amount. FlatHealAmount = % (50 = 50%).")]
    public bool HealIsPercentage = false;

    [Header("─── Move Flags ───")]
    public MoveFlags Flags = MoveFlags.None;

    [Header("─── Secondary Effects ───")]
    [Tooltip("List of secondary effects. Evaluated after damage in order.")]
    public MoveSecondaryEffect[] SecondaryEffects;

    [Header("─── Special Behavior ───")]
    [Tooltip("Removes entry hazards on the user's side (Rapid Spin, Defog).")]
    public bool RemovesHazards = false;

    [Tooltip("If true, uses target's DEF stat even for Special moves (e.g., Psyshock).")]
    public bool UsesPhysicalDefense = false;

    [Tooltip("If true, user switches out after use (U-turn, Volt Switch).")]
    public bool SwitchAfterUse = false;

    [Tooltip("If true, traps the target and deals damage over 4-5 turns (Fire Spin, Wrap).")]
    public bool IsBinding = false;

    [Tooltip("If true, the move always hits (bypasses accuracy/evasion checks).")]
    public bool BypassesAccuracy = false;

    [Tooltip("Percentage of target's current HP dealt as damage. Overrides BasePower when > 0.")]
    [Range(0f, 1f)]
    public float PercentCurrentHPDamage = 0f;

    [Tooltip("Fixed damage dealt (e.g., Seismic Toss = user's level, Dragon Rage = 40). 0 = use formula.")]
    public int FixedDamage = 0;
}
