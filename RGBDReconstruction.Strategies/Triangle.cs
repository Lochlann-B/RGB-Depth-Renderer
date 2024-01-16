using System.Numerics;
using OpenTK.Mathematics;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace RGBDReconstruction.Strategies;

public class Triangle
{
    private Vector3 _v1;
    private Vector3 _v2;
    private Vector3 _v3;

    private Vector3 _s12;
    private Vector3 _s13;
    private Vector3 _s23;

    private Vector3 _normal;

    public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        _v1 = v1;
        _v2 = v2;
        _v3 = v3;

        _s12 = GetVectorFromPoints(_v1, _v2);
        _s23 = GetVectorFromPoints(_v2, _v3);
        _s13 = GetVectorFromPoints(_v1, _v3);
    }
    
    public bool HasLengthLongerThanThreshold(float thresholdLength)
    {
        return (_s12.LengthFast >= thresholdLength || _s23.LengthFast >= thresholdLength ||
                _s13.LengthFast >= thresholdLength);
    }

    public Vector3[] GetVerticesAsList()
    {
        return [_v1, _v2, _v3];
    }

    public Vector3 GetNormal()
    {
        if (_normal == null)
        {
            CalculateNormal();
        }

        return _normal;
    }

    private void CalculateNormal()
    {
        _normal = Vector3.Normalize(Vector3.Cross(_s12, _s23));
    }

    private static Vector3 GetVectorFromPoints(Vector3 v1, Vector3 v2)
    {
        return new Vector3(v2[0] - v1[0], v2[1] - v1[1], v2[2] - v1[2]);
    }
}