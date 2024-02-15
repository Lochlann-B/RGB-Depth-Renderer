using Geometry;
using OpenTK;
using SimpleScene.Util.ssBVH;

namespace OpenTKDowngradeHelper;

public class TriangleBVHNodeAdaptor : SSBVHNodeAdaptor<Triangle>
{
    protected ssBVH<Triangle> _bvh;
    protected Dictionary <Triangle, ssBVHNode<Triangle>> _triangleToLeafMap 
        = new Dictionary <Triangle, ssBVHNode<Triangle>>();
    public ssBVH<Triangle> BVH
    {
        get { return _bvh; }
    }
    public void setBVH(ssBVH<Triangle> bvh)
    {
        _bvh = bvh;
    }

    public Vector3 objectpos(Triangle obj)
    {
        var c = obj.GetCenterFloats();
        return new Vector3(c[0], c[1], c[2]);
    }

    public float radius(Triangle obj)
    {
        return obj.Radius();
    }

    public void mapObjectToBVHLeaf(Triangle obj, ssBVHNode<Triangle> leaf)
    {
        _triangleToLeafMap[obj] = leaf;
    }

    public void unmapObject(Triangle obj)
    {
        _triangleToLeafMap.Remove(obj);
    }

    public void checkMap(Triangle obj)
    {
        if (!_triangleToLeafMap.ContainsKey (obj)) {
            throw new Exception("missing map for a shuffled child");
        }
    }

    public ssBVHNode<Triangle> getLeaf(Triangle obj)
    {
        return _triangleToLeafMap[obj];
    }
}