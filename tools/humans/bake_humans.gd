extends Node
# Bakes a set of bodies for the game: a people (its build), a sex and a stage of life each, as .glb files on the
# game's humanoid rig, with eyes, eyebrows, eyelashes and hair fitted to each face; and writes out, once, every
# skin, eye, eyebrow, eyelash and hair texture the game may dress them in (from the MakeHuman system assets, CC0,
# in Humanizer's asset packs). Run in the Humanizer project (it's AGPL: it stays here, only the baked bodies and the
# textures go into the game):
#   xvfb-run godot --path . res://bake_humans.tscn -- [--only=human_male_adult,...] [--textures-only]
#
# Each surface of a baked body is tagged with an empty material named "<equipment>|<texture file>", so the game
# knows what each one is and what to dress it in; the body surface's skin is the game's own choice.

const OUT_DIR = "/home/user/Tristenwold/game/assets/humans/"
const TEX_DIR = OUT_DIR + "tex/"

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

# The hair each body gets (one style a body; the peoples and sexes spread them about), and the eyes and brows.
const HAIR = {
	"female": ["Hair-Bob01", "Hair-Long01", "Hair-Braid01", "Hair-Ponytail01", "Hair-Bob02", "Hair-Long01", "Hair-Braid01"],
	"male": ["Hair-Short01", "Hair-Short02", "Hair-Short03", "Hair-Short04", "Hair-Afro01", "Hair-Short02", "Hair-Short03"],
}
const EYES = ["LeftEye-LowPolyEyeball", "RightEye-LowPolyEyeball"]
# Real clothes on every body (MakeHuman's system clothes, CC0): a shirt, trousers and shoes, which the game tints by
# trade and class. Each is a surface of its own, tagged like the hair, so the game can colour or drop it.
const CLOTHES = [["Shirt-MakeHumanTShirt", null], ["Pants-SuitSlacks", "uncolored"], ["Shoes-01", "brown"]]
const LASHES = ["LeftEyelash", "RightEyelash"]
const BROWS = 12
# The skins the game picks from: an age, a face, a sex.
const SKIN = "^(young|middleage|old)_(african|asian|caucasian)_(female|male)$"

var only: PackedStringArray = []
var textures_only = false
var out_dir = OUT_DIR
var tex_dir = TEX_DIR
var no_clothes = false
var exported := {} # texture resource path -> file written

func _ready():
	await get_tree().process_frame
	await get_tree().process_frame
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--only="): only = a.substr(7).split(",")
		if a == "--textures-only": textures_only = true
		if a.begins_with("--out="): out_dir = a.substr(6); tex_dir = out_dir + "tex/"
		if a == "--no-clothes": no_clothes = true
	DirAccess.make_dir_recursive_absolute(tex_dir)
	export_textures()
	var made = 0
	if not textures_only:
		var i = 0
		for people in PEOPLES:
			for sex in ["female", "male"]:
				for stage in STAGES:
					var name = "%s_%s_%s" % [people, sex, stage]
					i += 1
					if only.size() > 0 and name not in only: continue
					await bake(name, people, sex, stage, i)
					made += 1
	print("baked ", made, " bodies into ", out_dir, ", ", exported.size(), " textures into ", tex_dir)
	get_tree().quit()

# Every texture the game may dress a body in, once: the skins (an age, a face and a sex each), eyes, brows,
# lashes and hair. Hair and brows are written as they are; the game tints them.
func export_textures():
	var types: Array = ["Body-Default"] + EYES + LASHES
	for i in range(1, BROWS + 1): types += ["LeftEyebrow-%03d" % i, "RightEyebrow-%03d" % i]
	# (hair textures are written as the bodies take them: one a style, the grey one where there is one)
	var skin = RegEx.create_from_string(SKIN)
	# The iris is an overlay on the eyeball, not a material: written as it is (the game colours it and lays it on).
	var iris = load("res://humanizer/material/LeftEye-LowPolyEyeball/overlay_iris_albedo.image.res")
	if iris is Texture2D and iris.get_image() != null:
		var img: Image = iris.get_image().duplicate()
		if img.is_compressed(): img.decompress()
		img.save_png(tex_dir + "lefteye-lowpolyeyeball__iris.png")
		print("  texture lefteye-lowpolyeyeball__iris.png ", img.get_width(), "x", img.get_height(), " ", img.get_format())
	for t in types:
		var et = HumanizerRegistry.equipment.get(t)
		if et == null: push_warning("no equipment " + t); continue
		for tex_name in et.textures:
			if t == "Body-Default" and skin.search(tex_name) == null: continue # overlays (a black eye, a tattoo), not skins
			save_texture(t, tex_name, et.textures[tex_name])

