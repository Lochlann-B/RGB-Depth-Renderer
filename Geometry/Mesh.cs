using OpenTK.Mathematics;

namespace Geometry;

public class Mesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> texCoords)
{

    public Mesh(MeshLayout meshLayout, List<float> vertexPositions, List<float> vertexNormals, List<float> texCoords, List<float> xRanges, List<float> yRanges, List<float> zRanges) : this(meshLayout, vertexPositions, vertexNormals, texCoords)
    {
        this.xRanges = xRanges;
        this.yRanges = yRanges;
        this.zRanges = zRanges;
    }

    private List<Triangle>? _triangularMesh = null;

    public List<Triangle> GetMeshTriangles()
    {
        if (_triangularMesh != null)
        {
            return _triangularMesh;
        }
        
        _triangularMesh = new List<Triangle>();
        
        var indices = MeshLayout.IndexArray;
        for (int i = 0; i < indices.Length; i += 3)
        {
            // Make triangle using positions and indices
            var triangleVertices = new List<Vector3>();

            for (int j = i; j < i + 3; j++)
            {
                triangleVertices.Add(new Vector3(
                    VertexPositions[3 * indices[j]],
                    VertexPositions[3 * indices[j] + 1],
                    VertexPositions[3 * indices[j] + 2]
                ));
            }

            var triangle = new Triangle(triangleVertices[0], triangleVertices[1], triangleVertices[2]);
            _triangularMesh.Add(triangle);
        }

        return _triangularMesh;
    }

    public float[] GetContiguousMeshData()
    {
        var vertexAttribs = MeshLayout.VertexAttributes;
        var posVertexAttribs = vertexAttribs["positions"];
        var normVertexAttribs = vertexAttribs["normals"];
        var texVertexAttribs = vertexAttribs["textureCoordinates"];

        int numPosComponents = posVertexAttribs.NumComponents;
        int numNormComponents = normVertexAttribs.NumComponents;
        
        int stride = MeshLayout.Stride;

        float[] contiguousMeshData = new float[VertexNormals.Count + VertexPositions.Count + texCoords.Count];

        int idx = 1;
        int currentUpperLimitIndex = numPosComponents;
        VertexPositions.ForEach(pos =>
        {
            contiguousMeshData[idx-1] = pos;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numPosComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        idx = 1 + numPosComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents;
        VertexNormals.ForEach(norm =>
        {
            contiguousMeshData[idx-1] = norm;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numNormComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });
        int numTexComponents = texVertexAttribs.NumComponents;
        idx = 1 + numNormComponents + numPosComponents;
        currentUpperLimitIndex = numPosComponents + numNormComponents + numTexComponents;
        texCoords.ForEach(tex =>
        {
            contiguousMeshData[idx-1] = tex;
            if ((idx ) % (currentUpperLimitIndex) == 0)
            {
                idx += stride - (numTexComponents - 1);
                currentUpperLimitIndex += stride;
            }
            else
            {
                idx++;
            }
        });

        return contiguousMeshData;
    }

    public MeshLayout MeshLayout { get; } = meshLayout;
    public List<float> VertexPositions { get; } = vertexPositions;
    public List<float> VertexNormals { get; } = vertexNormals;
    public List<float> xRanges { get; set; }
    public List<float> yRanges { get; set; }
    public List<float> zRanges { get; set; }
}