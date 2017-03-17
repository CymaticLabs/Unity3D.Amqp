using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Custom editor for the <see cref="AmqpClient"/> class.
    /// </summary>
    [CustomEditor(typeof(AmqpClient))]
    public class AmqpClientEditor : Editor
    {
        #region Fields

        // The index of the selected connection
        int index = 0, lastIndex = 0;

        // The target instance being edited
        AmqpClient client;

        // The name of the selected connection
        SerializedProperty connection;

        #endregion Fields

        #region Methods

        private void OnEnable()
        {
            // Reference the selected client
            client = (AmqpClient)target;

            // Get a reference to the serialized connection property
            connection = serializedObject.FindProperty("Connection");

            // Load configuration data
            AmqpConfigurationEditor.LoadConfiguration();

            // Restore the connection index
            var connectionNames = AmqpConfigurationEditor.GetConnectionNames();

            for (var i = 0; i < connectionNames.Length; i++)
            {
                var cName = connectionNames[i];
                if (connection.stringValue == cName)
                {
                    index = i;
                    break;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            // Update client
            serializedObject.Update();

            // Generate the connection dropdown options/content
            var connectionNames = AmqpConfigurationEditor.GetConnectionNames();
            var options = new List<GUIContent>();

            for (var i = 0; i < connectionNames.Length; i++)
            {
                var cName = connectionNames[i];
                if (string.IsNullOrEmpty(client.Connection) || client.Connection == cName) index = i;
                options.Add(new GUIContent(cName));
            }

            // Connections drop down
            string tooltip = "Select the AMQP connection to use. Connections can be configured in the AMQP/Configuration menu.";
            index = EditorGUILayout.Popup(new GUIContent("Connection", tooltip), index, options.ToArray());

            // If the index has changed, record the change
            if (index != lastIndex) Undo.RecordObject(target, "Undo Connection change");

            // Set the connection name based on dropdown value
            client.Connection = connection.stringValue = options[index].text;

            // Draw the rest of the inspector's default layout
            DrawDefaultInspector();

            // Save/serialized modified connection
            serializedObject.ApplyModifiedProperties();

            // Update the last connection index
            lastIndex = index;
        }

        #endregion Methods
    }
}
