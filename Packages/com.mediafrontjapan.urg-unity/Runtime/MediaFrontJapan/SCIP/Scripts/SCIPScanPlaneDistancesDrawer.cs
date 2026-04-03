using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace MediaFrontJapan.SCIP
{
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    abstract class SCIPScanPlaneDistancesDrawer : Graphic
    {
        [SerializeField] protected SCIPScanPlane scanPlane = default;
        [NonSerialized] NativeArray<int> distances;

        protected override void OnEnable()
        {
            base.OnEnable();
            SetVerticesDirty();
        }

        protected void SetDistances(NativeArray<int> distances)
        {
            this.distances = distances;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var transformation = scanPlane.Client.Transformation;
            if (transformation == default || !distances.IsCreated)
            {
                return;
            }
            using (var positions = transformation.TransformDistancesToPositions(distances))
            {
                vh.AddVert(Vector3.zero, color, default);
                for (int i = 0; i < positions.Length; i++)
                {
                    var pos = positions[i];
                    vh.AddVert(new float3(pos, 0), color, default);
                }
                for (int i = 0; i < positions.Length - 1; i++)
                {
                    vh.AddTriangle(0, i + 2, i + 1);
                }
            }
        }
    }
}
