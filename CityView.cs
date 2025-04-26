using Godot;
using System;
using System.Collections.Generic;
using System.Linq;



public class CityView : Control
{

	// flavor-text templates per good; {city}, {quantity}, {good} will be replaced
	private static readonly Dictionary<string,string[]> _templates = new Dictionary<string,string[]>
	{
		["Wood"] = new[]
		{
			"The sawmills in {city} are running low on {good}. They need {quantity} logs to keep building!",
			"{city}'s carpenters are begging for {quantity} bundles of {good}. Can you help?",
			"Word is that {city} wants {quantity} stacks of {good} for new fortifications."
		},
		["Stone"] = new[]
		{
			"{city} has finished mining but still needs {quantity} blocks of {good}.",
			"A landslide wiped out the nearby quarry—{city} now requires {quantity} stones.",
			"Repair crews in {city} are waiting on {quantity} slabs of {good}."
		},
		["Raw Iron"] = new[]
		{
			"Foundries in {city} crave {quantity} ores of {good} for smelting.",
			"{city}’s blacksmiths ran out of {good}—they need {quantity} lumps, pronto!",
			"Rumor says {city} will pay handsomely for {quantity} chunks of raw iron."
		},
		["Iron Ingots"] = new[]
		{
			"{city}'s armories want {quantity} {good} for forging weapons.",
			"Merchants report a shortage of {quantity} ingots in {city}.",
			"Forge-masters in {city} will trade favours for {quantity} pieces of {good}."
		},
		["Cotton"] = new[]
		{
			"Textile guilds in {city} need {quantity} bolts of {good}.",
			"{city}’s tailors are desperate for {quantity} sacks of cotton.",
			"An order from {city} calls for {quantity} bales of {good} immediately."
		},
		["Fabric"] = new[]
		{
			"{city}’s nobles demand {quantity} rolls of fine {good}.",
			"Dressmakers in {city} placed an urgent order: {quantity} yards of fabric.",
			"{city}’s festival costumes need {quantity} lengths of {good}."
		},
		["Grain"] = new[]
		{
			"Granaries in {city} are low—deliver {quantity} sacks of {good}.",
			"{quantity} bushels of grain are needed to feed {city}'s populace.",
			"{city} will collapse into famine without {quantity} heaps of {good}."
		},
		["Bread"] = new[]
		{
			"Bakers in {city} ran out of {good}—they ask for {quantity} loaves.",
			"The poor quarter of {city} needs {quantity} fresh loaves of bread.",
			"Deliver {quantity} baskets of {good} to stave off hunger in {city}."
		},
	};
	
	// Label styling
	[Export] public DynamicFont LabelFont;
	[Export] public Color LabelColor = new Color(1,1,1);
	[Export] public Color normalColor = new Color(0.5f, 0.3f, 0.1f); // Normal brown
	
	// Path to your .tscn file – adjust as needed.
	[Export] private PackedScene QuestContainerScene;
		
	private Label               _cityNameLabel;
	private TextureRect         _bannerTexture;
	
	private GraphControl        _graph;
	private MarketSimulator     _economy;
	private RandomNumberGenerator _rng;
	private Button              _buyButton;
	private Button              _sellButton;
	private VBoxContainer       _tradePanel;
	private Label               _goldLabel;
	private AudioStreamPlayer   _buySound;
	private AudioStreamPlayer   _sellSound;
	private AudioStreamPlayer   _questCompleteSound;
	private VBoxContainer       _questsContainer;
	private VBoxContainer       _trackedQuestsContainer;

	// markup & markdown factors
	private const double BuyMarkup  = 1.10;
	private const double SellMarkdown = 0.90;
	
	
	private List<Label> _buyPriceLabels = new List<Label>();
	private List<Label> _sellPriceLabels = new List<Label>();
	private List<Label> _goodsOwnedLabels = new List<Label>();
	
	private List<SpinBox> _amountFields = new List<SpinBox>();
	
