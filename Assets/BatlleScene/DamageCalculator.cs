using UnityEngine;

public static class DamageCalculator
{
    public static int Calculate(PokemonInstance attacker, PokemonInstance defender, SkillData skill, bool isCrit, WeatherType weather)
    {
        if (skill.FixedDamage > 0)
            return skill.FixedDamage;

        if (skill.PercentCurrentHPDamage > 0f)
            return Mathf.Max(1, Mathf.RoundToInt(defender.CurrentHP * skill.PercentCurrentHPDamage));

        int power = skill.BasePower;
        if (power <= 0) return 0;

        int level = attacker.Level;

        bool isPhysical = skill.Category == MoveCategory.Physical;
        bool usePhysDef = isPhysical || skill.UsesPhysicalDefense;

        int atkStat = GetEffectiveAttack(attacker, isPhysical, isCrit);
        int defStat = GetEffectiveDefense(defender, usePhysDef, isCrit);
        defStat = Mathf.Max(1, defStat);

        float baseDmg = ((2f * level / 5f + 2f) * power * atkStat / defStat) / 50f + 2f;

        float typeEff = TypeChart.GetDualEffectiveness(
            skill.SkillElement,
            defender.SpeciesData.PrimaryType,
            defender.SpeciesData.SecondaryType
        );

        float critMod = isCrit ? 2f : 1f;
        float randomMod = Random.Range(0.85f, 1f);

        float burnMod = 1f;
        if (isPhysical && attacker.CurrentStatus == PrimaryStatus.Burn)
            burnMod = 0.5f;

        float weatherMod = GetWeatherModifier(skill.SkillElement, weather);

        float finalDmg = baseDmg * typeEff * critMod * randomMod * burnMod * weatherMod;
        return Mathf.Max(1, Mathf.FloorToInt(finalDmg));
    }

    public static int GetEffectiveAttack(PokemonInstance pokemon, bool physical, bool isCrit)
    {
        TargetStat statType = physical ? TargetStat.Attack : TargetStat.SpAttack;
        float natureMod = NatureHelper.GetNatureModifier(pokemon.Nature, statType);

        int baseStat, iv, ev, stage;

        if (physical)
        {
            baseStat = pokemon.SpeciesData.BaseStats.Attack;
            iv = pokemon.IVs.Attack; ev = pokemon.EVs.Attack;
            stage = pokemon.StatStages.Attack;
        }
        else
        {
            baseStat = pokemon.SpeciesData.BaseStats.SpAttack;
            iv = pokemon.IVs.SpAttack; ev = pokemon.EVs.SpAttack;
            stage = pokemon.StatStages.SpAttack;
        }

        int raw = pokemon.CalculateStat(baseStat, iv, ev, natureMod);
        if (isCrit && stage < 0) stage = 0;
        return Mathf.Max(1, Mathf.RoundToInt(raw * PokemonInstance.GetStageMultiplier(stage)));
    }

    public static int GetEffectiveDefense(PokemonInstance pokemon, bool physical, bool isCrit)
    {
        TargetStat statType = physical ? TargetStat.Defense : TargetStat.SpDefense;
        float natureMod = NatureHelper.GetNatureModifier(pokemon.Nature, statType);

        int baseStat, iv, ev, stage;

        if (physical)
        {
            baseStat = pokemon.SpeciesData.BaseStats.Defense;
            iv = pokemon.IVs.Defense; ev = pokemon.EVs.Defense;
            stage = pokemon.StatStages.Defense;
        }
        else
        {
            baseStat = pokemon.SpeciesData.BaseStats.SpDefense;
            iv = pokemon.IVs.SpDefense; ev = pokemon.EVs.SpDefense;
            stage = pokemon.StatStages.SpDefense;
        }

        int raw = pokemon.CalculateStat(baseStat, iv, ev, natureMod);
        if (isCrit && stage > 0) stage = 0;
        return Mathf.Max(1, Mathf.RoundToInt(raw * PokemonInstance.GetStageMultiplier(stage)));
    }

    public static float GetEffectiveSpeed(PokemonInstance pokemon)
    {
        float natureMod = NatureHelper.GetNatureModifier(pokemon.Nature, TargetStat.Speed);
        int baseStat = pokemon.CalculateStat(
            pokemon.SpeciesData.BaseStats.Speed,
            pokemon.IVs.Speed,
            pokemon.EVs.Speed,
            natureMod
        );

        float staged = baseStat * PokemonInstance.GetStageMultiplier(pokemon.StatStages.Speed);

        if (pokemon.CurrentStatus == PrimaryStatus.Paralysis)
            staged *= 0.25f;

        return staged;
    }

    public static bool RollAccuracy(PokemonInstance attacker, PokemonInstance defender, SkillData skill)
    {
        if (skill.BypassesAccuracy || skill.Accuracy == 0)
            return true;

        float accMultiplier = PokemonInstance.GetStageMultiplier(attacker.AccuracyStage);
        float evaMultiplier = PokemonInstance.GetStageMultiplier(defender.EvasionStage);
        float effective = skill.Accuracy * (accMultiplier / evaMultiplier);

        int roll = Random.Range(1, 101);
        return roll <= Mathf.Clamp(effective, 1f, 100f);
    }

    public static bool RollCritical(PokemonInstance attacker, SkillData skill)
    {
        int stage = skill.CritStageBonus;
        float threshold = stage switch
        {
            0 => 1f / 16f,
            1 => 1f / 8f,
            2 => 1f / 4f,
            _ => 1f / 2f
        };

        return Random.value < threshold;
    }

    public static float GetWeatherModifier(ElementType moveType, WeatherType weather)
    {
        if (weather == WeatherType.Sun)
        {
            if (moveType == ElementType.Fire) return 1.5f;
            if (moveType == ElementType.Water) return 0.5f;
        }
        else if (weather == WeatherType.Rain)
        {
            if (moveType == ElementType.Water) return 1.5f;
            if (moveType == ElementType.Fire) return 0.5f;
        }
        return 1f;
    }
}
