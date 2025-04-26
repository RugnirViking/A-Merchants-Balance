using Godot;
using System;
using System.Collections.Generic;


public static class SaveManager
{
	private const string GlobalStorageKey = "RugnirShemonMerchantGame";
	
	private static readonly bool IsBrowser = OS.HasFeature("HTML5");

	public static void Save(string key, string data)
	{
		if (IsBrowser)
		{
			JavaScript.Eval($"localStorage.setItem('{GlobalStorageKey+key}', '{data}');");
		}
		else
		{
			// Save to file
			var file = new File();
			file.Open($"user://savegame_{key}.json", File.ModeFlags.Write);
			file.StoreString(data);
			file.Close();
		}
	}

	public static string Load(string key)
	{
		if (IsBrowser)
		{
			return JavaScript.Eval($"localStorage.getItem('{GlobalStorageKey+key}')") as string ?? "";
		}
		else
		{
			// Load from file
			var file = new File();
			if (file.FileExists($"user://savegame_{key}.json"))
			{
				file.Open($"user://savegame_{key}.json", File.ModeFlags.Read);
				string data = file.GetAsText();
				file.Close();
				return data;
			}
			else
			{
				return "";
			}
		}
	}

	public static void Clear(string key)
	{
		if (IsBrowser)
		{
			JavaScript.Eval($"localStorage.removeItem('{GlobalStorageKey+key}');");
		}
		else
		{
			if (new File().FileExists($"user://savegame_{key}.json"))
			{
				var dir = new Directory();
				if (dir.Open("user://") == Error.Ok)
				{
					dir.Remove($"savegame_{key}.json");
				}
			}
		}
	}
	public static List<string> GetSaveKeys()
	{
		var keys = new List<string>();

		if (IsBrowser)
		{
			// get an array of all localStorage keys starting with our prefix,
			// then strip off the prefix so 'slot1','slot2',etc.
			int prefixLen = GlobalStorageKey.Length;
			var js = 
			  $"JSON.stringify(Object.keys(localStorage)" +
			  $".filter(k=>k.startsWith('{GlobalStorageKey}'))" +
			  $".map(k=>k.slice({prefixLen})))";
			var json = JavaScript.Eval(js) as string;
			var parsed = JSON.Parse(json);
			if (parsed.Error == Error.Ok)
				foreach (var item in (parsed.Result as Godot.Collections.Array)){
					if (item!="lastSaved")
						keys.Add(item as string);
				}
		}
		else
		{
			var dir = new Directory();
			if (dir.Open("user://") == Error.Ok)
			{
				dir.ListDirBegin(false, true);
				string f;
				while ((f = dir.GetNext()) != "")
				{
					if (f.StartsWith("savegame_") && f.EndsWith(".json"))
					{
						
						// strip "savegame_" and ".json"
						var inner = f.Substring("savegame_".Length,
							f.Length - "savegame_".Length - ".json".Length);
						if (inner!="lastSaved")
						{
							keys.Add(inner);
						}
					}
				}
				dir.ListDirEnd();
			}
		}

		return keys;
	}
}

public static class GameState
{
	// Don't need to be saved
	public static string CurrentCity;
	public static Texture CityBanner;
	public static event Action<int> OnGoldChanged;
	public static string CurrentSaveGameKey = "slot1";
	
	
	// Need to be saved
	public static bool isFirstLoad = true;
	public static ulong firstLoadSeed = 0;
	public static Vector2 worldPos;
	
	public static Dictionary<string, MarketSimulator> CityEconomies 
		= new Dictionary<string, MarketSimulator>();
		
		
	// Player stuff
	private static int _playerGold = 1000;
	public static int playerGold
	{
		get => _playerGold;
		set
		{
			if (_playerGold != value)
			{
				_playerGold = value;
				OnGoldChanged?.Invoke(_playerGold);
			}
		}
	}

	public static int[] goodsOwned = new int[8];
	
	public static List<string> goodsNames = new List<String>{
		"Wood",
		"Stone",
		"Raw Iron",
		"Iron Ingots",
		"Cotton",
		"Fabric",
		"Grain",
		"Bread"
	};
	
	public static List<int> goodsWeights = new List<int>{
		5,
		10,
		15,
		18,
		3,
		3,
		5,
		3
	};
	
	// audio controls
	
	public static float masterVolume  = 1.0f;
	public static float sfxVolume     = 1.0f;
	public static float ambientVolume = 1.0f;
	public static float musicVolume   = 1.0f;
	
