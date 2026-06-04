using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Fluctuating-price resource exchange driven by the Market building. Every
/// Sell of a commodity drives its price down; every Buy drives it up. Prices
/// drift back toward the base rate (0.7 sell / 1.3 buy) over time. The spread
/// between sell and buy is always maintained so trading is never free.
/// <see cref="CommandSystem"/> calls Sell/Buy on hotkeys; <see cref="GameManager"/>
/// calls <see cref="Tick"/> each update.
/// </summary>
public static class MarketSystem
{
    public const int Batch = 100;           // units moved per trade

    const float BaseSell = 0.7f;            // base sell rate (commodity → gold ratio)
    const float BaseBuy  = 1.3f;            // base buy  rate
    const float PriceShift = 0.05f;         // rate change per trade
    const float DriftRate  = 0.002f;        // drift back to base per second
    const float MinSell    = 0.3f;
    const float MaxBuy     = 2.5f;

    static readonly float[] _sellRate = { BaseSell, BaseSell, BaseSell }; // Food/Wood/Stone
    static readonly float[] _buyRate  = { BaseBuy,  BaseBuy,  BaseBuy  };

    const float GuildsAdjust = 0.10f;  // MKTT: Guilds narrows the spread by this much each side

    /// <summary>Guilds (player Market tech) raises sell and lowers buy → narrower spread.</summary>
    static float GuildsAdj()
        => (GameManager.Instance?.tech?.HasGuilds ?? false) ? GuildsAdjust : 0f;

    public static int SellGold(ResourceKind k)  => Mathf.RoundToInt(Batch * (_sellRate[Idx(k)] + GuildsAdj()));
    public static int BuyCost(ResourceKind k)   => Mathf.RoundToInt(Batch * (_buyRate[Idx(k)] - GuildsAdj()));

    /// <summary>Drift prices back to base over time. Call once per frame from GameManager.</summary>
    public static void Tick(float dt)
    {
        for (int i = 0; i < 3; i++)
        {
            _sellRate[i] = Mathf.MoveTowards(_sellRate[i], BaseSell, DriftRate * dt);
            _buyRate[i]  = Mathf.MoveTowards(_buyRate[i],  BaseBuy,  DriftRate * dt);
        }
    }

    /// <summary>Sell a batch of <paramref name="kind"/> for gold. No-op if short.</summary>
    public static void Sell(ResourceManager rm, ResourceKind kind)
    {
        if (rm == null || kind == ResourceKind.Gold) return;
        if (rm.Get(kind) < Batch) return;
        rm.Gain(kind, -Batch);
        rm.Gain(ResourceKind.Gold, SellGold(kind));
        int i = Idx(kind);
        _sellRate[i] = Mathf.Max(MinSell, _sellRate[i] - PriceShift);
        _buyRate[i]  = Mathf.Max(_sellRate[i] + 0.2f, _buyRate[i] - PriceShift * 0.5f);
    }

    /// <summary>Buy a batch of <paramref name="kind"/> with gold. No-op if short.</summary>
    public static void Buy(ResourceManager rm, ResourceKind kind)
    {
        if (rm == null || kind == ResourceKind.Gold) return;
        if (rm.gold < BuyCost(kind)) return;
        rm.Gain(ResourceKind.Gold, -BuyCost(kind));
        rm.Gain(kind, Batch);
        int i = Idx(kind);
        _buyRate[i]  = Mathf.Min(MaxBuy, _buyRate[i] + PriceShift);
        _sellRate[i] = Mathf.Min(_buyRate[i] - 0.2f, _sellRate[i] + PriceShift * 0.5f);
    }

    /// <summary>Current prices for HUD display (sell/buy per 100 units).</summary>
    public static (int sell, int buy) Rates(ResourceKind kind)
        => (SellGold(kind), BuyCost(kind));

    static int Idx(ResourceKind k) => k switch
    {
        ResourceKind.Food  => 0,
        ResourceKind.Wood  => 1,
        ResourceKind.Stone => 2,
        _                  => 0,
    };
}
