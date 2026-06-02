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

    const float CaptureSeconds = 5f;   // uncontested presence needed to flip
    const float DecayRate      = 1.5f; // progress lost per second when uncontested
    const float GoldPerSecond  = 0.5f; // passive gold per second while controlled

    // Neutral gold + per-team tints (match WorldRoot.TeamColors / minimap dots).
    static readonly Color Neutral = new Color(1f, 0.82f, 0.2f);
    static readonly Color[] TeamTint =
    {
        new Color(0.16f, 0.36f, 0.69f), // blue
        new Color(0.75f, 0.22f, 0.17f), // red
        new Color(0.15f, 0.68f, 0.38f), // green
        new Color(0.95f, 0.61f, 0.07f), // yellow
    };

    float _goldAccum;
    Material _orbMat;

    /// <summary>Factory hands over the orb material so capture can re-tint it.</summary>
    public void SetOrb(Material orbMat) => _orbMat = orbMat;

    /// <summary>Advance capture + passive income. Called by <see cref="RelicSystem"/>.</summary>
    public void UpdateCapture(float dt)
    {
        // Tally nearby units per team.
        var counts = new int[4];
        for (int i = 0; i < unitsNearby.Count; i++)
        {
            var u = unitsNearby[i];
            if (u != null && u.teamId >= 0 && u.teamId < 4) counts[u.teamId]++;
        }

        // Dominant single team (a tie for the lead = contested → no capture).
        int top = -1, topCount = 0; bool tie = false;
        for (int t = 0; t < 4; t++)
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

        // Passive gold trickle for the owner (accumulate fractional, grant whole gold).
        if (controllingTeam >= 0)
        {
            _goldAccum += GoldPerSecond * dt;
            if (_goldAccum >= 1f)
            {
                int g = Mathf.FloorToInt(_goldAccum);
                _goldAccum -= g;
                var gm = GameManager.Instance;
                if (gm != null) gm.teamRes[controllingTeam].Gain(ResourceKind.Gold, g);
            }
        }
    }

    void ApplyColor()
    {
        if (_orbMat == null) return;
        _orbMat.color = controllingTeam >= 0 && controllingTeam < TeamTint.Length
            ? TeamTint[controllingTeam]
            : Neutral;
    }
}
