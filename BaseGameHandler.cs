using Godot;
using System;

public class BaseGameHandler : Node2D
{
	// Speed the world scrolls under the player (px/sec)
	[Export]
	public float MoveSpeed = 200f;

		  
	private TextureRect    _background;
	private ShaderMaterial _material;
	private Texture        _texture;
	private Sprite         _player;
	private Line2D         _line;
	private ColorRect      _glassEffect;
	private ShaderMaterial _glassMaterial;

	// World offset & target in “virtual” world coords
	private Vector2 _worldPos  = Vector2.Zero;
	public Vector2 _targetPos = Vector2.Zero;
	private Vector2 _clickScreenPos = Vector2.Zero;
	private bool    _isDragging;
	
	private MultiMeshInstance2D _scatter;
	private MultiMesh _multiMesh;

	private const int ScatterCount = 200;
	private const float WorldExtent = 30f;
	private const int SheetCols = 9;
	private const int SheetRows = 2;
	
	
	private void InitScatter()
	{
		_scatter = GetNode<MultiMeshInstance2D>("BackgroundLayer/ScatterLayer");
		_multiMesh = new MultiMesh();

		_multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3d;
		_multiMesh.ColorFormat = MultiMesh.ColorFormatEnum.Float; // ✅ Enable per-instance Color
		_multiMesh.CustomDataFormat = MultiMesh.CustomDataFormatEnum.None;

		const int cols = 9;
		const int rows = 2;
		const int count = 1000;
		const float extent = 3000f;

		_multiMesh.InstanceCount = count;
		_scatter.Multimesh = _multiMesh;

		// Create the quad mesh (128x128)
		var quad = new QuadMesh();
		quad.Size = new Vector2(128, 128);

		// Load and assign the shader material
		ShaderMaterial mat = (ShaderMaterial)GD.Load("res://scatter_material.tres");
		quad.Material = mat;

		// Assign mesh to MultiMesh
		_multiMesh.Mesh = quad;

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		for (int i = 0; i < count; i++)
		{
			// Random world position
			Vector2 worldPos = new Vector2(
				rng.RandfRange(-extent, extent),
				rng.RandfRange(-extent, extent)
			);

			float rot = Mathf.Tau/2;
			float scale = 1.0f;

			Transform2D xform2D = new Transform2D(rot, worldPos).Scaled(Vector2.One * scale);

			// Convert to 3D Transform
			Transform xform3D = new Transform();
			xform3D.basis = new Basis(
				new Vector3(xform2D.x.x, xform2D.x.y, 0),
				new Vector3(xform2D.y.x, xform2D.y.y, 0),
				new Vector3(0, 0, 1)
			);
			xform3D.origin = new Vector3(xform2D.origin.x, xform2D.origin.y, 0);
			_multiMesh.SetInstanceTransform(i, xform3D);

			// Pick a tile from the spritesheet
			int u = rng.RandiRange(0, cols - 1);
			int v = rng.RandiRange(0, rows - 1);

			// Normalize tile coords and store in Color.rg
			float uNorm = (float)u / (float)cols;
			float vNorm = (float)v / (float)rows;
			_multiMesh.SetInstanceColor(i, new Color(uNorm, vNorm, 0));
		}

		// Pass sheet size to shader
		mat.SetShaderParam("sheet_size", new Vector2(cols, rows));
	}


	
	public override void _Ready()
	{
		// Grab your nodes
		_background     = GetNode<TextureRect>("BackgroundLayer/Background");
		_material       = (ShaderMaterial)_background.Material;
		_texture        = _background.Texture;
		_player         = GetNode<Sprite>("Player");
		_line           = GetNode<Line2D>("LineLayer/TargetLine");
		_glassEffect    = GetNode<ColorRect>("GlassLayer/GlassEffect");
		_glassMaterial  = (ShaderMaterial)_glassEffect.Material;

		// Center immediately
		CenterPlayer();
		GetViewport().Connect("size_changed", this, nameof(OnViewportSizeChanged));
		
		InitScatter();
	}

	private void OnViewportSizeChanged()
	{
		CenterPlayer();
	}

	private void CenterPlayer()
	{
		// Position sprite in the exact middle of the viewport
		Vector2 size = GetViewport().Size;
		_player.Position = size * 0.5f;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == (int)ButtonList.Left)
		{
			if (mb.Pressed)
			{
				_isDragging = true;
				UpdateTarget(mb.Position);
			}
			else
			{
				_isDragging = false;
			}
		}
		else if (@event is InputEventMouseMotion mm && _isDragging)
		{
			UpdateTarget(mm.Position);
		}
	}

	private void UpdateTarget(Vector2 mouseScreenPos)
	{
		// Convert screen position to UV
		Vector2 screenSize = GetViewport().Size;
		Vector2 uv = mouseScreenPos / screenSize;

		// Apply the *same* distortion as the shader
		// Match these to your shader's uniforms
		float strength = 1.0f;
		Vector2 rectSize = new Vector2(0.95f, 0.95f); // example, tweak to match shader
		Vector2 distortedUV = ApplyBarrelDistortion(uv, strength, rectSize);

		// Convert distorted UV back to screen position
		Vector2 distortedScreenPos = distortedUV * screenSize;

		// Calculate relative click offset from screen center
		Vector2 center = screenSize * 0.5f;
		Vector2 clickRel = distortedScreenPos - center;

		// Set world target accordingly
		_targetPos = _worldPos + clickRel;
	}
	
	private Vector2 ApplyBarrelDistortion(Vector2 uv, float strength, Vector2 rectSize)
	{
		float dynamicStrength = strength * (1f - Mathf.Max(rectSize.x, rectSize.y));

		// Map to -1..1 around screen center
		Vector2 centered = uv * 2f - new Vector2(1f, 1f);
		float r = centered.Length();

		// Apply distortion
		Vector2 distorted = centered + centered * (dynamicStrength * r * r);

		// Back to 0..1 UV
		return (distorted + new Vector2(1f, 1f)) * 0.5f;
	}

	public override void _Process(float delta)
	{
		if (_isDragging)
		{
			// Continuously refresh target even if the mouse isn't moving
			UpdateTarget(GetViewport().GetMousePosition());
		}
		// move world offset
		Vector2 diff = _targetPos - _worldPos;
		if (diff.Length() > 1f)
		{
			Vector2 step = diff.Normalized() * MoveSpeed * delta;
			if (step.Length() > diff.Length()) step = diff;
			_worldPos += step;

			// scroll the shader‐driven background
			Vector2 sz = _texture.GetSize();
			Vector2 uv = new Vector2(
				(_worldPos.x / sz.x) % 1f,
				(_worldPos.y / sz.y) % 1f
			);
			if (uv.x < 0) uv.x += 1f;
			if (uv.y < 0) uv.y += 1f;
			_material.SetShaderParam("uv_offset", uv);
		}

		// **draw the line** in screen‑space
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		Vector2 screenTarget = screenCenter + (_targetPos - _worldPos);

		// update Line2D points
		_line.Points = new Vector2[] { screenCenter, screenTarget };
		
		_scatter.Position = -_worldPos;
	}
}
