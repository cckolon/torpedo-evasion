using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

//Passive sonar equation is SNR=SL-TL-LE-NL
//For spherical spreading: TL = 20*log_10(distance in meters)
//For cylindrical spreading: TL = 10*log_10(distance in meters)
//Use cylindrical spreading for most stuff because it's less accurate but makes game more fun
//typical LE is 

public class sonarscript : MonoBehaviour
{
    Texture2D screen;
    public int width;
    public int height;
    public int mipCount;
    public GameObject[] torpedos = new GameObject[1];
    public GameObject[] players = new GameObject[1];
    public List<GameObject> noisythings = new List<GameObject>();
    public List<float> sourceLevels = new List<float>();
    public GameObject subject;
    public GameObject waterline;
    public Camera mainCamera;
    public float subjectSourceLevel;
    public float noiseLevel;
    public float heading;
    public float lastHeading;
    public float sonarInterval; // seconds
    public float le;
    public int baffledRegion;
    public float subjectHeading;
    public Vector3 subjectPosition;
    Color[] screenarray = new Color[720*360];
    Color[] lasttickbackground = new Color[1800];
    // Start is called before the first frame update
    void Start()
    {
        subject = transform.parent.transform.parent.gameObject;
        screen = new Texture2D(width,height);
        for (int i=0; i<screenarray.Length; i++)
        {
            screenarray[i] = Color.black;
            screenarray[i].a = 1f;
        }
        screen.SetPixels(screenarray);
        screen.Apply(true);
        gameObject.GetComponent<RawImage>().texture = screen;
        waterline = GameObject.FindGameObjectWithTag("Waterline");
        le = waterline.GetComponent<environmental>().le;
        InvokeRepeating("RefreshDisplay",0f,sonarInterval);
        InvokeRepeating("RefreshList",0f,2);
    }

    // Update is called once per frame
    void Update()
    {}
    
    void RefreshDisplay()
    {
        lastHeading=heading;
        if (mainCamera != null)
        {
            heading = 360f/width*(int)((width/360f)*mainCamera.transform.rotation.eulerAngles.y);
        }
        if (subject != null)
        {
            subjectHeading = 360f/width*(int)((width/360f)*subject.transform.rotation.eulerAngles.y);
            subjectPosition = subject.transform.position;
        }
        noiseLevel = subjectSourceLevel/2;
        for (int j =0; j< (height-1); j++)
        {
            for (int i = 0; i<width; i++)
            {
                screenarray[j*width+i] = screenarray[((j+1)*width+(int)((i+((heading-lastHeading)*width/360))%width))];
            }
        }
        for (int i = width*(height-1); i<width*height; i++)
        {
            if((i + (heading - subjectHeading)*width/360)% width> baffledRegion*width/360 && (i + (heading - subjectHeading)*width/360) % width<width-baffledRegion*width/360)
            {
                float pixelheading = (i*360/width + heading - subjectHeading)%360;
                screenarray[i] = lasttickbackground[(int)((i+((heading-lastHeading)*width/360))+Mathf.Round(Random.Range(0,250*EasySin(pixelheading))/50))%width]*.8f;
                screenarray[i] += Color.green*Random.Range(0,(noiseLevel+le-35)/100)*.2f;
                screenarray[i].a = 1f;
            }
            else
            {
                screenarray[i] = Color.black;
            }
        }
        for (int i=0;i<width;i++)
        {
            lasttickbackground[i]=screenarray[width*(height-1)+i];
        }
        for (int i = 0; i<noisythings.Count(); i++)
        {
            if (noisythings[i] != null)
            {
                float brg = 180 + Vector3.SignedAngle(new Vector3(Mathf.Sin(heading*Mathf.PI/180),0,Mathf.Cos(heading*Mathf.PI/180)),Vector3.ProjectOnPlane((noisythings[i].transform.position - subjectPosition),Vector3.up),Vector3.up);
                float snr = sourceLevels[i] - 10*Mathf.Log(3*(subjectPosition-noisythings[i].transform.position).magnitude,10)-noiseLevel-le;
                if(((int)brg + heading - subjectHeading)% 360> baffledRegion && ((int)brg + heading - subjectHeading) % 360<360-baffledRegion)
                {
                    screenarray[width*(height-1)+(int)(brg*width/360-1)]+=Color.green*Mathf.Clamp01(Random.Range(0,snr/30));
                    for (int j = 1; j < width/180; j++)
                    {
                        screenarray[width*(height-1)+(int)(brg*width/360-1)+j]+=Color.green*Mathf.Clamp01(Random.Range(0,snr/30-j*180f/width));
                        screenarray[width*(height-1)+(int)(brg*width/360-1)-j]+=Color.green*Mathf.Clamp01(Random.Range(0,snr/30-j*180f/width));
                    }
                    if (snr > 30)
                    {
                        for (int j = width/180; j< 3*width/360; j++)
                        {
                            screenarray[width*(height-1)+(int)(brg*width/360-1)+j]=Color.black;
                            screenarray[width*(height-1)+(int)(brg*width/360-1)-j]=Color.black;
                        }
                    }
                }
            }
        }
        screen.SetPixels(screenarray);
        screen.Apply(false);
    }

    float EasySin(float x)
    {
        return (2*Mathf.Abs(((x/180-.5f)%2+2)%2-1)-1);
    }

    void RefreshList()
    {
        noisythings.Clear();
        sourceLevels.Clear();
        GameObject[] torpedos = GameObject.FindGameObjectsWithTag("torpedo");
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject[] aiPlayers = GameObject.FindGameObjectsWithTag("AIPlayer");
        foreach (GameObject i in torpedos)
        {
            noisythings.Add(i);
            sourceLevels.Add(i.GetComponent<zemtorpedo>().sourceLevel);
        }
        foreach (GameObject i in players)
        {
            if (i != subject)
            {
                noisythings.Add(i);
                if (i.TryGetComponent(out playersub j))
                {
                    sourceLevels.Add(j.sourceLevel);
                }
                else if (i.TryGetComponent(out aisub k))
                {
                    sourceLevels.Add(k.sourceLevel);
                }
                else
                {
                    sourceLevels.Add(1.0f);
                }
            }
        }
        foreach (GameObject i in aiPlayers)
        {
            if (i != subject)
            {
                noisythings.Add(i);
                if (i.TryGetComponent(out playersub j))
                {
                    sourceLevels.Add(j.sourceLevel);
                }
                else if (i.TryGetComponent(out aisub k))
                {
                    sourceLevels.Add(k.sourceLevel);
                }
                else
                {
                    sourceLevels.Add(1.0f);
                }
            }
        }
        if (subject != null)
        {
            subjectSourceLevel = subject.GetComponent<playersub>().sourceLevel;
        }
        else
        {
            subjectSourceLevel = 0;
        }
    }

}
