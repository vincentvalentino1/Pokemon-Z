#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Disk paths under <c>Assets/</c> for gameplay <see cref="UnityEngine.Resources"/> folders (Pokémon, battle anim, etc.).
/// After migration, assets live under <see cref="MigratedResourcesRoot"/>; before that, under <see cref="LegacyResourcesRoot"/>.
/// Runtime <c>Resources.Load</c> logical paths stay the same either way.
/// </summary>
public static class GameContentAssetPaths
{
    public const string GameContentRoot = "Assets/GameContent";
    public const string MigratedResourcesRoot = GameContentRoot + "/Resources";
    public const string LegacyResourcesRoot = "Assets/Resources";

    public static string PokemonResourcesFolder => FirstExistingSubfolder("Pokemon");
    public static string PokedexResourcesFolder => FirstExistingSubfolder("Pokedex");
    public static string AnimationResourcesFolder => FirstExistingSubfolder("Animation");
    public static string CharacterResourcesFolder => FirstExistingSubfolder("Character");
    public static string SpawnResourcesFolder => FirstExistingSubfolder("Spawn");
    public static string BehaviorResourcesFolder => FirstExistingSubfolder("Behavior");

    public static string ForrestSpawnProfileAssetPath => $"{SpawnResourcesFolder}/ForrestSpawn.asset";

    public static bool HasMigratedPokemonResources =>
        AssetDatabase.IsValidFolder(MigratedResourcesRoot + "/Pokemon");

    static string FirstExistingSubfolder(string name)
    {
        string migrated = MigratedResourcesRoot + "/" + name;
        string legacy = LegacyResourcesRoot + "/" + name;
        if (AssetDatabase.IsValidFolder(migrated))
            return migrated;
        if (AssetDatabase.IsValidFolder(legacy))
            return legacy;
        return legacy;
    }

    /// <summary>Creates <paramref name="assetFolderPath"/> and parents under <c>Assets/</c> if missing.</summary>
    public static void EnsureFolderExists(string assetFolderPath)
    {
        assetFolderPath = assetFolderPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(assetFolderPath))
            return;

        if (assetFolderPath == "Assets")
            return;

        int lastSlash = assetFolderPath.LastIndexOf('/');
        if (lastSlash <= 0)
            return;

        string parent = assetFolderPath[..lastSlash];
        string leaf = assetFolderPath[(lastSlash + 1)..];
        EnsureFolderExists(parent);
        if (!AssetDatabase.IsValidFolder(assetFolderPath))
            AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
