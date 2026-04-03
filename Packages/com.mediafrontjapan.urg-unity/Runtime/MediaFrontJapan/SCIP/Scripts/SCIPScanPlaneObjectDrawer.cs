using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace MediaFrontJapan.SCIP
{
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    sealed class SCIPScanPlaneObjectDrawer : Graphic
    {
        [SerializeField] SCIPScanPlane scanPlane = default;
        [NonSerialized] NativeArray<float2> objectLocalPositions;

        protected override void OnEnable()
        {
            base.OnEnable();
            scanPlane.ObjectLocalPositionsChanged += SetPositions;
            if (scanPlane.ObjectLocalPositions.IsCreated)
            {
                SetPositions(scanPlane.ObjectLocalPositions);
            }
        }

        protected override void OnDisable()
        {
            if (scanPlane)
            {
                scanPlane.ObjectLocalPositionsChanged -= SetPositions;
            }

            base.OnDisable();
        }

        void SetPositions(NativeArray<float2> positions)
        {
            objectLocalPositions = positions;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!objectLocalPositions.IsCreated)
            {
                return;
            }
            for (int i = 0; i < objectLocalPositions.Length; i++)
            {
                var pos = objectLocalPositions[i];
                float size = scanPlane.objectSize;
                vh.AddVert(new float3(pos - size, 0), color, default);
                vh.AddVert(new float3(pos.x, pos.y + size, 0), color, default);
                vh.AddVert(new float3(pos.x + size, pos.y - size, 0), color, default);
                var i3 = i * 3;
                vh.AddTriangle(i3, i3 + 1, i3 + 2);
            }
        }
    }
}
