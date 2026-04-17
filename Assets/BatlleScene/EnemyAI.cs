using UnityEngine;
using System.Collections.Generic;

public static class EnemyAI
{
    public static SkillData PickMove(PokemonInstance pokemon)
    {
        List<SkillData> usable = new List<SkillData>();

        if (pokemon.EquippedSkills != null)
        {
            for (int i = 0; i < pokemon.EquippedSkills.Length; i++)
            {
                EquippedSkill slot = pokemon.EquippedSkills[i];
                if (slot != null && slot.IsUsable(pokemon.CurrentTP))
                    usable.Add(slot.Data);
            }
        }

        if (usable.Count > 0)
            return usable[Random.Range(0, usable.Count)];

        return null;
    }
}
