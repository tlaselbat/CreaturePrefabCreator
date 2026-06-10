using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.GeneratedPrefabs;
using CreaturePrefabCreator.Overrides;
using CreaturePrefabCreator.RuntimeModifiers;
using UnityEngine;

namespace CreaturePrefabCreator.Network
{
    /// <summary>
    /// Handles server-client config synchronization.
    /// Server sends its config hash and data to clients.
    /// Clients register prefabs using server config if available.
    /// </summary>
    public static class ConfigSyncManager
    {
        private static string _localConfigHash;
        private static CreaturePrefabCreatorConfigRoot _serverConfig;
        private static bool _isServer;
        private static bool _configReceivedFromServer;
        private static bool _isRegistered;

        public static void Initialize()
        {
            // Calculate local config hash
            _localConfigHash = CalculateConfigHash();
            CreaturePrefabCreatorPlugin.Instance?.Log($"Local config hash: {_localConfigHash}");

            // Determine if we're server (will be accurate once ZNet is initialized)
            _isServer = false; // Will check again when needed
        }

        /// <summary>
        /// Called when ZNet is available to determine server/client role.
        /// </summary>
        public static void OnZNetReady()
        {
            if (ZNet.instance == null) return;

            _isServer = ZNet.instance.IsServer();
            
            if (_isServer)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log("Running as server - config will be shared with clients.");
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.Log("Running as client - will request config from server if needed.");
            }
        }

        /// <summary>
        /// Called when joining a server. Returns true if we should wait for server config.
        /// </summary>
        public static bool ShouldWaitForServerConfig()
        {
            if (ZNet.instance == null) return false;
            if (_isServer) return false; // Server uses local config
            if (_configReceivedFromServer) return false; // Already have server config
            if (ZNet.instance.IsServer()) return false; // Single player / P2P host

            // Client on dedicated server - should wait
            CreaturePrefabCreatorPlugin.Instance?.Log("Client on dedicated server - will wait for server config.");
            return true;
        }

