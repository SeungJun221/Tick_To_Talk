using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace TickToTalk.Editor
{
    internal static class RelayEditorBootstrap
    {
        [InitializeOnLoadMethod]
        static void ResumeRequestedConnection()
        {
            const string request = "Temp/ttt-bridge-request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            EditorApplication.delayCall += () =>
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("MCPForUnity.Editor.Services.Transport.Transports.StdioBridgeHost")).FirstOrDefault(t => t != null);
                type?.GetMethod("Start", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
            };
        }
    }
}




