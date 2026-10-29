extends Node
# Bakes a set of bodies for the game: a people (its build), a sex and a stage of life each, as .glb files on the
# game's humanoid rig. Run in the Humanizer project (it's AGPL: it stays here, only the baked bodies go into the game):
#   xvfb-run godot --path . res://bake_humans.tscn

const OUT_DIR = "/home/user/Tristenwold/game/assets/humans/"

# The peoples' builds, as MakeHuman macros (0 to 1): height, weight, muscle, and how much of each of MakeHuman's three
# base faces goes in (every people is a blend; the game's races aren't earthly ones).
const PEOPLES = {
	"human": {"height": 0.5, "weight": 0.5, "muscle": 0.5, "african": 0.3, "asian": 0.3, "caucasian": 0.4},
	"elf": {"height": 0.75, "weight": 0.3, "muscle": 0.45, "african": 0.15, "asian": 0.55, "caucasian": 0.3},
	"dwarf": {"height": 0.05, "weight": 0.8, "muscle": 0.85, "african": 0.2, "asian": 0.2, "caucasian": 0.6},
	"orc": {"height": 0.7, "weight": 0.7, "muscle": 1.0, "african": 0.5, "asian": 0.3, "caucasian": 0.2},
	"goblin": {"height": 0.0, "weight": 0.2, "muscle": 0.4, "african": 0.3, "asian": 0.5, "caucasian": 0.2},
	"half_giant": {"height": 1.0, "weight": 0.75, "muscle": 0.9, "african": 0.4, "asian": 0.2, "caucasian": 0.4},
	"dragonkind": {"height": 0.8, "weight": 0.45, "muscle": 0.7, "african": 0.35, "asian": 0.35, "caucasian": 0.3},
}
# The stages of life as MakeHuman's age (0 baby, 0.12 child, 0.25 young, 1 old).
const STAGES = {"young": 0.14, "adolescent": 0.22, "adult": 0.4, "elder": 0.95} # seasoned wears an adult's body

var only: PackedStringArray = []

func _ready():
	await get_tree().process_frame
	await get_tree().process_frame
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--only="): only = a.substr(7).split(",")
	DirAccess.make_dir_recursive_absolute(OUT_DIR)
	var made = 0
	for people in PEOPLES:
		for sex in ["female", "male"]:
			for stage in STAGES:
				var name = "%s_%s_%s" % [people, sex, stage]
				if only.size() > 0 and name not in only: continue
				await bake(name, people, sex, stage)
				made += 1
	print("baked ", made, " bodies into ", OUT_DIR)
	get_tree().quit()

func bake(name: String, people: String, sex: String, stage: String):
	var macros = HumanizerMacroService.get_default_macros()
	var build = PEOPLES[people]
	for k in build: macros[k] = build[k]
	macros["gender"] = 1.0 if sex == "male" else 0.0
	macros["age"] = STAGES[stage]
	macros["proportions"] = 0.5
	var config = HumanConfig.new()
	config.set_targets(macros)
	config.rig = "game_engine-RETARGETED"
	config.add_equipment(HumanizerEquipment.new("Body-Default"))
	if config.has_method("init_macros"): config.init_macros()
	var hz = Humanizer.new()
	await hz.load_config_async(config)
	var root = Node3D.new()
	root.name = name
	add_child(root)
	var skeleton: Skeleton3D = hz.get_skeleton()
	skeleton.name = "GeneralSkeleton"
	root.add_child(skeleton)
	skeleton.owner = root
	var mi = MeshInstance3D.new()
	mi.name = "Body"
	mi.mesh = hz.get_combined_meshes()
	mi.mesh.surface_set_material(0, null) # the game paints it
	root.add_child(mi)
	mi.owner = root
	mi.skeleton = NodePath("../GeneralSkeleton")
	mi.skin = skeleton.create_skin_from_rest_transforms()
	var doc = GLTFDocument.new()
	var st = GLTFState.new()
	doc.append_from_scene(root, st)
	var err = doc.write_to_filesystem(st, OUT_DIR + name + ".glb")
	var box = mi.get_aabb()
	print(name, ": ", err, " ", "%.2f m tall" % box.size.y, ", ", mi.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX].size(), " vertices")
	root.queue_free()
