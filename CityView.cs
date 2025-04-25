using Godot;
using System;
using System.Collections.Generic;



public class CityView : Control
{

	
	private Label               _cityNameLabel;
	private TextureRect         _bannerTexture;
	
	private GraphControl _graph;
	private MarketSimulator _economy;
	private RandomNumberGenerator _rng;
	private Button          _buyButton;
	private Button          _sellButton;

	// markup & markdown factors
	private const double BuyMarkup  = 1.10;
	private const double SellMarkdown = 0.90;
	
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

		
	}
	
	public override void _Process(float delta)
	{
		if (Engine.GetFramesDrawn() % 60 == 0)
		{
			_economy.StepSimulation();
			_graph.AddValue((float)_economy.AverageExpected);
		}
		
		if (Engine.GetFramesDrawn() % 60 == 0)
		{
			GD.Print(
				$"City {Name}: Agents={_economy.AgentCount}, AvgGoods={_economy.AverageGoodsPerAgent:F2}, " +
				$"TotalGoods={_economy.TotalGoods}, AvgExpected={_economy.AverageExpected:F2}, " +
				$"AvgCurrUtil={_economy.AverageCurrentUtility:F2}, AvgPotUtil={_economy.AveragePotentialUtility:F2}, " +
				$"AvgMoney={_economy.AverageMoney:F2}, Produced={_economy.Produced:F2}, " + 
				$"Consumed={_economy.Consumed:F2}, " 
				 
			);
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
		_economy.ExternalBuy(BuyMarkup, 100);
	}

	private void OnSellPressed()
	{
		_economy.ExternalSell(SellMarkdown, 100);
	}
}



