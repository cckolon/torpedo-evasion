using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class aisub : MonoBehaviour
{
    // for unit conversion, 1 unit is 10 ft
    // 1 unit per second = 6 kts
    // 300 units = 1 kyd
    // 600 units = 1 nm
    // 2klb per 100 foot depth
    public bool invincible;
    float orderedpitch = 0; //positive is up
    float rudder = 0; //positive is right
    public float bell;
    float orderedballast = 0;
    float ballast = 0;
    float buoyancy = 0;
    public float topspeed;
    Rigidbody rb;
    public float pitchSpeed;
    public float rollSpeed;
    public float ballastSpeed;
    public float turnSpeed;
    public Camera maincam;
    public bool cavitation;
    public ParticleSystem cavitationTrail;
    public Transform[] floatpoints = new Transform[3];
    public float depth;
    public float orderedDepth;
    float waterheight;
    bool embtstatus;
    bool embtavailable = true;
    Transform sternplanes;
    Transform rudderobj;
    ParticleSystem[] embteffects = new ParticleSystem[2];
    ParticleSystem[] venteffects = new ParticleSystem[2];
    public GameObject torpedo;
    public float maxI;
    public float coeffP;
    public float coeffI;
    public float coeffD;
    public float maxAngleTorque;
    public float orderedHeading;
    bool slr = false;
    bool[] flooding = {false,false,false};
    float[] floodwater = new float[3];
    public bool jamplane = false;
    public bool jamrudder = false;
    int jamdirection = 1; // positive is dive/right
    Vector3 lastAngleError;
    float lastDepthError;
    Vector3 angleI;
    LayerMask terrain;
    public float depthBelowKeel;
    bool groundingpermitted = true;
    public float health;
    public float length;
    public float sourceLevel;
    bool floodingrepair = false;
    public float torpedoReloadTime;
    public float lastShotTime;
    public List<GameObject> enemies = new List<GameObject>();
    public List<GameObject> torpedoList = new List<GameObject>();
    public GameObject target;
    public Rigidbody targetRb;
    public float le;
    public List<float> snrs = new List<float>();
    RaycastHit hit;
    public float baffledRegion;
    public float preferredDepth;
    public float tooFarRange;
    public float tooCloseRange;
    public float desiredClosingRate;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.inertiaTensorRotation = Quaternion.identity;
        sternplanes = transform.Find("stern planes");
        rudderobj = transform.Find("rudder");
        embteffects = new ParticleSystem[] {transform.Find("EMBTeffects/aft").gameObject.GetComponent<ParticleSystem>(),transform.Find("EMBTeffects/fwd").gameObject.GetComponent<ParticleSystem>()};
        venteffects = new ParticleSystem[] {transform.Find("venteffects/aft").gameObject.GetComponent<ParticleSystem>(),transform.Find("venteffects/fwd").gameObject.GetComponent<ParticleSystem>()};
        terrain = LayerMask.GetMask("Terrain");
        for (int i = 0; i<2; i++)
        {
            embteffects[i].Stop();
            venteffects[i].Stop();
        }
        InvokeRepeating("SecondLoop",0f,1.0f);
        orderedDepth = (waterheight - transform.position.y)*10 + 40;
        foreach (GameObject i in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (i != gameObject)
            {
                enemies.Add(i);
            }
        }
    }

    void FixedUpdate()
    {
        depth = (waterheight - transform.position.y)*10 + 40;
        AnglePID();
        BuoyancyCalc();
        AutoPilot();

    }

    void SecondLoop()
    {
        Sounding();
        AcousticCalc();
        if (target != null)
        {
            HuntSub();
        }
        else
        {
            Search();
        }
    }

    void AutoPilot()
    {
        float depthError = depth - orderedDepth;
        float depthSignal = (depthError/5-rb.velocity.y*10);
        orderedballast = Mathf.Clamp(ballast - buoyancy + depthError/(10-Mathf.Abs(bell)) - rb.velocity.y*20+60*(embtstatus ? 1 : 0),-50,50);
        float maxpitch = Mathf.Clamp((depth-50)/5,0,30);
        if (transform.InverseTransformVector(rb.velocity).z > .5)
        {
            orderedpitch = Mathf.Clamp(depthSignal*5/5+orderedpitch*0/5,-30,maxpitch);
        }
        else if (transform.InverseTransformVector(rb.velocity).z < -.5)
        {
            orderedpitch = Mathf.Clamp(-depthSignal*5/5-orderedpitch*0/5,-30,maxpitch);
        }
        else
        {
            orderedpitch = Mathf.Clamp(depthSignal*5/5+orderedpitch*0/5,-30,maxpitch)*2*transform.InverseTransformVector(rb.velocity).z;
        }
        if (jamrudder)
        {
            rudder = 30*jamdirection;
        }
        else
        {
            rudder = Mathf.Clamp(((orderedHeading - transform.rotation.eulerAngles.y+540)%360-180)*Mathf.Sign(transform.InverseTransformVector(rb.velocity).z),-30,30);
        }
    }

    void AnglePID()
    {
        Vector3 angleError = new Vector3(pitchSpeed*(-orderedpitch-(transform.eulerAngles.x+180)%360+180),0,rollSpeed*(-rudder-(transform.eulerAngles.z+180)%360+180));
        angleI = angleI + angleError*Time.fixedDeltaTime;
        Vector3 angleD = (angleError - lastAngleError)/Time.fixedDeltaTime;
        lastAngleError = angleError;
        Vector3 angleSignal = angleError*coeffP+angleI*coeffI+angleD*coeffD;
        Vector3 torqueSignal = Vector3.Scale(rb.inertiaTensor.normalized, angleSignal) + Vector3.Cross(rb.angularVelocity,Vector3.Scale(rb.inertiaTensor.normalized,rb.angularVelocity));
        if (angleI.magnitude > maxI)
        {
            angleI = angleI.normalized*maxI;
        }
        if (torqueSignal.magnitude > maxAngleTorque)
        {
            torqueSignal = torqueSignal.normalized*maxAngleTorque;
        }
        if (jamplane)
        {
            rb.AddRelativeTorque(jamdirection*maxAngleTorque*transform.InverseTransformVector(rb.velocity).z*12/topspeed*(new Vector3(1,0,0)));
            sternplanes.transform.rotation = Quaternion.RotateTowards(sternplanes.transform.rotation,transform.rotation*Quaternion.Euler(90-Mathf.Clamp(jamdirection*30,-30,30),0,0),30f*Time.fixedDeltaTime);
        }
        else
        {
            rb.AddRelativeTorque((Mathf.Abs(transform.InverseTransformVector(rb.velocity).z)+1)*12/topspeed*torqueSignal);
            sternplanes.transform.rotation = Quaternion.RotateTowards(sternplanes.transform.rotation,transform.rotation*Quaternion.Euler(90-Mathf.Clamp(torqueSignal.x*5,-30,30),0,0),30f*Time.fixedDeltaTime);
        }
        if (!slr)
        {
            rb.AddForce(transform.forward*(float)bell*.01f*topspeed);
        }
        rb.AddTorque(transform.up *rudder*turnSpeed* transform.InverseTransformVector(rb.velocity).z);
        rudderobj.transform.rotation = Quaternion.RotateTowards(rudderobj.transform.rotation,transform.rotation*Quaternion.Euler(0,90-Mathf.Clamp(rudder,-30,30),90),30f*Time.fixedDeltaTime);
    }

    
    public void Search()
    {
        enemies.Clear();
        foreach (GameObject i in GameObject.FindGameObjectsWithTag("Player"))
        {
            if (i != gameObject)
            {
                enemies.Add(i);
            }
        }
        if (Physics.Raycast(transform.position, new Vector3(Mathf.Sin(orderedHeading*Mathf.PI/180),0,Mathf.Cos(orderedHeading*Mathf.PI/180)), 100, terrain))
        {
            bell = 1;
            int newhead = Random.Range(0,359);
            if (!Physics.Raycast(transform.position,new Vector3(Mathf.Sin(newhead*Mathf.PI/180),0,Mathf.Cos(newhead*Mathf.PI/180)),out hit, 100, terrain))
            {
                orderedHeading = newhead;
            }
        }
        snrs.Clear();
        for(int i=0; i < enemies.Count; i++)
        {
            float enemySourceLevel;
            if (enemies[i] != null)
            {
                if (enemies[i].TryGetComponent(out playersub j))
                {
                    enemySourceLevel = j.sourceLevel;
                }
                else if (enemies[i].TryGetComponent(out aisub k))
                {
                    enemySourceLevel = k.sourceLevel;
                }
                else
                {
                    enemySourceLevel = 0f;
                }
                if (Mathf.Abs(Quaternion.FromToRotation(-transform.forward,enemies[i].transform.position-transform.position).eulerAngles.y)<baffledRegion)
                {
                    snrs.Add(0);
                }
                else
                {
                    snrs.Add(enemySourceLevel - 10*Mathf.Log(9*(transform.position-enemies[i].transform.position).magnitude,10)-(le + sourceLevel)/3);
                }
            }
            else
            {
                enemies.RemoveAt(i);
            }
        }
        if (Time.time % 30 < 5)
        {
            bell = 1;
        }
        else
        {
            bell = (int)Mathf.Clamp(depth / 125,1,5);//prevents cavitation
        }
        for(int i=0; i< enemies.Count;i++)
        {
            if (snrs[i] > 5)
            {
                if (target == null && snrs[i] == Mathf.Max(snrs.ToArray()))
                {
                    target = enemies[i];
                    targetRb = target.GetComponent<Rigidbody>();
                }
            }
        }
    }

    public void HuntSub()
    {
        if (target != null)
        {
            Vector3 targetheadingvector = Vector3.ProjectOnPlane(target.transform.forward,Vector3.up);
            Vector3 targetpositionvector = Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
            float targetrange = targetpositionvector.magnitude;
            float targetaob = Vector3.SignedAngle(targetheadingvector,-targetpositionvector,Vector3.up);//positive stbd, negative port
            float closingrate = Vector3.Dot(rb.velocity - targetRb.velocity,targetpositionvector)/(targetpositionvector.magnitude+.01f);
            float targetbell = Mathf.Clamp(5*targetRb.velocity.magnitude/(topspeed/6),0,5);
            if (Physics.Linecast(transform.position,target.transform.position,terrain))
            {
                UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                UnityEngine.AI.NavMesh.CalculatePath(new Vector3(transform.position.x, waterheight, transform.position.z), new Vector3(target.transform.position.x, waterheight, target.transform.position.z), UnityEngine.AI.NavMesh.AllAreas, path);
                if (path.corners.Length > 1)
                {
                    for (int i=0; i<path.corners.Length -1; i++)
                    {
                        Debug.DrawLine(path.corners[i], path.corners[i+1], Color.red,1);
                    }
                    orderedHeading = Quaternion.FromToRotation(Vector3.forward,path.corners[1]-transform.position).eulerAngles.y;
                    print("Navigate");
                }
                bell = Mathf.Clamp(targetbell,0,5);
            }
            else if (targetrange > tooFarRange)
            {
                print("Point/Close");
                orderedHeading = Quaternion.FromToRotation(Vector3.forward,targetpositionvector).eulerAngles.y;
                if (closingrate < desiredClosingRate*.9)
                {
                    bell = Mathf.Clamp(bell + .1f,0,5);
                }
                else if (closingrate > desiredClosingRate*1.1)
                {
                    bell = Mathf.Clamp(bell - .1f,0,5);
                }
            }
            else if (Mathf.Abs(targetaob)<90)
            {
                print("Spiral Out");
                Vector3 spiraloutvec = Quaternion.Euler(0,-110*Mathf.Sign(targetaob),0)*targetpositionvector.normalized;
                print(Mathf.Clamp((targetrange - tooCloseRange)/(tooFarRange-tooCloseRange),0,1));
                print(Mathf.Clamp((tooFarRange-targetrange)/(tooFarRange-tooCloseRange),0,1));
                Vector3 travelvec = Mathf.Clamp((targetrange - tooCloseRange)/(tooFarRange-tooCloseRange),0,1)*(-targetheadingvector.normalized)+Mathf.Clamp((tooFarRange-targetrange)/(tooFarRange-tooCloseRange),0,1)*spiraloutvec;
                orderedHeading = Quaternion.FromToRotation(Vector3.forward,travelvec).eulerAngles.y;
                bell = Mathf.Clamp(targetbell,0,5);
            }
            else if (targetrange < tooCloseRange)
            {
                print("Stop and Point");
                orderedHeading = Quaternion.FromToRotation(Vector3.forward,targetpositionvector).eulerAngles.y;
                bell = Mathf.Min(targetbell,1);
            }
            else if (Mathf.Abs(targetaob)<120)
            {
                print("Spiral In");
                bell = Mathf.Clamp(targetbell,0,5);
                Vector3 spiralinvec = Quaternion.Euler(0,-60*Mathf.Sign(targetaob),0)*targetpositionvector.normalized;
                orderedHeading = Quaternion.FromToRotation(Vector3.forward,spiralinvec).eulerAngles.y;
            }
            else
            {
                print("Mirror");
                orderedHeading = target.transform.rotation.eulerAngles.y;
                bell = Mathf.Clamp(targetbell,0,5);
            }
            float enemySourceLevel;
            if (target.TryGetComponent(out playersub j))
            {
                enemySourceLevel = j.sourceLevel;
            }
            else if (target.TryGetComponent(out aisub k))
            {
                enemySourceLevel = k.sourceLevel;
            }
            else
            {
                enemySourceLevel = 0f;
            }
            if (Mathf.Abs(Quaternion.FromToRotation(-transform.forward,target.transform.position-transform.position).eulerAngles.y) < baffledRegion)
            {
                target = null;
                targetRb = null;
            }
            else if (enemySourceLevel - 10*Mathf.Log(9*(transform.position-target.transform.position).magnitude,10)-(le + sourceLevel)/3<3)
            {
                orderedHeading = Quaternion.FromToRotation(Vector3.forward,targetpositionvector).eulerAngles.y;
                target = null;
                targetRb = null;
            }
        }
    }


    void BuoyancyCalc()
    {
        if (Mathf.Abs(orderedballast - ballast)<.01)
        {
            ballast = orderedballast;
        }
        else
        {
            ballast += Mathf.Sign(orderedballast - ballast)*ballastSpeed*Time.fixedDeltaTime;
        }
        buoyancy = ballast - 2*depth/100 + 60*(embtstatus ? 1 : 0);
        rb.AddForce(Vector3.up * buoyancy * .02f);
        foreach (Transform i in floatpoints)
        {
            if (i.position.y > waterheight)
            {
                rb.AddForceAtPosition(Vector3.down*0.5f*(i.position.y-waterheight),i.position);
            }
        }
        if (flooding[0] | flooding[1] | flooding[2])
        {
            FloodLoop();
        }
    }

    void AcousticCalc()
    {
        if (Mathf.Abs(bell)> depth/125+1 && !cavitation)
        {
            cavitation = true;
            cavitationTrail.Play();
        }
        else if (Mathf.Abs(bell) < depth/125+1 && cavitation)
        {
            cavitation = false;
            cavitationTrail.Stop();
        }
        sourceLevel = rb.velocity.magnitude*6 + 40 + (cavitation ? 50 : 0);
    }

    void OnBlow()
    {
        if (embtavailable)
        {
            embtstatus = true;
            embtavailable = false;
            orderedDepth = 50;
            StartCoroutine("EMBTblow");
        }
    }

    void OnVent()
    {
        if (embtstatus)
        {
            embtstatus = false;
            StartCoroutine("EMBTvent");
        }
    }

    void Shoot()
    {
        if (Time.time - lastShotTime > torpedoReloadTime)
        {
            GameObject newtorp = Instantiate(torpedo,transform.TransformPoint(new Vector3(0,0,0)),transform.rotation);
            newtorp.GetComponent<zemtorpedo>().shooter = gameObject;
            newtorp.GetComponent<zemtorpedo>().transitVector = maincam.transform.forward;
            newtorp.GetComponent<Rigidbody>().velocity = rb.velocity + transform.forward*30.0f;
        }
    }

    public void OnCollisionEnter(Collision col)
    {
        Vector3 collisionpoint = transform.InverseTransformPoint(col.contacts[0].point);
        int floodlocation = (int)(collisionpoint.z*3/length+1.5);
        if (floodlocation > 2){floodlocation = 2;}
        if (floodlocation <0 ){floodlocation =0;}
        if (col.gameObject.tag == "Torpedo")
        {
            health = health - 1f;
            CauseCasualty(floodlocation);
        }
        else
        {
            if (groundingpermitted)
            {
                if (rb.velocity.sqrMagnitude>1)
                {
                    CauseCasualty(floodlocation);
                    health = health - .5f;
                    StartCoroutine("GroundingDelay");
                }
            }
        }
        if (health <= 0)
        {
            Die();
        }
    }

    void Sounding()
    {
        Collider[] results = new Collider[1];
        if (Physics.SphereCast(transform.position,15,Vector3.down,out hit,50,terrain))
        {
            orderedDepth = Mathf.Min(depth - 200 + hit.distance*10,preferredDepth);
        }
        else if (Physics.OverlapSphereNonAlloc(transform.position,30,results,terrain)>0)
        {
            orderedDepth=depth-200;
            print("too close!");
        }
        else
        {
            orderedDepth = orderedDepth*.8f + .2f*(depth + Mathf.Clamp(preferredDepth-depth,-50,50));
        }
    }

    void CauseCasualty(int location)
    {
        if (!invincible)
        {
            if (location == 0)
            {
                float choice = Random.Range(0,3);
                if (choice < 1)
                {
                    flooding[0] = true;
                }
                else if (choice < 2)
                {
                    StartCoroutine("Jam");
                }
                else
                {
                    StartCoroutine("SLR");
                }
            }
            else if (location == 1)
            {
                float choice = Random.Range(0,2);
                if (choice < 1)
                {
                    flooding[1] = true;
                }
                else
                {
                    StartCoroutine("SLR");
                }
            }
            else
            {
                float choice = Random.Range(0,2);
                if (choice < 1)
                {
                    flooding[2] = true;
                }
                else
                {
                    StartCoroutine("Jam");
                }
            }
        }
    }

    void FloodLoop()
    {
        for (int i = 0; i < 3; i++)
        {
            if (flooding[i])
            {
                floodwater[i] = Mathf.Clamp(floodwater[i] + (Mathf.Sqrt(Mathf.Max(depth,0))-13)*Time.fixedDeltaTime/1000,0,1); // Floodwater is between 0 and 1.
                if (floodwater[i] == 0)
                {
                    if (floodingrepair == false)
                    {
                        floodingrepair = true;
                        StartCoroutine("FloodRepair");
                    }
                }
                else
                {
                    floodingrepair = false;
                    StopCoroutine("FloodRepair");
                }
                rb.AddForceAtPosition(Vector3.down*floodwater[i]*3f,floatpoints[i].position); // A floodwater value of 1 produces a downward force of 3
            }
        }
    }

    void Die()
    {
        Destroy(gameObject);
    }

    IEnumerator Jam()
    {
        yield return new WaitForSeconds(.5f);
        int type = Random.Range(0,2);
        jamdirection = (int)Mathf.Clamp(Random.Range(0,2),0,1)*2-1;
        if (type == 0)
        {
            jamplane = true;
        }
        else
        {
            jamrudder = true;
            rudder = 30*jamdirection;
        }
        yield return new WaitForSeconds(Random.Range(15,45));
        jamrudder = false;
        jamplane = false;
    }

    IEnumerator SLR()
    {
        slr = true;
        bell = 0;
        yield return new WaitForSeconds(Random.Range(10,20));
        yield return new WaitForSeconds(Random.Range(10,20));
        yield return new WaitForSeconds(Random.Range(10,20));
        slr = false;
    }

    IEnumerator GroundingDelay()
    {
        groundingpermitted = false;
        yield return new WaitForSeconds(5f);
        groundingpermitted = true;
    }

    IEnumerator FloodRepair()
    {
        yield return new WaitForSeconds(Random.Range(10,20));
        for (int i = 0; i<3; i++)
        {
            if (floodwater[i] == 0)
            {
                flooding[i] = false;
            }
        }
    }

}
