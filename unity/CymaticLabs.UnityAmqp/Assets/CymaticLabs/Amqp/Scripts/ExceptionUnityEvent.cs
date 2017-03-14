using System;
using UnityEngine.Events;

namespace CymaticLabs.Unity3D.Amqp
{
    /// <summary>
    /// A Unity-specific event for exception-based events.
    /// </summary>
    [Serializable]
    public class ExceptionUnityEvent : UnityEvent<Exception>
    {
    }
}
