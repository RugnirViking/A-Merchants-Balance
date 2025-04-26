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
	
	[Export] public int NumGoods = 8;
	[Export] public float[] MidPrice = { 10f, 12f, 8f, 14f, 6f, 20f, 5f, 9f };
	[Export] public float[] Stickiness = { 2f, 8f, 4f, 1f, 0.5f, 10f, 3f, 6f };
	[Export] public float BuyImpact = 0.25f;
	[Export] public float SellImpact = 0.25f;
	[Export] public float Drift = 0.2f;
	[Export] public float CrossInfluence = 0.05f;
	[Export] public float SpreadFraction = 0.1f;
	
	

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
			_economy = new MarketSimulator(
				NumGoods, MidPrice, Stickiness, BuyImpact, SellImpact,
				Drift, CrossInfluence, SpreadFraction
			);
			GameState.CityEconomies[Name] = _economy;
		}
	}

	public override void _Process(float delta)
	{
		if (Engine.GetFramesDrawn() % 600 == 0)
		{
			_economy.StepSimulation();
		}
	}
}


public class Actor
{
	public double[] BaseGoodValue;            // "S" parameter per good
	public double[] ExpectedMarketValue;      // belief of price per good
	public int[] GoodsCount;                  // holdings per good
	public double BeliefVolatility = 0.4;

	public double BaseWorkValue;
	public double BaseLeisureValue;
	public double Hunger = 0;

	public bool[] didProduce;
	public int[] didConsume;

	private double[] _halfPersonalAt;         // "D" per good
	private RandomNumberGenerator _rng;

	public Actor(int numGoods,
				 double[] basePV,
				 double[] expV,
				 int[] initGoods,
				 int initMoney,
				 double[] halfAt,
				 double baseWorkValue,
				 double baseLeisureValue,
				 RandomNumberGenerator rng)
	{
		BaseGoodValue       = basePV;
		ExpectedMarketValue = expV;
		GoodsCount          = initGoods;
		Money               = initMoney;
		_halfPersonalAt     = halfAt;
		BaseWorkValue       = baseWorkValue;
		BaseLeisureValue    = baseLeisureValue;
		_rng                = rng;
	}

	public int Money { get; set; }

	// diminishing utility: U(S, x, D) = S / ((x/D)^3 + 1)
	private double Utility(double S, double x, double D)
		=> S / (Math.Pow(x / D, 3) + 1);

	// Sum of current utilities over goods
	public double CurrentUtility()
		=> Enumerable.Range(0, GoodsCount.Length)
					 .Sum(i => Utility(BaseGoodValue[i], GoodsCount[i], _halfPersonalAt[i]));

	// Sum of potential utilities if acquiring one more of each good
	public double PotentialUtility(int goodIndex)
		=> Utility(BaseGoodValue[goodIndex], GoodsCount[goodIndex] + 1, _halfPersonalAt[goodIndex]);

	public bool IsBuyer(int goodIndex)
		=> ExpectedMarketValue[goodIndex] < PotentialUtility(goodIndex);

	public bool IsSeller(int goodIndex)
		=> ExpectedMarketValue[goodIndex] >= Utility(BaseGoodValue[goodIndex], GoodsCount[goodIndex], _halfPersonalAt[goodIndex]);

	public bool CanTrade(int goodIndex, IEnumerable<Actor> all)
		=> all.Any(o => o.IsSeller(goodIndex)
					 && o.GoodsCount[goodIndex] > 0
					 && ExpectedMarketValue[goodIndex] >= o.ExpectedMarketValue[goodIndex]
					 && Money >= ExpectedMarketValue[goodIndex]);

	// Decide action: produce, consume, or prepare to trade (for each good)
	public void DecideAction(int workGoodsPerStep, double halfMoneyUtil, double halfLeisureUtil)
	{
		didProduce = new bool[GoodsCount.Length];
		didConsume = new int[GoodsCount.Length];

		double uTradeMax = ExpectedMarketValue.Max();             // highest price belief
		double uWork      = BaseWorkValue;
		double uLeisure   = BaseLeisureValue;
		
		// 1) compute current utility of each good
		double[] uGoods = new double[GoodsCount.Length];
		for (int i = 0; i < GoodsCount.Length; i++)
			uGoods[i] = Utility(BaseGoodValue[i], GoodsCount[i], _halfPersonalAt[i]);

		// 2) find the good with the highest utility
		int bestIdx = 0;
		double bestUtil = uGoods[0];
		for (int i = 1; i < uGoods.Length; i++)
		{
			if (uGoods[i] > bestUtil)
			{
				bestUtil = uGoods[i];
				bestIdx = i;
			}
		}
		// 3) compare that best‐good utility against trade and leisure
		if (bestUtil <= uTradeMax && bestUtil >= uLeisure)
		{
			// produce one unit of the highest‐utility good
			GoodsCount[bestIdx] += workGoodsPerStep;
			didProduce[bestIdx] = true;
		}
		
		ApplyBreakage(0.005); // 5% chance per item to break
	}
	
