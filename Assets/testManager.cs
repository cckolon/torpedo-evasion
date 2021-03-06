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
    public int numberOfTests;
    public float minTurnSpeed;
    public float maxTurnSpeed;
    public float turnSpeedIncrement;
    bool testingnow = false;
    // Start is called before the first frame update
    void Start()
    {
        turnSpeedIncrement = (maxTurnSpeed-minTurnSpeed)/(numberOfTests-1);
    }

    // Update is called once per frame
    void Update()
    {
        if (!testingnow && numberOfTests > 0)
        {
            numberOfTests -= 1;
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
        submarine.transform.rotation = submarineStartLocation.rotation * Quaternion.Euler(0,Random.Range(0,360),0);
        GameObject torp = Instantiate(torpedo,torpedoStartLocation.position,torpedoStartLocation.rotation);
        torp.GetComponent<zemtorpedo>().turnSpeed = minTurnSpeed;
        yield return new WaitForSeconds(torpedoRunTime+1f);
        Destroy(torp);
        SetTesting(false);
    }
}