	public static void SaveGame()
	{
		// build a big Dictionary
		var data = new Godot.Collections.Dictionary
		{
			["isFirstLoad"]   = isFirstLoad,
			["firstLoadSeed"] = firstLoadSeed,
			["worldPosX"]     = worldPos.x,
			["worldPosY"]     = worldPos.y,
			["playerGold"]    = playerGold,
			["goodsOwned"]    = new Godot.Collections.Array(goodsOwned),
			["masterVolume"]  = masterVolume,
			["sfxVolume"]     = sfxVolume,
			["ambientVolume"] = ambientVolume,
			["musicVolume"]   = musicVolume
		};

		// serialize all CityEconomies
		var econDict = new Godot.Collections.Dictionary();
		foreach (var kv in CityEconomies)
			econDict[kv.Key] = kv.Value.ToDictionary();
		data["CityEconomies"] = econDict;

		// turn into JSON text
		string json = JSON.Print(data);
		SaveManager.Save(CurrentSaveGameKey, json);
		
		SaveManager.Save("lastSaved", CurrentSaveGameKey);
		GD.Print("GameState saved.");
	}
	
	public static void LoadGame()
	{
		string json = SaveManager.Load(CurrentSaveGameKey);
		if (string.IsNullOrEmpty(json))
		{
			GD.Print("No save found for key " + CurrentSaveGameKey);
			return;
		}

		var parsed = JSON.Parse(json);
		if (parsed.Error != Error.Ok)
		{
			GD.PrintErr("Failed to parse save JSON: " + parsed.Error);
			return;
		}

		var data = parsed.Result as Godot.Collections.Dictionary;

		// --- primitive fields ---
		isFirstLoad   = data.Contains("isFirstLoad")
						  ? (bool)data["isFirstLoad"]
						  : true;

		if (data.Contains("firstLoadSeed"))
		{
			var raw = data["firstLoadSeed"];
			if (raw is double d)
			{
				// clamp to [0, UInt64.MaxValue] and drop fraction
				if (d <= 0) 
					firstLoadSeed = 0;
				else if (d >= ulong.MaxValue) 
					firstLoadSeed = ulong.MaxValue;
				else 
					firstLoadSeed = (ulong)d;
			}
			else if (raw is long l)
			{
				firstLoadSeed = l < 0 ? 0UL : (ulong)l;
			}
			else if (raw is string s && ulong.TryParse(s, out var parsedulong))
			{
				firstLoadSeed = parsedulong;
			}
			else
			{
				// fallback
				firstLoadSeed = 0;
			}
		}
		else
		{
			firstLoadSeed = 0;
		}

		float px = data.Contains("worldPosX")
						  ? Convert.ToSingle(data["worldPosX"])
						  : 0f;
		float py = data.Contains("worldPosY")
						  ? Convert.ToSingle(data["worldPosY"])
						  : 0f;
		worldPos = new Vector2(px, py);

		playerGold = data.Contains("playerGold")
						  ? Convert.ToInt32(data["playerGold"])
						  : 1000;

		// --- goodsOwned array ---
		if (data.Contains("goodsOwned"))
		{
			var arr = data["goodsOwned"] as Godot.Collections.Array;
			for (int i = 0; i < goodsOwned.Length && i < arr.Count; i++)
				goodsOwned[i] = Convert.ToInt32(arr[i]);
		}

		// --- volume settings ---
		masterVolume  = data.Contains("masterVolume")  ? Convert.ToSingle(data["masterVolume"])  : 1f;
		sfxVolume     = data.Contains("sfxVolume")     ? Convert.ToSingle(data["sfxVolume"])     : 1f;
		ambientVolume = data.Contains("ambientVolume") ? Convert.ToSingle(data["ambientVolume"]) : 1f;
		musicVolume   = data.Contains("musicVolume")   ? Convert.ToSingle(data["musicVolume"])   : 1f;

		// --- CityEconomies ---
		CityEconomies.Clear();
		if (data.Contains("CityEconomies"))
		{
			var econDict = data["CityEconomies"] as Godot.Collections.Dictionary;
			foreach (string cityKey in econDict.Keys)
			{
				var simDict = econDict[cityKey] as Godot.Collections.Dictionary;
				CityEconomies[cityKey] = MarketSimulator.FromDictionary(simDict);
			}
		}

		GD.Print("GameState loaded.");
	}
}

public class BaseGameHandler : Node2D
{
	// Speed the world scrolls under the player (px/sec)
	[Export]
	public float MoveSpeed = 200f;

		  
	private TextureRect       _background;
	private ShaderMaterial    _material;
	private Texture           _texture;
	private Sprite            _player;
	private Line2D            _line;
	private ColorRect         _glassEffect;
	private ShaderMaterial    _glassMaterial;
	private ShaderMaterial    _xMapEdgeEffect;
	private ShaderMaterial    _yMapEdgeEffect;
	private Node2D            _labelContainer;
	private Minimap           _minimap;
	private Label             _goldLabel;
	private Label             _positionLabel;
	private AudioStreamPlayer _bgmPlayer;
	private Panel             _autosavePanel;
	private SaveLoadDialog    _saveLoadDialog;
	private Control    		  _inventoryScreen;

