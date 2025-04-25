using Godot;
using System;
using System.Collections.Generic;



public class CityView : Control
{

	
	private Label               _cityNameLabel;
	private TextureRect         _bannerTexture;
	
	private GraphControl _graph;
	private List<Actor>  _actors;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	
	public override void _Ready()
	{
		_cityNameLabel           = FindNode("CityName",true,false) as Label;
		_bannerTexture           = FindNode("TextureRect",true,false) as TextureRect;
		
		_cityNameLabel.Text    =   GameState.CurrentCity;
		_bannerTexture.Texture =   GameState.CityBanner;
		
		
		// 1) pull the list the Cityscript registered:
		_actors = GameState.CityActors[GameState.CurrentCity];

		// 2) hook up your graph UI (assumes you added GraphControl under this scene)
		_graph = FindNode("MarketGraph",true,false) as GraphControl;
		
		GD.Print($"MarketGraph: {_graph}");

		// 3) optional: reseed RNG so that city sim is reproducible
		_rng.Seed = GameState.firstLoadSeed;
		
	}
	
	public override void _Process(float delta)
	{
		StepSimulation();
		// compute average and push to graph:
		double sum = 0;
		foreach (var a in _actors) sum += a.ExpectedMarketValue;
		_graph.AddValue((float)(sum / _actors.Count));
	}

	private void StepSimulation()
	{
		// exactly the same code from your Cityscript.Stepsimulation, but using _actors
		var buyers = new List<Actor>();
		var sellers = new List<Actor>();

		foreach (var a in _actors)
			if (a.IsBuyer()) buyers.Add(a);
			else             sellers.Add(a);

		int matched = Math.Min(buyers.Count, sellers.Count);
		buyers.Shuffle(); sellers.Shuffle();

		for (int i = 0; i < matched; i++)
		{
			var b = buyers[i];
			var s = sellers[i];
			if (b.ExpectedMarketValue >= s.ExpectedMarketValue)
			{
				b.ExpectedMarketValue -= b.BeliefVolatility;
				s.ExpectedMarketValue += s.BeliefVolatility;
			}
			else
			{
				b.ExpectedMarketValue += b.BeliefVolatility;
				s.ExpectedMarketValue -= s.BeliefVolatility;
			}
		}

		for (int i = matched; i < buyers.Count; i++)
			buyers[i].ExpectedMarketValue += buyers[i].BeliefVolatility;
		for (int i = matched; i < sellers.Count; i++)
			sellers[i].ExpectedMarketValue -= sellers[i].BeliefVolatility;
	}
	public void _on_Button_pressed()
	{
		var world = (PackedScene)GD.Load("res://Node2D.tscn");
		GetTree().ChangeSceneTo(world);
	}
}



