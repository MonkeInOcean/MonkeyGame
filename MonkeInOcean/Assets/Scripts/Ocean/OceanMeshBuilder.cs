using UnityEngine;

namespace Ocean
{
    /// <summary>
    /// Builds a flat, evenly-subdivided grid mesh in the XZ plane centered on the
    /// origin. The Ocean shader displaces it with Gerstner waves in world space,
    /// and <see cref="OceanController"/> recenters it on the camera each frame, so
    /// this grid only needs to cover the visible area around the player rather than
    /// the whole world.
    /// </summary>
    public static class OceanMeshBuilder
    {
        /// <summary>
        /// Creates a grid of <paramref name="resolution"/> x <paramref name="resolution"/>
        /// quads spanning <paramref name="size"/> world units, centered at local origin.
        /// </summary>
        public static Mesh Build(int resolution, float size)
        {
            resolution = Mathf.Clamp(resolution, 2, 400);
            int vertsPerSide = resolution + 1;
            int vertCount = vertsPerSide * vertsPerSide;

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var indices = new int[resolution * resolution * 6];

            float half = size * 0.5f;
            float step = size / resolution;

            for (int z = 0; z < vertsPerSide; z++)
            {
                for (int x = 0; x < vertsPerSide; x++)
                {
                    int i = z * vertsPerSide + x;
                    float px = -half + x * step;
                    float pz = -half + z * step;
                    vertices[i] = new Vector3(px, 0f, pz);
                    uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
                }
            }

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int bl = z * vertsPerSide + x;
                    int br = bl + 1;
                    int tl = bl + vertsPerSide;
                    int tr = tl + 1;

                    indices[t++] = bl;
                    indices[t++] = tl;
                    indices[t++] = tr;

                    indices[t++] = bl;
                    indices[t++] = tr;
                    indices[t++] = br;
                }
            }

            var mesh = new Mesh { name = "OceanGrid" };
            // large grids exceed 65k verts, so use a 32-bit index buffer
            mesh.indexFormat = vertCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateNormals(); // flat up-normals; real normals come from the shader

            // Oversized bounds so wave displacement + recentering never frustum-culls it.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, size, size));
            return mesh;
        }
    }
}
