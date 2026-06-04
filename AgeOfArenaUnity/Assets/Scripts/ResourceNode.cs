using UnityEngine;

/// <summary>
/// A harvestable resource node (tree / gold mine / stone mine). Attached to the
/// root GameObject produced by <see cref="ResourceFactory"/>, which also adds a
/// collider so the node can be picked by selection/command raycasts.
/// </summary>
public class ResourceNode : MonoBehaviour
{
    public ResourceKind kind;
    public int amount;
    public int maxAmount;
    public int gathererCap = 6;
    public int currentGatherers;

    /// <summary>Trees/mines disappear when emptied; farm fields stay as a building.</summary>
    public bool destroyOnDeplete = true;

    // Auto-reseed (farms): when emptied, spend wood from the owner's ledger to
    // refill back to maxAmount. Off by default; ResourceFactory.FarmField enables it.
    public bool renewable;
    public int reseedWoodCost;
    public int ownerTeamId;

    // Slow decay: idle farms lose food over time even when not harvested, forcing
    // the player to tend/reseed. 0 = no decay (trees/mines).
    public float decayPerSecond;

    public bool Depleted => amount <= 0;
    public bool HasRoom => currentGatherers < gathererCap;

    public void Init(ResourceKind kind, int amount)
    {
        this.kind = kind;
        this.amount = amount;
        this.maxAmount = amount;
    }

    float _decayAccum;   // fractional decay carry (so sub-1.0/tick decay still applies)

    /// <summary>Advance reseed + decay. Called from <see cref="GameManager.SimTick"/> with the
    /// sim dt — NOT a MonoBehaviour Update — so farm food economy is part of the deterministic,
    /// fixed-step tick stream (and frame-rate independent), not driven by render Time.deltaTime.</summary>
    public void SimStep(float dt)
    {
        // Auto-reseed a depleted renewable field (farm) by spending the owner's
        // wood. Runs regardless of gatherers so harvesting continues uninterrupted;
        // if the owner can't afford it the field stays empty and retries next tick.
        if (renewable && Depleted)
        {
            var rm = GameManager.Instance?.teamRes[ownerTeamId];
            if (rm != null && rm.CanAfford(0, reseedWoodCost, 0, 0))
            {
                rm.Deduct(0, reseedWoodCost, 0, 0);
                // Horse Collar / Heavy Plow raise the reseeded farm's food capacity.
                int farmBonus = GameManager.Instance?.teamTech[ownerTeamId]?.FarmCapacityBonus ?? 0;
                amount = maxAmount + farmBonus;
                _decayAccum = 0f;
            }
        }

        // Slow decay of idle farms. Accumulate the fractional amount so sub-1.0-per-tick
        // decay isn't rounded away to zero (the old RoundToInt(amount - rate*dt) never decayed
        // at all for small dt) and so the rate is independent of tick length.
        if (decayPerSecond > 0f && amount > 0)
        {
            // Franks: idle farms decay at half rate (farmDecayMult 0.5); other civs ×1.0.
            float decayMult = GameManager.Instance?.TeamCivBonus(ownerTeamId).farmDecayMult ?? 1f;
            _decayAccum += decayPerSecond * decayMult * dt;
            if (_decayAccum >= 1f)
            {
                int dec = Mathf.FloorToInt(_decayAccum);
                amount = Mathf.Max(0, amount - dec);
                _decayAccum -= dec;
            }
        }

        // Remove emptied nodes once their gatherers have left (GameManager's end-of-tick
        // compaction clears the null hole from gm.nodes). Farm fields opt out so the placed
        // building isn't destroyed.
        if (destroyOnDeplete && Depleted && currentGatherers == 0)
            Destroy(gameObject);
    }

    /// <summary>Remove up to <paramref name="n"/> units; returns the amount actually taken.</summary>
    public int Take(int n)
    {
        int taken = Mathf.Min(n, amount);
        amount -= taken;
        return taken;
    }
}
