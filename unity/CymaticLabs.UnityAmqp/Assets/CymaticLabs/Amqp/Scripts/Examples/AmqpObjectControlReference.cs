using UnityEngine;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// Registers a Unity object's transform for AMQP control via an ID and the
    /// <see cref="AmqpObjectListController"/>/
    /// </summary>
    public class AmqpObjectControlReference : MonoBehaviour
    {
        [Tooltip("The AMQP 'id' of the object.")]
        public string AmqpId;

        // Register on start
        private void Start()
        {
            // Self register with the global instance of the object list controller
            AmqpObjectListController.Instance.RegisterObject(this);
        }

        // Unregister on destroy
        private void OnDestroy()
        {
            AmqpObjectListController.Instance.UnregisterObject(this);
        }
    }
}
