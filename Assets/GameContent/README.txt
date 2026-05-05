Game Content — folder guide
============================

Authoring (ScriptableObjects you create in the editor)
------------------------------------------------------
  PokemonData/      Species templates (optional future home; can mirror Resources layout)
  PokemonAssets/    Art/meshes not loaded via Resources (optional)
  Skills/           Move / SkillData assets
  Behaviour/        World AI profiles (can also live under Resources/Behavior until migrated)
  RouteSpawn/       WorldSpawnProfile assets (can live under Resources/Spawn until migrated)
  Pokedex/          PokedexDatabase and related

Runtime Resources (loads via Resources.Load paths like "Pokemon/Name")
------------------------------------------------------------------------
  Before migration: Assets/Resources/<Pokemon|Pokedex|Animation|...>
  After migration:  Assets/GameContent/Resources/<same folder names>

  Logical paths do NOT change when you migrate — only the physical folder moves.

Collaboration
-------------
  • Teammates can keep adding assets under Assets/Resources/... until you run the migrator.
  • Editor tools pick GameContent/Resources first when that folder exists, otherwise Assets/Resources.

Migrate when ready
------------------
  Unity menu: Tools → Game Content → Migrate Resources Into GameContent…
  Commit or back up first. After migrating, push; others pull the new paths.

Battle code
-----------
  Battle scripts stay under Assets/BattleScene/ (separate from Game Content layout).
