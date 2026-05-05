public enum BattleState
{
    BattleStart,
    PlayerTurn,
    EnemyTurn,
    ResolveTurn,
    TurnEnd,
    BattleOver
}

public enum BattleResolution
{
    None,
    PlayerWon,
    PlayerLost,
    Captured,
    Escaped
}
