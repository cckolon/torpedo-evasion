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
    public Texture2D screenTemplate;
    Texture2D screen;
    public int width;
    public int height;
    public int mipCount;
    public GameObject[] torpedos = new GameObject[1];
    public GameObject[] players = new GameObject[1];
    public List<GameObject> noisythings = new List<GameObject>();
    public List<float> sourceLevels = new List<float>();
    public GameObject subject;
    public Camera mainCamera;
    public float subjectSourceLevel;
    public float noiseLevel;
    public int heading;
    public int lastHeading;
    public float sonarInterval; // seconds
    public float le;
    public int baffledRegion;
    public int subjectHeading;
    public Vector3 subjectPosition;
    // Start is called before the first frame update
    Color[] screenarray = new Color[360*60];
    void Start()
    {
        subject = transform.parent.transform.parent.gameObject;
        mipCount = screenTemplate.mipmapCount;
        width = screenTemplate.width;
        height = screenTemplate.height;
        screen = new Texture2D(width,height);
        screen.SetPixels(screenTemplate.GetPixels());
        Color[] screenarray = new Color[width*height];
        for (int i=0; i<screenarray.Length; i++)
        {
            screenarray[i] = Color.black;
        }
        screen.SetPixels(screenarray);
        screen.Apply(false);
        gameObject.GetComponent<RawImage>().texture = screen;
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
            heading = (int)mainCamera.transform.rotation.eulerAngles.y;
        }
        if (subject != null)
        {
            subjectHeading = (int)subject.transform.rotation.eulerAngles.y;
            subjectPosition = subject.transform.position;
        }
        noiseLevel = (le + subjectSourceLevel-20)/3;
        for (int j =0; j< (height-1); j++)
        {
            for (int i = 0; i<width; i++)
            {
                screenarray[j*width+i] = screenarray[(j+1)*width+((heading-lastHeading+i)%360)];
            }
        }
        for (int i = width*(height-1); i<width*height; i++)
        {
            if((i + heading - subjectHeading)% width> baffledRegion*width/360 && (i + heading - subjectHeading) % width<width-baffledRegion*width/360)
            {
                screenarray[i] = Color.green*Random.Range(0,noiseLevel/60);
                screenarray[i].a = 1f;
            }
            else
            {
                screenarray[i] = Color.black;
            }
        }
        for (int i = 0; i<noisythings.Count(); i++)
        {
            if (noisythings[i] != null)
            {
                int brg = 180 + (int)Vector3.SignedAngle(new Vector3(Mathf.Sin(heading*Mathf.PI/180),0,Mathf.Cos(heading*Mathf.PI/180)),Vector3.ProjectOnPlane((noisythings[i].transform.position - subjectPosition),Vector3.up),Vector3.up);
                float snr = sourceLevels[i] - 10*Mathf.Log(3*(subjectPosition-noisythings[i].transform.position).magnitude,10)-noiseLevel;
                if((brg + heading - subjectHeading)% 360> baffledRegion && (brg + heading - subjectHeading) % 360<360-baffledRegion)
                {
                    screenarray[width*(height-1)+brg*width/360-1]+=Color.green*Mathf.Clamp01(Random.Range(0,snr/30));
                }
            }
        }
        screen.SetPixels(screenarray);
        screen.Apply(false);
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
