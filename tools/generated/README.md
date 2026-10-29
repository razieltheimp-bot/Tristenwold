# Generated models: making them, and bringing them into the game

The game loads ordinary glTF (.glb) files: a prop is a static mesh, a creature is a rigged mesh with clips named
Idle, Walk, Run, Attack and Death. Anything that writes one can feed the game, and the generative tools do. This is
the pipeline: what to make, with what, and how `import_model.py` turns the result into a file the game uses.

## What the tools are, and what they need

| Tool | Takes | Gives | Needs | Licence |
|---|---|---|---|---|
| **Hunyuan3D-2 / 2.1** (Tencent) | text, or one image | a textured mesh | an NVIDIA GPU: about 6 GB of VRAM for the shape alone, 12 to 24 GB with textures (2.1: 10 / 21 / 29 GB); a low-VRAM fork runs the shape on 6 GB | Tencent's own community licence: fine for a game, but it excludes the EU, UK and South Korea, and some files still carry an older non-commercial header |
| **TRELLIS** (Microsoft) and **UniForge3D** (built on it; a few photos of one thing, no poses needed) | an image, or several photos | a textured mesh | an NVIDIA GPU with 16 GB | MIT (TRELLIS); check UniForge3D's own |
| **CharacterGen** (SIGGRAPH 2024) | one drawing of a character | a clean mesh in a standing pose, ready to rig | an NVIDIA GPU | Apache 2.0 on its Hugging Face space |
| **Mixamo** (Adobe, free with an account, in the browser) | a character mesh | the mesh rigged, and an animation per download | nothing: it's a website | free to use in games |
| **Real3D-Portrait**, **Audio2Face** | a face and a voice | the face talking | a GPU; Audio2Face is NVIDIA's and goes into Unreal | not for Godot: our faces aren't rigged for it |

None of them run where this game is built (no GPU there, and Hugging Face, where their weights live, isn't
reachable). They run on your developer friend's machine, or any rented GPU for an hour. The output comes back as
a file, and the file goes through the importer here.

## The pipeline

1. **Make the mesh.** A monster, a horse, a chest, a tree: Hunyuan3D from a prompt ("a hunched swamp troll with
   moss on its back, game asset, T-pose"), or TRELLIS from concept art. A person or a humanoid monster that has to
   walk: CharacterGen from a front-view drawing, because it gives a standing pose that rigs cleanly.
2. **Rig and animate it (creatures only).** Upload the mesh to Mixamo, place the markers, and download:
   the character *with skin* in a T-pose (the mesh and the rig), and then, *without skin*, one FBX per movement:
   an idle, a walk, a run, an attack and a death (Mixamo has hundreds; pick what suits).
3. **Import it.** With Blender 5.2 as a Python module (`pip install bpy==5.2.3`), from the repository root:

   ```
   python tools/generated/import_model.py prop chest.glb --name iron_chest --height 0.7
   python tools/generated/import_model.py creature troll_tpose.fbx --name swamp_troll --height 2.6 \
       --clip Idle=troll_idle.fbx --clip Walk=troll_walk.fbx --clip Run=troll_run.fbx \
       --clip Attack=troll_swing.fbx --clip Death=troll_dying.fbx
   ```

   It puts the feet on the ground, makes it the height you say, turns it to face the game's way, cuts it down to a
   triangle budget (generated meshes are 100,000 triangles and up; `--tris` to change the budget), shrinks the
   textures to 2048 a side, names the clips, borrows a clip that's missing (Run from Walk, Attack and Death from
   Idle) and writes `game/assets/props/generated/iron_chest.glb` or `game/assets/creatures/swamp_troll.glb`.
4. **Let Godot take it in:** in the `game` folder, `godot --headless --import` (or open the project once).
5. **Use it.** A prop: `Models.Place(this, "res://assets/props/generated/iron_chest.glb", at, 1)`. A creature:
   a body in `Wildfolk.cs` (`Creature.Create("swamp_troll.glb", 2.6f)`, the way the imp is made), an entry in the
   bestiary and a look; or send the file over and that's done here.

Write what you made and under what licence into `game/assets/creatures/CREDITS.md` (or the props' CREDITS), the
way the rest are.

## What's worth generating first

The game's people are KayKit's (chibi, CC0) and their animations are KayKit's rig; the creatures are a mix of
packs. Replacing the people is a different job from adding creatures: every class, NPC look and piece of equipment
is built on that one rig, so a realistic human needs a new rig with the same clip set and the equipment refitted,
which is weeks, not an afternoon. Creatures and props drop straight in, so start there:

- **Monsters the packs don't have:** a wyvern (smaller than the dragons, for the crags), a marsh troll, a
  harbour kraken's tentacles for a sea event, a giant boar for the pine woods, a barrow wight.
- **Pirates' and raiders' gear:** a grapnel and rope, a pirate's chest, a clan standard, a hide tent, a war drum.
- **Ships:** a pirate sloop with a black sail (the Widow's Tooth), to stand in for the trader the pirates sail now.
- **Town dressing:** a shipwright's adze and sawhorse, nets drying, lobster pots, a tar barrel, a ship's figurehead.
- **Lifelike faces:** not through these tools. A realistic hero would mean swapping the whole character set for a
  realistic rigged one (Quaternius's Universal Base Characters are CC0 and rigged) and refitting the gear; ask
  for that as its own piece of work.
