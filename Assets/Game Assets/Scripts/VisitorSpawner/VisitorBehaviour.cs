using DG.Tweening;
using FoodStreet.Navigation;
using System;
using System.Collections;
using UnityEngine;

public class VisitorBehaviour : MonoBehaviour
{

    public static int MOVE_HASH = Animator.StringToHash("Move"); 
    public static int SIT_HASH = Animator.StringToHash("Sit"); 
    public static int Eat_HASH = Animator.StringToHash("Eat"); 

    public enum States
    {
        Arriving,
        GoToTable,
        Waiting,
        Eating,
        Leaving,
    }

    public States currentState = States.Arriving;
    public bool stateEntered;

    private Vector3 startPosition;

    [HideInInspector] Animator animator;
    [HideInInspector]public PathFollower agent;

    private Zone zone;

    public bool isMoving;

    TableBehaviour.Chair activeChair;


    public Action<VisitorBehaviour> OnDisableEvent;
    
    bool isInitialized;


    public void Initialize(Zone zone, Vector3 startPos)
    {
        isInitialized = true;
        this.zone = zone;
        startPosition = startPos;

        stateEntered = false;
        isMoving = false;

        activeChair = null;

        currentState= States.Arriving;
        if (agent == null)
        {
            agent = NavAgentSetup.Ensure(gameObject);
        }
        agent.enabled = true;
        if(animator == null)
            animator= GetComponentInChildren<Animator>();

    }

    private void OnDisable()
    {
        isInitialized= false;
        OnDisableEvent?.Invoke(this);
    }

    private void Update()
    {
        if (!isInitialized)
            return;
        animator.SetFloat(MOVE_HASH, agent.Velocity.magnitude / agent.Speed);
        switch (currentState)
        {
            case States.Arriving:
                Arrive();
                break;
            case States.GoToTable:
                GoToTable();
                break;
            case States.Waiting:
                Waiting();
                break;
            case States.Eating:
                Eating();
                break;
            case States.Leaving:
                Leaving();
                break;
        }
    }

    private void Arrive()
    {

        if (stateEntered)
            return;
        stateEntered=true;

        activeChair= zone.GetTable().GetChair();
        activeChair.visitor= this;
        activeChair.isBusy = true;

        zone.AddActiveChairs(activeChair);
        MoveVisitor(zone.EntryPosition.position, () =>
        {
            ChangeState(States.GoToTable);

        });
    }
    
    private void GoToTable()
    {

        if (stateEntered)
            return;
        stateEntered=true;

        if(activeChair == null)
        {
            if(zone.GetTable() != null)
            {
                activeChair = zone.GetTable().GetChair();
            }

            if (activeChair != null)
            {
                activeChair.visitor = this;
                activeChair.isBusy = true;
                zone.AddActiveChairs(activeChair);

            }
            else
            {
                Debug.Log("Leaving From Go To Table State!");

                ChangeState(States.Leaving);
            }
        }

            MoveVisitor(activeChair.sitPos.position, () =>
            {
                ChangeState(States.Waiting);
            });
       
    } 
    private void Waiting()
    {


        if (stateEntered)
            return;
        stateEntered = true;
        
        animator.SetBool(SIT_HASH, true);
            activeChair.isSitted = true;
        agent.enabled = false;
        transform.DORotateQuaternion(activeChair.sitPos.rotation, .5f);
        transform.DOMove(activeChair.sitPos.position, 0.4f).OnComplete(() =>
        {
            transform.SetParent(activeChair.sitPos);
            StartCoroutine(CustomDelayCall(2, () =>
            {
                ChangeState(States.Eating);

            }));

        });


    } 
    private void Eating()
    {
        if (!activeChair.foodServed)
            return;
        if (stateEntered)
            return;
        stateEntered = true;

        

        animator.SetBool(Eat_HASH, true);

        int randTime = UnityEngine.Random.Range(4,7);
        /*DOVirtual.DelayedCall*/
        StartCoroutine(CustomDelayCall(randTime, () =>
        {
            if(activeChair.foodPlatePos.childCount > 0)
            {
                activeChair.foodPlatePos.GetChild(0).DOScale(0,.5f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    zone.InventoryPosition.ReturnFoodPlate(activeChair.foodPlatePos.GetChild(0).gameObject);
                    Debug.Log("Leaving After Eating!");
                    ChangeState(States.Leaving);
                });
            }

        }));

    }

    IEnumerator CustomDelayCall(float delay, Action onComp)
    {
        yield return new WaitForSeconds(delay);
        onComp?.Invoke();
    }

    private void Leaving()
    {
        if (stateEntered)
            return;
        stateEntered = true;

        animator.SetBool(SIT_HASH, false);
        animator.SetBool(Eat_HASH, false);

        zone.Remove(activeChair);
        activeChair.ResetStates();

        transform.SetParent(null);

        agent.enabled = true;
        MoveVisitor(zone.EntryPosition.position, () =>
        {
            MoveVisitor(startPosition, () =>
                    {
                        //transform.DOPunchScale(new Vector3(1.2f,1.2f,1.2f), 1);
                        transform.DOScale(0, .5f).SetEase(Ease.InBack).OnComplete(() =>
                        {
                        gameObject.SetActive(false);

                        });
                    });
        });
    }

    public void ResetVisitor(bool disable = false)
    {
        StopMovement();
        zone.Remove(activeChair);

        transform.SetParent(null);

        if (agent != null)
        {
            agent.enabled = true;
            agent.Stop();
        }

        isMoving = false;
        stateEntered = false;

        currentState = States.Arriving;
        activeChair = null;

        if (disable)
            gameObject.SetActive(false);

    }

    
    Coroutine movingCoroutine;
    public void MoveVisitor(Vector3 targetPos, System.Action onReached)
    {
        agent.SetDestination(targetPos);
        movingCoroutine = StartCoroutine(IsMoving(onReached));
    }

    public void ChangeState(States state)
    {
        stateEntered = false;
        currentState = state;
    }

    public void StopMovement()
    {
        

        if (movingCoroutine != null)
        {
            StopCoroutine(movingCoroutine);
            movingCoroutine= null;
        }
        isMoving = false;
    }

    // Arrival comes from the follower rather than from a distance test against the target.
    // A chair or a counter sits on a solid tile, so the visitor is routed to the nearest
    // spot it actually fits — a distance test against the target itself would never be
    // satisfied and the visitor would wait there forever.
    IEnumerator IsMoving(Action onReached)
    {
        isMoving= true;
        yield return null;
        while(!agent.HasArrived)
        {
            yield return null;
        }
        isMoving= false;
        onReached?.Invoke();
    }
}

