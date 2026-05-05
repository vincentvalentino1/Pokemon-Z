using UnityEngine;
using UnityEngine.SceneManagement;

public class OpenWorldDebugUI : MonoBehaviour
{
    [Header("Debug UI")]
    public bool ShowAtStart = true;
    public KeyCode ToggleKey = KeyCode.F3;
    public Rect WindowRect = new Rect(16f, 16f, 360f, 240f);

    bool _visible;

    void Awake()
    {
        _visible = ShowAtStart;
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
            _visible = !_visible;
    }

    void OnGUI()
    {
        if (!_visible)
            return;

        WindowRect = GUI.Window(9917, WindowRect, DrawWindow, "Open World Debug");
    }

    void DrawWindow(int id)
    {
        OpenWorldEncounterManager manager = OpenWorldEncounterManager.Instance;
        string activeScene = SceneManager.GetActiveScene().name;

        GUILayout.Label($"Scene: {activeScene}");
        GUILayout.Space(4f);

        if (manager == null)
        {
            GUILayout.Label("Manager: NOT FOUND");
            GUILayout.Label("Add OpenWorldEncounterManager in world scene.");
            GUILayout.Space(6f);
            GUILayout.Label($"Toggle UI: {ToggleKey}");
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            return;
        }

        GUILayout.Label("Manager: OK");
        GUILayout.Label($"Pending Encounter: {manager.HasPendingEncounter}");
        GUILayout.Label($"Party Count: {manager.PartyCount}");
        GUILayout.Label($"Storage Count: {manager.StorageCount}");
        GUILayout.Space(4f);

        string leadName = GetPokemonName(manager.PlayerLeadPokemon);
        GUILayout.Label($"Lead Pokemon: {leadName}");

        string encounterName = manager.CurrentEncounter != null
            ? GetPokemonName(manager.CurrentEncounter.WildPokemon)
            : "None";
        GUILayout.Label($"Current Wild: {encounterName}");

        GUILayout.Space(6f);
        GUILayout.Label("Test controls:");
        GUILayout.Label("- Walk into a wild Pokemon's engage radius to start battle");
        GUILayout.Label("- Aggressive Pokemon should notice/chase before engaging");
        GUILayout.Label("- Finish the battle to return here and respawn a new wild Pokemon");
        GUILayout.Label($"- Toggle this panel: {ToggleKey}");

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    static string GetPokemonName(PokemonInstance pokemon)
    {
        if (pokemon == null)
            return "None";

        if (!string.IsNullOrWhiteSpace(pokemon.Nickname))
            return pokemon.Nickname;

        if (pokemon.SpeciesData != null && !string.IsNullOrWhiteSpace(pokemon.SpeciesData.PokemonName))
            return pokemon.SpeciesData.PokemonName;

        return "Unknown";
    }
}