        /// <summary>
        /// Called when client connects to server. Requests config from server.
        /// </summary>
        public static void RequestServerConfig()
        {
            if (_isServer || ZNet.instance == null) return;

            CreaturePrefabCreatorPlugin.Instance?.Log("Requesting config from server...");
            
            // Create RPC package
            ZPackage pkg = new ZPackage();
            pkg.Write(_localConfigHash); // Send our hash so server knows if we need full config
            
            // Send to server
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, "CPC_RequestConfig", pkg);
        }

        /// <summary>
        /// Server receives config request from client.
        /// </summary>
        public static void OnServerReceiveConfigRequest(long sender, ZPackage pkg)
        {
            if (!_isServer) return;

            string clientHash = pkg.ReadString();
            string serverConfigJson = SerializeConfigToJson(CreaturePrefabCreatorPlugin.Instance?.LoadedConfig);
            string serverHash = CalculateHash(serverConfigJson);

            // If client has different config, send full config
            if (clientHash != serverHash)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"Client {sender} has different config. Sending server config...");

                ZPackage response = new ZPackage();
                response.Write(serverConfigJson);
                response.Write(serverHash);
                
                ZRoutedRpc.instance?.InvokeRoutedRPC(sender, "CPC_SendConfig", response);
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"Client {sender} has matching config.");
            }
        }

        /// <summary>
        /// Client receives config from server.
        /// </summary>
        public static void OnClientReceiveConfig(ZPackage pkg)
        {
            if (_isServer) return;

            string configJson = pkg.ReadString();
            string serverHash = pkg.ReadString();
            
            CreaturePrefabCreatorPlugin.Instance?.Log("Received config from server.");

            try
            {
                _serverConfig = DeserializeConfigFromJson(configJson);
                
                // Validate config before applying
                if (!ValidateConfig(_serverConfig))
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogError("Server config validation failed. Rejecting config.");
                    _serverConfig = null;
                    _configReceivedFromServer = false;
                    return;
                }
                
                _configReceivedFromServer = true;

                // Check if different from local
                if (serverHash != _localConfigHash)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning("Server config differs from local. Will use server config for prefabs.");
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance?.Log("Server config matches local.");
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Failed to parse server config: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers prefabs using server config if available, otherwise local.
        /// </summary>
        public static void RegisterPrefabsWithServerConfig()
        {
            if (_isRegistered) return;
            _isRegistered = true;

            CreaturePrefabCreatorConfigRoot config = _serverConfig ?? CreaturePrefabCreatorPlugin.Instance?.LoadedConfig;
            
            if (config == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError("No config available for prefab registration!");
                return;
            }

            CreaturePrefabCreatorPlugin.Instance?.Log("Registering prefabs...");

            // Phase 4.3: Config reload state management
            // Restore all runtime AI states before applying new config
            try
            {
                RuntimeModifierManager.ClearAll();
                CreaturePrefabCreatorPlugin.Instance?.Log("[ConfigReload] Cleared all runtime AI states before config reload");
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[ConfigReload] Failed to clear runtime states: {ex.Message}");
            }

            // Overrides must run first so inherited scale multipliers are registered
            // before GenerateAll looks them up for generated variants.
            PrefabOverrideManager.ApplyAll(config.PrefabOverrides);
            GeneratedPrefabManager.GenerateAll(config.GeneratedPrefabs);
            RuntimeModifierManager.Initialize(config.RuntimeModifiers);
        }

        private static string CalculateConfigHash()
        {
            string json = SerializeConfigToJson(CreaturePrefabCreatorPlugin.Instance?.LoadedConfig);
            return CalculateHash(json);
        }

        private static string CalculateHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
            }
        }

        private static string SerializeConfigToJson(CreaturePrefabCreatorConfigRoot config)
        {
            if (config == null) return "{}";
            
            using (var stream = new MemoryStream())
            {
                var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(CreaturePrefabCreatorConfigRoot));
                serializer.WriteObject(stream, config);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static CreaturePrefabCreatorConfigRoot DeserializeConfigFromJson(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(CreaturePrefabCreatorConfigRoot));
                return (CreaturePrefabCreatorConfigRoot)serializer.ReadObject(stream);
            }
        }

        /// <summary>
        /// Gets the config to use (server config if available, otherwise local).
        /// </summary>
        public static CreaturePrefabCreatorConfigRoot GetActiveConfig()
        {
            return _serverConfig ?? CreaturePrefabCreatorPlugin.Instance?.LoadedConfig;
        }

        /// <summary>
        /// Validates config structure and version compatibility.
        /// </summary>
        private static bool ValidateConfig(CreaturePrefabCreatorConfigRoot config)
        {
            if (config == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError("Config validation failed: Config is null");
                return false;
            }

            // Check version compatibility
            if (string.IsNullOrEmpty(config.Version))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning("Config validation: Missing version field, assuming compatibility");
            }
            else if (!IsVersionCompatible(config.Version))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Config validation failed: Version {config.Version} is not compatible with current plugin version");
                return false;
            }

            // Check schema version
            if (config.SchemaVersion < 1)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Config validation failed: Schema version {config.SchemaVersion} is too old");
                return false;
            }
            else if (config.SchemaVersion > 1)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Config validation: Schema version {config.SchemaVersion} is newer than expected. Some features may not work.");
            }

            // Validate required collections
            if (config.GeneratedPrefabs == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning("Config validation: GeneratedPrefabs is null, initializing empty collection");
                config.GeneratedPrefabs = new System.Collections.Generic.List<GeneratedPrefabConfig>();
            }

            if (config.PrefabOverrides == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning("Config validation: PrefabOverrides is null, initializing empty collection");
                config.PrefabOverrides = new System.Collections.Generic.List<PrefabOverrideConfig>();
            }

            if (config.RuntimeModifiers == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning("Config validation: RuntimeModifiers is null, initializing empty collection");
                config.RuntimeModifiers = new System.Collections.Generic.List<RuntimeModifierConfig>();
            }

            CreaturePrefabCreatorPlugin.Instance?.Log($"Config validation passed: Version {config.Version}, Schema {config.SchemaVersion}");
            return true;
        }

        /// <summary>
        /// Checks if the config version is compatible with the current plugin.
        /// Simple version check - allows same major version.
        /// </summary>
        private static bool IsVersionCompatible(string version)
        {
            try
            {
                var versionParts = version.Split('.');
                if (versionParts.Length >= 1 && int.TryParse(versionParts[0], out int majorVersion))
                {
                    // Allow same major version (1.x.x)
                    return majorVersion == 1;
                }
            }
            catch
            {
                // If parsing fails, assume compatibility for safety
            }
            return true;
        }
    }
}
