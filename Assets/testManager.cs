using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class testManager : MonoBehaviour
{
    public float torpedoRunTime;
    public GameObject torpedo;
    public GameObject submarine;
    public Transform torpedoStartLocation;
    public Transform submarineStartLocation;
    public float minutesToTest;
    public float minTurnSpeed;
    public float maxTurnSpeed;
    public float turnSpeedIncrement;
    bool testingnow = false;
    int numberoftests;
    // Start is called before the first frame update
    void Start()
    {
        numberoftests = (int)(minutesToTest*60/torpedoRunTime);
        turnSpeedIncrement = (maxTurnSpeed-minTurnSpeed)/(numberoftests-1);
    }

    // Update is called once per frame
    void Update()
    {
        if (!testingnow && numberoftests > 0)
        {
            numberoftests -= 1;
            SetTesting(true);
            StartCoroutine("TestTorp");
            minTurnSpeed = minTurnSpeed + turnSpeedIncrement;
        }
    }

    void SetTesting(bool b)
    {
        testingnow = b;
    }

    IEnumerator TestTorp()
    {
        submarine.transform.position = submarineStartLocation.position;
        submarine.transform.rotation = submarineStartLocation.rotation;
        GameObject torp = Instantiate(torpedo,torpedoStartLocation.position,torpedoStartLocation.rotation);
        torp.GetComponent<zemtorpedo>().turnSpeed = minTurnSpeed;
        yield return new WaitForSeconds(torpedoRunTime+1f);
        Destroy(torp);
        SetTesting(false);
    }
}
