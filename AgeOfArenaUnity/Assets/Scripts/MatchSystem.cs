using UnityEngine;

/// <summary>
/// Win/lose arbiter with three victory paths:
///  • Conquest — a team is eliminated when its Town Center is destroyed; the player
///    (team 0) loses if theirs falls and wins once no enemy (1-3) has a TC left.
///  • Wonder — a team that finishes a <see cref="BuildingType.Wonder"/> and keeps it
///    standing for <see cref="WonderHoldTime"/> wins; destroying it cancels the count.
///  • Relics — a team that controls every relic on the map for <see cref="RelicHoldTime"/>
///    wins; losing any relic resets the count.
/// On game over the simulation freezes (<see cref="Time.timeScale"/> = 0) and
/// <see cref="HUD.ShowGameOver"/> shows the result + score; pressing R restarts.
/// </summary>
public class MatchSystem : MonoBehaviour
{
    const float CheckInterval = 1f;
    /// <summary>Seconds a finished Wonder must survive to win. Set by WorldRoot.</summary>
    public float WonderHoldTime = 60f;
    /// <summary>Seconds a team must hold all relics to win. Set by WorldRoot.</summary>
    public float RelicHoldTime  = 60f;

    static readonly string[] TeamNames = { "Mavi (Sen)", "Kırmızı", "Yeşil", "Sarı" };

    /// <summary>
    /// Match time limit in seconds. 0 = no limit (standard). Set by WorldRoot before play.
    /// When elapsed, all teams are scored; highest score wins.
    /// </summary>
    public float MatchTimeLimit = 0f;

    float _timer = CheckInterval;
    float _matchElapsed;
    bool  _over;

    /// <summary>True once the match has ended (win/lose/resign). Read by FocusPause (N9) so
    /// resuming from a focus-loss pause never un-freezes a finished game.</summary>
    public bool IsOver => _over;

    readonly float[] _wonderTimer = new float[4];
    readonly float[] _relicTimer  = new float[4];

    /// <summary>Active victory-countdown line for the HUD top bar ("" = none).</summary>
    public string VictoryStatus { get; private set; } = "";

    /// <summary>Remaining match time in seconds, or float.MaxValue if no limit.</summary>
    public float TimeRemaining => MatchTimeLimit > 0f
        ? Mathf.Max(0f, MatchTimeLimit - _matchElapsed)
        : float.MaxValue;

    /// <summary>Player voluntarily concedes (called from pause menu).</summary>
    public void Resign()
    {
        if (_over) return;
        var gm = GameManager.Instance;
        End(false, "Teslim oldu", gm);
    }

