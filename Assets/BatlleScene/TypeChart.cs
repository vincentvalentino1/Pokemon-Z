using UnityEngine;
using System;

public static class TypeChart
{
    static readonly float[,] Chart;

    static TypeChart()
    {
        int count = Enum.GetValues(typeof(ElementType)).Length;
        Chart = new float[count, count];

        for (int i = 0; i < count; i++)
            for (int j = 0; j < count; j++)
                Chart[i, j] = 1f;

        void Set(ElementType atk, ElementType def, float val) =>
            Chart[(int)atk, (int)def] = val;

        Set(ElementType.Normal, ElementType.Rock, 0.5f);
        Set(ElementType.Normal, ElementType.Steel, 0.5f);
        Set(ElementType.Normal, ElementType.Ghost, 0f);

        Set(ElementType.Fire, ElementType.Fire, 0.5f);
        Set(ElementType.Fire, ElementType.Water, 0.5f);
        Set(ElementType.Fire, ElementType.Grass, 2f);
        Set(ElementType.Fire, ElementType.Ice, 2f);
        Set(ElementType.Fire, ElementType.Bug, 2f);
        Set(ElementType.Fire, ElementType.Rock, 0.5f);
        Set(ElementType.Fire, ElementType.Dragon, 0.5f);
        Set(ElementType.Fire, ElementType.Steel, 2f);

        Set(ElementType.Water, ElementType.Fire, 2f);
        Set(ElementType.Water, ElementType.Water, 0.5f);
        Set(ElementType.Water, ElementType.Grass, 0.5f);
        Set(ElementType.Water, ElementType.Ground, 2f);
        Set(ElementType.Water, ElementType.Rock, 2f);
        Set(ElementType.Water, ElementType.Dragon, 0.5f);

        Set(ElementType.Grass, ElementType.Fire, 0.5f);
        Set(ElementType.Grass, ElementType.Water, 2f);
        Set(ElementType.Grass, ElementType.Grass, 0.5f);
        Set(ElementType.Grass, ElementType.Poison, 0.5f);
        Set(ElementType.Grass, ElementType.Ground, 2f);
        Set(ElementType.Grass, ElementType.Flying, 0.5f);
        Set(ElementType.Grass, ElementType.Bug, 0.5f);
        Set(ElementType.Grass, ElementType.Rock, 2f);
        Set(ElementType.Grass, ElementType.Dragon, 0.5f);
        Set(ElementType.Grass, ElementType.Steel, 0.5f);

        Set(ElementType.Electric, ElementType.Water, 2f);
        Set(ElementType.Electric, ElementType.Grass, 0.5f);
        Set(ElementType.Electric, ElementType.Electric, 0.5f);
        Set(ElementType.Electric, ElementType.Ground, 0f);
        Set(ElementType.Electric, ElementType.Flying, 2f);
        Set(ElementType.Electric, ElementType.Dragon, 0.5f);

        Set(ElementType.Ice, ElementType.Fire, 0.5f);
        Set(ElementType.Ice, ElementType.Water, 0.5f);
        Set(ElementType.Ice, ElementType.Grass, 2f);
        Set(ElementType.Ice, ElementType.Ice, 0.5f);
        Set(ElementType.Ice, ElementType.Ground, 2f);
        Set(ElementType.Ice, ElementType.Flying, 2f);
        Set(ElementType.Ice, ElementType.Dragon, 2f);
        Set(ElementType.Ice, ElementType.Steel, 0.5f);

        Set(ElementType.Fighting, ElementType.Normal, 2f);
        Set(ElementType.Fighting, ElementType.Ice, 2f);
        Set(ElementType.Fighting, ElementType.Poison, 0.5f);
        Set(ElementType.Fighting, ElementType.Flying, 0.5f);
        Set(ElementType.Fighting, ElementType.Psychic, 0.5f);
        Set(ElementType.Fighting, ElementType.Bug, 0.5f);
        Set(ElementType.Fighting, ElementType.Rock, 2f);
        Set(ElementType.Fighting, ElementType.Ghost, 0f);
        Set(ElementType.Fighting, ElementType.Dark, 2f);
        Set(ElementType.Fighting, ElementType.Steel, 2f);
        Set(ElementType.Fighting, ElementType.Fairy, 0.5f);

        Set(ElementType.Poison, ElementType.Grass, 2f);
        Set(ElementType.Poison, ElementType.Poison, 0.5f);
        Set(ElementType.Poison, ElementType.Ground, 0.5f);
        Set(ElementType.Poison, ElementType.Rock, 0.5f);
        Set(ElementType.Poison, ElementType.Ghost, 0.5f);
        Set(ElementType.Poison, ElementType.Steel, 0f);
        Set(ElementType.Poison, ElementType.Fairy, 2f);

        Set(ElementType.Ground, ElementType.Fire, 2f);
        Set(ElementType.Ground, ElementType.Grass, 0.5f);
        Set(ElementType.Ground, ElementType.Electric, 2f);
        Set(ElementType.Ground, ElementType.Poison, 2f);
        Set(ElementType.Ground, ElementType.Flying, 0f);
        Set(ElementType.Ground, ElementType.Bug, 0.5f);
        Set(ElementType.Ground, ElementType.Rock, 2f);
        Set(ElementType.Ground, ElementType.Steel, 2f);

        Set(ElementType.Flying, ElementType.Grass, 2f);
        Set(ElementType.Flying, ElementType.Electric, 0.5f);
        Set(ElementType.Flying, ElementType.Fighting, 2f);
        Set(ElementType.Flying, ElementType.Bug, 2f);
        Set(ElementType.Flying, ElementType.Rock, 0.5f);
        Set(ElementType.Flying, ElementType.Steel, 0.5f);

        Set(ElementType.Psychic, ElementType.Fighting, 2f);
        Set(ElementType.Psychic, ElementType.Poison, 2f);
        Set(ElementType.Psychic, ElementType.Psychic, 0.5f);
        Set(ElementType.Psychic, ElementType.Dark, 0f);
        Set(ElementType.Psychic, ElementType.Steel, 0.5f);

        Set(ElementType.Bug, ElementType.Fire, 0.5f);
        Set(ElementType.Bug, ElementType.Grass, 2f);
        Set(ElementType.Bug, ElementType.Fighting, 0.5f);
        Set(ElementType.Bug, ElementType.Poison, 0.5f);
        Set(ElementType.Bug, ElementType.Flying, 0.5f);
        Set(ElementType.Bug, ElementType.Psychic, 2f);
        Set(ElementType.Bug, ElementType.Ghost, 0.5f);
        Set(ElementType.Bug, ElementType.Dark, 2f);
        Set(ElementType.Bug, ElementType.Steel, 0.5f);
        Set(ElementType.Bug, ElementType.Fairy, 0.5f);

        Set(ElementType.Rock, ElementType.Fire, 2f);
        Set(ElementType.Rock, ElementType.Ice, 2f);
        Set(ElementType.Rock, ElementType.Fighting, 0.5f);
        Set(ElementType.Rock, ElementType.Ground, 0.5f);
        Set(ElementType.Rock, ElementType.Flying, 2f);
        Set(ElementType.Rock, ElementType.Bug, 2f);
        Set(ElementType.Rock, ElementType.Steel, 0.5f);

        Set(ElementType.Ghost, ElementType.Normal, 0f);
        Set(ElementType.Ghost, ElementType.Psychic, 2f);
        Set(ElementType.Ghost, ElementType.Ghost, 2f);
        Set(ElementType.Ghost, ElementType.Dark, 0.5f);

        Set(ElementType.Dragon, ElementType.Dragon, 2f);
        Set(ElementType.Dragon, ElementType.Steel, 0.5f);
        Set(ElementType.Dragon, ElementType.Fairy, 0f);

        Set(ElementType.Dark, ElementType.Fighting, 0.5f);
        Set(ElementType.Dark, ElementType.Psychic, 2f);
        Set(ElementType.Dark, ElementType.Ghost, 2f);
        Set(ElementType.Dark, ElementType.Dark, 0.5f);
        Set(ElementType.Dark, ElementType.Fairy, 0.5f);

        Set(ElementType.Steel, ElementType.Fire, 0.5f);
        Set(ElementType.Steel, ElementType.Water, 0.5f);
        Set(ElementType.Steel, ElementType.Electric, 0.5f);
        Set(ElementType.Steel, ElementType.Ice, 2f);
        Set(ElementType.Steel, ElementType.Rock, 2f);
        Set(ElementType.Steel, ElementType.Steel, 0.5f);
        Set(ElementType.Steel, ElementType.Fairy, 2f);

        Set(ElementType.Fairy, ElementType.Fire, 0.5f);
        Set(ElementType.Fairy, ElementType.Poison, 0.5f);
        Set(ElementType.Fairy, ElementType.Fighting, 2f);
        Set(ElementType.Fairy, ElementType.Dragon, 2f);
        Set(ElementType.Fairy, ElementType.Dark, 2f);
        Set(ElementType.Fairy, ElementType.Steel, 0.5f);
    }

    public static float GetEffectiveness(ElementType attackType, ElementType defenseType)
    {
        return Chart[(int)attackType, (int)defenseType];
    }

    public static float GetDualEffectiveness(ElementType attackType, ElementType primaryDef, ElementType secondaryDef)
    {
        float eff = GetEffectiveness(attackType, primaryDef);
        if (primaryDef != secondaryDef)
            eff *= GetEffectiveness(attackType, secondaryDef);
        return eff;
    }
}