	// World offset & target in “virtual” world coords
	private Vector2 _worldPos  = Vector2.Zero;
	public Vector2 _targetPos = Vector2.Zero;
	private Vector2 _clickScreenPos = Vector2.Zero;
	private bool    _isDragging;
	
	private MultiMeshInstance2D _scatter;
	private MultiMesh _multiMesh;

	private const int ScatterCount = 200;
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
		_xMapEdgeEffect = (ShaderMaterial)(FindNode("XMapEdgeEffect",true,false) as ColorRect).Material;
		_yMapEdgeEffect = (ShaderMaterial)(FindNode("YMapEdgeEffect",true,false) as ColorRect).Material;
		_texture        = _background.Texture;
		_player         = GetNode<Sprite>("Player");
		_line           = GetNode<Line2D>("LineLayer/TargetLine");
		_glassEffect    = GetNode<ColorRect>("GlassLayer/GlassEffect");
		_glassMaterial  = (ShaderMaterial)_glassEffect.Material;
		_labelContainer = GetNodeOrNull<Node2D>("CityLabels");
		_goldLabel      = FindNode("GoldLabel", true, false) as Label;
		_positionLabel  = FindNode("PositonLabel", true, false) as Label;
		_bgmPlayer      = FindNode("BGMPlayer", true, false) as AudioStreamPlayer;
		_autosavePanel  = FindNode("AutosavePanel", true, false) as Panel;
		_saveLoadDialog = FindNode("SaveLoadDialog", true, false) as SaveLoadDialog;
		_inventoryScreen= FindNode("InventoryScreen", true, false) as Control;
		
		
		_saveLoadDialog.Connect("popup_hide",    this, nameof(OnSaveLoadClosed));
		
		if (_labelContainer == null)
		{
			GD.PrintErr("CityLabels node not found!");
			return;
		}
		
		var rng = new RandomNumberGenerator();
		if (GameState.isFirstLoad)
		{
			
			GameState.LoadGame();
			rng.Randomize();
			GameState.isFirstLoad = false;
			GameState.firstLoadSeed = rng.Seed;
		} else{
			rng.Seed = GameState.firstLoadSeed;
			_worldPos = GameState.worldPos;
			_targetPos = _worldPos;
		}
		
		// bind the gold to the label
		GameState.OnGoldChanged += UpdateGold;
		UpdateGold(GameState.playerGold); // set the initial value
		
		
		// Center immediately
		CenterPlayer();
		GetViewport().Connect("size_changed", this, nameof(OnViewportSizeChanged));
		
		InitScatter(rng);
		// update shader scroll in ready 
		
		_worldPos = GameState.worldPos;
		_targetPos = GameState.worldPos;
		UpdateShaderScroll();
		
		InitMinimap();
		
