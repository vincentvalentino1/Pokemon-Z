using System;
using UnityEngine;

[Serializable]
public class PokedexSpeciesEntry
{
    public PokemonData Species;
    [Tooltip("Optional texture path under Resources without file extension.")]
    public string PortraitResourcePath;
}

[CreateAssetMenu(fileName = "PokedexDatabase", menuName = "Game Content/Pokedex/Database")]
public class PokedexDatabase : ScriptableObject
{
    public PokedexSpeciesEntry[] SpeciesEntries;
}
