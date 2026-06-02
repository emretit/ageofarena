using System.Collections;
using UnityEngine;

/// <summary>
/// Floating damage number that spawns above a hit target, drifts upward, fades
/// out and destroys itself. Call <see cref="Show"/> from CombatSystem/Projectile
/// whenever damage is dealt. Uses TextMesh (no Canvas overhead — fine for RTS).
/// </summary>
public class DamagePopup : MonoBehaviour
{
    const float Duration   = 0.75f;
    const float RiseSpeed  = 2.2f;
    const float StartAlpha = 1f;

    static readonly Color NormalColor = Color.white;
    static readonly Color CritColor   = new Color(1f, 0.92f, 0.15f);  // golden yellow

    TextMesh _text;
    float    _elapsed;
    Color    _baseColor;

    public static void Show(Vector3 worldPos, int amount, bool isCrit = false)
    {
        var go  = new GameObject("DmgPopup");
        go.transform.position = worldPos + new Vector3(
            Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.15f, 0.15f));

        var tm = go.AddComponent<TextMesh>();
        tm.text          = amount.ToString();
        tm.fontSize      = isCrit ? 32 : 24;
        tm.fontStyle     = isCrit ? FontStyle.Bold : FontStyle.Normal;
        tm.alignment     = TextAlignment.Center;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.characterSize = 0.12f;
        tm.color         = isCrit ? CritColor : NormalColor;

        // Face the camera (billboard) via a child-free LookAt each frame.
        var popup = go.AddComponent<DamagePopup>();
        popup._text      = tm;
        popup._baseColor = tm.color;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;

        transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

        // Always face main camera.
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        float t = _elapsed / Duration;
        _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
            Mathf.Lerp(StartAlpha, 0f, t));

        if (_elapsed >= Duration) Destroy(gameObject);
    }
}
