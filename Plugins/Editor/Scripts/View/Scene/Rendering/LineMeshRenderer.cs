﻿using UnityEngine;
using System.Collections.Generic;
using InternalRealtimeCSG;
using System.Linq;
using Unity.Collections;
using UnityEngine.Pool;

namespace RealtimeCSG
{
	internal sealed class LineMeshManager
	{
		private sealed class LineMesh
		{
			public const int MaxVertexCount = 65000 - 4;

			public int VertexCount { get; set; } = 0;

			public NativeArray<Vector3>	vertices1	= new NativeArray<Vector3>(MaxVertexCount, Allocator.Persistent);
			public NativeArray<Vector3>	vertices2	= new NativeArray<Vector3>(MaxVertexCount, Allocator.Persistent);
			public NativeArray<Vector4>	offsets		= new NativeArray<Vector4>(MaxVertexCount, Allocator.Persistent);
			public NativeArray<Color> colors = new NativeArray<Color>(MaxVertexCount, Allocator.Persistent);

			public LineMesh()
			{
			}

			public static LineMesh CreateInstance()
			{
				GenericPool<LineMesh>.Get(out var lineMesh);
				lineMesh.Clear();
				return lineMesh;
			}

			public void Clear()
			{
				VertexCount	= 0;
			}
		
			public void AddLine(Vector3 A, Vector3 B, float thickness, float dashSize, Color color)
			{
				if (float.IsInfinity(A.x) || float.IsInfinity(A.y) || float.IsInfinity(A.z) ||
					float.IsInfinity(B.x) || float.IsInfinity(B.y) || float.IsInfinity(B.z) ||
					float.IsNaN(A.x) || float.IsNaN(A.y) || float.IsNaN(A.z) ||
					float.IsNaN(B.x) || float.IsNaN(B.y) || float.IsNaN(B.z))
					return;

				int n = VertexCount;
				vertices1[n] = B; vertices2[n] = A; offsets[n] = new Vector4(thickness, -1, dashSize); colors[n] = color; n++;
				vertices1[n] = B; vertices2[n] = A; offsets[n] = new Vector4(thickness, +1, dashSize); colors[n] = color; n++;
				vertices1[n] = A; vertices2[n] = B; offsets[n] = new Vector4(thickness, -1, dashSize); colors[n] = color; n++;
				vertices1[n] = A; vertices2[n] = B; offsets[n] = new Vector4(thickness, +1, dashSize); colors[n] = color; n++;
				VertexCount = n;
			}

			private int[] indices = null;
			private Mesh mesh;

			public void CommitMesh()
			{
				if (VertexCount == 0)
				{
					if (mesh != null && mesh.vertexCount != 0)
					{
						mesh.Clear(true);
					}
					return;
				}

				if (mesh)
				{
					mesh.Clear(true);
				}
				else
				{
					mesh = new Mesh();
					mesh.hideFlags = HideFlags.HideAndDontSave | HideFlags.DontUnloadUnusedAsset;
					mesh.MarkDynamic();
				}

				int reqSize = VertexCount * 6 / 4;
				if (indices == null || indices.Length != reqSize)
					indices = new int[reqSize];

				for (int i = 0, j = 0; i < VertexCount; i += 4, j += 6)
				{
					indices[j + 0] = i + 0; indices[j + 1] = i + 1; indices[j + 2] = i + 2;
					indices[j + 3] = i + 0; indices[j + 4] = i + 2; indices[j + 5] = i + 3;
				}
				
				var newVertices1	= new NativeArray<Vector3>(VertexCount, Allocator.Temp);
				var newVertices2	= new NativeArray<Vector3>(VertexCount, Allocator.Temp);
				var newOffsets		= new NativeArray<Vector4>(VertexCount, Allocator.Temp);
				var newColors		= new NativeArray<Color>(VertexCount, Allocator.Temp);

				for (int i = 0; i < VertexCount; i++)
				{
					newVertices1[i] = vertices1[i];
					newVertices2[i] = vertices2[i];
					newOffsets[i] = offsets[i];
					newColors[i] = colors[i];
				}
				
				mesh.SetVertices(newVertices1);
				mesh.SetUVs(0, newVertices2);
				mesh.SetUVs(1, newOffsets);
				mesh.SetColors(newColors);
				mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
				mesh.RecalculateBounds();
				mesh.UploadMeshData(false);
				
				newVertices1.Dispose();
				newVertices2.Dispose();
				newOffsets.Dispose();
				newColors.Dispose();
			}
			
