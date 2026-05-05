using UnityEngine;

public readonly struct CatchAttemptResult
{
    public bool Success { get; }
    public int SuccessfulShakes { get; }
    public float CatchChance { get; }

    public CatchAttemptResult(bool success, int successfulShakes, float catchChance)
    {
        Success = success;
        SuccessfulShakes = successfulShakes;
        CatchChance = catchChance;
    }
}

public static class CatchCalculator
{
    public static CatchAttemptResult TryCatch(PokemonInstance target, float ballBonus = 1f)
    {
        if (target == null || target.SpeciesData == null || target.IsFainted || target.MaxHP <= 0)
            return new CatchAttemptResult(false, 0, 0f);

        float maxHP = Mathf.Max(1f, target.MaxHP);
        float currentHP = Mathf.Clamp(target.CurrentHP, 1, target.MaxHP);
        float catchRate = Mathf.Clamp(target.SpeciesData.CatchRate, 1, 255);
        float statusBonus = GetStatusBonus(target.CurrentStatus);

        float a = ((3f * maxHP - 2f * currentHP) * catchRate * Mathf.Max(0.1f, ballBonus) * statusBonus) / (3f * maxHP);
        float catchChance = Mathf.Clamp01(a / 255f);

        if (a >= 255f)
            return new CatchAttemptResult(true, 4, 1f);

        a = Mathf.Max(1f, a);
        float b = 1048560f / Mathf.Sqrt(Mathf.Sqrt(16711680f / a));

        int shakes = 0;
        for (int i = 0; i < 4; i++)
        {
            if (Random.Range(0, 65536) >= b)
                break;

            shakes++;
        }

        return new CatchAttemptResult(shakes == 4, shakes, catchChance);
    }

    static float GetStatusBonus(PrimaryStatus status)
    {
        return status switch
        {
            PrimaryStatus.Sleep => 2f,
            PrimaryStatus.Freeze => 2f,
            PrimaryStatus.Burn => 1.5f,
            PrimaryStatus.Poison => 1.5f,
            PrimaryStatus.BadlyPoisoned => 1.5f,
            PrimaryStatus.Paralysis => 1.5f,
            _ => 1f
        };
    }
}
