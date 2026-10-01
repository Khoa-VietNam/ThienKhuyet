using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>Typed publish/subscribe hub that keeps gameplay systems decoupled. Cleared on every play session.</summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            handlers.Clear();
        }

        public static void Clear()
        {
            handlers.Clear();
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type t = typeof(T);
            handlers.TryGetValue(t, out Delegate existing);
            handlers[t] = Delegate.Combine(existing, handler);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type t = typeof(T);
            if (!handlers.TryGetValue(t, out Delegate existing)) return;
            Delegate next = Delegate.Remove(existing, handler);
            if (next == null) handlers.Remove(t);
            else handlers[t] = next;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!handlers.TryGetValue(typeof(T), out Delegate d)) return;
            Action<T> a = d as Action<T>;
            if (a == null) return;
            // Copy the invocation list so handlers may unsubscribe while being invoked.
            Delegate[] list = a.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try
                {
                    ((Action<T>)list[i])(evt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
