using UnityEngine;

/// <summary>
/// Orthographic isometric camera matching the AoE2-style view from the
/// Three.js version: ~30° pitch, 45° yaw, pan with WASD / arrows / edge,
/// zoom with the scroll wheel, rotate with Q/E.
/// </summary>
[RequireComponent(typeof(Camera))]
public class IsometricCameraRig : MonoBehaviour
{
    public float panSpeed = 25f;
    public float zoomSpeed = 4f;      // orthographic units per mouse-wheel notch
    public float minSize = 6f;
    public float maxSize = 30f;
    public float rotateSpeed = 90f;
    public float edgeMargin = 10f;    // px from a screen edge that triggers edge-scroll pan
    public Vector2 bounds = new Vector2(60, 60); // half-extents of pannable area

    Camera _cam;
    Vector3 _focus;   // ground point the rig looks at
    float _yaw = 45f;
    const float Pitch = 30f;
    const float Distance = 60f;

    float _shakeTimer;
    float _shakeMagnitude;

    /// <summary>Combined pan intent on the ground plane: WASD/arrow keys plus mouse
    /// edge-scroll. Read directly off keys (not the "Horizontal"/"Vertical" axes) so
    /// panning works even if the legacy InputManager axes are missing or smoothed.</summary>
    Vector2 ReadPanInput()
    {
        float x = 0f, z = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  z -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    z += 1f;

        // Edge-scroll: only while the cursor is inside the game window.
        Vector3 mp = Input.mousePosition;
        if (edgeMargin > 0f && mp.x >= 0f && mp.x <= Screen.width && mp.y >= 0f && mp.y <= Screen.height)
        {
            if (mp.x <= edgeMargin) x -= 1f;
            else if (mp.x >= Screen.width - edgeMargin) x += 1f;
            if (mp.y <= edgeMargin) z -= 1f;
            else if (mp.y >= Screen.height - edgeMargin) z += 1f;
        }
        return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(z, -1f, 1f));
    }

    public void Init(Vector3 focus)
    {
        _cam = GetComponent<Camera>();
        _cam.orthographic = true;
        _cam.orthographicSize = 11f;  // start a bit zoomed in on the bigger map
        _cam.nearClipPlane = 0.1f;
        _cam.farClipPlane = 300f;
        _focus = focus;
        Apply();
    }

    void Update()
    {
        if (_cam == null) return;

        // Pan (camera-relative on the ground plane). Combines WASD/arrow keys with
        // AoE-style mouse edge-scroll: the cursor near a screen edge pans that way.
        var move = ReadPanInput();  // x = strafe, y = forward/back on the ground plane
        if (move.sqrMagnitude > 0.001f)
        {
            var fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            var delta = Vector3.ClampMagnitude(right * move.x + fwd * move.y, 1f) * panSpeed * Time.deltaTime;
            _focus += delta;
            _focus.x = Mathf.Clamp(_focus.x, -bounds.x, bounds.x);
            _focus.z = Mathf.Clamp(_focus.z, -bounds.y, bounds.y);
        }

        // Zoom — mouseScrollDelta is axis-independent, so it works reliably under
        // activeInputHandler=2 (legacy "Mouse ScrollWheel" axis can read as 0).
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.0001f)
            _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * zoomSpeed, minSize, maxSize);

        // Rotate
        if (Input.GetKey(KeyCode.Q)) _yaw -= rotateSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) _yaw += rotateSpeed * Time.deltaTime;

        Apply();

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            var offset = Random.insideUnitSphere * _shakeMagnitude;
            offset.y = 0f;
            transform.position += offset;
            if (_shakeTimer <= 0f) _shakeMagnitude = 0f;
        }
    }

    /// <summary>Snap the rig to look at a world point (clamped to pannable bounds).
    /// Used by control-group double-tap to jump the camera to the selected army.</summary>
    public void FocusOn(Vector3 worldPos)
    {
        _focus.x = Mathf.Clamp(worldPos.x, -bounds.x, bounds.x);
        _focus.z = Mathf.Clamp(worldPos.z, -bounds.y, bounds.y);
        Apply();
    }

    public void Shake(float duration, float magnitude)
    {
        _shakeTimer    = Mathf.Max(_shakeTimer, duration);
        _shakeMagnitude = Mathf.Max(_shakeMagnitude, magnitude);
    }

    void Apply()
    {
        var rot = Quaternion.Euler(Pitch, _yaw, 0);
        transform.position = _focus - rot * Vector3.forward * Distance;
        transform.rotation = rot;
    }
}
