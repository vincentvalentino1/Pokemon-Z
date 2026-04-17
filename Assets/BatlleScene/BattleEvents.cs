using System;

public static class BattleEvents
{
    public static Action<string> OnMoveUsed;
    public static Action<PokemonInstance, int, bool> OnDamageDealt;
    public static Action<PokemonInstance> OnFainted;
    public static Action<float> OnSuperEffective;
    public static Action<bool> OnCriticalHit;
    public static Action<string> OnStatusInflicted;
    public static Action<string> OnMessageShown;
    public static Action OnBattleStart;
    public static Action<bool> OnBattleEnd;
    public static Action<WeatherType> OnWeatherChanged;

    public static void Clear()
    {
        OnMoveUsed = null;
        OnDamageDealt = null;
        OnFainted = null;
        OnSuperEffective = null;
        OnCriticalHit = null;
        OnStatusInflicted = null;
        OnMessageShown = null;
        OnBattleStart = null;
        OnBattleEnd = null;
        OnWeatherChanged = null;
    }
}
