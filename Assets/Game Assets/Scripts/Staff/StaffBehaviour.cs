using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class StaffBehaviour : MonoBehaviour
{

    public static int MOVE_HASH = Animator.StringToHash("Move");
    public static int HAND_HASH = 1;

    Zone zone;
    NavMeshAgent agent;
    Animator animator;

    public enum StaffStates
    {
        Free,
        Serving,
        ReturnToWaiting
    }
    public StaffStates currentState;
    public bool stateEntered;


    private bool isMoving;

    private TableBehaviour.Chair chair;

    [SerializeField] Transform itemContainer;
    private GameObject itemInHand;
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    public void Initialize(Zone zone)
    {
        this.zone = zone;
    }

    private void Update()
    {
        animator.SetFloat(MOVE_HASH, agent.velocity.magnitude / agent.speed);

        switch (currentState)
        {
            case StaffStates.Free:
                Free();
                break;
            case StaffStates.Serving:
                Serve();
                break;
            case StaffStates.ReturnToWaiting:
                ReturnToWaiting();
                break;
        }
    }
    bool stateChanged;
    void Free()
    {
        if (OrderSystem.instance.TotalOrdersClaimed== 0)
            return;
        if(stateChanged) return;
        if (zone.ActiveChairs().Count == 0)
            return;
        chair = zone.ActiveChairs().Find(x => x.isSitted && !x.foodServed);
       

        if (chair == null ) return;
        if (chair.gettingServed) return;


        if (stateEntered)
            return;

        stateEntered = true;
        chair.gettingServed = true;

        MoveVisitor(zone.InventoryPosition.transform.position, () =>
        {
            HandState(1);
            
            GameObject plate = zone.InventoryPosition.GetFoodPlate(zone.InventoryPosition.transform.position);
            itemInHand = plate;

            plate.transform.DOJump(itemContainer.position, 3, 1, 0.5f).SetDelay(0.5f).OnComplete(() =>
            {
                plate.transform.SetParent(itemContainer);
                plate.transform.localPosition = Vector3.zero;

                stateChanged = true;
                StartCoroutine(CustomDelayCall(1f, () =>
                {
                    stateEntered = false;
                    currentState = StaffStates.Serving;
                    stateChanged = false;
                }));
            });
        });


    }

    void Serve()
    {
        if (stateEntered)
            return;
        stateEntered = true;

       

        MoveVisitor(chair.sitPos.position, () =>
        {
            if (chair.foodServed)
            {
                zone.InventoryPosition.ReturnFoodPlate(itemInHand);
                HandState(0);
                stateEntered = false;
                currentState = StaffStates.ReturnToWaiting;
                return;
            }
           
            OrderSystem.instance.DecreaseOrdersClaimed();
            chair.foodServed = true;

            HandState(0);

            itemInHand.transform.DOJump(chair.foodPlatePos.position, 3, 1, .5f).OnStart(() => { itemInHand.transform.rotation = Quaternion.identity; }).OnComplete(() =>
            {

            itemInHand.transform.SetParent(chair.foodPlatePos);
                itemInHand.transform.rotation = Quaternion.identity;
                itemInHand = null;
            StartCoroutine(CustomDelayCall(.5f, () =>
            {
               
                stateEntered = false;
                currentState = StaffStates.ReturnToWaiting;

            }));

            });
        });
    }
    IEnumerator CustomDelayCall(float delay, Action onComp)
    {
        yield return new WaitForSeconds(delay);
        onComp?.Invoke();
    }
    void ReturnToWaiting()
    {
        if (stateEntered)
            return;
        stateEntered = true;



        MoveVisitor(zone.InventoryPosition.transform.position, () =>
        {

            DOVirtual.DelayedCall(.5f, () =>
            {
                stateEntered = false;
                currentState = StaffStates.Free;

            });
        });
    }

    private void HandState(int weight)
    {
        animator.SetLayerWeight(HAND_HASH, weight);
    }

    public void MoveVisitor(Vector3 targetPos, System.Action onReached)
    {
        agent.SetDestination(targetPos);
        StartCoroutine(IsMoving(targetPos, onReached));
    }

    IEnumerator IsMoving(Vector3 targetPos, System.Action onReached)
    {
        isMoving = true;
        while (Vector3.Distance(transform.position, targetPos) > agent.stoppingDistance)
        {
            yield return null;
        }
        isMoving = false;
        onReached?.Invoke();
    }

    public void StopMovement(Action onStop)
    {
        isMoving = false;
        agent.SetDestination(transform.position);
        onStop?.Invoke();
    }
    
}