	public void ApplyBreakage(double breakProbability)
	{
		for (int i = 0; i < GoodsCount.Length; i++)
		{
			int broken = 0;
			for (int j = 0; j < GoodsCount[i]; j++)
			{
				if (_rng.Randf() < breakProbability)
				{
					broken++;
				}
			}
			GoodsCount[i] -= broken;
			didConsume[i] += broken;
			if (GoodsCount[i] < 0) GoodsCount[i] = 0; // Safety check
		}
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
/// <summary>
/// Simple price-only simulator: tracks mid-market prices, applies drift,
/// player impact, and cross-commodity influence.
/// </summary>
public class MarketSimulator
{
	public int _numGoods;
	public float[] MidPrice;
	private float[] BasePrice;
	private float[] Stickiness;
	private float _buyImpact;
	private float _sellImpact;
	private float _drift;
	private float _crossInfluence;
	private float _spreadFraction;
	private Random _rng = new Random();

	public MarketSimulator(int numGoods, float[] initialPrices, float[] stickiness,
								float buyImpact, float sellImpact,
								float drift, float crossInfluence,
								float spreadFraction)
	{
		_numGoods = numGoods;
		MidPrice = (float[])initialPrices.Clone();
		BasePrice = (float[])initialPrices.Clone();
		Stickiness = (float[])stickiness.Clone();
		_buyImpact = buyImpact;
		_sellImpact = sellImpact;
		_drift = drift;
		_crossInfluence = crossInfluence;
		_spreadFraction = spreadFraction;
	}
	
	// Convert this simulator into a serializable Dictionary
	public Godot.Collections.Dictionary ToDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			["numGoods"]       = _numGoods,
			["midPrice"]       = new Godot.Collections.Array(MidPrice),
			["basePrice"]      = new Godot.Collections.Array(BasePrice),
			["stickiness"]     = new Godot.Collections.Array(Stickiness),
			["buyImpact"]      = _buyImpact,
			["sellImpact"]     = _sellImpact,
			["drift"]          = _drift,
			["crossInfluence"] = _crossInfluence,
			["spreadFraction"] = _spreadFraction
		};
	}

	// Rebuild a MarketSimulator from one of those Dictionaries
	public static MarketSimulator FromDictionary(Godot.Collections.Dictionary data)
	{
		var sim = new MarketSimulator(
			Convert.ToInt32(data["numGoods"]),
			ArrayFrom(data["basePrice"] as Godot.Collections.Array),
			ArrayFrom(data["stickiness"] as Godot.Collections.Array),
			Convert.ToSingle(data["buyImpact"]),
			Convert.ToSingle(data["sellImpact"]),
			Convert.ToSingle(data["drift"]),
			Convert.ToSingle(data["crossInfluence"]),
			Convert.ToSingle(data["spreadFraction"])
		);

		sim.MidPrice         = ArrayFrom(data["midPrice"] as Godot.Collections.Array);

		return sim;
	}

	private static float[] ArrayFrom(Godot.Collections.Array arr)
	{
		var outF = new float[arr.Count];
		for (int i = 0; i < arr.Count; i++)
			outF[i] = Convert.ToSingle(arr[i]);
		return outF;
	}
	
	public void StepSimulation()
	{
		float[] old = (float[])MidPrice.Clone();

		// 1) random drift
		for (int i = 0; i < _numGoods; i++)
		{
			float d = (float)(_rng.NextDouble() * 2 - 1) * _drift;
			MidPrice[i] += d;
		}

		// 2) cross-commodity bleed
		for (int i = 0; i < _numGoods; i++)
			for (int j = 0; j < _numGoods; j++)
				if (i != j)
				{
					float deltaJ = MidPrice[j] - old[j];
					int sign = _rng.NextDouble() < 0.5 ? -1 : 1;
					MidPrice[i] += deltaJ * _crossInfluence * sign;
				}

		// 3) Drift towards baseprice (faster if further out)
		for (int i = 0; i < _numGoods; i++)
		{
			float diff = BasePrice[i] - MidPrice[i];
			MidPrice[i] += diff/ (Stickiness[i]*2);
		}
		
		// 4) clamp
		for (int i = 0; i < _numGoods; i++)
		{
			MidPrice[i] = Mathf.Max(MidPrice[i], 0.01f);
			MidPrice[i] = Mathf.Min(MidPrice[i], BasePrice[i]*2.5f);
		}
	}

	/// <summary>
	/// External buy: uses log scaling for diminishing returns on size,
	/// plus resistance as price deviates.
	/// </summary>
	public void ExternalBuy(int goodIndex, float markup, int amount = 1)
	{
		int i = goodIndex;
		float deviation = Mathf.Abs(MidPrice[i] - BasePrice[i]) / BasePrice[i];
		float resistance = 1f / (1f + deviation*Stickiness[i]);
		float sizeFactor = Mathf.Log(1 + amount * markup);
		float delta = _buyImpact * sizeFactor * resistance;
		MidPrice[i] += delta;
		MidPrice[i] = Mathf.Max(MidPrice[i], 0.01f);
	}

	/// <summary>
	/// External sell: mirror of buy with log scaling and resistance.
	/// </summary>
	public void ExternalSell(int goodIndex, float markdown, int amount = 1)
	{
		int i = goodIndex;
		float deviation = Mathf.Abs(MidPrice[i] - BasePrice[i]) / BasePrice[i];
		float resistance = 1f / (1f + deviation*Stickiness[i]);
		float sizeFactor = Mathf.Log(1 + amount * markdown);
		float delta = _sellImpact * sizeFactor * resistance;
		MidPrice[i] -= delta;
		MidPrice[i] = Mathf.Max(MidPrice[i], 0.01f);
	}
	/// <summary>
	/// Returns the current mid-market prices for graphing/logging
	/// </summary>
	public float[] GetPrices()
	{
		return (float[])MidPrice.Clone();
	}


	public float GetBuyPrice(int i)    => MidPrice[i] * (1 + _spreadFraction);
	public float GetSellPrice(int i)   => MidPrice[i] * (1 - _spreadFraction);
}

