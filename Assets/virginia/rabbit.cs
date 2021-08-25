using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class rabbit : MonoBehaviour
{
    // for unit conversion, 1 unit is 10 ft
    // 1 unit per second = 6 kts
    // 300 units = 1 kyd
    // 600 units = 1 nm
    // 2klb per 100 foot depth
    public bool invincible;
    float orderedpitch = 0; //positive is up
    float rudder = 0; //positive is right
    int bell = 5;
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
    bool lookactive = false;
    float zoomspeed = .1f;
    public Transform[] floatpoints = new Transform[3];
    float depth;
    float orderedDepth;
    float waterheight;
    bool embtstatus;
    bool embtavailable = true;
    Transform sternplanes;
    Transform rudderobj;
    ParticleSystem[] embteffects = new ParticleSystem[2];
    ParticleSystem[] venteffects = new ParticleSystem[2];
    public Color oceanFog;
    public Color airFog;
    public float waveHeight;
    public float maxI;
    public float coeffP;
    public float coeffI;
    public float coeffD;
    public float coeffDP;
    public float coeffDI;
    public float coeffDD;
    public float maxDI;
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
    float depthI;
    Vector3 angleI;
    float cameraHeight;
    LayerMask terrain;
    float depthBelowKeel;
    bool autocontrol = true;
    bool groundingpermitted = true;
    public float health;
    public float length;
    // Start is called before the first frame update
    public bool corkscrew;
    public bool depthexcursion;
    public bool zigzag;
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.inertiaTensorRotation = Quaternion.identity;
        maincam = Camera.main;
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
        orderedDepth = 200;
    }

    // Update is called once per frame
    void Update()
    {
        maincam.transform.LookAt(transform.position);
        FogCheck();
    }

    void FixedUpdate()
    {
        depth = (waterheight - transform.position.y)*10 + 40;
        AnglePID();
        BuoyancyCalc();
        if (autocontrol)
        {
            AutoPilot();
        }
        if (corkscrew){CorkscrewLoop();}
        if (depthexcursion){DepthExcursionLoop();}
        if (zigzag){ZigzagLoop();}
    }

    void SecondLoop()
    {
        Sounding();
    }

    void AutoPilot()
    {
        float depthError = depth - orderedDepth;
        depthI = depthI + depthError*Time.fixedDeltaTime;
        float depthD = (depthError-lastDepthError)/Time.fixedDeltaTime;
        lastDepthError = depthError;
        float depthSignal = (depthError * coeffDP + depthI*coeffDI + depthD*coeffDD)/Mathf.Min(1,Mathf.Abs(bell));
        if (Mathf.Abs(depthI) > maxDI)
        {
            depthI = maxDI*Mathf.Sign(depthI);
        }
        if (transform.InverseTransformVector(rb.velocity).z > .5)
        {
            orderedpitch = Mathf.Clamp(depthSignal*5/5+orderedpitch*0/5,-30,30);
        }
        else if (transform.InverseTransformVector(rb.velocity).z < .5)
        {
            orderedpitch = Mathf.Clamp(-depthSignal*5/5-orderedpitch*0/5,-30,30);
        }
        else
        {
            orderedpitch = 0;
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
           // rb.AddRelativeTorque(12/topspeed*torqueSignal);
            sternplanes.transform.rotation = Quaternion.RotateTowards(sternplanes.transform.rotation,transform.rotation*Quaternion.Euler(90-Mathf.Clamp(jamdirection*30,-30,30),0,0),30f*Time.fixedDeltaTime);
        }
        else
        {
            rb.AddRelativeTorque((Mathf.Abs(transform.InverseTransformVector(rb.velocity).z)+1)*12/topspeed*torqueSignal);
            sternplanes.transform.rotation = Quaternion.RotateTowards(sternplanes.transform.rotation,transform.rotation*Quaternion.Euler(90-Mathf.Clamp(torqueSignal.x*5,-30,30),0,0),30f*Time.fixedDeltaTime);
        }
        rb.AddForce(transform.forward*(float)bell*.01f*topspeed);
        rb.AddTorque(transform.up *rudder*turnSpeed* transform.InverseTransformVector(rb.velocity).z);
        rudderobj.transform.rotation = Quaternion.RotateTowards(rudderobj.transform.rotation,transform.rotation*Quaternion.Euler(0,90-Mathf.Clamp(rudder,-30,30),90),30f*Time.fixedDeltaTime);
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

    void OnBlow()
    {
        if (embtavailable)
        {
            embtstatus = true;
            embtavailable = false;
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

    void OnLook(InputValue input)
    {
        if (lookactive)
        {
            Vector2 lookVec = input.Get<Vector2>();
            maincam.transform.RotateAround(transform.position, Vector3.up, lookVec.x/10);
            maincam.transform.RotateAround(transform.position, maincam.transform.right, -lookVec.y/10);
        }
    }

    void OnActivateLook()
    {
        lookactive = !lookactive;
    }

    void OnZoom(InputValue input)
    {
        maincam.transform.Translate((Vector3.forward)*input.Get<float>()*zoomspeed);
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
    }

    void Sounding()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity, terrain))
        {
            depthBelowKeel = hit.distance*10f;
        }
    }

    void FogCheck()
    {
        if(cameraHeight > waterheight +waveHeight/2 && maincam.transform.position.y < waterheight + waveHeight)
        {
            maincam.transform.Translate(-(waveHeight+1)*Vector3.up);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = oceanFog;
            RenderSettings.fogDensity = 0.01f;
        }
        else if (cameraHeight < waterheight+waveHeight/2 && maincam.transform.position.y > waterheight)
        {
            maincam.transform.Translate((waveHeight+1)*Vector3.up);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = airFog;
            RenderSettings.fogStartDistance = 1000;
            RenderSettings.fogEndDistance = 4000;
        }
        cameraHeight = maincam.transform.position.y;
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
                floodwater[i] = Mathf.Clamp(floodwater[i] + (Mathf.Sqrt(depth)-13)*Time.fixedDeltaTime/1000,0,1);
                if (floodwater[i] == 0)
                {
                    StartCoroutine("FloodRepair");
                }
                else
                {
                    StopCoroutine("FloodRepair");
                }
                rb.AddForceAtPosition(Vector3.down*floodwater[i]*3f,floatpoints[i].position);
            }
        }
    }

    void Die()
    {
        ;
    }

    IEnumerator EMBTblow()
    {
        for (int i = 0; i<2; i++)
        {
            embteffects[i].Play();
        }
        yield return new WaitForSeconds(5f);
        for (int i = 0; i<2; i++)
        {
            embteffects[i].Stop();
        }
    }

    IEnumerator EMBTvent()
    {
        for (int i = 0; i<2; i++)
        {
            venteffects[i].Play();
        }
        yield return new WaitForSeconds(5f);
        for (int i = 0; i<2; i++)
        {
            venteffects[i].Stop();
        }
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

    void CorkscrewLoop()
    {
        orderedHeading = (transform.rotation.eulerAngles.y + 90)%360;
    }

    void DepthExcursionLoop()
    {
        if (depth < 200)
        {
            orderedDepth = 800;
        }
        else if (depth > 700)
        {
            orderedDepth = 100;
        }
    }

    void ZigzagLoop()
    {
        float heading = transform.eulerAngles.y;
        if (heading > 60 && heading <300)
        {
            if (heading < 90)
            {
                orderedHeading = 270;
            }
            else if (heading < 270)
            {
                orderedHeading = 0;
            }
            else if (heading < 300)
            {
                orderedHeading = 90;
            }
        }
        else
        {
            if (orderedHeading == 0)
            {
                orderedHeading = 90;
            }
        }
    }

}
