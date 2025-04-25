using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

//-----------------------------------
// Cityscript uses the shared simulator
//-----------------------------------
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
	[Export] public int InitialMoney = 100;
	[Export] public int InitialGoods = 5;
	[Export] public double HalfPersonalValueAt = 5.0;
	[Export] public double HalfMoneyUtility = 25.0;
	[Export] public double HalfLeisureUtility = 3.0;
	[Export] public int WorkGoods = 1;
	
	

	private MarketSimulator _economy;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	public override void _Ready()
	{
		// seed RNG globally + city offset
		_rng.Seed = GameState.firstLoadSeed;
		ulong offset = (ulong)Position.x * 92821 + (ulong)Position.y * 68917;
		_rng.Seed += offset;
		if (GameState.CityEconomies.TryGetValue(Name, out var existing))
		{
			_economy = existing;
		}
		else
		{
			// instantiate and cache
			_economy = new MarketSimulator(
				Name, _rng,
				NumActors,
				MinPersonal, MaxPersonal,
				MinExpected, MaxExpected,
				InitialMoney, InitialGoods,
				HalfPersonalValueAt
			);
			GameState.CityEconomies[Name] = _economy;
		}
	}

	public override void _Process(float delta)
	{
		if (Engine.GetFramesDrawn() % 600 == 0)
		{
			_economy.StepSimulation();
			GD.Print($"Avg Expected in {Name}: {_economy.AverageExpected:F2}");
		}
	}
}


//-----------------------------------
// Actor with multi-utility and decisions
//-----------------------------------
public class Actor
{
	public double BaseGoodValue;
	public double ExpectedMarketValue;
	public double BeliefVolatility = 0.1;
	public int GoodsCount;
	public int Money;

	// New:
	public double Hunger = 0;
	public int LeisureCount = 0;

	
	public Actor(
		double baseGood, double exp,
		int initGoods, int initMoney,
		double halfPersonalAt)
	{
		BaseGoodValue = baseGood;
		ExpectedMarketValue = exp;
		GoodsCount = initGoods;
		Money = initMoney;
	}

	private double Utility(double baseVal, double x, double halfAt)
		=> baseVal / (Math.Pow(x / halfAt, 3) + 1);

		
	public double CurrentGoodUtility() => Utility(BaseGoodValue, GoodsCount,3);
	public double PotentialGoodUtility() => Utility(BaseGoodValue, GoodsCount + 1,3);
	
	public bool IsBuyer() => ExpectedMarketValue < PotentialGoodUtility();
	public bool IsSeller() => ExpectedMarketValue >= CurrentGoodUtility();
	public bool CanTrade(IEnumerable<Actor> all)
		=> all.Any(o => o.IsSeller() && o.GoodsCount > 0
					   && ExpectedMarketValue >= o.ExpectedMarketValue
					   && Money >= o.ExpectedMarketValue);
}







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

public class MarketSimulator
{
	public List<Actor> Actors { get; private set; } = new List<Actor>();
	private RandomNumberGenerator _rng;

	// Simulation parameters
	private readonly int _initialMoney;
	private readonly int _initialGoods;
	private readonly double _minPersonal, _maxPersonal;
	private readonly double _minExpected, _maxExpected;
	private readonly double _halfPersonalValueAt;

	public MarketSimulator(
		string cityName,
		RandomNumberGenerator rng,
		int numActors,
		double minPersonal, double maxPersonal,
		double minExpected, double maxExpected,
		int initialMoney, int initialGoods,
		double halfAt)
	{
		_rng                    = rng;
		_initialMoney           = initialMoney;
		_initialGoods           = initialGoods;
		_minPersonal            = minPersonal;
		_maxPersonal            = maxPersonal;
		_minExpected            = minExpected;
		_maxExpected            = maxExpected;
		_halfPersonalValueAt    = halfAt;

		SeedAndPopulate(cityName, numActors);
	}

	private void SeedAndPopulate(string cityName, int numActors)
	{
		for (int i = 0; i < numActors; i++)
		{
			double basePV = _rng.RandfRange((float)_minPersonal, (float)_maxPersonal);
			double expV    = _rng.RandfRange((float)_minExpected, (float)_maxExpected);
			int initGoods  = _rng.RandiRange(0, _initialGoods);
			int initMoney  = _initialMoney;
			Actors.Add(new Actor(basePV, expV, initGoods, initMoney, _halfPersonalValueAt));
		}
	}

