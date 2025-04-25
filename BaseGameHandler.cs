using Godot;
using System;
using System.Collections.Generic;


public static class GameState
{
	public static string CurrentCity;
	public static Texture CityBanner;
	public static bool isFirstLoad = true;
	public static ulong firstLoadSeed = 0;
	public static Vector2 worldPos;
	
	public static Dictionary<string, MarketSimulator> CityEconomies 
		= new Dictionary<string, MarketSimulator>();
}

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
	private Node2D         _labelContainer;
	private Minimap        _minimap;

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
	
	private List<Vector2> _scatterPositions = new List<Vector2>();
	private List<Cityscript> _cities = new List<Cityscript>();
	
	// Keeping track of closest city for the enter city popup
	private bool _isNearCity = false;
	private Cityscript _closestCity = null;
	
	private Dictionary<string, Vector2> _atlasMap = new Dictionary<string, Vector2>
	{
		{ "campmarker1", new Vector2(0, 0) },
		{ "citymarker1", new Vector2(1, 0) },
		{ "citymarker2", new Vector2(2, 0) },
		{ "citymarker3", new Vector2(3, 0) },
		{ "citymarker4", new Vector2(4, 0) },
		{ "citymarker5", new Vector2(5, 0) },
		{ "citymarker6", new Vector2(6, 0) },
		{ "citymarker7", new Vector2(7, 0) },
		{ "citymarker8", new Vector2(8, 0) },
		{ "citymarker9", new Vector2(0, 1) },
		{ "forestmarker1", new Vector2(1, 1) },
		{ "forestmarker2", new Vector2(2, 1) },
		{ "hillsmarker1", new Vector2(3, 1) },
		{ "mountains1", new Vector2(4, 1) },
		{ "pondmarker1", new Vector2(5, 1) },
		{ "pondmarker2", new Vector2(6, 1) },
		{ "tentmarker1", new Vector2(7, 1) },
		{ "treesmarker1", new Vector2(8, 1) }
	};

	private Vector2 GetAtlasUV(string key)
	{
		return _atlasMap.ContainsKey(key) ? _atlasMap[key] : Vector2.Zero;
	}

	private void SpawnScatter(ref int index, int count, List<string> textureKeys, float extent, bool allowFlip, RandomNumberGenerator rng, float minDistance = 75f)
	{
		int attemptsPerItem = 200000;

		for (int i = 0; i < count; i++)
		{
			Vector2 pos = Vector2.Zero;
			bool accepted = false;

			for (int attempt = 0; attempt < attemptsPerItem; attempt++)
			{
				float x = rng.RandfRange(-extent, extent);
				float y = rng.RandfRange(-extent, extent);
				pos = new Vector2(x, y);

				if (minDistance <= 0f)
				{
					accepted = true;
					break;
				}

				bool farEnough = true;
				foreach (var existing in _scatterPositions)
				{
					if (pos.DistanceSquaredTo(existing) < minDistance * minDistance)
					{
						farEnough = false;
						break;
					}
				}

				if (farEnough)
				{
					accepted = true;
					break;
				}
			}

			if (!accepted) continue; // skip this item if all attempts failed

			_scatterPositions.Add(pos);

			float rot = Mathf.Tau / 2;
			float scale = 0.5f;

			bool flip = allowFlip && rng.RandiRange(0, 1) == 1;
			string key = textureKeys[rng.RandiRange(0, textureKeys.Count - 1)];
			Vector2 uv = GetAtlasUV(key);

			Transform2D xform2D = new Transform2D(rot, pos).Scaled(Vector2.One * scale);
			Transform xform3D = new Transform(
				new Basis(new Vector3(xform2D.x.x, xform2D.x.y, 0), new Vector3(xform2D.y.x, xform2D.y.y, 0), new Vector3(0, 0, 1)),
				new Vector3(xform2D.origin.x, xform2D.origin.y, 0)
			);

			_multiMesh.SetInstanceTransform(index, xform3D);
			_multiMesh.SetInstanceColor(index, new Color(
				uv.x / 9f,
				uv.y / 2f,
				flip ? 1f : 0f,
				1f
			));
			index++;
		}
	}

	private void SpawnCluster(ref int index, int clusters, int itemsPerCluster, float clusterRadius, List<string> textureKeys, float extent, RandomNumberGenerator rng)
	{
		for (int c = 0; c < clusters; c++)
		{
			Vector2 center = new Vector2(
				rng.RandfRange(-extent, extent),
				rng.RandfRange(-extent, extent)
			);

			for (int i = 0; i < itemsPerCluster; i++)
			{
				Vector2 offset = new Vector2(
					rng.RandfRange(-clusterRadius, clusterRadius),
					rng.RandfRange(-clusterRadius, clusterRadius)
				);

				Vector2 pos = center + offset;

				_scatterPositions.Add(pos);
				float rot = Mathf.Tau/2;
				float scale = 0.5f;

				bool flip = rng.RandiRange(0, 1) == 1;
				string key = textureKeys[rng.RandiRange(0, textureKeys.Count - 1)];
				Vector2 uv = GetAtlasUV(key);

				Transform2D xform2D = new Transform2D(rot, pos).Scaled(Vector2.One * scale);
				Transform xform3D = new Transform(
					new Basis(new Vector3(xform2D.x.x, xform2D.x.y, 0), new Vector3(xform2D.y.x, xform2D.y.y, 0), new Vector3(0, 0, 1)),
					new Vector3(xform2D.origin.x, xform2D.origin.y, 0)
				);

				_multiMesh.SetInstanceTransform(index, xform3D);
				_multiMesh.SetInstanceColor(index, new Color(
					uv.x / 9f,
					uv.y / 2f,
					flip ? 1f : 0f,
					1f
				));

				index++;
			}
		}
	}
	
	private void SpawnCities(ref int index){
		var cityParent = GetNodeOrNull<CanvasLayer>("CitiesLayer");
		var prototype = _labelContainer.GetNode<NinePatchRect>("LabelPrototype");

		_cities.Clear();
		
		if (cityParent != null)
		{
			foreach (Cityscript city in cityParent.GetChildren())
			{
				if (city is Position2D pos2D)
				{
					_cities.Add(city);
					Vector2 pos = pos2D.GlobalPosition;
					Vector2 worldPos = pos2D.Position; // this is the world-relative position
					city.SetMeta("world_pos", worldPos); // store as metadata
					
					_scatterPositions.Add(pos);
					string markerKey = city.AtlasKey;
					bool flip = city.FlipX;
					Vector2 uv = GetAtlasUV(markerKey);

					Transform2D xform2D = new Transform2D(Mathf.Tau / 2, pos);
					Transform xform3D = new Transform(
						new Basis(
							new Vector3(xform2D.x.x, xform2D.x.y, 0),
							new Vector3(xform2D.y.x, xform2D.y.y, 0),
							new Vector3(0, 0, 1)
						),
						new Vector3(xform2D.origin.x, xform2D.origin.y, 0)
					);

					_multiMesh.SetInstanceTransform(index, xform3D);
					_multiMesh.SetInstanceColor(index, new Color(
						uv.x / 9f,
						uv.y / 2f,
						flip ? 1f : 0f,
						1f
					));

					
					var panel = (NinePatchRect)prototype.Duplicate();
					var label = panel.GetNode<PanelContainer>("LabelPrototype2").GetNode<Label>("TextForLabel");
					label.Text = city.Name;

					panel.Visible = true;

					_labelContainer.AddChild(panel);
					CallDeferred(nameof(SetupPanelPosition), panel, city.GlobalPosition);


					index++;
				}
			}
		}
	}
	public void SetupPanelPosition(Panel panel, Vector2 worldPos)
	{
		Vector2 size = panel.RectSize;
		
		// Just manually center it instead of using pivot
		Vector2 offset = new Vector2(size.x / 2f, size.y); // center-bottom
		panel.RectPosition = worldPos - offset + new Vector2(0, 100);
	}
	private void InitScatter(RandomNumberGenerator rng)
	{
		_scatter = GetNode<MultiMeshInstance2D>("BackgroundLayer/ScatterLayer");
		_multiMesh = new MultiMesh();

		_multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3d;
		_multiMesh.ColorFormat = MultiMesh.ColorFormatEnum.Float; // ✅ Enable per-instance Color
		_multiMesh.CustomDataFormat = MultiMesh.CustomDataFormatEnum.None;

		const int cols = 9;
		const int rows = 2;
		const float extent = 4000f;

		_scatter.Multimesh = _multiMesh;

		// Create the quad mesh (128x128)
		var quad = new QuadMesh();
		quad.Size = new Vector2(128, 128);

		// Load and assign the shader material
		ShaderMaterial mat = (ShaderMaterial)GD.Load("res://scatter_material.tres");
		quad.Material = mat;

		// Assign mesh to MultiMesh
		_multiMesh.Mesh = quad;

		int index = 0;

		// Set the correct instance count
		int magicNumberCount = 3250;
		int cityCount = GetNodeOrNull<CanvasLayer>("CitiesLayer").GetChildren().Count;
		_multiMesh.InstanceCount = magicNumberCount + cityCount; // exact amount we will need, important

		// Define texture groups
		var forest = new List<string> { "forestmarker1", "forestmarker2", "treesmarker1" };
		var hills = new List<string> { "hillsmarker1" };
		var mountains = new List<string> { "mountains1" };
		var ponds = new List<string> { "pondmarker1", "pondmarker2" };
		var camps = new List<string> { "campmarker1", "tentmarker1" };

		// Generate clustered forests
		SpawnCluster(ref index, 30, 20, 200f, forest, extent, rng);

		// Generate other scatter
		SpawnScatter(ref index, 1500, hills, extent, true, rng);
		SpawnScatter(ref index, 750, mountains, extent, true, rng);
		SpawnScatter(ref index, 300, ponds, extent, false, rng);
		SpawnScatter(ref index, 100, camps, extent, true, rng);
		

		SpawnCities(ref index);
		GD.Print("Cities count: ", _cities.Count);

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
		_labelContainer = GetNodeOrNull<Node2D>("CityLabels");
		if (_labelContainer == null)
		{
			GD.PrintErr("CityLabels node not found!");
			return;
		}
		
		var rng = new RandomNumberGenerator();
		if (GameState.isFirstLoad)
		{
			rng.Randomize();
			GameState.isFirstLoad = false;
			GameState.firstLoadSeed = rng.Seed;
		} else{
			rng.Seed = GameState.firstLoadSeed;
			_worldPos = GameState.worldPos;
			_targetPos = _worldPos;
		}
		// Center immediately
		CenterPlayer();
		GetViewport().Connect("size_changed", this, nameof(OnViewportSizeChanged));
		
		InitScatter(rng);
		// update shader scroll in ready 
		UpdateShaderScroll();
		
		InitMinimap();
	}
	private void InitMinimap()
	{
		_minimap = GetNode<Control>("UILayer/Minimap") as Minimap;
		var container = _minimap.GetNode("CitiesContainer");
		_minimap._baseGameHandler = this;

		foreach (var city in _cities)
		{
			var dot = new ColorRect();
			dot.Color = new Color(1, 0, 0); // red dot
			dot.RectSize = new Vector2(6, 6);
			dot.RectPivotOffset = dot.RectSize / 2;

			Vector2 worldPos = (Vector2)city.GetMeta("world_pos");
			Vector2 mapSize = _minimap.RectSize;
			worldPos = worldPos - new Vector2(80,80);
			dot.RectPosition = NormalizeToMinimap(worldPos, mapSize);

			container.AddChild(dot);
		}
	}
	private void UpdateMinimap()
	{
		var minimap = GetNode<Control>("UILayer/Minimap");
		var playerDot = minimap.GetNode<Control>("PlayerDot");

		Vector2 minimapSize = minimap.RectSize;
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		Vector2 normalized = NormalizeToMinimap(_worldPos + screenCenter, minimapSize);
		playerDot.RectPosition = normalized - playerDot.RectSize * 0.5f;
	}
	
	private void UpdateShaderScroll()
	{
		Vector2 sz = _texture.GetSize();
		Vector2 uv = new Vector2(
			(_worldPos.x / sz.x) % 1f,
			(_worldPos.y / sz.y) % 1f
		);
		if (uv.x < 0) uv.x += 1f;
		if (uv.y < 0) uv.y += 1f;
		_material.SetShaderParam("uv_offset", uv);
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
	
	public void SetWorldTarget(Vector2 pos)
	{
		
		_targetPos = pos;
	}
	
	private string GetKeyNameAttackedToAction(string actionName)
	{
		var events = InputMap.GetActionList(actionName);

		foreach (InputEvent ev in events)
		{

			if (ev is InputEventKey keyEvent)
			{
				string keyName = OS.GetScancodeString(keyEvent.PhysicalScancode);
				return keyName;
			}
		}

		return "[Unbound]";
	}
	
	public Vector2 MinimapClickToWorld(Vector2 minimapClick)
	{
		
		Vector2 minimapSize = _minimap.RectSize;
		Vector2 worldMin = new Vector2(-2000, -2000);
		Vector2 worldMax = new Vector2(2000, 2000);
		Vector2 worldSize = worldMax - worldMin;

		// Get local position inside the minimap
		Vector2 localClick = minimapClick - _minimap.GetGlobalRect().Position;
		Vector2 percent = localClick / minimapSize;

		// Map to world space (adjusted for screen center)
		
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		return worldMin + percent * worldSize - screenCenter;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == (int)ButtonList.Left)
		{
			if (mb.Pressed)
			{
				_isDragging = true;
				UpdateTarget(mb.Position);
						foreach (var city in _cities)
						{
							Vector2 cityPos = (Vector2)city.GetMeta("world_pos");
							Vector2 toCity = cityPos - _worldPos;	
						}
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
		else if(@event.IsActionPressed("enterCity"))
		{
			if (_isNearCity && _closestCity != null)
			{
				EnterCityView(_closestCity);
			}
		}
	}
	
	Vector2 NormalizeToMinimap(Vector2 worldPos, Vector2 minimapSize)
	{
		Vector2 worldMin = new Vector2(-2000, -2000);
		Vector2 worldMax = new Vector2(2000, 2000);
		Vector2 worldSize = worldMax - worldMin;

		Vector2 normalized = (worldPos - worldMin) / worldSize;
		return normalized * minimapSize;
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

			UpdateShaderScroll();
		}

		// **draw the line** in screen‑space
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		Vector2 screenTarget = screenCenter + (_targetPos - _worldPos);

		// update Line2D points
		_line.Points = new Vector2[] { screenCenter, screenTarget };
		
		_scatter.Position = -_worldPos;
		_labelContainer.Position = -_worldPos;
		
		
		float cityProximityRadius = 150f;
		Cityscript closestCity = null;
		float closestDist = float.MaxValue;
		_isNearCity = false;
		_closestCity = null;
		foreach (var city in _cities)
		{
			Vector2 cityScreen = ((Vector2)city.GetMeta("world_pos") - _worldPos);
			Vector2 toCityScreen = cityScreen - screenCenter;
			float dist = toCityScreen.Length();

			if (dist < cityProximityRadius && dist < closestDist)
			{
				closestCity  = city;
				closestDist  = dist;
				_isNearCity  = true;
				_closestCity = closestCity;
			}
		}

		
		if (closestCity != null)
		{
			ShowCityPrompt(closestCity);
			
		}
		else
		{
			HideCityPrompt();
		}
		UpdateMinimap();

	}
	
	private void EnterCityView(Cityscript city)
	{
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		GD.Print("Entering city: ", city.Name);
		GameState.CurrentCity = city.Name;
		GameState.CityBanner = city.bannerTex;
		GameState.worldPos = (Vector2)city.GetMeta("world_pos") - screenCenter;
		
		
		var cityView = (PackedScene)GD.Load("res://CityView.tscn");
		GetTree().ChangeSceneTo(cityView);
	}
	
	private void ShowCityPrompt(Cityscript city)
	{
		var prompt = GetNode<NinePatchRect>("CityPrompt");
		var label = prompt.GetNode<Label>("LabelPrototype2/Label");
		var keyname = GetKeyNameAttackedToAction("enterCity");
		label.Text = $"Press {keyname} to enter {city.Name}";
		prompt.Visible = true;

		Vector2 cityScreenPos = (city.GlobalPosition - _worldPos);
		prompt.RectPosition = cityScreenPos - prompt.RectSize * 0.5f - new Vector2(0, 70);

	}

	private void HideCityPrompt()
	{
		GetNode<NinePatchRect>("CityPrompt").Visible = false;
	}
	
	private void _on_ColorRect2_mouse_exited()
	{
			GD.Print("Clicked!");
	}
}


