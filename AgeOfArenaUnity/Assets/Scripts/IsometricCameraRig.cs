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
    public float zoomSpeed = 8f;
    public float minSize = 6f;
    public float maxSize = 30f;
    public float rotateSpeed = 90f;
    public Vector2 bounds = new Vector2(60, 60); // half-extents of pannable area

    Camera _cam;
    Vector3 _focus;   // ground point the rig looks at
    float _yaw = 45f;
    const float Pitch = 30f;
    const float Distance = 60f;

    float _shakeTimer;
    float _shakeMagnitude;

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

        // Pan (camera-relative on the ground plane)
        var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        if (move.sqrMagnitude > 0.001f)
        {
            var fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            var delta = (right * move.x + fwd * move.z).normalized * panSpeed * Time.deltaTime;
            _focus += delta;
            _focus.x = Mathf.Clamp(_focus.x, -bounds.x, bounds.x);
            _focus.z = Mathf.Clamp(_focus.z, -bounds.y, bounds.y);
        }

        // Zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
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
