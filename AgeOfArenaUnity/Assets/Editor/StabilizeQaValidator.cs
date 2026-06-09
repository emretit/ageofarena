#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batchmode camera QA for the current stabilization wave. It creates a temporary
/// scene, spawns the high-risk visual targets, renders camera proof images, and
/// asserts scale/grounding/selection/team-color invariants without touching game
/// scenes or authored assets.
/// </summary>
public static class StabilizeQaValidator
{
    struct SpawnedTarget
    {
        public readonly GameObject Root;
        public readonly string Label;
        public readonly bool Unit;

        public SpawnedTarget(GameObject root, string label, bool unit)
        {
            Root = root;
            Label = label;
            Unit = unit;
        }
    }

    static readonly UnitType[] UnitTargets =
    {
        UnitType.Scout,
        UnitType.Cavalry,
        UnitType.CavalryArcher,
        UnitType.Mangudai,
        UnitType.Cataphract,
        UnitType.TradeCart,
    };

    static readonly BuildingType[] BuildingTargets =
    {
        BuildingType.Stable,
        BuildingType.Market,
        BuildingType.ArcheryRange,
        BuildingType.Blacksmith,
        BuildingType.Monastery,
        BuildingType.University,
        BuildingType.Dock,
        BuildingType.SiegeWorkshop,
        BuildingType.Farm,
        BuildingType.MiningCamp,
        BuildingType.Outpost,
        BuildingType.WatchTower,
        BuildingType.BombardTower,
        BuildingType.Wonder,
    };

    [MenuItem("Tools/Age of Arena/Run Stabilize QA Camera Gate")]
    public static void RunFromMenu()
    {
        RunOrThrow();
        Debug.Log("[StabilizeQaValidator] Camera QA validation passed.");
    }

