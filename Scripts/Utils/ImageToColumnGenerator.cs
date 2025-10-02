using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Utils
{
    [RequireComponent(typeof(SpriteRenderer), typeof(PolygonCollider2D))]
    public class ImageToColumnGenerator : MonoBehaviour
    {
        [Header("Image Settings")]
        [SerializeField] private float columnHeight = 1f;
        private SpriteRenderer _spriteRenderer;

        private GameObject _columnObject;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        internal static string ColumnChildObjectName = "Column";

        #region Main Generation Method
        public void GenerateColumnFromImage()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer is null)
            {
                Debug.LogWarning($"No source sprite renderer found on GameObject {gameObject.name}");
                return;
            }
            
            InitializeColumnObject();

            try
            {
                ExtractImageGeometry(_spriteRenderer, out var vertices, out var triangles);
                var columnMesh = GenerateColumnMesh(vertices, triangles, columnHeight);
                ApplyMaterialToMesh(columnMesh);

                // Leave a slight space between the column obj and the sprite obj
                _columnObject.transform.position += new Vector3(0f, 0f, 0.05f);
            }
            catch (Exception e)
            {
                Debug.LogError($"{gameObject.name} column generation failed: {e.Message}");
            }
        }

        private void InitializeColumnObject()
        {
            _columnObject = new GameObject(ColumnChildObjectName, typeof(MeshFilter), typeof(MeshRenderer));
            _columnObject.transform.SetParent(transform);
            
            _meshFilter = _columnObject.GetComponent<MeshFilter>();
            _meshRenderer = _columnObject.GetComponent<MeshRenderer>();

            if (_meshFilter.mesh == null)
                _meshFilter.mesh = new Mesh();
        }
        #endregion

        #region Image Processing

        private void ExtractImageGeometry(SpriteRenderer spriteRenderer, out Vector2[] vertices, out ushort[] triangles)
        {
            vertices = spriteRenderer.sprite.vertices;
            triangles = spriteRenderer.sprite.triangles;
        }
        #endregion

        #region Mesh Generation
        private Mesh GenerateColumnMesh(Vector2[] imgVertices, ushort[] imgTriangles, float height = 1.0f)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // Top face (facing -z)
            foreach (var vertex in imgVertices)
                vertices.Add(new Vector3(vertex.x, vertex.y, 0));
            
            var topFaceCopyVidOffset = vertices.Count;
            foreach (var vertex in imgVertices)
                vertices.Add(new Vector3(vertex.x, vertex.y, 0));
            
            foreach (var vid in imgTriangles)
                triangles.Add((int)vid);
            
            // Bottom face (facing +z)
            var bottomFaceVidOffset = vertices.Count;
            foreach (var vertex in imgVertices)
                vertices.Add(new Vector3(vertex.x, vertex.y, height));
            
            var bottomFaceCopyVidOffset = vertices.Count;
            foreach (var vertex in imgVertices)
                vertices.Add(new Vector3(vertex.x, vertex.y, height));
            
            if (imgTriangles.Length % 3 != 0)
                throw new IndexOutOfRangeException("Vertices count in a triangles list should be divisible by 3");
            for (int i = 0; i < imgTriangles.Length - 3; i += 3)
            {
                // Permute the last 2 vertex of each triangle to flip the face
                var vid0 = imgTriangles[i];
                var vid1 = imgTriangles[i + 1];
                var vid2 = imgTriangles[i + 2];
                triangles.Add((int)vid2 + bottomFaceVidOffset);
                triangles.Add((int)vid1 + bottomFaceVidOffset);
                triangles.Add((int)vid0 + bottomFaceVidOffset);
            }
            
            // Side face
            var vertexUsedCount = 0;
            var vertexUsed = new bool[imgVertices.Length];
            for (int i = 0; i < vertexUsed.Length; i++)
                vertexUsed[i] = false;

            var currVertexId = 0;
            while (vertexUsed.Contains(false))
            {
                var nextVertexId = SelectAdjacentCapVertexId(imgVertices, currVertexId, ref vertexUsed);
                if (nextVertexId == -1)
                {
                    if (vertexUsedCount == imgVertices.Length - 1)
                        nextVertexId = 0;
                    else
                        throw new Exception($"Next vertex id -1! Curr vertex #{currVertexId}: {imgVertices[currVertexId]}");
                }

                var vidTopA1 = currVertexId;
                var vidTopA2 = nextVertexId;
                
                var vidTopB1 = currVertexId + topFaceCopyVidOffset;
                var vidTopB2 = nextVertexId + topFaceCopyVidOffset;
                
                var vidBottomA1 = currVertexId + bottomFaceVidOffset;
                var vidBottomA2 = nextVertexId + bottomFaceVidOffset;
                
                var vidBottomB1 = currVertexId + bottomFaceCopyVidOffset;
                var vidBottomB2 = nextVertexId + bottomFaceCopyVidOffset;
                
                // Triangle 1
                triangles.Add(vidTopA1);
                triangles.Add(vidTopA2);
                triangles.Add(vidBottomA1);
                
                // Triangle 1 - flipped
                triangles.Add(vidBottomB1);
                triangles.Add(vidTopB2);
                triangles.Add(vidTopB1);
                
                // Triangle 2
                triangles.Add(vidTopA2);
                triangles.Add(vidBottomA2);
                triangles.Add(vidBottomA1);
                
                // Triangle 2 - flipped
                triangles.Add(vidBottomB1);
                triangles.Add(vidBottomB2);
                triangles.Add(vidTopB2);

                vertexUsedCount++;
                vertexUsed[currVertexId] = true;
                currVertexId = nextVertexId;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private int SelectAdjacentCapVertexId(Vector2[] imgVertices, int startVertexId, ref bool[] verticesUsed)
        {
            var startVertex = imgVertices[startVertexId];
            
            // 按离起始点的距离排序
            var sortedVertices = new List<VertexInfo>();
            for (int i = 0; i < imgVertices.Length; i++)
            {
                if (verticesUsed[i])
                    continue;
                
                var vertex = imgVertices[i];
                sortedVertices.Add(new VertexInfo
                {
                    ID = i,
                    Value = vertex,
                });
            }
            
            sortedVertices.Sort(new VertexInfoComparer(new VertexInfo
            {
                ID = -1,
                Value = startVertex,
            }));

            // 开始按照距离 startVertex 从近到远的方式检测每个点是否是 startVertex 在边缘上的相邻点
            for (int i = 0; i < sortedVertices.Count; i++)
            {
                var nextVertex = sortedVertices[i].Value;
                
                // 获取垂直平分线上对称的两个点，记为 C, D
                var distinctPoints = GetMirrorPointsOnPerpendicularBisector(startVertex, nextVertex, out var c, out var d, 0.05f);
                if (!distinctPoints)
                    continue;
                
                // 创建两个射线，一个从 C 点穿过去，一个从 D 点穿过去
                var rayA = new Ray(new Vector3(c.x, c.y, -1), Vector3.forward);
                var rayB = new Ray(new Vector3(d.x, d.y, -1), Vector3.forward);
                
                var hitA = Physics2D.GetRayIntersection(rayA, Mathf.Infinity);
                var hitB = Physics2D.GetRayIntersection(rayB, Mathf.Infinity);

                var intersectA = (
                    hitA.collider is not null && 
                    hitA.collider.gameObject.GetComponent<SpriteRenderer>() == _spriteRenderer);
                var intersectB = (
                    hitB.collider is not null &&
                    hitB.collider.gameObject.GetComponent<SpriteRenderer>() == _spriteRenderer);
                
                // 仅当两个点中只有一个在图像内、另一个在图像外时，才判定 startVertex <-> nextVertex 之间的连线是图像边界
                var isEdge = (intersectA || intersectB) && !(intersectA && intersectB);
                if (isEdge)
                    return sortedVertices[i].ID;
            }

            return -1;
        }

        #region Adjacent Cap Vertex Selection Helpers
        private struct VertexInfo
        {
            public int ID;
            public Vector2 Value;
        }

        private class VertexInfoComparer : IComparer<VertexInfo>
        {
            private readonly VertexInfo _origin;
            
            public int Compare(VertexInfo x, VertexInfo y)
            {
                var disX = VertexPairDistance(x.Value, _origin.Value);
                var disY = VertexPairDistance(y.Value, _origin.Value);
                if (disX > disY)
                    return 1;
                if (Mathf.Approximately(disX, disY))
                    return 0;
                return -1;
            }
            
            private static float VertexPairDistance(Vector2 a, Vector2 b)
            {
                return (a - b).sqrMagnitude;
            }
            
            public VertexInfoComparer(VertexInfo origin)
            {
                _origin = origin;
            }
        }

        private bool GetMirrorPointsOnPerpendicularBisector(Vector2 a, Vector2 b, out Vector2 c, out Vector2 d, float h = 1.0f)
        {
            // Omit overlapping point pair
            if (Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y))
            {
                c = a;
                d = b;
                return false;
            }
            
            // Mid point
            var mid = (a + b) / 2f;
            
            // Normal vector
            var dy = b.y - a.y;
            var dx = b.x - a.x;
            var norm1 = (new Vector2(-dy, dx)).normalized;
            var norm2 = (new Vector2(dy, -dx)).normalized;
            
            // Mirror points on PB
            c = mid + (norm1 * h);
            d = mid + (norm2 * h);
            return true;
        }
        #endregion
        #endregion

        #region Material Application
        private void ApplyMaterialToMesh(Mesh mesh)
        {
            var material = new Material(Shader.Find("Standard"));
            material.mainTexture = null;

            _meshRenderer.material = material;
            _meshFilter.mesh = mesh;
        }

        private void GenerateMeshCollider()
        {
            MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = _meshFilter.mesh;
            meshCollider.convex = true;
        }
        #endregion
    }
}