			public void Draw()
			{
				if (VertexCount == 0 || mesh == null)
					return;
				Graphics.DrawMeshNow(mesh, MathConstants.identityMatrix);
			}

			internal void Destroy()
			{
				if (mesh) UnityEngine.Object.DestroyImmediate(mesh);
				mesh = null;
				indices = null;
				
				GenericPool<LineMesh>.Release(this);
			}
		}

		public void Begin()
		{
			if (lineMeshes == null || lineMeshes.Count == 0)
				return;
			currentLineMesh = 0;
			for (int i = 0; i < lineMeshes.Count; i++) lineMeshes[i].Clear();
		}

		public void End()
		{
			if (lineMeshes == null || lineMeshes.Count == 0)
				return;

			var max = Mathf.Min(currentLineMesh, lineMeshes.Count);
			for (int i = 0; i <= max; i++)
				lineMeshes[i].CommitMesh();
		}

		public void Render(Material genericLineMaterial)
		{
			if (lineMeshes == null || lineMeshes.Count == 0 || !genericLineMaterial)
				return;
			MaterialUtility.InitGenericLineMaterial(genericLineMaterial);
			if (genericLineMaterial.SetPass(0))
			{
				var max = Mathf.Min(currentLineMesh, lineMeshes.Count - 1);
				for (int i = 0; i <= max; i++)
					lineMeshes[i].Draw();
			}
		}

		private List<LineMesh> lineMeshes = new List<LineMesh>();
		private int currentLineMesh = 0;
		
		public LineMeshManager()
		{
			lineMeshes.Add(LineMesh.CreateInstance());
		}

		public void DrawLine(Vector3 A, Vector3 B, Color color, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMesh = lineMeshes[currentLineMesh];
			if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; }
			lineMesh.AddLine(A, B, thickness, dashSize, color);
		}

