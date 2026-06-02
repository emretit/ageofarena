using UnityEngine;

/// <summary>
/// Fixed-rate resource exchange driven by the Market building. Sell a commodity
/// (food/wood/stone) for gold, or buy a commodity with gold, in fixed batches.
/// Stateless (no fluctuating commodity price) so it survives match restarts and
/// needs no per-frame tick — <see cref="CommandSystem"/> calls it on a hotkey.
/// Operates on the player's <see cref="ResourceManager"/> (team 0).
/// </summary>
public static class MarketSystem
{
    public const int Batch = 100;       // units moved per trade
    const float SellRate = 0.7f;        // 100 commodity -> 70 gold
    const float BuyRate  = 1.3f;        // 130 gold -> 100 commodity

    /// <summary>Gold received for selling one batch of <paramref name="kind"/>.</summary>
    public static int SellGold => Mathf.RoundToInt(Batch * SellRate);
    /// <summary>Gold paid to buy one batch of any commodity.</summary>
    public static int BuyCost  => Mathf.RoundToInt(Batch * BuyRate);

    /// <summary>Sell a batch of <paramref name="kind"/> for gold. No-op if short.</summary>
    public static void Sell(ResourceManager rm, ResourceKind kind)
    {
        if (rm == null || kind == ResourceKind.Gold) return;
        if (rm.Get(kind) < Batch) return;
        rm.Gain(kind, -Batch);
        rm.Gain(ResourceKind.Gold, SellGold);
    }

    /// <summary>Buy a batch of <paramref name="kind"/> with gold. No-op if short.</summary>
    public static void Buy(ResourceManager rm, ResourceKind kind)
    {
        if (rm == null || kind == ResourceKind.Gold) return;
        if (rm.gold < BuyCost) return;             // guard: ResourceManager doesn't clamp at 0
        rm.Gain(ResourceKind.Gold, -BuyCost);
        rm.Gain(kind, Batch);
    }
}
