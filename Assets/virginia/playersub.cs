using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class playersub : MonoBehaviour
{
    // for unit conversion, 1 unit is 10 ft
    // 1 unit per second = 6 kts
    // 300 units = 1 kyd
    // 600 units = 1 nm
    // 2klb per 100 foot depth
    public bool invincible;
    float orderedpitch = 0; //positive is up
    float rudder = 0; //positive is right
    int bell = 1;
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
    public Text spd;
    public Text crs;
    public Text dep;
    public Text bal;
    public Text balhl;
    public Text manualDisplay;
    public Text ordDep;
    public Text ordCrs;
    public Text ordBal;
    public RectTransform eotNeedle;
    public RectTransform courseAngleNeedle;
    public RectTransform fineAngleNeedle;
    public RectTransform orderedAngleNeedle;
    public GameObject[] casualtySymbols = new GameObject[3];
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
    public Sprite floodSprite;
    public Sprite hydraulicRuptureSprite;
    public Sprite slrSprite;
    public Sprite emptySprite;
    bool slr = false;
    bool[] flooding = {false,false,false};
    float[] floodwater = new float[3];
    public Text[] emergencyText = new Text[3];
    public bool jamplane = false;
    public bool jamrudder = false;
    int jamdirection = 1; // positive is dive/right
    List<string> eventlog = new List<string>();
    public int eventListLength;
    public Text eventLogObject;
    Vector3 lastAngleError;
    float lastDepthError;
    float depthI;
    Vector3 angleI;
    float cameraHeight;
    LayerMask terrain;
    float depthBelowKeel;
    bool autocontrol;
    bool groundingpermitted = true;
    public float health;
    public float length;
    string[] bellnames = {"All Back Emergency","All Back Two Thirds","All Back One Third","All Stop","All Ahead One Third","All Ahead Two Thirds","All Ahead Standard","All Ahead Full","All Ahead Flank"};
    string[] ruddernames = {"Left Hard Rudder","Left Full Rudder","Left 20° Rudder","Left 15° Rudder","Left 10° Rudder","Left 5° Rudder","Rudder Amidships","Right 5° Rudder","Right 10° Rudder","Right 15° Rudder", "Right 20° Rudder", "Right Full Rudder","Right Hard Rudder"};
    string[] cardinals = {"North","East","South","West"};
    // Start is called before the first frame update
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
        foreach (Text i in emergencyText)
        {
            i.text = "";
        }
        foreach (GameObject i in casualtySymbols)
        {
            i.GetComponent<Image>().sprite = emptySprite;
        }
        eotNeedle.RotateAround(eotNeedle.parent.position,Vector3.forward,-bell*30);
        InvokeRepeating("SecondLoop",0f,1.0f);
        OnAuto();
        OnReset();
    }

    // Update is called once per frame
    void Update()
    {
        maincam.transform.LookAt(transform.position);
        UIUpdate();
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
        orderedAngleNeedle.localPosition = new Vector3(orderedAngleNeedle.localPosition.x,Mathf.Clamp(((orderedpitch+180)%360-180),-30,30)*4,0);
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

    void UIUpdate()
    {
        crs.text=string.Format("{0:D3}", ((int)transform.rotation.eulerAngles.y) % 360);
        spd.text=string.Format("{0:N1}", rb.velocity.magnitude*6);
        dep.text=string.Format("{0:N0}", depth);
        bal.text=string.Format("{0:N0}", Mathf.Abs(buoyancy));
        if (autocontrol)
        {
            if (Mathf.Abs(depth-orderedDepth) < 25)
            {
                ordDep.gameObject.SetActive(false);
            }
            else
            {
                ordDep.gameObject.SetActive(true);
                ordDep.text = string.Format("{0:N0}", orderedDepth);
            }
            if (Mathf.Abs((orderedHeading - transform.rotation.eulerAngles.y+540)%360-180)<10)
            {
                ordCrs.gameObject.SetActive(false);
            }
            else
            {
                ordCrs.gameObject.SetActive(true);
                ordCrs.text = string.Format("{0:D3}", (int)orderedHeading);
            }
        }
        if (Mathf.Abs(orderedballast - ballast)<1)
        {
            ordBal.gameObject.SetActive(false);
        }
        else
        {
            ordBal.gameObject.SetActive(true);
            if (orderedballast > ballast)
            {
                ordBal.text = string.Format("+{0:N0}", orderedballast - ballast);
            }
            else
            {
                ordBal.text = string.Format("{0:N0}", orderedballast - ballast);
            }
        }
        if (buoyancy > .5)
        {
            balhl.text = "LIGHT OVERALL";
        }
        else if (buoyancy < -.5)
        {
            balhl.text = "HEAVY OVERALL";
        }
        else
        {
            balhl.text = "OKAY OVERALL";
        }
        courseAngleNeedle.localPosition = new Vector3(courseAngleNeedle.localPosition.x,-Mathf.Clamp(((transform.rotation.eulerAngles.x+180)%360-180),-90,90)*4/3,0);
        fineAngleNeedle.localPosition = new Vector3(fineAngleNeedle.localPosition.x,-Mathf.Clamp(((transform.rotation.eulerAngles.x+180)%360-180),-30,30)*4,0);
    }

    void AddEvent(string e)
    {
        eventlog.Add(e);
        if (eventlog.Count > eventListLength)
        {
            eventlog.RemoveAt(0);
        }
        string guitext = "";
        foreach (string logevent in eventlog)
        {
            guitext += logevent;
            guitext += "\n";
        }
        eventLogObject.text = guitext;
        StopCoroutine("WaitAndClear");
        StartCoroutine("WaitAndClear");
    }

    void ClearEvents()
    {
        eventlog.Clear();
        eventLogObject.text = "";
    }

    void OnPitch(InputValue input)
    {
        if (autocontrol)
        {
            orderedDepth = Mathf.Max(0,Mathf.Round((orderedDepth + 50*input.Get<float>())/50)*50);
            StopCoroutine("AnnounceDepth");
            StartCoroutine("AnnounceDepth");
        }
        else
        {
            orderedpitch = Mathf.Clamp(orderedpitch + 5*input.Get<float>(),-30,30);
            orderedAngleNeedle.localPosition = new Vector3(orderedAngleNeedle.localPosition.x,Mathf.Clamp(((orderedpitch+180)%360-180),-30,30)*4,0);
            StopCoroutine("AnnouncePitch");
            StartCoroutine("AnnouncePitch");            
        }
    }

    void OnTurn(InputValue input)
    {
        if (autocontrol)
        {
            orderedHeading = Mathf.Round(((orderedHeading + 15*input.Get<float>()+360)%360)/15)*15;
            StopCoroutine("AnnounceTurn");
            StartCoroutine("AnnounceTurn");
        }
        else
        {
            if (jamrudder)
            {
                rudder = 30*jamdirection;
            }
            else
            {
                rudder = Mathf.Clamp(rudder + 5*input.Get<float>(),-30,30);
                StopCoroutine("AnnounceRudder");
                StartCoroutine("AnnounceRudder");
            }
        }
    }

    void OnBallast(InputValue input)
    {
        orderedballast = Mathf.Clamp(orderedballast + 10*input.Get<float>(),-50,50);
        StopCoroutine("AnnounceBallast");
        StartCoroutine("AnnounceBallast");   
    }

    void OnSpeed(InputValue input)
    {
        if (!slr)
        {
            if (bell + input.Get<float>() > -4 && bell + input.Get<float>() < 6)
            {
                eotNeedle.RotateAround(eotNeedle.parent.position,Vector3.forward,-input.Get<float>()*30);
            }
            bell = Mathf.Clamp(bell + (int)input.Get<float>(),-3,5);
            StopCoroutine("AnnounceSpeed");
            StartCoroutine("AnnounceSpeed");
        }
    }

    void OnReset()
    {
        if (autocontrol)
        {
            orderedHeading = transform.rotation.eulerAngles.y;
            orderedDepth = depth;
            orderedballast = ballast - buoyancy;
            depthI = 0;
            AddEvent("Steady as she goes, Pilot aye.");
            AddEvent(string.Format("OOD, she goes {0:D3}.",(int)orderedHeading));
            AddEvent(string.Format("Coming to depth {0:N0} feet.",orderedDepth));
            AddEvent("Ballasting for neutral trim.");
        }
        else
        {
            orderedpitch = 0;
            if (!jamrudder)
            {
                rudder = 0;
            }
            orderedballast = ballast - buoyancy;
            orderedAngleNeedle.localPosition = new Vector3(orderedAngleNeedle.localPosition.x,Mathf.Clamp(((orderedpitch+180)%360-180),-30,30)*4,0);
            AddEvent("Rudder amidships, 0° bubble, Pilot aye.");
            AddEvent("OOD, My rudder is amidships. 0° bubble.");
            AddEvent("Ballasting for neutral trim.");
        }
    }

    void OnBlow()
    {
        if (embtavailable)
        {
            AddEvent("Emergency surface the ship, Pilot aye.");
            embtstatus = true;
            embtavailable = false;
            StartCoroutine("EMBTblow");
        }
        else
        {
            AddEvent("OOD, EMBT is unavailable. Unable to blow.");
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

    void OnAuto()
    {
        autocontrol = !autocontrol;
        ordCrs.gameObject.SetActive(autocontrol);
        ordDep.gameObject.SetActive(autocontrol);
        manualDisplay.gameObject.SetActive(!autocontrol);
        if (autocontrol)
        {
            orderedHeading = transform.rotation.eulerAngles.y;
            orderedDepth = depth;
            depthI = 0;
            AddEvent("OOD, taking automatic control.");
            AddEvent(string.Format("Ordered course is {0:D3}.",((int)orderedHeading))+ string.Format(" Ordered depth is {0:N0} feet.",orderedDepth));
        }
        else
        {
            orderedAngleNeedle.localPosition = new Vector3(orderedAngleNeedle.localPosition.x,Mathf.Clamp(((orderedpitch+180)%360-180),-30,30)*4,0);
            AddEvent("OOD, taking manual control.");
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
                if (col.gameObject.tag == "Terrain")
                {
                    AddEvent("OOD, the ship is grounded!");
                }
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
                    AddEvent("Emergency Report, Emergency Report!");
                    AddEvent("Flooding, flooding in the Engine Room!");
                    casualtySymbols[0].GetComponent<Image>().sprite = floodSprite;
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
                    AddEvent("Emergency Report, Emergency Report!");
                    AddEvent("Flooding, flooding in the Reactor Compartment!");
                    casualtySymbols[1].GetComponent<Image>().sprite = floodSprite;
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
                    AddEvent("Emergency Report, Emergency Report!");
                    AddEvent("Flooding, flooding in the Forward Compartment!");
                    casualtySymbols[2].GetComponent<Image>().sprite = floodSprite;
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
                    emergencyText[i].text = string.Format("Repairing");
                }
                else
                {
                    StopCoroutine("FloodRepair");
                    emergencyText[i].text = string.Format("{0:N0}% Flooded",floodwater[i]*100);
                }
                rb.AddForceAtPosition(Vector3.down*floodwater[i]*3f,floatpoints[i].position);
            }
        }
    }

    void Die()
    {
        AddEvent("You Died.");
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
        AddEvent("Open forward and aft vents, Pilot Aye.");
        AddEvent("OOD, all vents open.");
        for (int i = 0; i<2; i++)
        {
            venteffects[i].Play();
        }
        yield return new WaitForSeconds(5f);
        AddEvent("OOD, all vents shut.");
        for (int i = 0; i<2; i++)
        {
            venteffects[i].Stop();
        }
    }

    IEnumerator WaitAndClear()
    {
        yield return new WaitForSeconds(10f);
        ClearEvents();
    }

    IEnumerator AnnounceSpeed()
    {
        yield return new WaitForSeconds(1f);
        AddEvent(bellnames[bell+3] +", Pilot aye.");
        yield return new WaitForSeconds(1f);
        AddEvent("OOD, Maneuvering answers "+bellnames[bell+3] + ".");
        if(bell == 5 | bell == -3)
        {
            yield return new WaitForSeconds(1f);
            AddEvent("CONN, Maneuvering, answering " + bellnames[bell+3] + " limited by power.");
        }
    }

    IEnumerator AnnounceRudder()
    {
        yield return new WaitForSeconds(1f);
        AddEvent(ruddernames[((int)rudder/5)+6] +", Pilot aye.");
        yield return new WaitForSeconds(1f);
        if (rudder < 0)
        {
            AddEvent("OOD, my rudder is Left " + (-rudder) + "°.");
        }
        else if (rudder > 0)
        {
            AddEvent("OOD, my rudder is Right " + rudder + "°.");
        }
        else
        {
            AddEvent("OOD, my rudder is amidships.");
        }
    }

    IEnumerator AnnounceTurn()
    {
        yield return new WaitForSeconds(1f);
        string direction;
        if(rudder > 0)
        {
            direction = "right";
        }
        else
        {
            direction = "left";
        }
        if ((int)orderedHeading % 90 == 0)
        {
            AddEvent("Come " + direction + ", steer course " + cardinals[(int)(orderedHeading/90)] + ", Pilot aye.");
        }
        else{
            AddEvent(string.Format("Come " + direction + ", steer course {0:D3}, Pilot aye.",(int)orderedHeading));
        }
        yield return new WaitForSeconds(1f);
        AddEvent("OOD, my rudder is " + direction + ".");
    }

    IEnumerator AnnounceDepth()
    {
        yield return new WaitForSeconds(1f);
        AddEvent(string.Format("Make my depth {0:N0} feet, Pilot aye.",orderedDepth));
    }

    IEnumerator AnnouncePitch()
    {
        yield return new WaitForSeconds(1f);
        string direction;
        if (orderedpitch > 0)
        {
            direction = "up ";
        }
        else if (orderedpitch < 0)
        {
            direction = "down ";
        }
        else 
        {
            direction = "";
        }
        AddEvent(string.Format("{0:N0}° ",Mathf.Abs(orderedpitch)) + direction + "bubble, Pilot aye.");
    }

    IEnumerator AnnounceBallast()
    {
        string direction;
        if (orderedballast < ballast)
        {
            direction = "Ingest";
        }
        else
        {
            direction = "Pump off";
        }
        float numpounds = Mathf.Abs(orderedballast - ballast);
        yield return new WaitForSeconds(1f);
        AddEvent(direction + string.Format(" {0:N0} thousand pounds, Copilot aye.",numpounds));
    }

    IEnumerator Jam()
    {
        AddEvent("Emergency Report, Emergency Report! Hydraulic rupture in the Engine Room!");
        casualtySymbols[0].GetComponent<Image>().sprite = hydraulicRuptureSprite;
        yield return new WaitForSeconds(.5f);
        int type = Random.Range(0,2);
        jamdirection = (int)Mathf.Clamp(Random.Range(0,2),0,1)*2-1;
        if (type == 0)
        {
            jamplane = true;
            if (jamdirection == 1)
            {
                AddEvent("Jam dive! Jam dive!");
            }
            else
            {
                AddEvent("Jam rise! Jam rise!");
            }
        }
        else
        {
            jamrudder = true;
            rudder = 30*jamdirection;
            if (jamdirection == 1)
            {
                AddEvent("Jam rudder! Rudder is jammed Hard Right!");
            }
            else
            {
                AddEvent("Jam rudder! Rudder is jammed Hard Left!");
            }
        }
        yield return new WaitForSeconds(Random.Range(15,45));
        AddEvent("Conn, Maneuvering. Hydraulics are restored.");
        AddEvent("OOD, I have control of the ship.");
        jamrudder = false;
        jamplane = false;
        OnReset();
        casualtySymbols[0].GetComponent<Image>().sprite = emptySprite;
    }

    IEnumerator SLR()
    {
        AddEvent("Conn, Maneuvering, steam line rupture!");
        AddEvent("OOD, loss of propulsion! Maneuvering answers All Stop!");
        casualtySymbols[0].GetComponent<Image>().sprite = slrSprite;
        slr = true;
        bell = 0;
        eotNeedle.RotateAround(eotNeedle.parent.position,Vector3.forward,-eotNeedle.rotation.eulerAngles.z);
        yield return new WaitForSeconds(Random.Range(10,20));
        AddEvent("Conn, Maneuvering, steam line rupture is isolated.");
        yield return new WaitForSeconds(Random.Range(10,20));
        AddEvent("Conn, Maneuvering, engine room watchstanders have regained their watchstations.");
        AddEvent("Restoring the engine room single valve.");
        yield return new WaitForSeconds(Random.Range(10,20));
        AddEvent("Conn, Maneuvering, propulsion is restored. Ready to answer all bells.");
        slr = false;
        casualtySymbols[0].GetComponent<Image>().sprite = emptySprite;
    }

    IEnumerator GroundingDelay()
    {
        groundingpermitted = false;
        yield return new WaitForSeconds(5f);
        groundingpermitted = true;
    }

    IEnumerator FloodRepair()
    {
        AddEvent("Emergency Report, Emergency Report! The flooding has stopped!");
        yield return new WaitForSeconds(Random.Range(10,20));
        for (int i = 0; i<3; i++)
        {
            if (floodwater[i] == 0)
            {
                flooding[i] = false;
                emergencyText[i].text = "";
                casualtySymbols[i].GetComponent<Image>().sprite = emptySprite;
                AddEvent("DC Central, Primary Scene, the flooding has been repaired!");
            }
        }
    }

}
