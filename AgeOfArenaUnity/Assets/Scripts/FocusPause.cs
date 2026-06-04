using UnityEngine;

/// <summary>
/// N9 (ROADMAP-V2): pause-on-blur. In a WebGL build the simulation keeps running while the
/// browser tab/window is unfocused (<see cref="Application.runInBackground"/> = true, set in
/// WorldRoot). This freezes the game (<see cref="Time.timeScale"/> = 0) whenever the app loses
/// focus and restores the previous speed on return, behind a small overlay.
///
/// It defers to pauses owned by others: if the game is already paused/over (timeScale ≈ 0) when
/// focus is lost it leaves it alone, and on return it never un-pauses a finished match
/// (<see cref="MatchSystem.IsOver"/>) or a pause it didn't create.
/// </summary>
public class FocusPause : MonoBehaviour
{
    float _savedScale = 1f;
    bool  _pausedByBlur;
    bool  _showOverlay;
    GUIStyle _style;

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) Resume(); else Suspend();
    }

    // Mobile/WebGL also raise OnApplicationPause; treat it the same way.
    void OnApplicationPause(bool isPaused)
    {
        if (isPaused) Suspend(); else Resume();
    }

    void Suspend()
    {
        if (_pausedByBlur) return;               // we already froze it
        if (Time.timeScale <= 0.0001f) return;   // game over / Esc menu already paused — leave it
        _savedScale   = Time.timeScale;
        _pausedByBlur = true;
        _showOverlay  = true;
        Time.timeScale = 0f;
    }

    void Resume()
    {
        _showOverlay = false;
        if (!_pausedByBlur) return;
        _pausedByBlur = false;

        // Don't un-pause a match that ended while we were unfocused, and don't clobber a hard
        // pause something else took over meanwhile.
        var match = GameManager.Instance != null ? GameManager.Instance.match : null;
        if (match != null && match.IsOver) return;
        if (Time.timeScale <= 0.0001f) Time.timeScale = _savedScale;
    }

    void OnGUI()
    {
        if (!_showOverlay) return;
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        _style ??= new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 28,
            normal = { textColor = Color.white } };
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "Duraklatıldı — pencereye geri dön", _style);
        GUI.color = prev;
    }
}
