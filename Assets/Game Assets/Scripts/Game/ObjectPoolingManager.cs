using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;


public class ObjectPoolingManager : MonoBehaviour
{
    public static ObjectPoolingManager Instance { get; private set; }

    private static Dictionary<System.Type, object> poolDictionary = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            poolDictionary = new Dictionary<System.Type, object>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void CreatePool<T>(T prefab, int initialSize, int maxSize) where T : Component
    {
        System.Type type = typeof(T);
        if (!poolDictionary.ContainsKey(type))
        {
            IObjectPool<T> pool = new ObjectPool<T>(
                createFunc: () => Instantiate(prefab),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false),
                actionOnDestroy: obj => Destroy(obj.gameObject),
                defaultCapacity: initialSize,
                maxSize: maxSize
            );

            poolDictionary.Add(type, pool);

            // Pre-warm the pool
            for (int i = 0; i < initialSize; i++)
            {
                T obj = pool.Get();
                pool.Release(obj);
            }
        }
    }

    public static T GetFromPool<T>() where T : Component
    {
        System.Type type = typeof(T);
        if (poolDictionary.ContainsKey(type))
        {
            return ((IObjectPool<T>)poolDictionary[type]).Get();
        }
        else
        {
            Debug.LogWarning($"Pool for type {type} doesn't exist.");
            return null;
        }
    }

    public static void ReturnToPool<T>(T obj) where T : Component
    {
        System.Type type = typeof(T);
        if (poolDictionary.ContainsKey(type))
        {
            ((IObjectPool<T>)poolDictionary[type]).Release(obj);
        }
        else
        {
            Debug.LogWarning($"Pool for type {type} doesn't exist.");
            Destroy(obj.gameObject);
        }
    }
}