    void Update()
    {
        if (_over)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                // ARES: preserve civ & difficulty so restart drops back into the same settings.
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    GameBootstrap.NextDifficulty = gm.difficulty;
                    GameBootstrap.NextGameMode   = gm.gameMode;
                }
                GameBootstrap.Restart();
            }
            return;
        }

        float dt = Time.unscaledDeltaTime;
        _matchElapsed += dt;

        // VTIME: time-limit check — fires once the clock runs out.
        if (MatchTimeLimit > 0f && _matchElapsed >= MatchTimeLimit)
        {
            CheckTimeUp();
            return;
        }

        // Throttle the scan; unscaled so it behaves identically regardless of timeScale.
        if ((_timer -= dt) > 0f) return;
        _timer = CheckInterval;
        CheckEnd();
    }

    void CheckEnd()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // ── Town Centers + Wonders alive per team ────────────────────────────────
        var tcAlive    = new bool[4];
        var hasWonder  = new bool[4];
        var bs = gm.buildings;
        for (int i = 0; i < bs.Count; i++)
        {
            var b = bs[i];
            if (b == null || b.hp <= 0f || b.teamId < 0 || b.teamId >= 4) continue;
            if (b.type == BuildingType.TownCenter) tcAlive[b.teamId] = true;
            else if (b.type == BuildingType.Wonder && !b.underConstruction) hasWonder[b.teamId] = true;
        }

        // ── Relic control per team (all relics held → countdown) ─────────────────
        int totalRelics = gm.relics != null ? gm.relics.Count : 0;

        VictoryStatus = "";
        for (int t = 0; t < 4; t++)
        {
            // Wonder countdown. N0.2: a win by the player or an ally is a shared win, not a loss.
            if (hasWonder[t]) _wonderTimer[t] += CheckInterval; else _wonderTimer[t] = 0f;
            if (_wonderTimer[t] >= WonderHoldTime) { End(gm.IsAllied(0, t), "Anıt zaferi", gm); return; }

            // Relic countdown — must hold every relic on the map.
            bool holdsAllRelics = totalRelics > 0 && gm.relicSystem != null
                                  && gm.relicSystem.CountControlled(t) == totalRelics;
            if (holdsAllRelics) _relicTimer[t] += CheckInterval; else _relicTimer[t] = 0f;
            if (_relicTimer[t] >= RelicHoldTime) { End(gm.IsAllied(0, t), "Kalıntı zaferi", gm); return; }

            // Surface the most advanced countdown for this team to the HUD.
            float w = _wonderTimer[t], r = _relicTimer[t];
            if (w > 0f) SetStatus(t, "Anıt", WonderHoldTime - w);
            else if (r > 0f) SetStatus(t, "Kalıntı", RelicHoldTime - r);
        }

        // ── Regicide ─────────────────────────────────────────────────────────────
        if (gm.gameMode == GameMode.Regicide)
        {
            var kingAlive = new bool[4];
            for (int i = 0; i < gm.units.Count; i++)
            {
                var u = gm.units[i];
                if (u != null && u.type == UnitType.King && u.hp > 0f && u.teamId >= 0 && u.teamId < 4)
                    kingAlive[u.teamId] = true;
            }
            if (!kingAlive[0]) { End(false, "Regicide (Kral öldü)", gm); return; }
            // N0.2: only an enemy king alive blocks the player's regicide win; allied kings don't.
            bool anyEnemyKing = false;
            for (int t = 1; t < 4; t++)
                if (kingAlive[t] && gm.IsEnemy(0, t)) { anyEnemyKing = true; break; }
            if (!anyEnemyKing) { End(true, "Regicide zaferi", gm); return; }
        }

        // ── Conquest (VDIPL: only counts enemy teams) ────────────────────────────
        bool playerAlive = tcAlive[0];
        bool anyEnemy = false;
        for (int t = 1; t < 4; t++)
            if (tcAlive[t] && gm.IsEnemy(0, t)) { anyEnemy = true; break; }
        if (!playerAlive)   End(false, "Fetih (TC yıkıldı)", gm);
        else if (!anyEnemy) End(true,  "Fetih", gm);
    }

    // VTIME: time is up — winner is team with highest score.
    void CheckTimeUp()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int best = 0;
        int bestScore = Score(0, gm);
        for (int t = 1; t < 4; t++)
        {
            int s = Score(t, gm);
            if (s > bestScore) { bestScore = s; best = t; }
        }
        // N0.2: highest-scoring team wins; shared with the player's alliance.
        End(gm.IsAllied(0, best), "Süre bitti", gm);
    }

    // Score = units × 10 + buildings × 20 + gold
    static int Score(int t, GameManager gm)
    {
        int s = 0;
        foreach (var u in gm.units)  if (u != null && u.teamId == t) s += 10;
        foreach (var b in gm.buildings) if (b != null && b.teamId == t) s += 20;
        s += (int)gm.teamRes[t].gold;
        return s;
    }

    /// <summary>Keep the single most-imminent countdown visible in the top bar.</summary>
    void SetStatus(int team, string kind, float remaining)
    {
        string line = $"{kind} zaferi — {TeamNames[team]}: {Mathf.CeilToInt(remaining)}s";
        // Prefer whichever countdown has less time left (more urgent).
        if (VictoryStatus.Length == 0) { VictoryStatus = line; return; }
    }

    void End(bool playerWon, string reason, GameManager gm)
    {
        _over = true;
        VictoryStatus = "";
        Time.timeScale = 0f;
        int score = Score(gm, 0);
        string subtitle = $"{reason} · Skorun: {score}";
        gm.hud?.ShowGameOver(playerWon, subtitle);
    }

    /// <summary>Composite end-of-game score for a team: army, buildings, economy,
    /// relics and age all contribute.</summary>
    static int Score(GameManager gm, int team)
    {
        if (gm == null) return 0;
        int units = 0, military = 0, blds = 0;
        for (int i = 0; i < gm.units.Count; i++)
        {
            var u = gm.units[i];
            if (u == null || u.teamId != team) continue;
            units++;
            if (u.type != UnitType.Villager) military++;
        }
        for (int i = 0; i < gm.buildings.Count; i++)
        {
            var b = gm.buildings[i];
            if (b != null && b.teamId == team && b.hp > 0f) blds++;
        }
        var res = gm.teamRes != null && team < gm.teamRes.Length ? gm.teamRes[team] : null;
        int resTotal = res != null ? res.food + res.wood + res.gold + res.stone : 0;
        int relics = gm.relicSystem != null ? gm.relicSystem.CountControlled(team) : 0;
        int age = gm.teamTech != null && team < gm.teamTech.Length ? (int)gm.teamTech[team].age : 0;

        return units * 10 + military * 15 + blds * 25 + resTotal / 10 + relics * 100 + age * 75;
    }
}