		(FindNode("gameVolumeSlider",true,false) as HSlider        ).Value = 100 * GameState.masterVolume;
		(FindNode("soundEffectsVolumeSlider",true,false) as HSlider).Value = 100 * GameState.sfxVolume;
		(FindNode("ambientVolumeSlider",true,false) as HSlider     ).Value = 100 * GameState.ambientVolume;
		(FindNode("musicVolumeSlider",true,false) as HSlider       ).Value = 100 * GameState.musicVolume;
		
		
	}
	
	private void UpdateGold(int newGold)
	{
		_goldLabel.Text = "Gold: " + newGold;
	}
	

	public override void _ExitTree()
	{
		GameState.OnGoldChanged -= UpdateGold;
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
		else if(@event.IsActionPressed("debugPos"))
		{
			foreach (Cityscript city in _cities){
				GD.Print($"City {city.Name} is at Pos {city.GetMeta("world_pos")}");
			}
			
			Vector2 screenCenter = GetViewport().Size * 0.5f;
			Vector2 _playerPos    = _worldPos + screenCenter;
			GD.Print($"Player pos: {_playerPos}");
		}
		else if(@event.IsActionPressed("debugTriggerAutosave"))
		{
			GameState.SaveGame();
		}
		else if(@event.IsActionPressed("debugClearSaves"))
		{
			SaveManager.Clear("test");
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
	
	private async void CallLater(float delaySeconds, string methodName)
	{
		await ToSignal(GetTree().CreateTimer(delaySeconds), "timeout");
		CallDeferred(methodName);
	}
	
	private void HideAutosave()
	{
		_autosavePanel.Hide();
	}
	
	private void Autosave()
	{
		_autosavePanel.Show();
		GameState.SaveGame();
		CallLater(0.5f, nameof(HideAutosave));
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
	public void UpdateCoords(){
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		Vector2 _playerPos    = _worldPos + screenCenter;
		_positionLabel.Text = $"Pos: {_playerPos.x.ToString("F0")}, {_playerPos.y.ToString("F0")}";
	}
	public void UpdateMapEdgeShaders()
	{
		float fadeDistance = 100f;
		Vector2 screenCenter = GetViewport().Size * 0.5f;
		Vector2 _playerPos    = _worldPos + screenCenter;
		Vector2 worldMin = new Vector2(-2000, -2000);
		Vector2 worldMax = new Vector2(2000, 2000);
		
		float xExtent = 0.0f;
		float yExtent = 0.0f;
		
		Vector2 xSide = new Vector2(-1, 0);
		Vector2 ySide = new Vector2(-1, 0);
		// left side?
		if (_playerPos.x < worldMin.x)
		{
			float d = worldMin.x - _playerPos.x;
			xExtent = Mathf.Clamp(d / fadeDistance, 0f, 1f);
			xSide = new Vector2(-1, 0);
		}
		// right side?
		else if (_playerPos.x > worldMax.x)
		{
			float d = _playerPos.x - worldMax.x;
			xExtent = Mathf.Clamp(d / fadeDistance, 0f, 1f);
			xSide = new Vector2(1, 0);
		}
		_xMapEdgeEffect.SetShaderParam("extent"  ,      xExtent);
		_xMapEdgeEffect.SetShaderParam("side_vec",      xSide);
		
		if (_playerPos.y < worldMin.y)
		{
			float d = worldMin.y - _playerPos.y;
			yExtent = Mathf.Clamp(d / fadeDistance, 0f, 1f);
			ySide = new Vector2(0, 1);
		}
		else if (_playerPos.y > worldMax.y)
		{
			float d = _playerPos.y - worldMax.y;
			yExtent = Mathf.Clamp(d / fadeDistance, 0f, 1f);
			ySide = new Vector2(0, -1);
		}
		
		_yMapEdgeEffect.SetShaderParam("extent"  ,      yExtent);
		_yMapEdgeEffect.SetShaderParam("side_vec",      ySide);
		
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
			GameState.worldPos = _worldPos;

			UpdateShaderScroll();
			UpdateCoords();
		}
		UpdateMapEdgeShaders();

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
		
		if (Engine.GetFramesDrawn() % 600 == 0){
			Autosave();
		}
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
	

	private void _on_pauseButton_pressed()
	{
		PopupMenu pauseMenu = FindNode("pauseMenu",true,false) as PopupMenu;
		pauseMenu.PopupCentered();
	}
	

	private void _on_resumeButton_pressed()
	{
		PopupMenu pauseMenu = FindNode("pauseMenu",true,false) as PopupMenu;
		pauseMenu.Hide();
	}

	private void _on_quitButton_pressed()
	{
		Autosave();
		var mainMenu = (PackedScene)GD.Load("res://mainMenu.tscn");
		GetTree().ChangeSceneTo(mainMenu);
	}
	
	private void UpdateSoundVolumes()
	{
		double musicVolume = 40*GameState.masterVolume*GameState.musicVolume-40;
		if (musicVolume>-40.0){
			_bgmPlayer.VolumeDb = (float)musicVolume;
		} else{
			_bgmPlayer.VolumeDb = (float)-80.0;
		}
		
	}
	
	private void OnSaveLoadClosed(){
		_worldPos = GameState.worldPos;
		_targetPos = GameState.worldPos;
		
		UpdateShaderScroll();
	}
	

	private void _on_gameVolumeSlider_value_changed(float value)
	{
		GameState.masterVolume = value/100;
		UpdateSoundVolumes();
	}
	private void _on_soundEffectsVolumeSlider_value_changed(float value)
	{
		GameState.sfxVolume = value/100;
		UpdateSoundVolumes();
	}
	private void _on_musicVolumeSlider_value_changed(float value)
	{
		GameState.musicVolume = value/100;
		UpdateSoundVolumes();
	}
	private void _on_ambientVolumeSlider_value_changed(float value)
	{
		GameState.ambientVolume = value/100;
		UpdateSoundVolumes();
	}
	private void _on_saveLoadButton_pressed()
	{
		((WindowDialog)_saveLoadDialog).PopupCentered();
	}

	private void _on_inventoryButton_pressed()
	{
		_inventoryScreen.Show();
	}
}












