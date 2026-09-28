using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace CaveDweller.Editor
{
    public static class MCPMenuFix
    {
        [MenuItem("Window/AB Unity MCP/Open Dashboard", false, -2)]
        public static void OpenDashboard()
        {
            MCPDashboardWindow.ShowWindow();
        }

        [MenuItem("Window/AB Unity MCP/Start Server", false, -1)]
        public static void StartServer()
        {
            Debug.Log("[MCPMenuFix] Starting AB-UMCP Server...");
            MCPBridgeServer.Start();
        }

        [MenuItem("Window/AB Unity MCP/Restart Server", false, 0)]
        public static void RestartServer()
        {
            Debug.Log("[MCPMenuFix] Restarting AB-UMCP Server...");
            MCPBridgeServer.Stop();
            EditorApplication.delayCall += () => MCPBridgeServer.Start();
        }

        [MenuItem("Window/AB Unity MCP/Stop Server", false, 1)]
        public static void StopServer()
        {
            Debug.Log("[MCPMenuFix] Stopping AB-UMCP Server...");
            MCPBridgeServer.Stop();
        }
    }
}
