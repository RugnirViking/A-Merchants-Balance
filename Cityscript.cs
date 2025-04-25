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
				HalfPersonalValueAt, 1, HalfMoneyUtility, HalfLeisureUtility, 0.25
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
	public double BaseWorkValue;
	public double BaseLeisureValue;
	public int GoodsCount;
	public int Money;
	public double Hunger = 0;
	private RandomNumberGenerator _rng;
	
	public bool didProduce = false;
	public bool didConsume = false;

	private double _halfPersonalAt;

	public Actor(double basePV, double exp, int goods, int money, double halfAt, double baseWorkValue, double baseLeisureValue, RandomNumberGenerator rng)
	{
		BaseGoodValue = basePV;
		ExpectedMarketValue = exp;
		GoodsCount = goods;
		Money = money;
		_halfPersonalAt = halfAt;
		BaseWorkValue = baseWorkValue;
		BaseLeisureValue = baseLeisureValue;
		_rng = rng;
	}

	// diminishing utility: U(x) = S/( (x/D)^3 +1 )
	private double Utility(double S, double x, double D)
		=> S / (Math.Pow(x / D, 3) + 1);

	public double CurrentGoodUtility()   => Utility(BaseGoodValue, GoodsCount, _halfPersonalAt);
	public double PotentialGoodUtility() => Utility(BaseGoodValue, GoodsCount+1, _halfPersonalAt);


	public bool IsBuyer()  => ExpectedMarketValue < PotentialGoodUtility();
	public bool IsSeller() => ExpectedMarketValue >= CurrentGoodUtility();
	public bool CanTrade(IEnumerable<Actor> all)
		=> all.Any(o => o.IsSeller() && o.GoodsCount>0 && ExpectedMarketValue>=o.ExpectedMarketValue && Money>=o.ExpectedMarketValue);

	// Action choices
	public void DecideAction(MarketSimulator sim, int workGoods, double halfMoneyUtil, double halfLeisureUtil)
	{
		// utilities
		double uTrade = ExpectedMarketValue; // proxy: price signal
		double uWork  = BaseWorkValue;
		double uLeisure  = BaseLeisureValue;
		
		didProduce = false;
		didConsume = false;
		
		// pick max
		if (uWork <= uTrade && CurrentGoodUtility()>=uLeisure)
		{
			// work: gain goods
			GoodsCount += workGoods;
			didProduce = true;
		}
		else if (Hunger >= ExpectedMarketValue && GoodsCount>0)
		{
			GoodsCount-=1; // nom nom nom bye bye, good
			Hunger = 0;
			didConsume = true;
		}
		// trade: join market (nothing extra here)
	}
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
	private readonly int _workGoods;
	private readonly double _halfMoneyUtility;
	private readonly double _halfLeisureUtility;
	private readonly double _hungerRate;

	public MarketSimulator(
		string cityName,
		RandomNumberGenerator rng,
		int numActors,
		double minPersonal, double maxPersonal,
		double minExpected, double maxExpected,
		int initialMoney, int initialGoods,
		double halfAt,
		int workGoods,
		double halfMoneyUtil,
		double halfLeisureUtil,
		double hungerRatePerStep)
	{
		_rng                    = rng;
		_initialMoney           = initialMoney;
		_initialGoods           = initialGoods;
		_minPersonal            = minPersonal;
		_maxPersonal            = maxPersonal;
		_minExpected            = minExpected;
		_maxExpected            = maxExpected;
		_halfPersonalValueAt    = halfAt;
		_workGoods              = workGoods;
		_halfMoneyUtility       = halfMoneyUtil;
		_halfLeisureUtility     = halfLeisureUtil;
		_hungerRate             = hungerRatePerStep;

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
			double workBasePV    = _rng.RandfRange((float)_minPersonal+1.5f, (float)_maxPersonal+1.5f);
			double leisureBasePV = _rng.RandfRange((float)_minPersonal-1.5f, (float)_maxPersonal-1.5f);
			Actors.Add(new Actor(basePV, expV, initGoods, initMoney, _halfPersonalValueAt, workBasePV, leisureBasePV, _rng));
		}
	}

	public void StepSimulation()
	{
		// 0) Each actor ages hunger and decides action
		foreach (var actor in Actors)
		{
			actor.Hunger += _hungerRate;
			actor.DecideAction(this, _workGoods, _halfMoneyUtility, _halfLeisureUtility);
		}

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
				buyer.GoodsCount++;
				buyer.Money     -= (int)price;
				seller.GoodsCount--;
				seller.Money     += (int)price;
				buyer.ExpectedMarketValue  -= buyer.BeliefVolatility;
				seller.ExpectedMarketValue += seller.BeliefVolatility;
			}
			else
			{
				buyer.ExpectedMarketValue  += buyer.BeliefVolatility;
				seller.ExpectedMarketValue -= seller.BeliefVolatility;
			}
		}

		// 2) unmatched belief updates
		foreach (var b in buyers.Skip(matched))
			if (b.CanTrade(Actors)) b.ExpectedMarketValue += b.BeliefVolatility;
		foreach (var s in sellers.Skip(matched))
			if (s.GoodsCount > 0)  s.ExpectedMarketValue -= s.BeliefVolatility;

		// 3) gossip/rumor
		RumorStep();
		// 4) global market signal (median personal utility)
		GlobalSignalStep();
	}

	private void RumorStep()
	{
		foreach (var actor in Actors)
		{
			var peer = Actors[_rng.RandiRange(0, Actors.Count - 1)];
			double rumor = peer.ExpectedMarketValue;
			actor.ExpectedMarketValue += Math.Sign(rumor - actor.ExpectedMarketValue) * actor.BeliefVolatility;
		}
	}

	private void GlobalSignalStep()
	{
		var utilities = Actors.Select(a => a.CurrentGoodUtility()).OrderBy(u => u).ToList();
		int n = utilities.Count;
		double median = (n%2==1) ? utilities[n/2] : (utilities[n/2-1]+utilities[n/2])/2.0;
		foreach (var actor in Actors)
			actor.ExpectedMarketValue += Math.Sign(median - actor.ExpectedMarketValue) * actor.BeliefVolatility;
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
	public double Produced => Actors.Count(a => a.didProduce);
	public double Consumed => Actors.Count(a => a.didConsume);
}