	public void StepSimulation()
	{
		// 1) match buyers & sellers
		var buyers  = Actors.Where(a => a.IsBuyer()).ToList();
		var sellers = Actors.Where(a => a.IsSeller()).ToList();

		buyers.Shuffle(); sellers.Shuffle();
		int matched = Math.Min(buyers.Count, sellers.Count);

		for (int i = 0; i < matched; i++)
		{
			var buyer  = buyers[i];
			var seller = sellers[i];
			double price = seller.ExpectedMarketValue;

			if (buyer.Money >= price && seller.GoodsCount > 0 && buyer.ExpectedMarketValue >= price)
			{
				// execute trade
				buyer.GoodsCount++;
				buyer.Money     -= (int)price;
				seller.GoodsCount--;
				seller.Money     += (int)price;
				buyer.ExpectedMarketValue  -= buyer.BeliefVolatility;
				seller.ExpectedMarketValue += seller.BeliefVolatility;
			}
			else
			{
				// failed transaction
				buyer.ExpectedMarketValue  += buyer.BeliefVolatility;
				seller.ExpectedMarketValue -= seller.BeliefVolatility;
			}
		}

		// 2) unmatched belief updates
		foreach (var b in buyers.Skip(matched))
			if (b.CanTrade(Actors))
				b.ExpectedMarketValue += b.BeliefVolatility;
		foreach (var s in sellers.Skip(matched))
			if (s.GoodsCount > 0)
				s.ExpectedMarketValue -= s.BeliefVolatility;

		// 3) gossip/rumor
		RumorStep();
		// 4) global market signal (median personal utility)
		GlobalSignalStep();
	}

	// Rumor step: actors hear from a random peer
	private void RumorStep()
	{
		foreach (var actor in Actors)
		{
			int idx = _rng.RandiRange(0, Actors.Count - 1);
			var peer = Actors[idx];
			double rumorPrice = peer.ExpectedMarketValue;
			if (rumorPrice > actor.ExpectedMarketValue)
				actor.ExpectedMarketValue += actor.BeliefVolatility;
			else if (rumorPrice < actor.ExpectedMarketValue)
				actor.ExpectedMarketValue -= actor.BeliefVolatility;
		}
	}

	// Global signal: nudge toward median personal utility
	private void GlobalSignalStep()
	{
		// median of current utilities
		var utilities = Actors.Select(a => a.CurrentGoodUtility()).OrderBy(u => u).ToList();
		double median;
		int n = utilities.Count;
		if (n % 2 == 1) median = utilities[n/2];
		else median = (utilities[n/2 - 1] + utilities[n/2]) / 2.0;

		foreach (var actor in Actors)
		{
			if (median > actor.ExpectedMarketValue)
				actor.ExpectedMarketValue += actor.BeliefVolatility;
			else if (median < actor.ExpectedMarketValue)
				actor.ExpectedMarketValue -= actor.BeliefVolatility;
		}
	}

	// External market interactions via UI
	public void ExternalBuy(double markup, int amount)
	{
		for (int i = 0; i < amount; i++)
		{
			var candidate = Actors
				.Where(a => a.GoodsCount > 0)
				.OrderBy(a => a.ExpectedMarketValue)
				.FirstOrDefault();
			if (candidate == null) break;

			double basePrice = candidate.ExpectedMarketValue;
			double price     = basePrice * markup;

			// remove good => increases marginal utility
			candidate.GoodsCount--;
			candidate.Money     += (int)price;
			candidate.ExpectedMarketValue += candidate.BeliefVolatility;

			// immediate rumor + global
			RumorStep();
			GlobalSignalStep();
		}
	}

	public void ExternalBuy(double markup) => ExternalBuy(markup, 1);

	public void ExternalSell(double markdown, int amount)
	{
		for (int i = 0; i < amount; i++)
		{
			var candidate = Actors
				.Where(a => a.Money > 0)
				.OrderByDescending(a => a.ExpectedMarketValue)
				.FirstOrDefault();
			if (candidate == null) break;

			double basePrice = candidate.ExpectedMarketValue;
			double price     = basePrice * markdown;

			// add good => decreases marginal utility
			candidate.GoodsCount++;
			candidate.Money     -= (int)price;
			candidate.ExpectedMarketValue -= candidate.BeliefVolatility;

			// immediate rumor + global
			RumorStep();
			GlobalSignalStep();
		}
	}

	public void ExternalSell(double markdown) => ExternalSell(markdown, 1);

	// Stats
	public int AgentCount => Actors.Count;
	public double AverageGoodsPerAgent => Actors.Average(a => a.GoodsCount);
	public int TotalGoods => Actors.Sum(a => a.GoodsCount);
	public double AverageExpected => Actors.Average(a => a.ExpectedMarketValue);
	public double AverageMoney => Actors.Average(a => a.Money);
	public double AverageCurrentUtility => Actors.Average(a => a.CurrentGoodUtility());
	public double AveragePotentialUtility => Actors.Average(a => a.PotentialGoodUtility());
}
