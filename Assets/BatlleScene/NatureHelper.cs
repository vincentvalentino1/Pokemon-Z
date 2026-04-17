public static class NatureHelper
{
    public static float GetNatureModifier(PokemonNature nature, TargetStat stat)
    {
        int index = (int)nature;
        int boosted = index / 5;
        int hindered = index % 5;

        if (boosted == hindered) return 1f;

        int statIndex = stat switch
        {
            TargetStat.Attack    => 0,
            TargetStat.Defense   => 1,
            TargetStat.Speed     => 2,
            TargetStat.SpAttack  => 3,
            TargetStat.SpDefense => 4,
            _ => -1
        };

        if (statIndex < 0) return 1f;
        if (statIndex == boosted) return 1.1f;
        if (statIndex == hindered) return 0.9f;
        return 1f;
    }
}
