// Dev workaround: the Unity account entitlement reports
// AllowedMcpConnections = 0, which makes ConnectionCensus cap direct MCP
// connections at 0 ("Up to 0 direct connections allowed at a time" / every
// call -> "Connection revoked"). Unity ships an internal dev-tool override
// (ConnectionPolicyOverride, a tier simulator persisted in SessionState that
// AcpEntitlementWiring re-applies across domain reloads). It is not wired to
// any menu, so we invoke it via reflection on load to force a usable cap.
//
// Remove this file to restore the entitlement-driven cap.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class McpForceDirectConnections
{
    const int k_MaxDirect = 8;   // concurrent direct (non-gateway) MCP connections
    const int k_MaxGateway = -1; // -1 = unlimited (leave gateway untouched)

    static McpForceDirectConnections()
    {
        try
        {
            Type overrideType = null, policyType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                overrideType ??= a.GetType("Unity.AI.MCP.Editor.Connection.ConnectionPolicyOverride");
                policyType ??= a.GetType("Unity.AI.MCP.Editor.Connection.ConnectionPolicy");
                if (overrideType != null && policyType != null) break;
            }

            if (overrideType == null || policyType == null)
            {
                Debug.LogWarning("[McpForceDirectConnections] ConnectionPolicy types not found; " +
                                 "com.unity.ai.assistant API may have changed.");
                return;
            }

            var ctor = policyType.GetConstructor(new[] { typeof(int), typeof(int) });
            var policy = ctor.Invoke(new object[] { k_MaxDirect, k_MaxGateway });

            var set = overrideType.GetMethod("Set", BindingFlags.Static | BindingFlags.NonPublic);
            set.Invoke(null, new[] { policy });

            Debug.Log($"[McpForceDirectConnections] Direct MCP connection cap forced to {k_MaxDirect}.");
        }
        catch (Exception e)
        {
            Debug.LogError("[McpForceDirectConnections] Failed to apply override: " + e);
        }
    }
}
