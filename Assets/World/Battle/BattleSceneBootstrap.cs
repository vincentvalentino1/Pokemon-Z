using UnityEngine;

public class BattleSceneBootstrap : MonoBehaviour
{
    public BattleSimple BattleController;

    void Awake()
    {
        if (BattleController == null)
            BattleController = FindObjectOfType<BattleSimple>();
    }

    void Start()
    {
        if (OpenWorldEncounterManager.Instance == null || BattleController == null)
            return;

        OpenWorldEncounterManager.EncounterPayload payload = OpenWorldEncounterManager.Instance.CurrentEncounter;
        if (payload == null || payload.WildPokemon == null)
            return;

        BattleController.SetCombatants(
            OpenWorldEncounterManager.Instance.PlayerLeadPokemon,
            payload.WildPokemon
        );

        BattleController.InitializeBattle();
        BattleEvents.OnBattleEnd -= HandleBattleEnd;
        BattleEvents.OnBattleEnd += HandleBattleEnd;
    }

    void OnDisable()
    {
        BattleEvents.OnBattleEnd -= HandleBattleEnd;
    }

    void HandleBattleEnd(BattleResolution resolution)
    {
        BattleEvents.OnBattleEnd -= HandleBattleEnd;

        if (OpenWorldEncounterManager.Instance != null)
            OpenWorldEncounterManager.Instance.CompleteEncounter(resolution);
    }
}
