using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pntorpedo : MonoBehaviour
{
    public float speed;// in knots
    public float detectionRange; //distance, in yards, at which a target can be detected
    public float detectionAngle;
    public float runTime; //in seconds
    public float turnSpeed;
    public float sourceLevel;
    public float pnGain;
    public GameObject shooter;
    public Vector3 transitVector;
    public GameObject target;
    public GameObject explosion;
    public List<GameObject> targetList = new List<GameObject>();
    public bool enable = false;
    int collisions = 0;
    bool permitcollision = true;
    bool reported = false;
    float oldspeed;
    float cosdetectionangle;
    float lastTime;
    float waterheight;
    GameObject waterline;
    Vector3 desiredRotation;
    Vector3 interceptPoint;
    Vector3 lastVelocity;
    Vector3 targetAcceleration;
    Rigidbody rb;
    Rigidbody targetRb;
    float startTime;
    // Start is called before the first frame update
    void Start()
    {
        oldspeed = speed;
        rb = GetComponent<Rigidbody>();
        waterline = GameObject.FindWithTag("Waterline");
        target = null;
        cosdetectionangle = Mathf.Cos(detectionAngle*Mathf.PI/180);
        waterheight = waterline.transform.position.y;
        foreach(GameObject i in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (i != shooter)
            {
                targetList.Add(i);
            }
        }
        foreach(GameObject i in GameObject.FindGameObjectsWithTag("AIPlayer"))
        {
            if (i != shooter)
            {
                targetList.Add(i);
            }
        }
        StartCoroutine(WaitAndEnable());
        SetCollider(false);
        rb.AddForce(transform.forward*100.0f);
        startTime = Time.time;
        InvokeRepeating("DetectEnemy",0f,1.0f);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!enable)
        {
            Transit();
        }
        else
        {
            if (target != null)
            {
                HuntTarget();
            }
            else
            {
                Vector3 targetdirection = interceptPoint - transform.position; //target direction is the vector from torpedo to intercept point
                Vector3 targetCross = Vector3.Cross(transform.forward.normalized,targetdirection.normalized); //use the cross product to find angle between where the torp is pointing and where it needs to point
                desiredRotation = targetCross.normalized*Mathf.Clamp(10*Mathf.Asin(targetCross.magnitude),0,1)+Vector3.Cross(transform.up,Vector3.up); //use arcsin of the magnitude of targetcross to find the angle, in radians. Torque is proportional to that. Multiply by the normalized axis.
            }
        }
        if (desiredRotation.magnitude > turnSpeed)
        {
            desiredRotation = desiredRotation.normalized*turnSpeed;
        }
        rb.AddTorque(desiredRotation);
        rb.AddForce(transform.forward*speed*.05f);
        if (transform.position.y > waterheight-.5)
        {
            rb.AddForce(Vector3.down*1.0f*(transform.position.y-waterheight+.5f));
        }
        if (Time.time > startTime + runTime & !reported)
        {
            reported = true;
            StartCoroutine("Report");
            Die();
        }
    }
    void OnCollisionEnter(Collision col)
    {
        if (col.gameObject.tag == "Player" & permitcollision)
        {
            collisions += 1;
        }
        Die();
    }

    void Die(){
        //var exp = Instantiate(explosion,transform.position,transform.rotation);
        //Destroy(gameObject);
        StartCoroutine("DisableForTest");
    }

    void DetectEnemy()
    {
        targetList.Clear();
        foreach(GameObject i in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (i != shooter)
            {
                targetList.Add(i);
            }
        }
        foreach(GameObject i in GameObject.FindGameObjectsWithTag("AIPlayer"))
        {
            if (i != shooter)
            {
                targetList.Add(i);
            }
        }
        foreach(GameObject t in targetList)
        {
            if (target == null)
            {
                if ((t.transform.position - transform.position).magnitude<detectionRange/3.3 & Vector3.Dot((t.transform.position-transform.position).normalized,transform.forward.normalized)>cosdetectionangle)
                {
                    target = t;
                    targetRb = target.GetComponent<Rigidbody>();
                    interceptPoint = target.transform.position;
                    enable = true;
                }
            }
            else if ((t.transform.position - transform.position).magnitude<(target.transform.position).magnitude & Vector3.Dot((t.transform.position-transform.position).normalized,transform.forward.normalized)>cosdetectionangle)
            {
                target = t;
                targetRb = target.GetComponent<Rigidbody>();
                enable = true;
            }
        }
    }

    void HuntTarget()
    {
        Vector3 relativemotion = targetRb.velocity - rb.velocity.magnitude*transform.forward; //relative velocity between torpedo and target
        Vector3 targetdirection = target.transform.position - transform.position; //vector from torpedo to target
        float targetDist = targetdirection.magnitude; //distance to target
        if (targetDist<detectionRange/3.3 & Vector3.Dot((target.transform.position-transform.position).normalized,transform.forward.normalized)>cosdetectionangle) //target is detected
        {
            float relativebearing = Vector3.SignedAngle(transform.forward,targetdirection,Vector3.up); //relative bearing to target
            float nextrelativebearing = Vector3.SignedAngle(transform.forward,targetdirection+relativemotion*Time.fixedDeltaTime,Vector3.up); //calculate next relative bearing using relative motion and system time step
            desiredRotation = Vector3.Cross(transform.up, Vector3.up-transform.forward*Mathf.Clamp(target.transform.position.y-transform.position.y+((transform.eulerAngles.x+180)%360-180)/2,-20,20)/10); //set the desired rotation to maintain torpedo upright and seek appropriate depth
            desiredRotation += transform.up*pnGain*(nextrelativebearing - relativebearing)/Time.fixedDeltaTime; //add a torque proportional to the rate of change of bearing between torpedo and target
        }
        else
        {
            interceptPoint = target.transform.position; //drive towards the target's last known location
            target = null;
            targetRb = null;
        }
    }
    void Transit()
    {
        Vector3 targetCross = Vector3.Cross(transform.forward.normalized,transitVector.normalized);
        desiredRotation = targetCross.normalized*Mathf.Clamp(10*Mathf.Asin(targetCross.magnitude),0,1); //use arcsin of the magnitude of targetcross to find the angle, in radians. Torque is proportional to that. Multiply by the normalized axis.
    }

    void SetCollider(bool isenabled)
    {
        GetComponent<Collider>().enabled = isenabled;
    }

    void SetSpeed(float newspeed)
    {
        speed = newspeed;
    }

    void AllowCollision(bool b)
    {
        permitcollision = b;
    }

    private IEnumerator WaitAndEnable()
    {
        yield return new WaitForSeconds(1.0f);
        SetCollider(true);
    }

    IEnumerator DisableForTest()
    {
        AllowCollision(false);
        SetSpeed(0);
        yield return new WaitForSeconds(10.0f);
        SetSpeed(oldspeed);
        AllowCollision(true);
    }

    IEnumerator Report()
    {
        print("In " + runTime + " seconds, 2D PN algorithm achieved " + collisions + " collisions.");
        print("Collision rate: "+ collisions/runTime);
        print("Speed: "+ oldspeed);
        print("Turn rate: " + turnSpeed);
        yield return new WaitForSeconds(.1f);
    }
}
