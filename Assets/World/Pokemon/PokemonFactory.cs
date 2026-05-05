using System.Collections.Generic;
using UnityEngine;

public static class PokemonFactory
{
    public static PokemonInstance CreateWildPokemon(PokemonData species, int level)
    {
        PokemonInstance instance = new PokemonInstance
        {
            SpeciesData = species,
            Level = Mathf.Clamp(level, 1, 100),
            Nature = (PokemonNature)Random.Range(0, 25),
            Gender = RollGender(species),
            IsShiny = Random.Range(0, 4096) == 0,
            Friendship = species != null ? species.BaseFriendship : 70
        };

        instance.IVs = new StatBlock(
            Random.Range(0, 32),
            Random.Range(0, 32),
            Random.Range(0, 32),
            Random.Range(0, 32),
            Random.Range(0, 32),
            Random.Range(0, 32)
        );

        instance.EVs = new StatBlock(0, 0, 0, 0, 0, 0);
        instance.RecalculateMaxValues();
        instance.CurrentHP = instance.MaxHP;
        instance.CurrentTP = instance.MaxTP;
        instance.Ability = PickAbility(species);
        instance.Nickname = species != null ? species.PokemonName : "Unknown";

        AssignMovesForLevel(instance, level);
        return instance;
    }

    public static PokemonInstance CreatePlayerPokemon(PokemonData species, int level)
    {
        PokemonInstance instance = CreateWildPokemon(species, level);
        instance.IsShiny = false;
        instance.OriginalTrainerName = "Player";
        instance.OriginalTrainerID = Random.Range(100000, 999999);
        return instance;
    }

    public static WorldPokemonRuntimeData CreateRuntimeWorldPokemon(PokemonData species, int level, WorldPokemonBehaviorProfile behaviorProfile, bool destroyAfterEncounter)
    {
        PokemonInstance instance = CreateWildPokemon(species, level);
        return new WorldPokemonRuntimeData(instance, behaviorProfile, destroyAfterEncounter);
    }

    static void AssignMovesForLevel(PokemonInstance instance, int level)
    {
        if (instance?.SpeciesData?.LevelUpMoves == null)
            return;

        List<SkillData> learnedMoves = new List<SkillData>(4);
        LearnableMove[] learnset = instance.SpeciesData.LevelUpMoves;
        for (int i = 0; i < learnset.Length; i++)
        {
            LearnableMove entry = learnset[i];
            if (entry == null || entry.Skill == null)
                continue;

            if (entry.LevelLearned > level)
                continue;

            learnedMoves.Remove(entry.Skill);
            learnedMoves.Add(entry.Skill);
            if (learnedMoves.Count > 4)
                learnedMoves.RemoveAt(0);
        }

        for (int i = 0; i < instance.EquippedSkills.Length; i++)
        {
            instance.EquippedSkills[i] = new EquippedSkill();
            if (i < learnedMoves.Count)
                instance.EquippedSkills[i].Data = learnedMoves[i];
        }
    }

    static AbilityData PickAbility(PokemonData species)
    {
        if (species == null)
            return null;

        List<AbilityData> abilities = new List<AbilityData>(3);
        if (species.PrimaryAbility != null) abilities.Add(species.PrimaryAbility);
        if (species.SecondaryAbility != null) abilities.Add(species.SecondaryAbility);
        if (species.HiddenAbility != null && Random.value < 0.1f) abilities.Add(species.HiddenAbility);

        if (abilities.Count == 0)
            return null;

        return abilities[Random.Range(0, abilities.Count)];
    }

    static PokemonGender RollGender(PokemonData species)
    {
        if (species == null || species.MaleRatio < 0)
            return PokemonGender.Genderless;

        float maleChance = Mathf.Clamp(species.MaleRatio, 0, 100) / 100f;
        return Random.value < maleChance ? PokemonGender.Male : PokemonGender.Female;
    }
}
