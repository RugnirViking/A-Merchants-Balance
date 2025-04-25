using Godot;
using System;
using System.Collections.Generic;
using System.Linq;



public class CityView : Control
{

	
	// Label styling
	[Export] public DynamicFont LabelFont;
	[Export] public Color LabelColor = new Color(1,1,1);
	[Export] public Color normalColor = new Color(0.5f, 0.3f, 0.1f); // Normal brown
	
	private Label               _cityNameLabel;
	private TextureRect         _bannerTexture;
	
	private GraphControl _graph;
	private MarketSimulator _economy;
	private RandomNumberGenerator _rng;
	private Button          _buyButton;
	private Button          _sellButton;
	private VBoxContainer _tradePanel;

	// markup & markdown factors
	private const double BuyMarkup  = 1.10;
	private const double SellMarkdown = 0.90;
	
	
	private List<Label> _buyPriceLabels = new List<Label>();
	private List<Label> _sellPriceLabels = new List<Label>();
	
	private List<SpinBox> _amountFields = new List<SpinBox>();
	
	
	public override void _Ready()
	{
		_cityNameLabel           = FindNode("CityName",true,false) as Label;
		_bannerTexture           = FindNode("TextureRect",true,false) as TextureRect;
		
		_buyButton = FindNode("BuyButton", true, false) as Button;
		_sellButton= FindNode("SellButton", true, false) as Button;

		_buyButton.Connect("pressed", this, nameof(OnBuyPressed));
		_sellButton.Connect("pressed", this, nameof(OnSellPressed));
		
		_cityNameLabel.Text    =   GameState.CurrentCity;
		_bannerTexture.Texture =   GameState.CityBanner;
		
		_rng = new RandomNumberGenerator();
		// 3) optional: reseed RNG so that city sim is reproducible
		_rng.Seed = GameState.firstLoadSeed;
		_economy = GameState.CityEconomies[GameState.CurrentCity];

		// 2) hook up your graph UI (assumes you added GraphControl under this scene)
		_graph = FindNode("MarketGraph",true,false) as GraphControl;
		
		GD.Print($"MarketGraph: {_graph}");
		
		// Setup trade UI
		_tradePanel = FindNode("TradePanel", true, false) as VBoxContainer;
		CreateTradeUI();

		
	}
	
	public override void _Process(float delta)
	{
		if (Engine.GetFramesDrawn() % 600 == 0)
		{
			_economy.StepSimulation();
			
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
			var label = new Label { Text = $"Good {i+1}:" };
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
			var ownedGoodsLabel = new Label { Text = "0" };
			ownedGoodsLabel.SizeFlagsHorizontal = (int)Control.SizeFlags.ExpandFill;
			if (LabelFont != null) ownedGoodsLabel.AddFontOverride("font", LabelFont);
			ownedGoodsLabel.AddColorOverride("font_color", LabelColor);
			hbox.AddChild(ownedGoodsLabel);
			_buyPriceLabels.Add(ownedGoodsLabel);
			
			_tradePanel.AddChild(hbox);
		}
	}
	
	// Updates the buy/sell price labels for a single good
	private void UpdatePriceLabels(int index)
	{
		_buyPriceLabels[index].Text = _economy.GetBuyPrice(index).ToString("F2")+ "("+(_economy.GetBuyPrice(index)*_amountFields[index].Value).ToString("F2")+")";
		_sellPriceLabels[index].Text = _economy.GetSellPrice(index).ToString("F2")+ "("+(_economy.GetSellPrice(index)*_amountFields[index].Value).ToString("F2")+")";
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
		_economy.ExternalBuy(goodIndex, 1f, (int)amountField.Value);
		
		UpdatePriceLabels(goodIndex);
	}

	private void OnSellPressed(int goodIndex, SpinBox amountField)
	{
		_economy.ExternalSell(goodIndex, 1f, (int)amountField.Value);
		UpdatePriceLabels(goodIndex);
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



