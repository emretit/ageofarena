using UnityEngine;

/// <summary>
/// Win/lose arbiter. A team is eliminated when its Town Center is destroyed.
/// The player (team 0) loses if their TC falls; they win once no enemy team
/// (1-3) has a Town Center left. On game over the simulation is frozen
/// (<see cref="Time.timeScale"/> = 0) and <see cref="HUD.ShowGameOver"/> is shown;
/// pressing R rebuilds the arena via <see cref="GameBootstrap.Restart"/>.
/// </summary>
public class MatchSystem : MonoBehaviour
{
    const float CheckInterval = 1f;
    float _timer = CheckInterval;
    bool _over;

    void Update()
    {
        if (_over)
        {
            if (Input.GetKeyDown(KeyCode.R)) GameBootstrap.Restart();
            return;
        }

        // Throttle the scan; unscaled so it behaves identically regardless of timeScale.
        if ((_timer -= Time.unscaledDeltaTime) > 0f) return;
        _timer = CheckInterval;
        CheckEnd();
    }

    void CheckEnd()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var alive = new bool[4];
        var bs = gm.buildings;
        for (int i = 0; i < bs.Count; i++)
        {
            var b = bs[i];
            if (b == null || b.type != BuildingType.TownCenter || b.hp <= 0f) continue;
            if (b.teamId >= 0 && b.teamId < 4) alive[b.teamId] = true;
        }

        bool playerAlive = alive[0];
        bool anyEnemy = alive[1] || alive[2] || alive[3];

        if (!playerAlive)   End(false);
        else if (!anyEnemy) End(true);
    }

    void End(bool playerWon)
    {
        _over = true;
        Time.timeScale = 0f;
        GameManager.Instance?.hud?.ShowGameOver(playerWon);
    }
}