# Writes a material's albedo image for the game (jpg, or png where there's alpha), at most 2048 wide; the file name.
func save_texture(type: String, tex_name: String, material) -> String:
	if material == null or not (material is StandardMaterial3D): return ""
	var tex: Texture2D = material.albedo_texture
	if tex == null: return ""
	var key = tex.resource_path if tex.resource_path != "" else type + "/" + tex_name
	if key in exported: return exported[key]
	var img: Image = tex.get_image()
	if img == null: return ""
	img = img.duplicate()
	if img.is_compressed(): img.decompress()
	var alpha = img.detect_alpha() != Image.ALPHA_NONE
	if img.get_width() > 2048: img.resize(2048, int(2048.0 * img.get_height() / img.get_width()), Image.INTERPOLATE_LANCZOS)
	var file = "%s__%s.%s" % [type.to_lower(), tex_name.to_lower(), "png" if alpha else "jpg"]
	if alpha: img.save_png(tex_dir + file)
	else: img.save_jpg(tex_dir + file, 0.9)
	exported[key] = file
	print("  texture ", file, " ", img.get_width(), "x", img.get_height())
	return file

# A hair style's texture: the grey one if it has one (the game tints them all; grey tints truest), else its first.
func hair_texture(style: String):
	var et = HumanizerRegistry.equipment.get(style)
	if et == null: return null
	for t in et.textures: if "gray" in t or "platinum" in t: return t
	return null

func bake(name: String, people: String, sex: String, stage: String, i: int):
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
	for e in EYES: config.add_equipment(HumanizerEquipment.new(e))
	var brow = "%03d" % (1 + (i * 7) % BROWS)
	config.add_equipment(HumanizerEquipment.new("LeftEyebrow-" + brow))
	config.add_equipment(HumanizerEquipment.new("RightEyebrow-" + brow))
	if sex == "female": for e in LASHES: config.add_equipment(HumanizerEquipment.new(e))
	var hair = HAIR[sex][(i * 3 + (0 if stage != "elder" else 1)) % HAIR[sex].size()]
	config.add_equipment(HumanizerEquipment.new(hair, hair_texture(hair)))
	if not no_clothes:
		for c in CLOTHES:
			if HumanizerRegistry.equipment.get(c[0]) == null: push_warning("no clothes " + c[0]); continue
			config.add_equipment(HumanizerEquipment.new(c[0], c[1]))
	if config.has_method("init_macros"): config.init_macros()
	var hz = Humanizer.new()
	await hz.load_config_async(config)
	# The materials come off a worker; give them a moment.
	for n in range(600):
		if hz.check_materials_done_generating(): break
		await get_tree().process_frame
	var root = Node3D.new()
	root.name = name
	add_child(root)
	var skeleton: Skeleton3D = hz.get_skeleton()
	skeleton.name = "GeneralSkeleton"
	root.add_child(skeleton)
	skeleton.owner = root
	var mi = MeshInstance3D.new()
	mi.name = "Body"
	var mesh: ArrayMesh = hz.get_combined_meshes()
	var parts = []
	for s in range(mesh.get_surface_count()):
		var equip = mesh.surface_get_name(s)
		var tex_name = config.equipment[equip].texture_name if equip in config.equipment else ""
		var file = "" if equip == "Body-Default" else save_texture(equip, tex_name, mesh.surface_get_material(s))
		var tag = StandardMaterial3D.new()
		tag.resource_name = equip + "|" + file
		mesh.surface_set_material(s, tag)
		parts.append("%s(%d)" % [equip, mesh.surface_get_array_len(s)])
	mi.mesh = mesh
	root.add_child(mi)
	mi.owner = root
	mi.skeleton = NodePath("../GeneralSkeleton")
	mi.skin = skeleton.create_skin_from_rest_transforms()
	var doc = GLTFDocument.new()
	var st = GLTFState.new()
	doc.append_from_scene(root, st)
	var err = doc.write_to_filesystem(st, out_dir + name + ".glb")
	var box = mi.get_aabb()
	print(name, ": ", err, " ", "%.2f m tall" % box.size.y, ", ", ", ".join(parts))
	root.queue_free()
