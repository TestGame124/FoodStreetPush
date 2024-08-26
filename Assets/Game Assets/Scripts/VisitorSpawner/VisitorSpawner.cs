using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class VisitorSpawner : MonoBehaviour
{
    Zone zone;
    
    ObjectPool<VisitorBehaviour> visitorPool;
    [SerializeField] VisitorBehaviour visitorPrefab;

    private const int visitorPoolSize = 10;

    [SerializeField]int maxVisitors = 1;
   
    [SerializeField] List<VisitorBehaviour> activeVisitors = new();

    private void Awake()
    {
        visitorPool = new ObjectPool<VisitorBehaviour>(() => { 
            VisitorBehaviour visitor = Instantiate(visitorPrefab, transform.position, transform.rotation);
            visitor.OnDisableEvent += visitorPool.Release;
            visitor.OnDisableEvent += DecreaseVisitors;

            return visitor;
        }, visitor =>
        {

            visitor.gameObject.SetActive(true);
        }, visitor =>
        {
            if (activeVisitors.Contains(visitor))
            {
                activeVisitors.Remove(visitor);
            }
            visitor.gameObject.SetActive(false);
        }, visitor =>
        {
            Destroy(visitor.gameObject);
        },false,visitorPoolSize,30);
    }
    private void OnEnable()
    {
        OrdersBoardUI.OnOrderClaimed += IncreaseVisitors;
    }

    private void OnDisable()
    {
        OrdersBoardUI.OnOrderClaimed -= IncreaseVisitors;
    }
    public void Initialize(Zone zone)
    {
        this.zone = zone;

        StartCoroutine(SpawnWithDelay());
       
        UpdateOrdersNeeded();

    }

    private void IncreaseVisitors()
    {
        if(OrderSystem.instance.TotalOrdersClaimed > 1)
            maxVisitors++;
        
        UpdateOrdersNeeded();

    }
    private void DecreaseVisitors(VisitorBehaviour visitor)
    {
        if (maxVisitors > 1)
            maxVisitors--;
        UpdateOrdersNeeded();

    }

    private void UpdateOrdersNeeded()
    {
        int ordersNeeded = maxVisitors - OrderSystem.instance.TotalOrdersClaimed;
        UIGame.GetUI().ordersNeededText.text = ordersNeeded.ToString();

    }

    public VisitorBehaviour SpawnVisitor()
    {
        VisitorBehaviour visitor = visitorPool.Get();
        int randStarting = Random.Range(0, zone.exitPositions.Length);

        visitor.transform.position = zone.exitPositions[randStarting].position;
        visitor.transform.rotation = transform.rotation;

        visitor.transform.DOScale(1, .5f).From(0).SetEase(Ease.InBack).OnComplete(() =>
        {
            int randomPos = Random.Range(0,zone.exitPositions.Length);
            visitor.Initialize(zone, zone.exitPositions[randomPos].position);

        });
        
        if (!activeVisitors.Contains(visitor))
        {
            activeVisitors.Add(visitor);
        }
        return visitor;
    }

    IEnumerator SpawnWithDelay()
    {
        while (true)
        {
            if(activeVisitors.Count < maxVisitors)
            {
                
                int randTime = Random.Range(3, 8);
                yield return new WaitForSeconds(randTime);
                SpawnVisitor();
            }
            
            yield return new WaitForSeconds(1);
        }
    }

}
