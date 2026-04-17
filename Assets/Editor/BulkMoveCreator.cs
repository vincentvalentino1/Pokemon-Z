using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class BulkMoveCreator : EditorWindow
{
    const string MoveFolder = "Assets/BatlleScene/Moves";
    const string AbilityFolder = "Assets/BatlleScene/Abilities";
    const string DataFolder = "Assets/BatlleScene";

    [MenuItem("Tools/Battle/Create All Moves and Abilities")]
    static void CreateAll()
    {
        EnsureFolder(MoveFolder);
        EnsureFolder(AbilityFolder);

        var moves = new Dictionary<string, SkillData>();
        var abilities = new Dictionary<string, AbilityData>();

        // ════════════════════════════════════════
        //  ABILITIES
        // ════════════════════════════════════════

        abilities["Overgrow"] = CreateAbility("Overgrow", "overgrow",
            "Powers up Grass-type moves when the Pokemon's HP is low.");
        abilities["Chlorophyll"] = CreateAbility("Chlorophyll", "chlorophyll",
            "Boosts the Pokemon's Speed stat in harsh sunlight.");
        abilities["Static"] = CreateAbility("Static", "static",
            "The Pokemon is charged with static electricity, so contact with it may cause paralysis.");
        abilities["LightningRod"] = CreateAbility("Lightning Rod", "lightning_rod",
            "The Pokemon draws in all Electric-type moves to boost its Sp. Atk stat.");

        // ════════════════════════════════════════
        //  EXISTING MOVES (recreate in Moves/)
        // ════════════════════════════════════════

        // Tackle
        moves["Tackle"] = CreateMove("Tackle", "A physical attack in which the user charges and slams into the target.",
            ElementType.Normal, MoveCategory.Physical, 40, 100, 3, flags: MoveFlags.Contact);

        // Vine Whip
        moves["VineWhip"] = CreateMove("Vine Whip", "The target is struck with slender, whiplike vines.",
            ElementType.Grass, MoveCategory.Physical, 45, 100, 4, flags: MoveFlags.Contact);

        // Leech Seed
        var leechSeed = CreateMove("Leech Seed", "A seed is planted on the target, stealing HP every turn.",
            ElementType.Grass, MoveCategory.Status, 0, 90, 3);
        leechSeed.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, InflictVolatile = VolatileStatus.LeechSeed }
        };
        SaveMove(leechSeed);
        moves["LeechSeed"] = leechSeed;

        // Thunderbolt
        var thunderbolt = CreateMove("Thunderbolt", "A strong electric blast crashes down on the target.",
            ElementType.Electric, MoveCategory.Special, 90, 100, 6);
        thunderbolt.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 10, InflictStatus = PrimaryStatus.Paralysis }
        };
        SaveMove(thunderbolt);
        moves["Thunderbolt"] = thunderbolt;

        // ════════════════════════════════════════
        //  BULBASAUR MOVES
        // ════════════════════════════════════════

        // Razor Leaf
        moves["RazorLeaf"] = CreateMove("Razor Leaf", "Sharp-edged leaves are launched to slash at opposing Pokemon.",
            ElementType.Grass, MoveCategory.Physical, 55, 95, 4, critBonus: 1);

        // Poison Powder
        var poisonPowder = CreateMove("Poison Powder", "The user scatters a cloud of poisonous dust that poisons the target.",
            ElementType.Poison, MoveCategory.Status, 0, 75, 3, flags: MoveFlags.Powder);
        poisonPowder.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, InflictStatus = PrimaryStatus.Poison }
        };
        SaveMove(poisonPowder);
        moves["PoisonPowder"] = poisonPowder;

        // Sleep Powder
        var sleepPowder = CreateMove("Sleep Powder", "The user scatters a big cloud of sleep-inducing dust around the target.",
            ElementType.Grass, MoveCategory.Status, 0, 75, 3, flags: MoveFlags.Powder);
        sleepPowder.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, InflictStatus = PrimaryStatus.Sleep }
        };
        SaveMove(sleepPowder);
        moves["SleepPowder"] = sleepPowder;

        // Growth
        var growth = CreateMove("Growth", "The user's body grows all at once, boosting Attack and Sp. Atk.",
            ElementType.Normal, MoveCategory.Status, 0, 0, 3);
        growth.BypassesAccuracy = true;
        growth.Target = MoveTarget.Self;
        growth.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, StatAffected = TargetStat.Attack, StageChange = 1, TargetsSelf = true },
            new MoveSecondaryEffect { Chance = 0, StatAffected = TargetStat.SpAttack, StageChange = 1, TargetsSelf = true }
        };
        SaveMove(growth);
        moves["Growth"] = growth;

        // Seed Bomb
        moves["SeedBomb"] = CreateMove("Seed Bomb", "The user slams a barrage of hard-shelled seeds on the target.",
            ElementType.Grass, MoveCategory.Physical, 80, 100, 5, flags: MoveFlags.Ballistic);

        // Take Down
        moves["TakeDown"] = CreateMove("Take Down", "A reckless, full-body charge attack that also hurts the user.",
            ElementType.Normal, MoveCategory.Physical, 90, 85, 6, recoil: 0.25f, flags: MoveFlags.Contact);

        // Sludge Bomb
        var sludgeBomb = CreateMove("Sludge Bomb", "Unsanitary sludge is hurled at the target. It may poison the target.",
            ElementType.Poison, MoveCategory.Special, 90, 100, 6, flags: MoveFlags.Ballistic);
        sludgeBomb.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 30, InflictStatus = PrimaryStatus.Poison }
        };
        SaveMove(sludgeBomb);
        moves["SludgeBomb"] = sludgeBomb;

        // ════════════════════════════════════════
        //  PIKACHU MOVES
        // ════════════════════════════════════════

        // Thunder Shock
        var thunderShock = CreateMove("Thunder Shock", "A jolt of electricity crashes down on the target.",
            ElementType.Electric, MoveCategory.Special, 40, 100, 3);
        thunderShock.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 10, InflictStatus = PrimaryStatus.Paralysis }
        };
        SaveMove(thunderShock);
        moves["ThunderShock"] = thunderShock;

        // Quick Attack
        moves["QuickAttack"] = CreateMove("Quick Attack", "The user lunges at the target at a speed that makes it almost invisible.",
            ElementType.Normal, MoveCategory.Physical, 40, 100, 3, priority: 1, flags: MoveFlags.Contact);

        // Iron Tail
        var ironTail = CreateMove("Iron Tail", "The target is slammed with a steel-hard tail.",
            ElementType.Steel, MoveCategory.Physical, 100, 75, 6, flags: MoveFlags.Contact);
        ironTail.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 30, StatAffected = TargetStat.Defense, StageChange = -1 }
        };
        SaveMove(ironTail);
        moves["IronTail"] = ironTail;

        // Thunder Wave
        var thunderWave = CreateMove("Thunder Wave", "The user launches a weak jolt of electricity that paralyzes the target.",
            ElementType.Electric, MoveCategory.Status, 0, 90, 3);
        thunderWave.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, InflictStatus = PrimaryStatus.Paralysis }
        };
        SaveMove(thunderWave);
        moves["ThunderWave"] = thunderWave;

        // Electro Ball
        moves["ElectroBall"] = CreateMove("Electro Ball", "The user hurls an electric orb. The faster the user, the greater the damage.",
            ElementType.Electric, MoveCategory.Special, 80, 100, 5, flags: MoveFlags.Ballistic);

        // Volt Tackle
        var voltTackle = CreateMove("Volt Tackle", "The user electrifies itself, then charges. It causes considerable damage to the user.",
            ElementType.Electric, MoveCategory.Physical, 120, 100, 8, recoil: 0.33f, flags: MoveFlags.Contact);
        voltTackle.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 10, InflictStatus = PrimaryStatus.Paralysis }
        };
        SaveMove(voltTackle);
        moves["VoltTackle"] = voltTackle;

        // Double Kick
        moves["DoubleKick"] = CreateMove("Double Kick", "The target is quickly kicked twice in succession using both feet.",
            ElementType.Fighting, MoveCategory.Physical, 30, 100, 4, minHits: 2, maxHits: 2, flags: MoveFlags.Contact);

        // ════════════════════════════════════════
        //  SHARED MOVES
        // ════════════════════════════════════════

        // Growl
        var growl = CreateMove("Growl", "The user growls in an endearing way, making opposing Pokemon less wary.",
            ElementType.Normal, MoveCategory.Status, 0, 100, 2, flags: MoveFlags.Sound);
        growl.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, StatAffected = TargetStat.Attack, StageChange = -1 }
        };
        SaveMove(growl);
        moves["Growl"] = growl;

        // Tail Whip
        var tailWhip = CreateMove("Tail Whip", "The user wags its tail cutely, making opposing Pokemon less wary and lowering their Defense.",
            ElementType.Normal, MoveCategory.Status, 0, 100, 2);
        tailWhip.SecondaryEffects = new MoveSecondaryEffect[] {
            new MoveSecondaryEffect { Chance = 0, StatAffected = TargetStat.Defense, StageChange = -1 }
        };
        SaveMove(tailWhip);
        moves["TailWhip"] = tailWhip;

        // Protect
        var protect = CreateMove("Protect", "Enables the user to evade all attacks. Its chance of failing rises if used in succession.",
            ElementType.Normal, MoveCategory.Status, 0, 0, 3, priority: 4, flags: MoveFlags.Protection);
        protect.BypassesAccuracy = true;
        protect.Target = MoveTarget.Self;
        SaveMove(protect);
        moves["Protect"] = protect;

        // ════════════════════════════════════════
        //  WIRE ONTO POKEMON DATA
        // ════════════════════════════════════════

        WireBulbasaur(moves, abilities);
        WirePikachu(moves, abilities);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BulkMoveCreator] Created {moves.Count} moves and {abilities.Count} abilities.");
        EditorUtility.DisplayDialog("Done",
            $"Created {moves.Count} move assets in {MoveFolder}\n" +
            $"Created {abilities.Count} ability assets in {AbilityFolder}\n\n" +
            "Bulbasaur and Pikachu PokemonData updated with:\n" +
            "- Abilities wired\n" +
            "- LevelUpMoves populated\n\n" +
            "Save the project (Cmd+S / Ctrl+S).", "OK");
    }

    // ────────────────────────────────────────────
    //  POKEMON DATA WIRING
    // ────────────────────────────────────────────

    static void WireBulbasaur(Dictionary<string, SkillData> m, Dictionary<string, AbilityData> a)
    {
        string path = $"{DataFolder}/Bulbasaur.asset";
        PokemonData bulba = AssetDatabase.LoadAssetAtPath<PokemonData>(path);
        if (bulba == null)
        {
            Debug.LogWarning($"[BulkMoveCreator] Could not find {path}");
            return;
        }

        Undo.RecordObject(bulba, "Wire Bulbasaur");

        bulba.PrimaryAbility = a["Overgrow"];
        bulba.HiddenAbility = a["Chlorophyll"];

        bulba.LevelUpMoves = new LearnableMove[] {
            new LearnableMove { Skill = m["Tackle"],       LevelLearned = 1 },
            new LearnableMove { Skill = m["Growl"],        LevelLearned = 1 },
            new LearnableMove { Skill = m["VineWhip"],     LevelLearned = 7 },
            new LearnableMove { Skill = m["Growth"],       LevelLearned = 9 },
            new LearnableMove { Skill = m["LeechSeed"],    LevelLearned = 9 },
            new LearnableMove { Skill = m["RazorLeaf"],    LevelLearned = 13 },
            new LearnableMove { Skill = m["PoisonPowder"], LevelLearned = 15 },
            new LearnableMove { Skill = m["SleepPowder"],  LevelLearned = 15 },
            new LearnableMove { Skill = m["SeedBomb"],     LevelLearned = 21 },
            new LearnableMove { Skill = m["TakeDown"],     LevelLearned = 25 },
            new LearnableMove { Skill = m["SludgeBomb"],   LevelLearned = 33 },
        };

        EditorUtility.SetDirty(bulba);
    }

    static void WirePikachu(Dictionary<string, SkillData> m, Dictionary<string, AbilityData> a)
    {
        string path = $"{DataFolder}/Pikachu.asset";
        PokemonData pika = AssetDatabase.LoadAssetAtPath<PokemonData>(path);
        if (pika == null)
        {
            Debug.LogWarning($"[BulkMoveCreator] Could not find {path}");
            return;
        }

        Undo.RecordObject(pika, "Wire Pikachu");

        pika.PrimaryAbility = a["Static"];
        pika.HiddenAbility = a["LightningRod"];

        pika.LevelUpMoves = new LearnableMove[] {
            new LearnableMove { Skill = m["ThunderShock"], LevelLearned = 1 },
            new LearnableMove { Skill = m["TailWhip"],     LevelLearned = 1 },
            new LearnableMove { Skill = m["Growl"],        LevelLearned = 5 },
            new LearnableMove { Skill = m["QuickAttack"],  LevelLearned = 10 },
            new LearnableMove { Skill = m["DoubleKick"],   LevelLearned = 13 },
            new LearnableMove { Skill = m["ElectroBall"],  LevelLearned = 18 },
            new LearnableMove { Skill = m["Thunderbolt"],  LevelLearned = 26 },
            new LearnableMove { Skill = m["IronTail"],     LevelLearned = 30 },
            new LearnableMove { Skill = m["VoltTackle"],   LevelLearned = 42 },
        };

        EditorUtility.SetDirty(pika);
    }

    // ────────────────────────────────────────────
    //  ASSET CREATION HELPERS
    // ────────────────────────────────────────────

    static SkillData CreateMove(string name, string desc,
        ElementType element, MoveCategory category,
        int power, int accuracy, int tpCost,
        int priority = 0, int critBonus = 0,
        float drain = 0f, float recoil = 0f,
        int minHits = 1, int maxHits = 1,
        MoveFlags flags = MoveFlags.None)
    {
        string assetPath = $"{MoveFolder}/{name}.asset";
        SkillData existing = AssetDatabase.LoadAssetAtPath<SkillData>(assetPath);
        SkillData skill = existing != null ? existing : ScriptableObject.CreateInstance<SkillData>();

        skill.SkillName = name;
        skill.Description = desc;
        skill.SkillElement = element;
        skill.Category = category;
        skill.BasePower = power;
        skill.Accuracy = accuracy;
        skill.TPCost = tpCost;
        skill.Priority = priority;
        skill.CritStageBonus = critBonus;
        skill.DrainPercent = drain;
        skill.RecoilPercent = recoil;
        skill.MinHits = minHits;
        skill.MaxHits = maxHits;
        skill.Flags = flags;
        skill.Target = MoveTarget.SingleEnemy;

        if (existing == null)
            AssetDatabase.CreateAsset(skill, assetPath);
        else
            EditorUtility.SetDirty(skill);

        return skill;
    }

    static void SaveMove(SkillData skill)
    {
        string assetPath = $"{MoveFolder}/{skill.SkillName}.asset";
        if (!AssetDatabase.LoadAssetAtPath<SkillData>(assetPath))
            AssetDatabase.CreateAsset(skill, assetPath);
        else
            EditorUtility.SetDirty(skill);
    }

    static AbilityData CreateAbility(string name, string id, string desc)
    {
        string assetPath = $"{AbilityFolder}/{name}.asset";
        AbilityData existing = AssetDatabase.LoadAssetAtPath<AbilityData>(assetPath);
        AbilityData ability = existing != null ? existing : ScriptableObject.CreateInstance<AbilityData>();

        ability.AbilityName = name;
        ability.AbilityID = id;
        ability.Description = desc;

        if (existing == null)
            AssetDatabase.CreateAsset(ability, assetPath);
        else
            EditorUtility.SetDirty(ability);

        return ability;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folderName = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
