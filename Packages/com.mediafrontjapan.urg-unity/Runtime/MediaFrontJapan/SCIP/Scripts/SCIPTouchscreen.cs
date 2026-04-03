#nullable enable
using System.Threading;
using System;
using Unity.Burst;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MediaFrontJapan.SCIP
{
    [InputControlLayout(displayName = "SCIPTouchscreen", stateType = typeof(TouchState))]
    public class SCIPTouchscreen : Touchscreen, IInputUpdateCallbackReceiver
    {
        public SCIPScanPlane[] scanPlanes = default!;
        NativeList<SCIPPointerData> pointers;
        SCIPTouchscreenEvent TouchscreenEvent;
        public void OnUpdate()
        {
            CollectScreenPositions(out var screenPositions, out var collectScreenPositionsJob);
            if (!pointers.IsCreated)
            {
                pointers = new NativeList<SCIPPointerData>(Allocator.Persistent);
            }
            var computePointerChangesJob = ComputeTouchscreenEvent(pointers.AsArray(), screenPositions, out TouchscreenEvent, collectScreenPositionsJob);
            screenPositions.Dispose(computePointerChangesJob);
            computePointerChangesJob.Complete();
            pointers.Clear();

            QueueStateEvent(TouchscreenEvent);

        }
        private unsafe void CollectScreenPositions(out NativeList<float2> screenPositions, out JobHandle collectScreenPositionsJob)
        {
            if (UST10LXScanPlaneConfig.ConfigingCount > 0)
            {
                screenPositions = new NativeList<float2>(Allocator.TempJob);
                collectScreenPositionsJob = default;
            }
            else
            {
                var pointCount = 0;
                foreach (var item in scanPlanes)
                {
                    pointCount += item.ObjectLocalPositions.Length;
                }
                screenPositions = new NativeList<float2>(pointCount, Allocator.TempJob);
                screenPositions.ResizeUninitialized(pointCount);
                var pointIndex = 0;
                var jobs = stackalloc JobHandle[scanPlanes.Length];
                for (int i = 0; i < scanPlanes.Length; i++)
                {
                    var scanPlane = scanPlanes[i];
                    var localPositions = scanPlane.ObjectLocalPositions;
                    jobs[i] = scanPlane.LocalToScreenPoint(localPositions, screenPositions.AsArray().GetSubArray(pointIndex, localPositions.Length));
                    pointIndex += localPositions.Length;
                }
                collectScreenPositionsJob = JobHandleUnsafeUtility.CombineDependencies(jobs, scanPlanes.Length);
            }

        }
        /// <param name="pointers"></param>
        /// <param name="newPointerPositions">This will be cleared after operation completed.</param>
        /// <param name="touchscreenEvent"></param>
        /// <param name="inputDeps"></param>
        /// <returns></returns>
        static JobHandle ComputeTouchscreenEvent(NativeArray<SCIPPointerData> pointers, NativeList<float2> newPointerPositions, out SCIPTouchscreenEvent touchscreenEvent, JobHandle inputDeps = default)
        {
            var newPointers = new NativeList<SCIPPointerData>(Allocator.TempJob);
            var alivedPointers = new NativeList<SCIPPointerData>(Allocator.TempJob);
            var deletedPointers = new NativeParallelHashMap<int, SCIPPointerData>(pointers.Length, Allocator.TempJob);
            touchscreenEvent = new SCIPTouchscreenEvent()
            {
                NewPointers = newPointers,
                AlivedPointers = alivedPointers,
                DeletedPointers = deletedPointers,
            };
            return new ComputeTouchscreenEventJob()
            {
                CurrentPointers = pointers,
                NewPositions = newPointerPositions,
                NewPointers = newPointers,
                AlivedPointers = alivedPointers,
                DeletedPointers = deletedPointers,
            }.Schedule(inputDeps);
        }
        internal void QueueStateEvent(SCIPTouchscreenEvent state)
        {
            var newPointers = state.NewPointers;
            for (int i = 0; i < newPointers.Length; i++)
            {
                var p = newPointers[i];

                var touchState = new TouchState()
                {
                    touchId = p.Id,
                    phase = TouchPhase.Began,
                    position = p.Position,
                };
                pointers.Add(p);
                InputSystem.QueueStateEvent(this, touchState);
            }
            newPointers.Dispose();

            var alivedPointers = state.AlivedPointers;
            for (int i = 0; i < alivedPointers.Length; i++)
            {
                var p = alivedPointers[i];
                var touchState = new TouchState()
                {
                    touchId = p.Id,
                    phase = TouchPhase.Moved,
                    position = p.Position,
                };
                pointers.Add(p);
                InputSystem.QueueStateEvent(this, touchState);
            }
            alivedPointers.Dispose();

            var deletedPointers = state.DeletedPointers;
            foreach (var v in deletedPointers)
            {
                var p = v.Value;
                var touchState = new TouchState()
                {
                    touchId = p.Id,
                    phase = TouchPhase.Ended,
                    position = p.Position,
                };
                InputSystem.QueueStateEvent(this, touchState);
            }
            deletedPointers.Dispose();
        }
        public new static SCIPTouchscreen current { get; private set; } = null!;
        public static IReadOnlyList<SCIPTouchscreen> All => s_AllMyDevices;
        private static List<SCIPTouchscreen> s_AllMyDevices = new List<SCIPTouchscreen>();
        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void OnAdded()
        {
            base.OnAdded();
            s_AllMyDevices.Add(this);
        }

        protected override void OnRemoved()
        {
            base.OnRemoved();
            s_AllMyDevices.Remove(this);
        }
    }

    public static class SpaceConversion
    {
        public static JobHandle LocalToScreenPoints(this RectTransform transform, NativeArray<float2> localPositions, NativeArray<float2> screenPositions, JobHandle dependsOn = default)
        {
            var rootCanvas = transform.RootCanvas();
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var worldCamera = rootCanvas.worldCamera;
                var localToProjection = math.mul(worldCamera.projectionMatrix * worldCamera.worldToCameraMatrix, transform.localToWorldMatrix);
                var localToProjectionXYW = new float3x3(localToProjection.c0.xyw, localToProjection.c1.xyw, localToProjection.c3.xyw);
                return new LocalToScreenSpaceCameraJob()
                {
                    LocalPositions = localPositions,
                    ScreenPositions = screenPositions,
                    LocalToProjection = localToProjectionXYW,
                    HalfScreenSize = new float2(Screen.width, Screen.height) * 0.5f,
                }.Schedule(localPositions.Length, 256, dependsOn);
            }
            else
            {
                var localToWorld = (float4x4)transform.localToWorldMatrix;
                var localToWorldXYW = new float3x3(localToWorld.c0.xyw, localToWorld.c1.xyw, localToWorld.c3.xyw);
                return new LocalToScreenSpaceOverlayJob()
                {
                    LocalPositions = localPositions,
                    ScreenPositions = screenPositions,
                    LocalToWorld = localToWorldXYW,
                }.Schedule(localPositions.Length, 256, dependsOn);
            }
        }
        public static float2 ScreenToLocalPoint(this RectTransform transform, float2 screenPosition)
        {
            var rootCanvas = transform.RootCanvas();
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                throw new NotSupportedException($"Currently, only {RenderMode.ScreenSpaceOverlay} is supported.");
            }
            else
            {
                return math.mul(transform.worldToLocalMatrix, new float4(screenPosition, 0, 1)).xy;
            }
        }
        static Canvas RootCanvas(this RectTransform rectTransform) => rectTransform.GetComponentInParent<Canvas>().rootCanvas;
    }
}
