using System;
using System.Collections;
using UnityEngine;

public class ObjectPool_Effect : MonoBehaviour, IPoolObject
{
    #region IPoolObject
    public float waitTime = 0.0f;

    private GameObject prefab = null;
    public GameObject Prefab
    {
        get
        {
            return prefab;
        }
        set
        {
            prefab = value;
        }
    }


    public void Start()
    {
        if (waitTime == 0)
        {
            return;
        }
        if (gameObject.activeSelf && gameObject.activeInHierarchy)
        {
            StartCoroutine(AutoPushToPool(waitTime));
        }
    }

    public void OnPushToPool()
    {
    }

    public IEnumerator AutoPushToPool(float timeDead)
    {
        yield return new WaitForSeconds(timeDead);
        ObjectPool.Instance.PushToPool(this, gameObject);
    }
    #endregion

}