    public static void RunFromCommandLine()
    {
        try
        {
            RunOrThrow();
            Debug.Log("[StabilizeQaValidator] Camera QA validation passed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[StabilizeQaValidator] Camera QA validation failed:\n" + ex);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    static void RunOrThrow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("StabilizeQaRoot");
        var targets = new List<SpawnedTarget>();
        Camera cam = null;
        try
        {
            CreateLighting(root.transform);
            CreateGround(root.transform);
            SpawnUnits(root.transform, targets);
            SpawnBuildings(root.transform, targets);
            ValidateTargets(targets);
            ValidateManualQaContracts(targets);

            cam = CreateCamera();
            Bounds allBounds = CombinedBounds(targets);
            RenderProof(cam, allBounds, "stabilize-qa-wide.png", 14f, requireAllTargets: targets);

            var unitTargets = targets.FindAll(t => t.Unit);
            Bounds unitBounds = CombinedBounds(unitTargets);
            RenderProof(cam, unitBounds, "stabilize-qa-units-close.png", 4f, requireAllTargets: unitTargets);
        }
        finally
        {
            if (cam != null)
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void SpawnUnits(Transform parent, List<SpawnedTarget> targets)
    {
        for (int i = 0; i < UnitTargets.Length; i++)
        {
            int teamId = i % 4;
            Vector3 pos = new Vector3((i - 2.5f) * 2.5f, 0f, -4f);
            UnitEntity unit = UnitFactory.Spawn(UnitTargets[i], parent, pos, teamId);
            Require(unit != null, $"{UnitTargets[i]} did not spawn.");
            ShowSelectionRing(unit.gameObject);
            targets.Add(new SpawnedTarget(unit.gameObject, UnitTargets[i].ToString(), unit: true));
        }
    }

    static void SpawnBuildings(Transform parent, List<SpawnedTarget> targets)
    {
        const int columns = 7;
        for (int i = 0; i < BuildingTargets.Length; i++)
        {
            int row = i / columns;
            int col = i % columns;
            Vector3 pos = new Vector3((col - 3) * 4.2f, 0f, 2f + row * 4.6f);
            var go = BuildingFactory.Create(BuildingTargets[i], parent, pos, TeamPalette.For(i % 4));
            Require(go != null, $"{BuildingTargets[i]} did not spawn.");
            targets.Add(new SpawnedTarget(go, BuildingTargets[i].ToString(), unit: false));
        }
    }

    static void ValidateTargets(List<SpawnedTarget> targets)
    {
        foreach (SpawnedTarget target in targets)
        {
            Renderer[] renderers = VisibleRenderers(target.Root);
            Require(renderers.Length > 0, $"{target.Label} has no visible renderers.");
            Bounds bounds = CombinedBounds(renderers);
            float maxExtent = target.Unit ? 8f : 18f;
            Require(bounds.size.x <= maxExtent && bounds.size.y <= maxExtent && bounds.size.z <= maxExtent,
                $"{target.Label} looks oversized in camera QA: {bounds.size}");
            float minGround = target.Unit ? -0.15f : -1.0f;
            float maxGround = target.Unit ? 0.35f : 0.50f;
            Require(bounds.min.y >= minGround, $"{target.Label} dips below ground: minY={bounds.min.y:0.###}");
            Require(bounds.min.y <= maxGround, $"{target.Label} appears to float: minY={bounds.min.y:0.###}");

            if (target.Unit)
            {
                Require(target.Root.GetComponent<CapsuleCollider>() != null, $"{target.Label} missing unit collider.");
                var lr = target.Root.GetComponentInChildren<LineRenderer>(true);
                Require(lr != null, $"{target.Label} missing selection LineRenderer.");
                Require(!lr.useWorldSpace, $"{target.Label} selection ring must be local-space.");
                RequireHasTeamColor(target.Root, TeamPalette.For(target.Root.GetComponent<UnitEntity>().teamId), target.Label);
            }
            else
            {
                Require(target.Root.GetComponent<BoxCollider>() != null, $"{target.Label} missing building collider.");
            }
        }
    }

    static void ValidateManualQaContracts(List<SpawnedTarget> targets)
    {
        foreach (SpawnedTarget target in targets)
        {
            if (!target.Unit) continue;
            var unit = target.Root.GetComponent<UnitEntity>();
            Require(unit != null, $"{target.Label} missing UnitEntity.");
            EnsureUnitAwakeForEditMode(unit);

            unit.MoveTo(target.Root.transform.position + new Vector3(1.5f, 0f, 1.5f));
            Require(unit.state == UnitState.Moving, $"{target.Label} MoveTo did not enter Moving state.");

            unit.attackMove = true;
            unit.moveQueue.Enqueue(unit.transform.position + Vector3.right);
            unit.Stop();
            Require(unit.state == UnitState.Idle, $"{target.Label} Stop did not return to Idle.");
            Require(!unit.attackMove, $"{target.Label} Stop did not clear attackMove.");
            Require(unit.moveQueue.Count == 0, $"{target.Label} Stop did not clear move queue.");
        }

        ValidateHotkeyDefaultsWithRestore();
    }

    static void ValidateHotkeyDefaultsWithRestore()
    {
        KeyCode patrol = Hotkeys.Get(HotkeyAction.Patrol);
        KeyCode formation = Hotkeys.Get(HotkeyAction.Formation);
        KeyCode townBell = Hotkeys.Get(HotkeyAction.TownBell);
        KeyCode repair = Hotkeys.Get(HotkeyAction.Repair);
        try
        {
            Hotkeys.Reset(HotkeyAction.Patrol);
            Hotkeys.Reset(HotkeyAction.Formation);
            Hotkeys.Reset(HotkeyAction.TownBell);
            Hotkeys.Reset(HotkeyAction.Repair);

            Require(Hotkeys.Get(HotkeyAction.Patrol) == KeyCode.P, "Patrol hotkey must default to P.");
            Require(Hotkeys.Get(HotkeyAction.Formation) == KeyCode.F, "Formation hotkey must default to F.");
            Require(Hotkeys.Get(HotkeyAction.TownBell) == KeyCode.H, "TownBell hotkey must default to H.");
            Require(Hotkeys.Get(HotkeyAction.Repair) == KeyCode.None, "Repair must remain contextual/unbound by default.");
            Require(!Hotkeys.IsBindableAction(HotkeyAction.Repair), "Repair must not participate in global hotkey conflicts.");
            Require(Hotkeys.ActionFor(KeyCode.H) == HotkeyAction.TownBell, "TownBell must own H even if old Repair prefs existed.");
        }
        finally
        {
            Hotkeys.Set(HotkeyAction.Patrol, patrol);
            Hotkeys.Set(HotkeyAction.Formation, formation);
            Hotkeys.Set(HotkeyAction.TownBell, townBell);
            Hotkeys.Set(HotkeyAction.Repair, repair);
        }
    }

    static void EnsureUnitAwakeForEditMode(UnitEntity unit)
    {
        if (unit.GetComponent<UnityEngine.AI.NavMeshAgent>() != null) return;
        var awake = typeof(UnitEntity).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(awake != null, "UnitEntity.Awake not found for edit-mode QA setup.");
        awake.Invoke(unit, null);
    }

    static void RenderProof(Camera cam, Bounds bounds, string fileName, float padding, List<SpawnedTarget> requireAllTargets)
    {
        FrameCamera(cam, bounds, padding);
        foreach (SpawnedTarget target in requireAllTargets)
            RequireInFrame(cam, CombinedBounds(VisibleRenderers(target.Root)), target.Label);

        string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        Texture2D image = RenderCamera(cam, 1280, 720);
        try
        {
            RequireImageContrast(image, fileName);
            File.WriteAllBytes(path, image.EncodeToPNG());
            Debug.Log($"[StabilizeQaValidator] Wrote camera QA proof: {path}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static Camera CreateCamera()
    {
        var go = new GameObject("StabilizeQaCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.09f);
        cam.orthographic = true;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        return cam;
    }

    static void FrameCamera(Camera cam, Bounds bounds, float padding)
    {
        Vector3 center = bounds.center;
        Vector3 direction = new Vector3(-1f, 1.15f, -1f).normalized;
        cam.transform.position = center - direction * 40f;
        cam.transform.LookAt(center);
        cam.orthographicSize = Mathf.Max(bounds.extents.x * 0.62f, bounds.extents.z * 0.72f, bounds.extents.y) + padding;
    }

    static Texture2D RenderCamera(Camera cam, int width, int height)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var previousActive = RenderTexture.active;
        var previousTarget = cam.targetTexture;
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();

        cam.targetTexture = previousTarget;
        RenderTexture.active = previousActive;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        return texture;
    }

    static void RequireInFrame(Camera cam, Bounds bounds, string label)
    {
        Vector3 vp = cam.WorldToViewportPoint(bounds.center);
        Require(vp.z > 0f, $"{label} is behind the QA camera.");
        Require(vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.02f && vp.y < 0.98f,
            $"{label} center is outside camera frame: {vp}");
    }

    static void RequireImageContrast(Texture2D image, string label)
    {
        float min = 1f;
        float max = 0f;
        int colored = 0;
        for (int y = 0; y < image.height; y += 12)
        {
            for (int x = 0; x < image.width; x += 12)
            {
                Color c = image.GetPixel(x, y);
                float lum = c.grayscale;
                min = Mathf.Min(min, lum);
                max = Mathf.Max(max, lum);
                if (Mathf.Abs(c.r - c.g) + Mathf.Abs(c.g - c.b) > 0.12f)
                    colored++;
            }
        }
        Require(max - min > 0.10f, $"{label} render looks blank or too flat.");
        Require(colored > 24, $"{label} render lacks colored team/prop pixels.");
    }

    static void RequireHasTeamColor(GameObject root, Color expected, string label)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer) continue;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null || !m.HasProperty("_Color")) continue;
                Color c = m.color;
                float delta = Mathf.Abs(c.r - expected.r) + Mathf.Abs(c.g - expected.g) + Mathf.Abs(c.b - expected.b);
                if (delta < 0.75f)
                    return;
            }
        }
        throw new InvalidOperationException($"{label} has no readable team-color material.");
    }

    static void ShowSelectionRing(GameObject root)
    {
        var lr = root.GetComponentInChildren<LineRenderer>(true);
        if (lr == null) return;
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = 0.08f;
        lr.positionCount = 24;
        lr.sharedMaterial = Prims.UnlitColorMat(Color.green);
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = (i / (float)lr.positionCount) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.65f, 0.02f, Mathf.Sin(a) * 0.65f));
        }
        lr.enabled = true;
    }

    static Renderer[] VisibleRenderers(GameObject root)
    {
        var list = new List<Renderer>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer) continue;
            if (r.name.IndexOf("BlobShadow", StringComparison.OrdinalIgnoreCase) >= 0) continue;
            list.Add(r);
        }
        return list.ToArray();
    }

    static Bounds CombinedBounds(List<SpawnedTarget> targets)
    {
        Require(targets.Count > 0, "No QA targets supplied.");
        var renderers = new List<Renderer>();
        foreach (SpawnedTarget t in targets)
            renderers.AddRange(VisibleRenderers(t.Root));
        return CombinedBounds(renderers.ToArray());
    }

    static Bounds CombinedBounds(Renderer[] renderers)
    {
        Require(renderers.Length > 0, "No renderers available for bounds.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void CreateLighting(Transform parent)
    {
        RenderSettings.ambientLight = new Color(0.45f, 0.48f, 0.50f);
        var lightGo = new GameObject("StabilizeQaSun");
        lightGo.transform.SetParent(parent, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
    }

    static void CreateGround(Transform parent)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "CameraQaGround";
        ground.transform.SetParent(parent, false);
        ground.transform.localScale = new Vector3(6f, 1f, 5f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = Prims.Mat(new Color(0.18f, 0.22f, 0.18f), 0f, 0.2f);
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