	private List<QuestContainer> _trackedQuestContainers = new List<QuestContainer>();
	
	
	public override void _Ready()
	{
		_cityNameLabel           = FindNode("CityName",true,false) as Label;
		_bannerTexture           = FindNode("TextureRect",true,false) as TextureRect;
		
		_buyButton  = FindNode("BuyButton", true, false)  as Button;
		_sellButton = FindNode("SellButton", true, false) as Button;
		
		_buySound   = FindNode("BuySound", true, false)   as AudioStreamPlayer;
		_sellSound  = FindNode("SellSound", true, false)  as AudioStreamPlayer;
		_questCompleteSound  = FindNode("QuestCompleteSound", true, false)  as AudioStreamPlayer;
		
		_questsContainer         = FindNode("AvailableQuests", true, false)  as VBoxContainer;
		_trackedQuestsContainer  = FindNode("TrackedQuests",   true, false)    as VBoxContainer;
		
		double sfxVolume = 40*GameState.masterVolume*GameState.sfxVolume-40;
		
		if (sfxVolume>-40.0){
			_buySound.VolumeDb = (float)sfxVolume;
			_sellSound.VolumeDb = (float)sfxVolume;
			_questCompleteSound.VolumeDb = (float)sfxVolume;
		} else{
			_buySound.VolumeDb = (float)-80.0;
			_sellSound.VolumeDb = (float)-80.0;
			_questCompleteSound.VolumeDb = (float)-80.0;
		}
		

		_buyButton.Connect("pressed", this, nameof(OnBuyPressed));
		_sellButton.Connect("pressed", this, nameof(OnSellPressed));
		
		_cityNameLabel.Text    =   GameState.CurrentCity;
		_bannerTexture.Texture =   GameState.CityBanner;
		
		_rng = new RandomNumberGenerator();
		_rng.Randomize();
		_economy = GameState.CityEconomies[GameState.CurrentCity];

		// 2) hook up your graph UI (assumes you added GraphControl under this scene)
		_graph = FindNode("MarketGraph",true,false) as GraphControl;
		
		GD.Print($"MarketGraph: {_graph}");
		
		// Setup trade UI
		_tradePanel = FindNode("TradePanel", true, false) as VBoxContainer;
		CreateTradeUI();

		_goldLabel = FindNode("GoldLabel", true, false) as Label;
		GameState.OnGoldChanged += UpdateGold;
		UpdateGold(GameState.playerGold); // set the initial value
		
		SpawnNewQuests();
		
	}
	public void RepopulateTrackedQuests()
	{
		foreach (Node child in _trackedQuestsContainer.GetChildren())
		{
			child.QueueFree();
		}
		
		_trackedQuestContainers.Clear();
		
		foreach (Quest quest in GameState.activeQuests)
		{
			if (quest._cityTarget == GameState.CurrentCity)
			{
				SpawnQuestUI(quest,true,_trackedQuestsContainer);
			}
		}
	}
	public void SpawnNewQuests()
	{
		foreach (Quest quest in GameState.activeQuests)
		{
			if (quest._cityTarget == GameState.CurrentCity)
			{
				SpawnQuestUI(quest,true,_trackedQuestsContainer);
			}
		}
		
		// spawn an available quest
		SpawnAvailableQuest();
	}
	
	public void SpawnAvailableQuest()
	{
		
		// 1) pick a random good index
		int goodType      = _rng.RandiRange(0, GameState.goodsNames.Count - 1);
		string goodName   = GameState.goodsNames[goodType];
		string city       = GameState.CurrentCity;

		// 2) decide quantity and reward
		int quantityNeeded  = _rng.RandiRange(5, 20);   // inclusive 5–20
		int rewardPerUnit   = _rng.RandiRange(5, 10);   // inclusive 5–10
		int totalRewardGold = quantityNeeded * rewardPerUnit;

		// 3) pick and fill a template
		var choices   = _templates[goodName];
		string tpl    = choices[_rng.RandiRange(0, choices.Length - 1)];
		string text   = tpl
			.Replace("{city}",    city)
			.Replace("{quantity}", quantityNeeded.ToString())
			.Replace("{good}",     goodName);

		// 4) assemble the Quest
		var q = new Quest
		{
			_questName       = $"Deliver {quantityNeeded}× {goodName} to {city}",
			_questText       = text,
			_questMission    = $"SELL 0/{quantityNeeded} {goodName.ToUpper()} IN {city}",
			_questReward     = $"{totalRewardGold} gold",
			_cityTarget      = city,
			_goodType        = goodType,
			_quanityNeeded   = quantityNeeded,
			_questRewardGold = totalRewardGold
			// _quantityDelivered defaults to 0
		};
		SpawnQuestUI(q, false, _questsContainer);
	}
	
