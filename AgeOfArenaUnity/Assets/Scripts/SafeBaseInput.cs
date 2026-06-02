using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A <see cref="BaseInput"/> that swallows the ArgumentException thrown when a
/// legacy Input axis/button is not defined in InputManager.asset. This project's
/// InputManager only defines Horizontal / Vertical / Mouse ScrollWheel, so
/// <see cref="StandaloneInputModule"/>'s keyboard-navigation polling
/// (GetButtonDown("Submit"/"Cancel"), GetAxisRaw(...)) would throw every frame and
/// flood the console. The HUD only needs pointer (mouse) UI input, so returning
/// false/0 for the missing buttons/axes is safe.
///
/// Attach to the EventSystem GameObject and assign as
/// <c>StandaloneInputModule.inputOverride</c> (a plain derived BaseInput is not
/// auto-detected by the module, only the override slot is honoured).
/// </summary>
public class SafeBaseInput : BaseInput
{
    public override bool GetButtonDown(string buttonName)
    {
        try { return base.GetButtonDown(buttonName); }
        catch { return false; }
    }

    public override float GetAxisRaw(string axisName)
    {
        try { return base.GetAxisRaw(axisName); }
        catch { return 0f; }
    }
}
