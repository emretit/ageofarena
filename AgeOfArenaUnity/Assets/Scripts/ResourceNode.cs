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

    public bool Depleted => amount <= 0;
    public bool HasRoom => currentGatherers < gathererCap;

    public void Init(ResourceKind kind, int amount)
    {
        this.kind = kind;
        this.amount = amount;
        this.maxAmount = amount;
    }

    void Update()
    {
        // Remove emptied nodes once their gatherers have left (GameManager's
        // end-of-frame compaction clears the null hole from gm.nodes). Farm fields
        // opt out so the placed building isn't destroyed.
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
