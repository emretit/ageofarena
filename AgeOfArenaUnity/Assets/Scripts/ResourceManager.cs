using System;

/// <summary>
/// Per-player resource ledger. Single-player slice = team 0 only. Port of the
/// Three.js ResourceManager, extended with a 4th resource (stone) for the
/// Unity build. Raises <see cref="OnChanged"/> whenever a value changes so the
/// HUD can refresh without polling.
/// </summary>
public class ResourceManager
{
    public int food = 200;
    public int wood = 200;
    public int gold = 100;
    public int stone = 200;  // M8/STONE: AoE2-parity stone economy (Castle/University/towers cost stone).

    public int pop = 0;
    public int popCap = 5;  // TC_BASE_POP

    public event Action OnChanged;

    public int Get(ResourceKind kind) => kind switch
    {
        ResourceKind.Food => food,
        ResourceKind.Wood => wood,
        ResourceKind.Gold => gold,
        ResourceKind.Stone => stone,
        _ => 0,
    };

    public void Gain(ResourceKind kind, int amount)
    {
        if (amount == 0) return;
        switch (kind)
        {
            case ResourceKind.Food: food += amount; break;
            case ResourceKind.Wood: wood += amount; break;
            case ResourceKind.Gold: gold += amount; break;
            case ResourceKind.Stone: stone += amount; break;
        }
        OnChanged?.Invoke();
    }

    public bool CanAfford(int foodCost, int woodCost, int goldCost, int stoneCost)
        => food >= foodCost && wood >= woodCost && gold >= goldCost && stone >= stoneCost;

    public void Deduct(int foodCost, int woodCost, int goldCost, int stoneCost)
    {
        food -= foodCost;
        wood -= woodCost;
        gold -= goldCost;
        stone -= stoneCost;
        OnChanged?.Invoke();
    }

    public void SetPop(int pop, int popCap)
    {
        this.pop = pop;
        this.popCap = popCap;
        OnChanged?.Invoke();
    }
}
