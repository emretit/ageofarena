using UnityEngine;

/// <summary>
/// Resource tribute between teams (M8/TRIB). A team sends a resource amount to
/// another team; a flat <see cref="TaxRate"/> (30%) is skimmed off the top unless
/// the sending team has researched <see cref="TechType.Coinage"/> (then it is
/// tax-free, AoE2-style). Both teams' <see cref="ResourceManager"/> ledgers are
/// updated. Used by the diplomacy/AI layers (M11) and the HUD tribute controls.
/// </summary>
public static class TributeSystem
{
    /// <summary>Fraction skimmed when the sender lacks Coinage.</summary>
    public const float TaxRate = 0.30f;

    /// <summary>
    /// Send <paramref name="amount"/> of <paramref name="kind"/> from one team to
    /// another. Returns false (no-op) on invalid teams, non-positive amount, or if
    /// the sender can't afford it. The receiver gets <c>amount</c> (Coinage) or
    /// <c>amount × (1 − TaxRate)</c> (no Coinage); the sender always pays the full amount.
    /// </summary>
    public static bool Tribute(int fromTeam, int toTeam, ResourceKind kind, int amount)
    {
        var gm = GameManager.Instance;
        if (gm == null || amount <= 0 || fromTeam == toTeam) return false;
        if (fromTeam < 0 || toTeam < 0
            || fromTeam >= gm.teamRes.Length || toTeam >= gm.teamRes.Length) return false;

        var from = gm.teamRes[fromTeam];
        var to   = gm.teamRes[toTeam];
        if (from == null || to == null || from.Get(kind) < amount) return false;

        // Coinage (sender's Market tech) removes the tribute tax.
        bool taxFree = gm.teamTech[fromTeam] != null && gm.teamTech[fromTeam].Has(TechType.Coinage);
        int received = taxFree ? amount : Mathf.RoundToInt(amount * (1f - TaxRate));

        from.Gain(kind, -amount);
        to.Gain(kind, received);
        return true;
    }
}
