using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CymaticLabs.Unity3D.Amqp.SimpleJSON;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Edits the AMQP connection list.
    /// </summary>
    public class AmqpConfigurationEditor : EditorWindow
    {
        #region Fields

        // The connection index
        static int index = 0, lastIndex = 0;

        // The internal list of connections
        static List<AmqpConnection> connections = new List<AmqpConnection>();

        // Connection values
        static string Name = "localhost";
        static string Host = "localhost";
        static int AmqpPort = 5672;
        static int WebPort = 15672;
        static string VirtualHost = "/";
        static string Username;
        static string Password;
        static short ReconnectInterval = 5;
        static ushort RequestedHeartBeat = 30;
        
        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the path to the project's Assets/Resources directory.
        /// </summary>
        public static string ResourcesDirectory
        {
            get { return Path.Combine(Application.dataPath, "Resources"); }
        }


        /// <summary>
        /// Gets the file name for the AMQP connections data.
        /// </summary>
        public static string ConfigurationFilename
        {
            get { return Path.Combine(ResourcesDirectory, AmqpClient.ConfigurationFilename); }
        }

        /// <summary>
        /// Gets whether or not the editor window has been initialized yet.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        #endregion Properties

        #region Methods

        #region Init

        // Initialize the editor window
        [MenuItem("AMQP/Configuration")]
        static void Init()
        {
            EditorWindow window = GetWindow(typeof(AmqpConfigurationEditor), false, "AMQP");
            window.Show();
            LoadConfiguration();

            try
            {
                if (!IsInitialized)
                {
                    IsInitialized = true;

                    // If so, update the form to the new connections values
                    var c = connections[index];
                    Name = c.Name;
                    Host = c.Host;
                    AmqpPort = c.AmqpPort;
                    WebPort = c.WebPort;
                    VirtualHost = c.VirtualHost;
                    Username = c.Username;
                    Password = c.Password;
                    ReconnectInterval = c.ReconnectInterval;
                    RequestedHeartBeat = c.RequestedHeartBeat;
                }
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("{0}", ex);
            }
        }

        #endregion Init

        #region Rendering

        // Draw the GUI for the editor window
        void OnGUI()
        {
            //var selectedObject = Selection.activeGameObject;

            // Get the current list of loaded connection names
            var connectionNames = new List<string>();
            foreach (var c in connections) connectionNames.Add(c.Name);

            // Connections drop down
            EditorGUILayout.LabelField("AMQP Connections");
            index = EditorGUILayout.Popup(index, connectionNames.ToArray());

            // Check to see if the connection index has changed
            if (index != lastIndex)
            {
                // If so, update the form to the new connections values
                var c = connections[index];
                Name = c.Name;
                Host = c.Host;
                AmqpPort = c.AmqpPort;
                WebPort = c.WebPort;
                VirtualHost = c.VirtualHost;
                Username = c.Username;
                Password = c.Password;
                ReconnectInterval = c.ReconnectInterval;
                RequestedHeartBeat = c.RequestedHeartBeat;
            }

            // Create/Delete buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) SaveConfiguration();
            if (GUILayout.Button("Delete")) DeleteConnection();
            EditorGUILayout.EndHorizontal();

            // Name
            Name = EditorGUILayout.TextField("Name", Name);

            // Host
            Host = EditorGUILayout.TextField("Host", Host);

            // AmqpPort
            var amqpPort = EditorGUILayout.TextField("AMQP Port", AmqpPort.ToString());
            int.TryParse(amqpPort, out AmqpPort);

            // WebPort
            var webPort = EditorGUILayout.TextField("Web Port", WebPort.ToString());
            int.TryParse(webPort, out WebPort);

            // VirtualHost
            VirtualHost = EditorGUILayout.TextField("Virtual Host", VirtualHost);

            // Username
            Username = EditorGUILayout.TextField("Username", Username);

            // Password
            Password = EditorGUILayout.PasswordField("Password", Password);

            // ReconnectInterval
            var reconnectInterval = EditorGUILayout.TextField("Reconnect Interval", ReconnectInterval.ToString());
            short.TryParse(reconnectInterval, out ReconnectInterval);

            // RequestedHeartBeat
            var requestedHeartBeat = EditorGUILayout.TextField("Requested Heart Beat", RequestedHeartBeat.ToString());
            ushort.TryParse(requestedHeartBeat, out RequestedHeartBeat);



            // Update the last index
            lastIndex = index;
        }

        #endregion Rendering

        #region Load/Save

        /// <summary>
        /// Loads AMQP configuration from disk.
        /// </summary>
        /// <param name="refreshAssets">When True, a refresh of the asset database will be forced.</param>
        public static void LoadConfiguration(bool refreshAssets = false)
        {
            // Ensure that a AMQP configuration file exists
            EnsureConfigurationFile();

            // Look for connections file
            var filename = ConfigurationFilename;
            connections.Clear();

            if (refreshAssets)
            {
                // Update Unity assets (needed to get the editor to see these new assets)
                AssetDatabase.Refresh();
            }

            if (File.Exists(filename))
            {
                // Parse connection JSON data
                try
                {
                    var jsonText = File.ReadAllText(filename);
                    var config = JSON.Parse(jsonText).AsObject;
                    var jsonConnections = config["Connections"].AsArray;

                    // Populate the connection list from the ata
                    for (int i = 0; i < jsonConnections.Count; i++)
                        connections.Add(AmqpConnection.FromJsonObject(jsonConnections[i].AsObject));
                }
                catch (Exception ex)
                {
                    Debug.LogErrorFormat("{0}", ex);
                }
            }
        }

        // Saves the current connections to disk
        static void SaveConfiguration(bool createConnection = true)
        {
            // Check to see if the current values are for a new connection
            var isNew = true;

            foreach (var c in connections)
            {
                // This is not a new connection...
                if (c.Name == Name)
                {
                    isNew = false;
                    break;
                }
            }

            // If this is a new connection, add it to the list before writing to disk
            if (createConnection && isNew)
            {
                var c = new AmqpConnection();
                c.Name = Name;
                c.Host = Host;
                c.AmqpPort = AmqpPort;
                c.WebPort = WebPort;
                c.VirtualHost = VirtualHost;
                c.Username = Username;
                c.Password = Password;
                c.ReconnectInterval = ReconnectInterval;
                c.RequestedHeartBeat = RequestedHeartBeat;
                connections.Add(c);
            }

            // Serialize and save to disk
            try
            {
                var config = new AmqpConfiguration();
                config.Connections = connections.ToArray();
                File.WriteAllText(ConfigurationFilename, JsonUtility.ToJson(config, true));
                LoadConfiguration(true);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("{0}", ex);
            }
        }

        // Deletes the current connection and save the changes to disk
        static void DeleteConnection()
        {
            connections.RemoveAt(index);
            SaveConfiguration(false);
        }

        #endregion Load/Save

        #region Utility

        // Ensures that an AMQP configuration file exists
        static void EnsureConfigurationFile()
        {
            try
            {
                var resourcesDir = ResourcesDirectory;

                // Ensure the resources directory
                if (!Directory.Exists(resourcesDir)) Directory.CreateDirectory(resourcesDir);

                // Ensure the configuration file
                var configFilename = ConfigurationFilename;

                if (!File.Exists(configFilename))
                {
                    var config = new AmqpConfiguration();

                    config.Connections = new AmqpConnection[]
                    {
                        new AmqpConnection()
                        {
                            Name = "localhost",
                            Host = "localhost",
                            AmqpPort = 5672,
                            WebPort = 15672,
                            VirtualHost = "/",
                            Username = "guest",
                            Password = "guest",
                            ReconnectInterval = 5,
                            RequestedHeartBeat = 30
                        }
                    };

                    File.WriteAllText(configFilename, JsonUtility.ToJson(config, true));

                    // Update Unity assets (needed to get the editor to see these new assets)
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("{0}", ex);
            }
        }

        /// <summary>
        /// Gets the current list of connection names.
        /// </summary>
        /// <returns>The current list of connection names.</returns>
        public static string[] GetConnectionNames()
        {
            var connectionNames = new List<string>();
            foreach (var c in connections) connectionNames.Add(c.Name);
            return connectionNames.ToArray();
        }

        /// <summary>
        /// Gets the current list of connection names.
        /// </summary>
        /// <returns>The current list of connection names.</returns>
        public static AmqpConnection[] GetConnections()
        {
            return connections.ToArray();
        }

        #endregion Utility

        #endregion Methods
    }
}