	public QuestContainer SpawnQuestUI(Quest quest, bool isTracked, VBoxContainer container)
	{
		var qc = (QuestContainer)QuestContainerScene.Instance();
		// populate it
		qc.showAcceptButton    = !isTracked;
		qc.quest               = quest;
		qc.QuestName           = quest._questName;
		qc.QuestText           = quest._questText;
		qc.QuestMission        = quest._questMission;
		qc.QuestReward         = quest._questReward;
		qc.CityView            = this;
		// add to the scene
		container.AddChild(qc);
		if (isTracked)
		{
			_trackedQuestContainers.Add(qc);
		}
		return qc;
	}
	
	private void UpdateGold(int newGold)
	{
		_goldLabel.Text = "Gold: " + newGold;
	}
	

	public override void _ExitTree()
	{
		GameState.OnGoldChanged -= UpdateGold;
	}
	
	public override void _Process(float delta)
	{
		if (Engine.GetFramesDrawn() % 600 == 0)
		{
			_economy.StepSimulation();
			for (int i = 0; i < GameState.CityEconomies[GameState.CurrentCity]._numGoods; i++){
				UpdatePriceLabels(i);
			}
			
			float[] prices = _economy.GetPrices();
			_graph.AddValues(prices);
			
			GD.Print(
				$"City {Name}: Prices=["
				+ string.Join(", ", prices.Select(p => p.ToString("F2")))
				+ "]"
			);
		}
	}
	
