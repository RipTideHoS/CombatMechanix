using System;
using UnityEngine;

// Test script to verify SignalR assembly loading
public class SignalRTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== SignalR Assembly Test ===");
        
        try
        {
            // Test 1: Can we load the SignalR Client assembly?
            var signalRAssembly = System.Reflection.Assembly.LoadFrom(
                Application.dataPath + "/Plugins/SignalR/Microsoft.AspNetCore.SignalR.Client.dll");
            Debug.Log("✅ SignalR Client assembly loaded: " + signalRAssembly.FullName);
            
            // Test 2: Can we find the HubConnection type?
            var hubConnectionType = signalRAssembly.GetType("Microsoft.AspNetCore.SignalR.Client.HubConnection");
            if (hubConnectionType != null)
            {
                Debug.Log("✅ HubConnection type found: " + hubConnectionType.FullName);
            }
            else
            {
                Debug.LogError("❌ HubConnection type not found in assembly");
            }
            
            // Test 3: List all available assemblies
            Debug.Log("=== All Loaded Assemblies ===");
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (assembly.FullName.Contains("SignalR") || assembly.FullName.Contains("AspNetCore"))
                {
                    Debug.Log("🔍 Found: " + assembly.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("❌ Assembly test failed: " + ex.Message);
        }
    }
}