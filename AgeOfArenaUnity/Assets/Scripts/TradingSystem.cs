using UnityEngine;

/// <summary>
/// Drives Trade Cart units (UnitType.TradeCart). Each cart finds the nearest
/// enemy-or-neutral Market on the map, walks to it, and returns to the player's
/// own Market to deposit a gold bonus proportional to the distance travelled.
/// One cart produces roughly 10–30 gold per round-trip depending on map distance.
/// Ticked from GameManager.
/// </summary>
public class TradingSystem : MonoBehaviour
{
    const float TradeGoldPerUnit = 0.18f; // gold earned per world-unit of travel
    const float MinGold          = 8f;
    const float DepositRange     = 4f;

    enum TradeState { SeekingMarket, Travelling, Returning }

    public void Tick(System.Collections.Generic.List<UnitEntity> units,
                     System.Collections.Generic.List<BuildingEntity> buildings,
                     float dt)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u == null || u.type != UnitType.TradeCart) continue;
            StepCart(u, buildings);
        }
    }

    void StepCart(UnitEntity u, System.Collections.Generic.List<BuildingEntity> buildings)
    {
        // Trade state in patrolA (home market) / patrolB (target market) + tradeActive /
        // tradeReturning. Driven entirely here (NOT the patrol auto-flip), so the cart is
        // paid exactly once per ROUND TRIP — on arrival back at the home market (AoE2),
        // not on the outbound leg, and never twice.
        if (!u.tradeActive)
        {
            // Phase 1: find home + target market, head out to the target.
            BuildingEntity home   = NearestMarket(buildings, u.transform.position, u.teamId,  true);
            BuildingEntity target = NearestMarket(buildings, u.transform.position, u.teamId, false);
            if (home == null || target == null) return;
            u.patrolA        = home.transform.position;
            u.patrolB        = target.transform.position;
            u.tradeActive    = true;
            u.tradeReturning = false;
            u.MoveTo(u.patrolB);
            return;
        }

        if (!u.tradeReturning)
        {
            // Phase 2 (outbound): reached the target market? pick up goods, head home.
            if (Vector3.Distance(u.transform.position, u.patrolB) < DepositRange)
            {
                u.tradeReturning = true;
                u.MoveTo(u.patrolA);
            }
            return;
        }

        // Phase 3 (inbound): reached the home market? deposit the gold and loop.
        if (Vector3.Distance(u.transform.position, u.patrolA) < DepositRange)
        {
            float tripDist = Vector3.Distance(u.patrolA, u.patrolB);
            float earnedF  = Mathf.Max(MinGold, tripDist * TradeGoldPerUnit);
            var   gm       = GameManager.Instance;
            if (gm != null)
            {
                // CARA: Market Caravan tech boosts trade-cart yield (×1.5). The same
                // distance-based route also models a Dock-based Trade Cog (water route)
                // — naval trade reuses this StepCart logic once a water map ships.
                earnedF *= gm.teamTech[u.teamId].TradeGoldMult;
                gm.teamRes[u.teamId].Gain(ResourceKind.Gold, Mathf.RoundToInt(earnedF));
            }
            // Re-find markets next tick (handles a market being built/destroyed) and repeat.
            u.tradeActive = false;
        }
    }

    static BuildingEntity NearestMarket(System.Collections.Generic.List<BuildingEntity> buildings,
                                        Vector3 from, int teamId, bool ownTeam)
    {
        BuildingEntity best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            if (b == null || b.type != BuildingType.Market || b.underConstruction) continue;
            if (ownTeam ? b.teamId != teamId : b.teamId == teamId) continue;
            float sq = (b.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }
}