		//*
		public void DrawLines(Matrix4x4 matrix, Vector3[] vertices, int[] indices, Color color, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var corner1 = new Vector4(thickness, -1, dashSize);
			var corner2 = new Vector4(thickness, +1, dashSize);
			var corner3 = new Vector4(thickness, +1, dashSize);
			var corner4 = new Vector4(thickness, -1, dashSize);

			var lineMeshIndex = currentLineMesh;
			while (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance());
			if (lineMeshes[lineMeshIndex].VertexCount + (indices.Length * 2) <= LineMesh.MaxVertexCount)
			{
				var lineMesh	= lineMeshes[lineMeshIndex];
				var vertices1	= lineMesh.vertices1;
				var vertices2	= lineMesh.vertices2;
				var offsets		= lineMesh.offsets;
				var colors		= lineMesh.colors;
				
				var n = lineMesh.VertexCount;
				for (int i = 0; i < indices.Length; i += 2)
				{
					var A = matrix.MultiplyPoint(vertices[indices[i + 0]]);
					var B = matrix.MultiplyPoint(vertices[indices[i + 1]]);
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner1; colors[n] = color; n++;
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner2; colors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner3; colors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner4; colors[n] = color; n++;
				}
				lineMesh.VertexCount = n;
			} else
			{  
				for (int i = 0; i < indices.Length; i += 2)
				{
					var lineMesh	= lineMeshes[lineMeshIndex];
					var vertexCount = lineMesh.VertexCount;
					if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { lineMeshIndex++; if (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[lineMeshIndex]; vertexCount = lineMesh.VertexCount; }
					var vertices1	= lineMesh.vertices1;
					var vertices2	= lineMesh.vertices2;
					var offsets		= lineMesh.offsets;
					var colors		= lineMesh.colors;

					var A = matrix.MultiplyPoint(vertices[indices[i + 0]]);
					var B = matrix.MultiplyPoint(vertices[indices[i + 1]]);
					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner1; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner2; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner3; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner4; colors[vertexCount] = color; vertexCount++;
					
					lineMesh.VertexCount += 4;
				}
				currentLineMesh = lineMeshIndex;
			}
		}

		public void DrawLines(Vector3[] vertices, int[] indices, Color color, float thickness = 1.0f, float dashSize = 0.0f) //2
		{
			var corner1 = new Vector4(thickness, -1, dashSize);
			var corner2 = new Vector4(thickness, +1, dashSize);
			var corner3 = new Vector4(thickness, +1, dashSize);
			var corner4 = new Vector4(thickness, -1, dashSize);

			var lineMeshIndex = currentLineMesh;
			while (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance());
			if (lineMeshes[lineMeshIndex].VertexCount + (indices.Length * 2) <= LineMesh.MaxVertexCount)
			{
				var lineMesh	= lineMeshes[lineMeshIndex];
				var vertices1	= lineMesh.vertices1;
				var vertices2	= lineMesh.vertices2;
				var offsets		= lineMesh.offsets;
				var colors		= lineMesh.colors;
				
				int n = lineMesh.VertexCount;
				for (int i = 0; i < indices.Length; i += 2)
				{
					var index0 = indices[i + 0];
					var index1 = indices[i + 1];
					if (index0 < 0 || index0 >= vertices.Length || 
						index1 < 0 || index1 >= vertices.Length)
						continue;

					var A = vertices[index0];
					var B = vertices[index1];

					if (float.IsInfinity(A.x) || float.IsInfinity(A.y) || float.IsInfinity(A.z) ||
						float.IsInfinity(B.x) || float.IsInfinity(B.y) || float.IsInfinity(B.z) ||
						float.IsNaN(A.x) || float.IsNaN(A.y) || float.IsNaN(A.z) ||
						float.IsNaN(B.x) || float.IsNaN(B.y) || float.IsNaN(B.z))
						continue;
					
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner1; colors[n] = color; n++;
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner2; colors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner3; colors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner4; colors[n] = color; n++;
				}
				
				lineMesh.VertexCount = n;
			} else
			{  
				for (int i = 0; i < indices.Length; i += 2)
				{
					var index0 = indices[i + 0];
					var index1 = indices[i + 1];
					if (index0 < 0 || index0 >= vertices.Length || 
						index1 < 0 || index1 >= vertices.Length)
						continue;

					var A = vertices[index0];
					var B = vertices[index1];

					if (float.IsInfinity(A.x) || float.IsInfinity(A.y) || float.IsInfinity(A.z) ||
						float.IsInfinity(B.x) || float.IsInfinity(B.y) || float.IsInfinity(B.z) ||
						float.IsNaN(A.x) || float.IsNaN(A.y) || float.IsNaN(A.z) ||
						float.IsNaN(B.x) || float.IsNaN(B.y) || float.IsNaN(B.z))
						continue;

					var lineMesh	= lineMeshes[lineMeshIndex];
					int vertexCount	= lineMesh.VertexCount;
					if (vertexCount + 4 >= LineMesh.MaxVertexCount) {  lineMeshIndex++; if (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh	= lineMeshes[lineMeshIndex]; lineMesh.Clear(); vertexCount = lineMesh.VertexCount; }
					var vertices1	= lineMesh.vertices1;
					var vertices2	= lineMesh.vertices2;
					var offsets		= lineMesh.offsets;
					var colors		= lineMesh.colors;

					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner1; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner2; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner3; colors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner4; colors[vertexCount] = color; vertexCount++;					
					lineMesh.VertexCount = vertexCount;
				}
				currentLineMesh = lineMeshIndex;
			}
		}
		
		public void DrawLines(Vector3[] vertices, int[] indices, Color[] colors, float thickness = 1.0f, float dashSize = 0.0f) //1
		{
			var corner1 = new Vector4(thickness, -1, dashSize);
			var corner2 = new Vector4(thickness, +1, dashSize);
			var corner3 = new Vector4(thickness, +1, dashSize);
			var corner4 = new Vector4(thickness, -1, dashSize);

			var lineMeshIndex = currentLineMesh;
			while (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance());
			var prevVertexCount = lineMeshes[lineMeshIndex].VertexCount;
			if (prevVertexCount + (indices.Length * 2) <= LineMesh.MaxVertexCount)
			{
				var lineMesh	= lineMeshes[lineMeshIndex];
				var vertices1	= lineMesh.vertices1;
				var vertices2	= lineMesh.vertices2;
				var offsets		= lineMesh.offsets;
				var meshColors	= lineMesh.colors;
				
				int n = lineMesh.VertexCount;
				for (int i = 0, c = 0; i < indices.Length; i += 2, c++)
				{
					var index0 = indices[i + 0];
					var index1 = indices[i + 1];
					if (index0 < 0 || index0 >= vertices.Length || 
						index1 < 0 || index1 >= vertices.Length)
						continue;

					var A = vertices[index0];
					var B = vertices[index1];

					if (float.IsInfinity(A.x) || float.IsInfinity(A.y) || float.IsInfinity(A.z) ||
						float.IsInfinity(B.x) || float.IsInfinity(B.y) || float.IsInfinity(B.z) ||
						float.IsNaN(A.x) || float.IsNaN(A.y) || float.IsNaN(A.z) ||
						float.IsNaN(B.x) || float.IsNaN(B.y) || float.IsNaN(B.z))
						continue;

					var color = colors[c];
					
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner1; meshColors[n] = color; n++;
					vertices1[n] = B; vertices2[n] = A; offsets[n] = corner2; meshColors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner3; meshColors[n] = color; n++;
					vertices1[n] = A; vertices2[n] = B; offsets[n] = corner4; meshColors[n] = color; n++;		
				}			
				lineMesh.VertexCount = n;
			} else
			{  
				for (int i = 0, c = 0; i < indices.Length; i += 2, c++)
				{
					var index0 = indices[i + 0];
					var index1 = indices[i + 1];
					if (index0 < 0 || index0 >= vertices.Length || 
						index1 < 0 || index1 >= vertices.Length)
						continue;

					var A = vertices[index0];
					var B = vertices[index1];

					if (float.IsInfinity(A.x) || float.IsInfinity(A.y) || float.IsInfinity(A.z) ||
						float.IsInfinity(B.x) || float.IsInfinity(B.y) || float.IsInfinity(B.z) ||
						float.IsNaN(A.x) || float.IsNaN(A.y) || float.IsNaN(A.z) ||
						float.IsNaN(B.x) || float.IsNaN(B.y) || float.IsNaN(B.z))
						continue;

					var color = colors[c];

					var lineMesh	= lineMeshes[lineMeshIndex];
					int vertexCount	= lineMesh.VertexCount;
					if (vertexCount + 4 >= LineMesh.MaxVertexCount) {  lineMeshIndex++; if (lineMeshIndex >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh	= lineMeshes[lineMeshIndex]; lineMesh.Clear(); vertexCount = lineMesh.VertexCount; }
					var vertices1	= lineMesh.vertices1;
					var vertices2	= lineMesh.vertices2;
					var offsets		= lineMesh.offsets;
					var meshColors	= lineMesh.colors;
					
					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner1; meshColors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = B; vertices2[vertexCount] = A; offsets[vertexCount] = corner2; meshColors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner3; meshColors[vertexCount] = color; vertexCount++;
					vertices1[vertexCount] = A; vertices2[vertexCount] = B; offsets[vertexCount] = corner4; meshColors[vertexCount] = color; vertexCount++;					
					lineMesh.VertexCount = vertexCount;
				}
			}
		}

		public void DrawLines(Matrix4x4 matrix, Vector3[] vertices, Color color, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMeshIndex = currentLineMesh;
			var lineMesh = lineMeshes[currentLineMesh];
			for (int i = 0; i < vertices.Length; i += 2)
			{
				if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; lineMesh.Clear(); }
				lineMesh.AddLine(matrix.MultiplyPoint(vertices[i + 0]), matrix.MultiplyPoint(vertices[i + 1]), thickness, dashSize, color);
			}
			currentLineMesh = lineMeshIndex;
		}

		public void DrawLines(Vector3[] vertices, Color color, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMeshIndex = currentLineMesh;
			var lineMesh = lineMeshes[currentLineMesh];
			for (int i = 0; i < vertices.Length; i += 2)
			{
				if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; lineMesh.Clear(); }
				lineMesh.AddLine(vertices[i + 0], vertices[i + 1], thickness, dashSize, color);
			}
			currentLineMesh = lineMeshIndex;
		}

		public void DrawLines(Matrix4x4 matrix, Vector3[] vertices, int[] indices, Color[] colors, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMeshIndex = currentLineMesh;
			var lineMesh = lineMeshes[currentLineMesh];
			for (int i = 0, c = 0; i < indices.Length; i += 2, c++)
			{
				if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; lineMesh.Clear(); }
				var index0 = indices[i + 0];
				var index1 = indices[i + 1];
				if (index0 < 0 || index1 < 0 || index0 >= vertices.Length || index1 >= vertices.Length)
					continue;
				lineMesh.AddLine(matrix.MultiplyPoint(vertices[index0]), matrix.MultiplyPoint(vertices[index1]), thickness, dashSize, colors[c]);
			}
			currentLineMesh = lineMeshIndex;
		}
		
