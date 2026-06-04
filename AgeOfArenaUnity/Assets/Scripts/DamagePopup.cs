using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Floating damage number that drifts upward and fades out after a hit.
/// N1.pool: uses a static <see cref="ObjectPool{T}"/> — zero Instantiate/Destroy on
/// the hot-path so combat with many simultaneous hits produces no GC alloc.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    const float Duration   = 0.75f;
    const float RiseSpeed  = 2.2f;
    const float StartAlpha = 1f;

    static readonly Color NormalColor = Color.white;
    static readonly Color CritColor   = new Color(1f, 0.92f, 0.15f);

    TextMesh _text;
    float    _elapsed;
    Color    _baseColor;

    // N1.pool
    static ObjectPool<DamagePopup> _pool;

    static ObjectPool<DamagePopup> Pool
    {
        get
        {
            if (_pool == null)
                _pool = new ObjectPool<DamagePopup>(
                    createFunc:      CreatePooled,
                    actionOnGet:     p => p.gameObject.SetActive(true),
                    actionOnRelease: p => p.gameObject.SetActive(false),
                    actionOnDestroy: p => { if (p != null) Destroy(p.gameObject); },
                    collectionCheck: false,
                    defaultCapacity: 32,
                    maxSize:         128
                );
            return _pool;
        }
    }

    static DamagePopup CreatePooled()
    {
        var go = new GameObject("DmgPopup_Pool");
        var tm = go.AddComponent<TextMesh>();
        tm.alignment     = TextAlignment.Center;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.characterSize = 0.12f;
        var p   = go.AddComponent<DamagePopup>();
        p._text = tm;
        go.SetActive(false);
        return p;
    }

    public static void Show(Vector3 worldPos, int amount, bool isCrit = false)
    {
        var p = Pool.Get();
        p.transform.position = worldPos + new Vector3(
            Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.15f, 0.15f));
        p._elapsed      = 0f;
        p._text.text    = amount.ToString();
        p._text.fontSize = isCrit ? 32 : 24;
        p._text.fontStyle = isCrit ? FontStyle.Bold : FontStyle.Normal;
        p._text.color   = isCrit ? CritColor : NormalColor;
        p._baseColor    = p._text.color;
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;

        float t = _elapsed / Duration;
        _text.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
            Mathf.Lerp(StartAlpha, 0f, t));

        if (_elapsed >= Duration) Pool.Release(this);
    }
}
