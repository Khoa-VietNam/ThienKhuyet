using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThienKhuyet.Core
{
    /// <summary>Pool of inactive GameObjects created by a factory. Used for projectiles, VFX, damage numbers, pickups and chunk props.</summary>
    public sealed class GameObjectPool
    {
        readonly Func<GameObject> factory;
        readonly Stack<GameObject> free = new Stack<GameObject>();
        readonly Transform root;
        int created;

        public int CreatedCount => created;
        public int FreeCount => free.Count;

        public GameObjectPool(string name, Func<GameObject> factory, Transform parent, int prewarm = 0)
        {
            this.factory = factory;
            var r = new GameObject("Pool_" + name);
            r.transform.SetParent(parent, false);
            root = r.transform;
            for (int i = 0; i < prewarm; i++) free.Push(Create());
        }

        GameObject Create()
        {
            GameObject go = factory();
            go.SetActive(false);
            go.transform.SetParent(root, false);
            created++;
            return go;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject go = null;
            while (free.Count > 0 && go == null) go = free.Pop();
            if (go == null) go = Create();
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return go;
        }

        public GameObject Get(Vector3 position)
        {
            return Get(position, Quaternion.identity);
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(root, false);
            free.Push(go);
        }
    }

    /// <summary>Registry of named pools living under one root object. Reset between play sessions.</summary>
    public static class Pools
    {
        static readonly Dictionary<string, GameObjectPool> pools = new Dictionary<string, GameObjectPool>();
        static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pools.Clear();
            root = null;
        }

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("~Pools");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        public static GameObjectPool Get(string id, Func<GameObject> factory, int prewarm = 0)
        {
            if (pools.TryGetValue(id, out GameObjectPool p) && p != null) return p;
            p = new GameObjectPool(id, factory, Root, prewarm);
            pools[id] = p;
            return p;
        }

        public static void Clear()
        {
            pools.Clear();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            root = null;
        }
    }

    /// <summary>Returns a pooled object to its pool (or destroys it) after a delay. Uses scaled or unscaled time.</summary>
    public sealed class AutoRelease : MonoBehaviour
    {
        GameObjectPool pool;
        float timer;
        bool unscaled;

        public void Arm(GameObjectPool owner, float seconds, bool useUnscaledTime = false)
        {
            pool = owner;
            timer = seconds;
            unscaled = useUnscaledTime;
            enabled = true;
        }

        void Update()
        {
            timer -= unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (timer > 0f) return;
            enabled = false;
            if (pool != null) pool.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
