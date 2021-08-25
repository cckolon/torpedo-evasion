using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class navgenerator : MonoBehaviour
{
    public bool generateMesh;
    public int recursionLevels; // number of times to execute subdivision loop. Set by user in Unity editor.
    List<Vector3> newverts = new List<Vector3>(); // new vertex list for the mesh
    List<int> newtris = new List<int>(); // new triangles list for the mesh
    List<Vector3> newnormals = new List<Vector3>(); // new normals list for the mesh
    List<Vector2> newuv = new List<Vector2>(); // new uv list for the mesh
    LayerMask terrain; // this is the layermask which will tell us whether a raycast collision is terrain or not
    // Start is called before the first frame update
    void Start()
    {
        if (generateMesh) // set by the user in Unity editor. Determines whether the script runs.
        {
            Mesh navplane = GetComponent<MeshFilter>().mesh; //finds the gameObject's attached mesh
            newverts.AddRange(navplane.vertices); // add old mesh's vertices, triangles, normals, and uvs to the lists
            newtris.AddRange(navplane.triangles);
            newnormals.AddRange(navplane.normals);
            newuv.AddRange(navplane.uv);
            terrain = LayerMask.GetMask("Terrain"); // anything in the "terrain" layermask will be treated as such
            int currenttris = 0; // counter for triangles
            for (int i = 0; i < recursionLevels; i++) // loop to subdivide the mesh iteratively
            {
                currenttris = newtris.Count; // reset counter to length of newtris
                SubdivideLevel(0,currenttris); // for each triangle, test it and subdivide if necessary
            }
            for (int i = 0; i < newtris.Count/3; i++) // test if each vertex of each triangle is above ground
            {
                bool aboveground = true;
                for (int j = 0; j<3; j++)
                {
                    if (!Physics.Raycast(transform.TransformPoint(newverts[newtris[3*i+j]]),-Vector3.up,Mathf.Infinity,terrain)) // raycast down. If there's not land there, the point is below terrain
                    {
                        aboveground = false;
                    }
                }
                if (!aboveground) // if all three vertices are not above ground, remove the triangle from newtris
                {
                    for (int j = 0; j<3; j++)
                    {
                        newtris.RemoveAt(3*i);
                    }
                    i = i-1; // lower incrementer to compensate for deleting the vertex from newtris
                }
            }
            Mesh newnavplane = new Mesh(); // generate the new mesh
            GetComponent<MeshFilter>().mesh = newnavplane; // render it in place of the old mesh
            newnavplane.vertices = newverts.ToArray(); // set the new mesh's properties
            newnavplane.triangles = newtris.ToArray();
            newnavplane.normals = newnormals.ToArray();
            newnavplane.uv = newuv.ToArray();
            AssetDatabase.CreateAsset(newnavplane, "Assets/navmesh.asset"); // save it to a file so it is available after the game is terminated
        }
    }


//                  x newtris[index + 1]
//                  |\
//                  |2\
//           l + 1  x--x  l + 2
//                  |\4|\
//                  |1\|3\
//  newtris[index]  x--x--x newtris [index + 2]
//                   l + 3
//
//  triangle 1: (newtris[index], l + 1, l + 3)
//  triangle 2: (newtris[index + 1], l + 2, l + 1)
//  triangle 3: (newtris[index + 2], l + 3, l + 2)
//  triangle 4: (l + 1, l + 2, l + 3)
//
//  A little schematic to help remember how the triangles work

    void SubdivideTriangle(int index) //index is the index of the first point in newtris
    {
        int l = newverts.Count-1;
        newverts.Add(newverts[newtris[index]]*.5f+newverts[newtris[index+1]]*.5f); // vertex l + 1 
        newverts.Add(newverts[newtris[index+1]]*.5f+newverts[newtris[index+2]]*.5f); // vertex l + 2
        newverts.Add(newverts[newtris[index+2]]*.5f+newverts[newtris[index]]*.5f); // vertex l + 3
        newuv.Add(newuv[newtris[index]]*.5f+newuv[newtris[index+1]]*.5f); // uv for vertex l + 1 
        newuv.Add(newuv[newtris[index+1]]*.5f+newuv[newtris[index+2]]*.5f); // uv for vertex l + 2
        newuv.Add(newuv[newtris[index+2]]*.5f+newuv[newtris[index]]*.5f); // uv for vertex l + 3
        newnormals.Add(Vector3.up);
        newnormals.Add(Vector3.up);
        newnormals.Add(Vector3.up);
        newtris.Add(newtris[index]);//first triangle
        newtris.Add(l+1);
        newtris.Add(l+3);
        newtris.Add(newtris[index+1]);//second triangle
        newtris.Add(l+2);
        newtris.Add(l+1);
        newtris.Add(newtris[index+2]);//third triangle
        newtris.Add(l+3);
        newtris.Add(l+2);
        newtris.Add(l+1);//fourth triangle
        newtris.Add(l+2);
        newtris.Add(l+3);
        newtris.RemoveAt(index); //remove old triangle
        newtris.RemoveAt(index);
        newtris.RemoveAt(index);
    }

    void SubdivideLevel(int startIndex, int endIndex) //startIndex and endIndex are the first point of the first triangle in newtris and the length of newtris
    {
        int i = startIndex;
        float buffer = 1f; //move linecasts down by this much, to ensure all "borderline" triangles are subdivided
        while (i < endIndex) // if any of the linecasts on each edge of the triangle intersect land, subdivide the triangle.
        {
            if (Physics.Linecast(transform.TransformPoint(newverts[newtris[i]])+Vector3.down*buffer,transform.TransformPoint(newverts[newtris[i+1]])+Vector3.down*buffer,terrain))
            {
                SubdivideTriangle(i);
                endIndex = endIndex-3;
            }
            else if (Physics.Linecast(transform.TransformPoint(newverts[newtris[i+1]])+Vector3.down*buffer,transform.TransformPoint(newverts[newtris[i+2]])+Vector3.down*buffer,terrain))
            {
                SubdivideTriangle(i);
                endIndex = endIndex-3;
            }
            else if (Physics.Linecast(transform.TransformPoint(newverts[newtris[i+2]])+Vector3.down*buffer,transform.TransformPoint(newverts[newtris[i]])+Vector3.down*buffer,terrain))
            {
                SubdivideTriangle(i);
                endIndex = endIndex-3;
            }
            else
            {
                i += 3; // add three to counter to skip to the next triangle. This is not necessary if the triangle is subdivided because the old triangle will be deleted. So instead, endIndex is updated to prevent an infinite loop.
            }
        }
    }
}
