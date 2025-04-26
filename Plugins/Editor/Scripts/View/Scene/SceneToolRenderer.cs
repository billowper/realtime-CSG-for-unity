using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RealtimeCSG
{
	internal static class SceneToolRenderer
	{
		[InitializeOnLoadMethod]
		private static void Init()
		{
			SceneVisibilityManager.visibilityChanged -= OnSceneVisChange;
			SceneVisibilityManager.visibilityChanged += OnSceneVisChange;
		}

		private static int _meshGeneration = -1;
		private static readonly LineMeshManager _lineMeshManager = new();
		private static bool _forceOutlineUpdate;
		
		private static void OnSceneVisChange()
		{
			SetOutlineDirty();
		}

		internal static void Cleanup()
		{
			_meshGeneration = -1;
			_lineMeshManager.Destroy();
		}

		internal static void SetOutlineDirty() { _forceOutlineUpdate = true; }
		
		internal static void OnPaint(SceneView sceneView)
        {
            if (!sceneView)
            {
	            return;
            }

            var camera = sceneView.camera;
            
			SceneDragToolManager.OnPaint(camera);

			if (Event.current.type != EventType.Repaint)
			{
				return;
			}

			if (CSGSettings.GridVisible)
			{
				CSGGrid.RenderGrid(camera);
			}

			if (CSGSettings.IsWireframeShown(sceneView))
			{
				if (_forceOutlineUpdate || _meshGeneration != InternalCSGModelManager.MeshGeneration)
				{
					_forceOutlineUpdate = false;
					_meshGeneration = InternalCSGModelManager.MeshGeneration;
					_lineMeshManager.Begin();
					for (int i = 0; i < InternalCSGModelManager.Brushes.Count; i++)
					{
						var brush = InternalCSGModelManager.Brushes[i];
						if (!brush)
						{
							continue;
						}

						if (SceneVisibilityManager.instance.IsHidden(brush.gameObject))
						{
							continue;
						}

						if (!brush.outlineColor.HasValue)
						{
							brush.outlineColor = ColorSettings.GetBrushOutlineColor(brush);
						}

						var brush_transformation = brush.compareTransformation.localToWorldMatrix;
						CSGRenderer.DrawSimpleOutlines(_lineMeshManager, brush.brushNodeID, brush_transformation, brush.outlineColor.Value);
					}
					_lineMeshManager.End();
				}

				MaterialUtility.LineDashMultiplier = 1.0f;
				MaterialUtility.LineThicknessMultiplier = 1.0f;
				MaterialUtility.LineAlphaMultiplier = 1.0f;
				_lineMeshManager.Render(MaterialUtility.NoZTestGenericLine);
			}
		}
	}
}
