# Realistic people

- `<people>_<sex>_<stage>.glb`: bodies baked from MakeHuman's base mesh and targets (the MakeHuman community's
  assets, CC0 1.0: https://github.com/makehumancommunity/makehuman, LICENSE.ASSETS.md) with the Humanizer plugin for
  Godot by NitroxNova (https://github.com/NitroxNova/humanizer, AGPL-3.0). Humanizer is a tool used to make these
  files and isn't part of the game; the baking script is in `tools/humans/`. Each is one people's build (height,
  weight, muscle and a blend of MakeHuman's base faces), a sex, and a stage of life, on Godot's humanoid skeleton.
- `moves.glb`: KayKit's Knight (the Adventurers pack, CC0: see `../kaykit/LICENSE-KayKit-Adventurers.txt`),
  imported with its bones mapped to Godot's humanoid profile (`kaykit_bonemap.tres`), so that its animations play on
  the bodies. Only its animations are used.
- `body_bonemap.tres`: the bodies' bones mapped to the same profile, so their rest pose matches the animations'.
- Each body carries its eyes, eyebrows, eyelashes and hair (MakeHuman's system assets, CC0, from Humanizer's asset
  packs: https://github.com/NitroxNova/humanizer_assets), fitted to its face.
- `tex/`: MakeHuman's system skin textures (an age, a face and a sex each), eye, eyebrow, eyelash and hair textures,
  from the same packs (CC0), at most 2048 wide; the hair, brow and lash textures turned grey so the game can tint
  them. The game picks a skin by age, face and sex and tints it for the people.
