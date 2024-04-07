// using Geometry;
// using OpenTK;
// using SimpleScene;
// using SimpleScene.Util.ssBVH;
//
// namespace OpenTKDowngradeHelper;
//
// public class SimpleSceneCommunicator
// {
//     public static List<ssBVHNode<Triangle>> GetRayHits(ssBVH<Triangle> bvh, Ray ray)
//     {
//         var source = ray.SourceAsFloats();
//         var direction = ray.DirectionAsFloats();
//         return bvh.traverse(new SSRay(new Vector3(source[0], source[1], source[2]),
//             new Vector3(direction[0], direction[1], direction[2])));
//     }
// }