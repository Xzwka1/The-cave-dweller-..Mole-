using UnityEditor;
using UnityMCP.Editor;

namespace CaveDweller.Editor
{
    public static class OpenMCPDashboard
    {
        [MenuItem("Window/AB Unity MCP/Dashboard", false, -1)]
        public static void Open()
        {
            MCPDashboardWindow.ShowWindow();
        }

        [MenuItem("Window/AB Unity MCP/Switch Port to 7891 & Restart", false, 1)]
        public static void SwitchTo7891()
        {
            MCPSettingsManager.UseManualPort = true;
            MCPSettingsManager.Port = 7891;
            MCPBridgeServer.Stop();
            MCPBridgeServer.Start();
            EditorUtility.DisplayDialog("AB Unity MCP", "Port switched to 7891 and Server restarted!", "OK");
        }

        [MenuItem("Window/AB Unity MCP/Switch Port to Auto & Restart", false, 2)]
        public static void SwitchToAuto()
        {
            MCPSettingsManager.UseManualPort = false;
            MCPBridgeServer.Stop();
            MCPBridgeServer.Start();
            EditorUtility.DisplayDialog("AB Unity MCP", "Port switched to Auto and Server restarted!", "OK");
        }
    }
}
