using UnityEngine;

/// <summary>
/// Asset-free selection ring drawn with a LineRenderer loop at a unit's feet.
/// Mirrors the Three.js RingGeometry(0.6, 0.7, 24) look. Hidden by default.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class SelectionRing : MonoBehaviour
{
    const int Segments = 24;
    const float Radius = 0.65f;
    const float Height = 0.02f;

    LineRenderer _lr;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.useWorldSpace = false;
        _lr.loop = true;
        _lr.widthMultiplier = 0.08f;
        _lr.numCornerVertices = 2;
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows = false;
        _lr.material = Prims.UnlitColorMat(Color.green);

        _lr.positionCount = Segments;
        for (int i = 0; i < Segments; i++)
        {
            float a = (i / (float)Segments) * Mathf.PI * 2f;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * Radius, Height, Mathf.Sin(a) * Radius));
        }
        _lr.enabled = false;
    }

    public void Show(Color color)
    {
        if (_lr == null) return;
        _lr.material.color = color;
        if (_lr.material.HasProperty("_Color")) _lr.material.SetColor("_Color", color);
        _lr.startColor = color;
        _lr.endColor = color;
        _lr.enabled = true;
    }

    public void Hide()
    {
        if (_lr != null) _lr.enabled = false;
    }
}