	// Trade UI Setup
	private void CreateTradeUI()
	{
		
		Color hoverColor = normalColor.Lightened(0.1f);   // Slightly lighter
		Color pressedColor = normalColor.Darkened(0.2f);  // Slightly darker

		// Create StyleBoxFlat for Normal
		StyleBoxFlat styleNormal = new StyleBoxFlat();
		styleNormal.BgColor = normalColor;

		// Create StyleBoxFlat for Hover
		StyleBoxFlat styleHover = new StyleBoxFlat();
		styleHover.BgColor = hoverColor;
		
		// Create StyleBoxFlat for Pressed
		StyleBoxFlat stylePressed = new StyleBoxFlat();
		stylePressed.BgColor = pressedColor;
		
		// Create controls for each good
		for (int i = 0; i < GameState.CityEconomies[GameState.CurrentCity]._numGoods; i++)
		{
			var hbox = new HBoxContainer();
			// Label
			var label = new Label { Text = $"{GameState.goodsNames[i]}:" };
			if (LabelFont != null) label.AddFontOverride("font", LabelFont);
			label.AddColorOverride("font_color", LabelColor);
			label.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			hbox.AddChild(label);
			label.SizeFlagsStretchRatio  = 0.7f;

			// Amount spinner
			var amountField = new SpinBox
			{
				MinValue = 1,
				MaxValue = 100,
				Value = 1,
				Step = 1
			};
			
			// Apply the styles to the SpinBox
			amountField.AddStyleboxOverride("up", styleNormal);
			amountField.AddStyleboxOverride("up_hover", styleHover);
			amountField.AddStyleboxOverride("up_pressed", stylePressed);
			
			amountField.AddStyleboxOverride("down", styleNormal);
			amountField.AddStyleboxOverride("down_hover", styleHover);
			amountField.AddStyleboxOverride("down_pressed", stylePressed);
			
			LineEdit lineEdit = amountField.GetLineEdit();
			if (LabelFont != null) lineEdit.AddFontOverride("font", LabelFont);
			lineEdit.AddColorOverride("font_color", LabelColor);
			amountField.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			
			
			hbox.AddChild(amountField);
			_amountFields.Add(amountField);

			// Max Button
			var maxBtn = new Button { Text = "Max" };
			var maxArgs = new Godot.Collections.Array { amountField, i };
			if (LabelFont != null) maxBtn.AddFontOverride("font", LabelFont);
			maxBtn.SizeFlagsStretchRatio  = 0.7f;
			maxBtn.AddColorOverride("font_color", LabelColor);
			maxBtn.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			
			// Apply style overrides
			maxBtn.AddStyleboxOverride("normal", styleNormal);
			maxBtn.AddStyleboxOverride("hover", styleHover);
			maxBtn.AddStyleboxOverride("pressed", stylePressed);
			
			maxBtn.Connect("pressed", this, nameof(OnMaxPressed), maxArgs);
			hbox.AddChild(maxBtn);
			
			// Min Button
			var minBtn = new Button { Text = "Min" };
			var minArgs = new Godot.Collections.Array { amountField, i };
			if (LabelFont != null) minBtn.AddFontOverride("font", LabelFont);
			minBtn.SizeFlagsStretchRatio  = 0.7f;
			minBtn.AddColorOverride("font_color", LabelColor);
			minBtn.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			
			// Apply style overrides
			minBtn.AddStyleboxOverride("normal", styleNormal);
			minBtn.AddStyleboxOverride("hover", styleHover);
			minBtn.AddStyleboxOverride("pressed", stylePressed);
			
			minBtn.Connect("pressed", this, nameof(OnMinPressed), maxArgs);
			hbox.AddChild(minBtn);

			// Buy Button
			var buyBtn = new Button { Text = "Buy" };
			var buyArgs = new Godot.Collections.Array { i, amountField };
			if (LabelFont != null) buyBtn.AddFontOverride("font", LabelFont);
			buyBtn.SizeFlagsStretchRatio  = 0.7f;
			buyBtn.AddColorOverride("font_color", LabelColor);
			buyBtn.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			
			// Apply style overrides
			buyBtn.AddStyleboxOverride("normal", styleNormal);
			buyBtn.AddStyleboxOverride("hover", styleHover);
			buyBtn.AddStyleboxOverride("pressed", stylePressed);
			
			buyBtn.Connect("pressed", this, nameof(OnBuyPressed), buyArgs);
			hbox.AddChild(buyBtn);

			// Sell Button
			var sellBtn = new Button { Text = "Sell" };
			var sellArgs = new Godot.Collections.Array { i, amountField };
			if (LabelFont != null) sellBtn.AddFontOverride("font", LabelFont);
			sellBtn.SizeFlagsStretchRatio  = 0.7f;
			sellBtn.AddColorOverride("font_color", LabelColor);
			sellBtn.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			
			// Apply style overrides
			sellBtn.AddStyleboxOverride("normal", styleNormal);
			sellBtn.AddStyleboxOverride("hover", styleHover);
			sellBtn.AddStyleboxOverride("pressed", stylePressed);
			
			sellBtn.Connect("pressed", this, nameof(OnSellPressed), sellArgs);
			hbox.AddChild(sellBtn);

			
			// Buy price display
			var buyPriceLabel = new Label { Text = _economy.GetBuyPrice(i).ToString("F2") + "("+_economy.GetBuyPrice(i).ToString("F2")+")" };
			buyPriceLabel.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			if (LabelFont != null) buyPriceLabel.AddFontOverride("font", LabelFont);
			buyPriceLabel.AddColorOverride("font_color", LabelColor);
			hbox.AddChild(buyPriceLabel);
			_buyPriceLabels.Add(buyPriceLabel);

			// Sell price display
			var sellPriceLabel = new Label { Text = _economy.GetSellPrice(i).ToString("F2") + "("+_economy.GetBuyPrice(i).ToString("F2")+")" };
			sellPriceLabel.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			if (LabelFont != null) sellPriceLabel.AddFontOverride("font", LabelFont);
			sellPriceLabel.AddColorOverride("font_color", LabelColor);
			hbox.AddChild(sellPriceLabel);
			_sellPriceLabels.Add(sellPriceLabel);
			
			// Owned display
			var ownedGoodsLabel = new Label { Text = GameState.goodsOwned[i].ToString("F2") };
			ownedGoodsLabel.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			if (LabelFont != null) ownedGoodsLabel.AddFontOverride("font", LabelFont);
			ownedGoodsLabel.AddColorOverride("font_color", LabelColor);
			hbox.AddChild(ownedGoodsLabel);
			_goodsOwnedLabels.Add(ownedGoodsLabel);
			
			_tradePanel.AddChild(hbox);
		}
	}
	
