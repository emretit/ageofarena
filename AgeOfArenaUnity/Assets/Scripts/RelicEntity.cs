using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A neutral, stationary relic / control point on the map. While a single team's
/// units stand within capture range (and no rival is contesting), that team
/// builds capture progress; once full, the relic flips to their control and the
/// orb tints to their colour. The controlling team earns a passive gold trickle
/// every second. Contested (two+ teams present in equal force) → nobody captures.
///
/// Proximity is fed in by <see cref="RelicSystem"/> each frame via
/// <see cref="unitsNearby"/>. Relics are NOT <see cref="IDamageable"/> — they
/// can't be destroyed, only captured — and are not hidden by Fog of War (like
/// resource nodes, they're a permanent map feature).
/// </summary>
public class RelicEntity : MonoBehaviour
{
    public int   controllingTeam = -1; // -1 = neutral, 0-3 = owning team
    public int   capturingTeam   = -1; // team currently building progress
    public float captureProgress;      // 0..CaptureSeconds toward capturingTeam

    public readonly List<UnitEntity> unitsNearby = new();

    // Monk relic carry (AoE2 model): a Monk can pick the relic up and haul it to a
    // friendly Monastery, where it locks in (heldInMonastery) and trickles gold.
    public UnitEntity carrier;          // Monk currently hauling this relic (null = not carried)
    public bool heldInMonastery;        // deposited in a Monastery → permanent control + gold
    /// <summary>Relic is free to be captured-by-proximity or picked up by a Monk.</summary>
    public bool Available => carrier == null && !heldInMonastery;

    const float CaptureSeconds = 5f;   // uncontested presence needed to flip
    const float DecayRate      = 1.5f; // progress lost per second when uncontested
    const float GoldPerSecond  = 0.5f; // passive gold per second while controlled

    // Neutral gold + per-team tints (match WorldRoot.TeamColors / minimap dots).
    static readonly Color Neutral = new Color(1f, 0.82f, 0.2f);

    float _goldAccum;
    Material _orbMat;

    /// <summary>Factory hands over the orb material so capture can re-tint it.</summary>
    public void SetOrb(Material orbMat) => _orbMat = orbMat;

    int[] _captureCounts;   // reused across ticks (UpdateCapture runs per relic per tick)

    /// <summary>Advance capture + passive income. Called by <see cref="RelicSystem"/>.</summary>
    public void UpdateCapture(float dt)
    {
        // Tally nearby units per team (reuse the buffer instead of allocating int[] each tick).
        int nTeams = GameManager.MaxTeams;
        if (_captureCounts == null) _captureCounts = new int[nTeams];
        var counts = _captureCounts;
        System.Array.Clear(counts, 0, counts.Length);
        for (int i = 0; i < unitsNearby.Count; i++)
        {
            var u = unitsNearby[i];
            if (u != null && u.teamId >= 0 && u.teamId < nTeams) counts[u.teamId]++;
        }

        // Dominant single team (a tie for the lead = contested → no capture).
        int top = -1, topCount = 0; bool tie = false;
        for (int t = 0; t < nTeams; t++)
        {
            if (counts[t] > topCount) { topCount = counts[t]; top = t; tie = false; }
            else if (counts[t] == topCount && counts[t] > 0) tie = true;
        }
        int dominant = (tie || topCount == 0) ? -1 : top;

        if (dominant >= 0 && dominant != controllingTeam)
        {
            if (capturingTeam != dominant) { capturingTeam = dominant; captureProgress = 0f; }
            captureProgress += dt;
            if (captureProgress >= CaptureSeconds)
            {
                controllingTeam = dominant;
                capturingTeam   = -1;
                captureProgress = 0f;
                ApplyColor();
            }
        }
        else
        {
            captureProgress = Mathf.Max(0f, captureProgress - DecayRate * dt);
            if (captureProgress <= 0f) capturingTeam = -1;
        }

        GrantGold(dt);
    }

    /// <summary>Passive gold trickle for the controlling team (held or proximity-controlled).</summary>
    public void GrantGold(float dt)
    {
        if (controllingTeam < 0) return;
        _goldAccum += GoldPerSecond * dt;
        if (_goldAccum >= 1f)
        {
            int g = Mathf.FloorToInt(_goldAccum);
            _goldAccum -= g;
            var gm = GameManager.Instance;
            if (gm != null) gm.teamRes[controllingTeam].Gain(ResourceKind.Gold, g);
        }
    }

    /// <summary>Lock the relic to a team (used on Monastery deposit) and re-tint the orb.</summary>
    public void ForceControl(int team)
    {
        controllingTeam = team;
        capturingTeam = -1;
        captureProgress = 0f;
        ApplyColor();
    }

    void ApplyColor()
    {
        if (_orbMat == null) return;
        _orbMat.color = controllingTeam >= 0
            ? TeamPalette.For(controllingTeam)
            : Neutral;
    }
}
