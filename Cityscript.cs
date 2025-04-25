using Godot;
using System;
using System.Collections.Generic;
public static class ListExtensions
{
	private static Random _rng = new Random();
	public static void Shuffle<T>(this IList<T> list)
	{
		int n = list.Count;
		while (n > 1)
		{
			int k = _rng.Next(n--);
			var tmp = list[n];
			list[n] = list[k];
			list[k] = tmp;
		}
	}
}
public class Cityscript : Position2D
{
	[Export] public string AtlasKey = "citymarker1";
	[Export] public bool FlipX = false;
	[Export] public Texture bannerTex;
	[Export] public int NumActors = 200;
	[Export] public double MinPersonal = 1.0;
	[Export] public double MaxPersonal = 20.0;
	[Export] public double MinExpected = 1.0;
	[Export] public double MaxExpected = 20.0;

	public List<Actor> _actors = new List<Actor>();
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		
		CallDeferred(nameof(InitializeEconomy));
	}
	
	public void InitializeEconomy()
	{
		_rng.Seed = GameState.firstLoadSeed;
		ulong seedOffset = (ulong)Position.x * 92821 + (ulong)Position.y * 68917;
		_rng.Seed += seedOffset;

		_actors.Clear();
		if (GameState.CityActors.ContainsKey(Name))
		{
			GD.Print($"Loading cached actors for {Name}");
			_actors = GameState.CityActors[Name];
		}
		else
		{
			for (int i = 0; i < NumActors; i++)
			{
				double pv = _rng.RandfRange((float) MinPersonal, (float) MaxPersonal);
				double ev = _rng.RandfRange((float) MinExpected, (float) MaxExpected);
				_actors.Add(new Actor(pv, ev));
			}
		}
	}
	
	public override void _Process(float delta)
	{
		// debug: print average expected value every second
		if (Engine.GetFramesDrawn() % 600 == 0)
		{
			
			StepSimulation();
			double avg = 0;
			foreach (var a in _actors) avg += a.ExpectedMarketValue;
			GD.Print($"Avg Expected in city {Name}: {avg / _actors.Count:F2}");
		}
	}

	private void StepSimulation()
	{
		// 1) separate buyers and sellers
		var buyers = new List<Actor>();
		var sellers = new List<Actor>();
		foreach (var a in _actors)
		{
			if (a.IsBuyer()) buyers.Add(a);
			else             sellers.Add(a);
		}

		// 2) match as many as possible
		int matched = Math.Min(buyers.Count, sellers.Count);
		// shuffle lists for random matching
		buyers.Shuffle(); 
		sellers.Shuffle();

		for (int i = 0; i < matched; i++)
		{
			var buyer  = buyers[i];
			var seller = sellers[i];
			if (buyer.ExpectedMarketValue >= seller.ExpectedMarketValue)
			{
				// transaction succeeded
				buyer.ExpectedMarketValue  -= buyer.BeliefVolatility;
				seller.ExpectedMarketValue += seller.BeliefVolatility;
			}
			else
			{
				// failed
				buyer.ExpectedMarketValue  += buyer.BeliefVolatility;
				seller.ExpectedMarketValue -= seller.BeliefVolatility;
			}
		}

		// 3) unmatched adjust their beliefs
		for (int i = matched; i < buyers.Count; i++)
			buyers[i].ExpectedMarketValue += buyers[i].BeliefVolatility;
		for (int i = matched; i < sellers.Count; i++)
			sellers[i].ExpectedMarketValue -= sellers[i].BeliefVolatility;
	}
}
public class Actor
{
	public double PersonalValue;         // how much this actor “values” the good
	public double ExpectedMarketValue;   // what price they expect in the market
	public double BeliefVolatility = 0.1;// how much they adjust after each (failed) transaction

	public Actor(double personal, double expected)
	{
		PersonalValue       = personal;
		ExpectedMarketValue = expected;
	}

	public bool IsBuyer()  => ExpectedMarketValue < PersonalValue;
	public bool IsSeller() => ExpectedMarketValue >= PersonalValue;
}