	// Updates the buy/sell price labels for a single good
	private void UpdatePriceLabels(int index)
	{
		_buyPriceLabels[index].Text = _economy.GetBuyPrice(index).ToString("F2")+ "("+(_economy.GetBuyPrice(index)*_amountFields[index].Value).ToString("F2")+")";
		_sellPriceLabels[index].Text = _economy.GetSellPrice(index).ToString("F2")+ "("+(_economy.GetSellPrice(index)*_amountFields[index].Value).ToString("F2")+")";
		
		_goodsOwnedLabels[index].Text = GameState.goodsOwned[index].ToString("F2");
	}
	// Signal handlers
	private void OnMaxPressed(SpinBox amountField, int goodIndex)
	{
		amountField.Value = amountField.MaxValue;
		UpdatePriceLabels(goodIndex);
	}
	
	// Signal handlers
	private void OnMinPressed(SpinBox amountField, int goodIndex)
	{
		amountField.Value = amountField.MinValue;
		UpdatePriceLabels(goodIndex);
	}


	private void OnBuyPressed(int goodIndex, SpinBox amountField)
	{
		int purchasePrice = (int)Math.Ceiling(_economy.GetBuyPrice(goodIndex)*amountField.Value);
		if (GameState.playerGold>=purchasePrice){
			_economy.ExternalBuy(goodIndex, 1f, (int)amountField.Value);
			
			// current state
			int oldQty     = GameState.goodsOwned[goodIndex];
			float oldAvg   = GameState.goodsAveragePrice[goodIndex];

			// compute new average cost
			float totalCostOld = oldAvg * oldQty;
			float totalCostNew = purchasePrice * (float)amountField.Value;
			int   newQty       = oldQty + (int)amountField.Value;

			GameState.goodsAveragePrice[goodIndex] = 
				(newQty > 0)
				  ? (totalCostOld + totalCostNew) / newQty
				  : 0f;
			
			GameState.goodsOwned[goodIndex] += (int)amountField.Value;
			GameState.playerGold -= purchasePrice;
			UpdatePriceLabels(goodIndex);
		}
		_buySound.Play();
	}

	private void OnSellPressed(int goodIndex, SpinBox amountField)
	{
		int sellPrice = (int)Math.Ceiling(_economy.GetSellPrice(goodIndex)*amountField.Value);
		
		bool completedOne = false;
		if (GameState.goodsOwned[goodIndex]>=(int)amountField.Value){
			_economy.ExternalSell(goodIndex, 1f, (int)amountField.Value);
			GameState.goodsOwned[goodIndex] -= (int)amountField.Value;
			
			foreach (QuestContainer questContainer in _trackedQuestContainers)
			{
				completedOne = questContainer.quest.DeliveredGoodToCity(goodIndex, (int)amountField.Value, GameState.CurrentCity, questContainer);
			}
			
			GameState.playerGold += sellPrice;
			UpdatePriceLabels(goodIndex);
			
			if (!completedOne)
			{
				_sellSound.Play();
			} 
			else 
			{
				_questCompleteSound.Play();
				RepopulateTrackedQuests();
			}
		}
	}
	public void _on_Button_pressed()
	{
		GameState.CityEconomies[GameState.CurrentCity] = _economy;
		var world = (PackedScene)GD.Load("res://Node2D.tscn");
		GetTree().ChangeSceneTo(world);
	}
	
	private void OnBuyPressed()
	{
		_economy.ExternalBuy(1, (float)1.10, 100);
	}

	private void OnSellPressed()
	{
		_economy.ExternalSell(1, (float)SellMarkdown, 100);
	}
}



