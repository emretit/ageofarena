#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batchmode visual smoke gate for the procedural/model hybrid factories. It does
/// not replace human camera QA, but it catches missing Resources paths and broken
/// root components before a playable build is made.
/// </summary>
public static class VisualFactoryValidator
{
    struct UnitVisualSpec
    {
        public readonly UnitType Type;
        public readonly string ExpectedChildName;

        public UnitVisualSpec(UnitType type, string expectedChildName)
        {
            Type = type;
            ExpectedChildName = expectedChildName;
        }
    }

    static readonly UnitVisualSpec[] MountedUnits =
    {
        new UnitVisualSpec(UnitType.Scout, "Horse"),
        new UnitVisualSpec(UnitType.Cavalry, "Horse"),
        new UnitVisualSpec(UnitType.CavalryArcher, "Horse"),
        new UnitVisualSpec(UnitType.Mangudai, "Horse"),
        new UnitVisualSpec(UnitType.Cataphract, "Horse_White"),
    };

    static readonly BuildingType[] PolishedBuildings =
    {
        BuildingType.ArcheryRange,
        BuildingType.Stable,
        BuildingType.Market,
        BuildingType.Farm,
        BuildingType.MiningCamp,
        BuildingType.Blacksmith,
        BuildingType.Monastery,
        BuildingType.University,
        BuildingType.Dock,
        BuildingType.SiegeWorkshop,
        BuildingType.Outpost,
        BuildingType.WatchTower,
        BuildingType.BombardTower,
        BuildingType.Wonder,
    };

    [MenuItem("Tools/Age of Arena/Validate Visual Factories")]
    public static void ValidateFromMenu()
    {
        ValidateOrThrow();
        Debug.Log("[VisualFactoryValidator] Visual factory validation passed.");
    }

    public static void ValidateFromCommandLine()
    {
        try
        {
            ValidateOrThrow();
            Debug.Log("[VisualFactoryValidator] Visual factory validation passed.");
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[VisualFactoryValidator] Visual factory validation failed:\n" + ex);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    static void ValidateOrThrow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RequireResource<GameObject>("Quaternius/Animals/Horse");
        RequireResource<GameObject>("Quaternius/Animals/Horse_White");
        RequireResource<GameObject>("Quaternius/Animals/Donkey");
        RequireResource<GameObject>("Kenney/FantasyTown/cart");

        var root = new GameObject("VisualFactoryValidatorRoot");
        try
        {
            ValidateMountedUnits(root.transform);
            ValidateTradeCart(root.transform);
            ValidateBuildings(root.transform);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static void ValidateMountedUnits(Transform root)
    {
        for (int i = 0; i < MountedUnits.Length; i++)
        {
            UnitVisualSpec spec = MountedUnits[i];
            var unit = UnitFactory.Spawn(spec.Type, root, new Vector3(i * 3f, 0f, 0f), 1);
            Require(unit != null, $"{spec.Type} did not spawn.");
            Require(unit.type == spec.Type, $"{spec.Type} spawned with type {unit.type}.");
            Require(unit.teamId == 1, $"{spec.Type} teamId was not preserved.");
            Require(unit.GetComponent<CapsuleCollider>() != null, $"{spec.Type} missing CapsuleCollider.");
            Require(unit.GetComponentInChildren<SelectionRing>(true) != null, $"{spec.Type} missing SelectionRing.");
            RequireDescendantNamed(unit.transform, spec.ExpectedChildName, $"{spec.Type} missing Quaternius {spec.ExpectedChildName} visual.");
            RequireRendererBounds(unit.gameObject, spec.Type.ToString(), maxExtent: 8f);
        }
    }

    static void ValidateTradeCart(Transform root)
    {
        var cart = UnitFactory.Spawn(UnitType.TradeCart, root, new Vector3(0f, 0f, 8f), 2);
        Require(cart != null, "TradeCart did not spawn.");
        Require(cart.type == UnitType.TradeCart, $"TradeCart spawned with type {cart.type}.");
        Require(cart.teamId == 2, "TradeCart teamId was not preserved.");
        Require(cart.GetComponent<CapsuleCollider>() != null, "TradeCart missing CapsuleCollider.");
        Require(cart.GetComponentInChildren<SelectionRing>(true) != null, "TradeCart missing SelectionRing.");
        RequireDescendantNamed(cart.transform, "Donkey", "TradeCart missing Quaternius Donkey visual.");
        RequireDescendantNamed(cart.transform, "cart", "TradeCart missing Kenney cart visual.");
        RequireRendererBounds(cart.gameObject, "TradeCart", maxExtent: 8f);
    }

    static void ValidateBuildings(Transform root)
    {
        for (int i = 0; i < PolishedBuildings.Length; i++)
        {
            BuildingType type = PolishedBuildings[i];
            var go = BuildingFactory.Create(type, root, new Vector3((i % 7) * 5f, 0f, 16f + (i / 7) * 5f), TeamPalette.For(i % 4));
            Require(go != null, $"{type} did not spawn.");
            Require(go.GetComponent<BuildingEntity>() != null, $"{type} missing BuildingEntity.");
            Require(go.GetComponent<BoxCollider>() != null, $"{type} missing BoxCollider.");
            RequireRendererBounds(go, type.ToString(), maxExtent: 18f);

            if (type == BuildingType.Stable)
                RequireDescendantNamed(go.transform, "Horse", "Stable missing horse prop.");
            else if (type == BuildingType.Market)
            {
                RequireDescendantNamed(go.transform, "Donkey", "Market missing donkey prop.");
                RequireDescendantNamed(go.transform, "cart", "Market missing cart prop.");
            }
        }
    }

    static void RequireResource<T>(string path) where T : UnityEngine.Object
    {
        Require(Resources.Load<T>(path) != null, $"Missing Resources asset: {path}");
    }

    static void RequireDescendantNamed(Transform root, string namePart, string message)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
                return;
        }
        throw new InvalidOperationException(message);
    }

    static void RequireRendererBounds(GameObject go, string label, float maxExtent)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        Require(renderers.Length > 0, $"{label} has no renderers.");

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Require(bounds.size.y > 0.05f, $"{label} renderer bounds are too flat: {bounds.size}.");
        Require(bounds.size.x <= maxExtent && bounds.size.y <= maxExtent && bounds.size.z <= maxExtent,
            $"{label} renderer bounds look oversized: {bounds.size}.\n{DescribeRenderers(renderers)}");
        Require(bounds.min.y > -1.0f, $"{label} renderer bounds dip too far below ground: minY={bounds.min.y:0.###}.");
    }

    static string DescribeRenderers(Renderer[] renderers)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            sb.Append(" - ")
                .Append(r.GetType().Name)
                .Append(" ")
                .Append(GetPath(r.transform))
                .Append(" bounds=")
                .Append(r.bounds.size)
                .Append(" center=")
                .Append(r.bounds.center)
                .Append(" localScale=")
                .Append(r.transform.localScale)
                .AppendLine();
        }
        return sb.ToString();
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
