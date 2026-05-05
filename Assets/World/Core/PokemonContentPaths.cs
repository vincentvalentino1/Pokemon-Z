/// <summary>
/// Logical paths for <see cref="UnityEngine.Resources"/> loads (unchanged after moving assets
/// to <c>Assets/GameContent/Resources/</c>). Authoring-only folders live under <see cref="AuthoringRootLabel"/>.
/// Run <b>Tools → Game Content → Migrate Resources Into GameContent…</b> when your team is ready to relocate runtime assets.
/// </summary>
public static class PokemonContentPaths
{
    public const string AuthoringRootLabel = "Assets/GameContent";

    /// <summary>Optional physical home for species ScriptableObjects (author here over time).</summary>
    public const string AuthoringPokemonData = AuthoringRootLabel + "/PokemonData";
    public const string AuthoringPokemonAssets = AuthoringRootLabel + "/PokemonAssets";
    public const string AuthoringSkills = AuthoringRootLabel + "/Skills";
    public const string AuthoringBehaviour = AuthoringRootLabel + "/Behaviour";
    public const string AuthoringRouteSpawn = AuthoringRootLabel + "/RouteSpawn";
    public const string AuthoringPokedex = AuthoringRootLabel + "/Pokedex";

    /// <summary>Resources.Load path (no extension) for the main Pokédex <see cref="PokedexDatabase"/>.</summary>
    public const string ResourcesPokedexDatabase = "Pokedex/PokedexDatabase";

    /// <summary>Resources folder for species assets and prefabs (current bulk content).</summary>
    public const string ResourcesPokemonRoot = "Pokemon";
}
