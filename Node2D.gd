extends Node2D



export (Vector2) var scroll_speed = Vector2(100, 100)  # pixels per second

onready var bg = $Background
onready var mat = bg.material as ShaderMaterial
onready var tex = bg.texture

func _process(delta):
	# get current offset
	var ofs = mat.get_shader_param("uv_offset")
	# convert pixel speed → UV units (px/sec ÷ texture_size)
	var tile_size = tex.get_size()
	ofs += Vector2(
		scroll_speed.x * delta / tile_size.x,
		scroll_speed.y * delta / tile_size.y
	)
	# wrap to [0,1] so it never grows unbounded
	ofs.x = fmod(ofs.x, 1.0)
	ofs.y = fmod(ofs.y, 1.0)
	mat.set_shader_param("uv_offset", ofs)
