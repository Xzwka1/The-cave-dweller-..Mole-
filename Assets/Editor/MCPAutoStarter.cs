using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CaveDweller.EditorTools
{
    [InitializeOnLoad]
    public static class MCPAutoStarter
    {
        static MCPAutoStarter()
        {
            EditorApplication.delayCall += EnsureStarted;
        }

        [MenuItem("Tools/Start AnkleBreaker MCP Bridge")]
        public static void EnsureStarted()
        {
            var serverType = Type.GetType("UnityMCP.Editor.MCPBridgeServer, AnkleBreaker.UnityMCP.Editor");
            if (serverType != null)
            {
                var isRunningProp = serverType.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
                var activePortProp = serverType.GetProperty("ActivePort", BindingFlags.Public | BindingFlags.Static);
                var startMethod = serverType.GetMethod("Start", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

                bool isRunning = (bool)(isRunningProp?.GetValue(null) ?? false);
                if (!isRunning)
                {
                    Debug.Log("[MCPAutoStarter] Starting AnkleBreaker MCP Bridge...");
                    startMethod?.Invoke(null, null);
                }
                else
                {
                    int port = (int)(activePortProp?.GetValue(null) ?? 0);
                    Debug.Log($"[MCPAutoStarter] AnkleBreaker MCP Bridge is already running on port {port}");
                }
            }
        }
    }
}
