# Baking the realistic people

The bodies in `game/assets/humans/` come from MakeHuman (CC0) through the Humanizer plugin for Godot 4 (AGPL-3.0,
https://github.com/NitroxNova/humanizer). Humanizer stays outside the game, as a tool: it bakes the bodies, and
the game ships only the baked `.glb` files.

To bake them again (after changing a people's build in `bake_humans.gd`, or adding a stage of life):

1. Clone Humanizer next to this repository (`git clone --depth 1 https://github.com/NitroxNova/humanizer`) and open
   it once in Godot 4.4 or later (or run `godot --headless --path humanizer --import`) so its assets are imported.
2. Copy `bake_humans.gd` into the Humanizer project's root, with a scene `bake_humans.tscn` that has one `Node`
   carrying that script. Point `OUT_DIR` at this repository's `game/assets/humans/`.
3. Run it, with a display (it renders nothing, but Humanizer wants one):
   `xvfb-run godot --path humanizer res://bake_humans.tscn` (or just run the scene in the editor).
   `-- --only=human_male_adult,elf_female_elder` bakes only those.
4. In the game folder, `godot --headless --import`, then copy the `_subresources` block from any existing
   `*.glb.import` in `game/assets/humans/` into each new one and import again: it maps the body's bones to Godot's
   humanoid profile so its rest pose matches the animations'.

The builds and ages: see `PEOPLES` and `STAGES` in the script. The game paints the clothes, hair and skin on at load
(`Humans.cs`), so the baked bodies carry no material.
