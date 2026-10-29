# Baking the realistic people

The bodies in `game/assets/humans/` come from MakeHuman (CC0) through the Humanizer plugin for Godot 4 (AGPL-3.0,
https://github.com/NitroxNova/humanizer). Humanizer stays outside the game, as a tool: it bakes the bodies, and
the game ships only the baked `.glb` files.

To bake them again (after changing a people's build in `bake_humans.gd`, or adding a stage of life):

1. Clone Humanizer next to this repository (`git clone --depth 1 https://github.com/NitroxNova/humanizer`), and its
   asset packs (`git clone --depth 1 https://github.com/NitroxNova/humanizer_assets`: MakeHuman's system skins, eyes,
   eyebrows, eyelashes and hair, CC0) into `humanizer/addons/humanizer_assets/` (the four zips). Open the project
   once in Godot 4.4 or later (or run `godot --headless --path humanizer --import`) so its assets are imported.
2. Copy `bake_humans.gd` into the Humanizer project's root, with a scene `bake_humans.tscn` that has one `Node`
   carrying that script. Point `OUT_DIR` at this repository's `game/assets/humans/`.
3. Run it, with a display (it renders nothing, but Humanizer wants one):
   `xvfb-run godot --path humanizer res://bake_humans.tscn` (or just run the scene in the editor).
   `-- --only=human_male_adult,elf_female_elder` bakes only those.
4. `python3 tools/humans/finish_textures.py` turns the hair, brow and lash textures grey, so the game can tint them.
5. In the game folder, `godot --headless --import`, then copy the `_subresources` block from any existing
   `*.glb.import` in `game/assets/humans/` into each new one and import again: it maps the body's bones to Godot's
   humanoid profile so its rest pose matches the animations'.

The builds and ages: see `PEOPLES` and `STAGES` in the script; the hair each body gets, `HAIR`. Each body is one
mesh with a surface for the body and one for each part fitted to its face (eyes, brows, lashes, hair), tagged with
an empty material named `<equipment>|<texture file>`. The script also writes every texture the game may use, once,
into `game/assets/humans/tex/` (`--textures-only` does just that). The game dresses the bodies at load
(`Humans.cs`): a skin texture by age, face and sex, tinted for the people, the clothes painted over it, and the
parts tinted with the hair colour.
