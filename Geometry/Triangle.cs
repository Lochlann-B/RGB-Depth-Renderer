using OpenTK.Mathematics;

namespace Geometry;

public class Triangle
{
    // TODO: Move other  pure geometry classes to this project
    
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
        _s13 = GetVectorFromPoints(_v3, _v1);
    }

    public bool PointIsInside(Vector3 p)
    {
        var c1 = p - _v1;
        var c2 = p - _v2;
        var c3 = p - _v3;
        var n = GetNormal();
        var res = Vector3.Dot(n, Vector3.Cross(_s12, c1)) > 0 &&
                  Vector3.Dot(n, Vector3.Cross(_s23, c2)) > 0 &&
                  Vector3.Dot(n, Vector3.Cross(_s13, c3)) > 0;
        return res;
    }

    public Vector3 Center()
    {
        return (_v1 + _v2 + _v3) / 3f;
    }

    public float[] GetCenterFloats()
    {
        var c = Center();
        return [c[0], c[1], c[2]];
    }

    public float Radius()
    {
        var c = Center();
        return Math.Max(Math.Max(Vector3.Distance(c, _v1), Vector3.Distance(c, _v2)), Vector3.Distance(c, _v3));
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
        if (_normal.Length <= 1e-6)
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

    public Vector3 V1 => _v1;
}