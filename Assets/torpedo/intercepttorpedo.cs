using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class intercepttorpedo : MonoBehaviour
{
    public float speed;// in knots
    public float detectionRange; //distance, in yards, at which a target can be detected
    public float detectionAngle;
    public float terminalDistance; //distance, in yards, where terminal homing begins
    public float runTime; //in seconds
    public float turnSpeed;
    public float sourceLevel;
    public GameObject shooter;
    public Transform interceptMarker;
    public Vector3 transitVector;
    public GameObject target;
    public GameObject explosion;
    public List<GameObject> targetList = new List<GameObject>();
    public bool enable = false;
    public float accelerationInertia;
    int collisions = 0;
    bool permitcollision = true;
    bool reported = false;
    float oldspeed;
    float cosdetectionangle;
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
        interceptMarker = transform.Find("InterceptMarker");
    }
    
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
                interceptMarker.position = interceptPoint;
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
            targetAcceleration = (1-accelerationInertia)*(targetRb.velocity - lastVelocity)/Time.fixedDeltaTime+accelerationInertia*targetAcceleration; //calculate target acceleration, including low-pass filter
            lastVelocity = targetRb.velocity; //reset target's last velocity
            float tgo = targetdirection.sqrMagnitude/Mathf.Max(Mathf.Abs(Vector3.Dot(relativemotion,targetdirection)),.1f); //calculate time to go
            if (targetDist < terminalDistance/3.3) //target is close enough to account for acceleration
            {
                interceptPoint = target.transform.position + targetRb.velocity*tgo + targetAcceleration*tgo*tgo/2; //calculate intercept point based on acceleration and velocity
            }
            else
            {
                interceptPoint = target.transform.position + targetRb.velocity*tgo; //calculate intercept point based on velocity (linear)
            }
            interceptMarker.position = interceptPoint; //move red intercept marker to display intercept point
            Vector3 interceptdirection = interceptPoint - transform.position; //intercept direction is the vector from torpedo to intercept point
            Vector3 targetCross = Vector3.Cross(transform.forward.normalized,interceptdirection.normalized); //use the cross product to find angle between where the torp is pointing and where it needs to point
            desiredRotation = targetCross.normalized*Mathf.Clamp(10*Mathf.Asin(targetCross.magnitude),0,1); //use arcsin of the magnitude of targetcross to find the angle, in radians. Torque is proportional to that. Multiply by the normalized axis.
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
        yield return new WaitForSeconds(.1f);
    }

    IEnumerator Report()
    {
        print("In " + runTime + " seconds, intercept algorithm achieved " + collisions + " collisions.");
        print("Collision rate: "+ collisions/runTime);
        print("Speed: "+ oldspeed);
        print("Turn rate: " + turnSpeed);
        yield return new WaitForSeconds(.1f);
    }

}