		public void DrawLines(Matrix4x4 matrix, Vector3[] vertices, Color[] colors, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMeshIndex = currentLineMesh;
			var lineMesh = lineMeshes[currentLineMesh];
			for (int i = 0, c = 0; i < vertices.Length; i += 2, c++)
			{
				if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; lineMesh.Clear(); }
				lineMesh.AddLine(matrix.MultiplyPoint(vertices[i + 0]), matrix.MultiplyPoint(vertices[i + 1]), thickness, dashSize, colors[c]);
			}
			currentLineMesh = lineMeshIndex;
		}

		public void DrawLines(Vector3[] vertices, Color[] colors, float thickness = 1.0f, float dashSize = 0.0f)
		{
			var lineMeshIndex = currentLineMesh;
			var lineMesh = lineMeshes[currentLineMesh];
			for (int i = 0, c = 0; i < vertices.Length; i += 2, c++)
			{
				if (lineMesh.VertexCount + 4 >= LineMesh.MaxVertexCount) { currentLineMesh++; if (currentLineMesh >= lineMeshes.Count) lineMeshes.Add(LineMesh.CreateInstance()); lineMesh = lineMeshes[currentLineMesh]; lineMesh.Clear(); }
				lineMesh.AddLine(vertices[i + 0], vertices[i + 1], thickness, dashSize, colors[c]);
			}
			currentLineMesh = lineMeshIndex;
		}

		internal void Destroy()
		{
			for (int i = 0; i < lineMeshes.Count; i++)
			{
				lineMeshes[i].Destroy();
			}
			lineMeshes.Clear();
			currentLineMesh = 0;
		}
		
		internal void Clear()
		{
			currentLineMesh = 0;
			for (int i = 0; i < lineMeshes.Count; i++) lineMeshes[i].Clear();
		}
	}
}
