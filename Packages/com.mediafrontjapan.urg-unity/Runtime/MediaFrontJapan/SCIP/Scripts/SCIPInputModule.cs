using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MediaFrontJapan.SCIP
{
    sealed class SCIPInputModule : StandaloneInputModule
    {
        [SerializeField] SCIPScanPlane[] scanPlanes = default;
        NativeList<SCIPPointerData> pointers;
        public override void Process()
        {
            base.Process();

            CollectScreenPositions(out var screenPositions, out var collectScreenPositionsJob);
            if (!pointers.IsCreated)
            {
                pointers = new NativeList<SCIPPointerData>(Allocator.Persistent);
            }
            var newPointers = new NativeList<SCIPPointerData>(Allocator.TempJob);
            var alivedPointers = new NativeList<SCIPPointerData>(Allocator.TempJob);
            var deletedPointers = new NativeParallelHashMap<int, SCIPPointerData>(pointers.Length, Allocator.TempJob);


            var computePointerChangesJob = new ComputeTouchscreenEventJob()
            {
                CurrentPointers = pointers.AsArray(),
                NewPositions = screenPositions,
                NewPointers = newPointers,
                AlivedPointers = alivedPointers,
                DeletedPointers = deletedPointers,
            }.Schedule(collectScreenPositionsJob);

            screenPositions.Dispose(computePointerChangesJob);

            computePointerChangesJob.Complete();

            pointers.Clear();

            foreach (var p in newPointers)
            {
                var pointerEventData = GetPointerData(p);
                ProcessTouchPress(pointerEventData, true, false);
                ProcessMove(pointerEventData);
                ProcessDrag(pointerEventData);
                pointers.Add(p);
            }
            newPointers.Dispose();
            foreach (var p in alivedPointers)
            {
                var pointerEventData = GetPointerData(p);
                ProcessTouchPress(pointerEventData, false, false);
                ProcessMove(pointerEventData);
                ProcessDrag(pointerEventData);
                pointers.Add(p);
            }
            alivedPointers.Dispose();
            foreach (var v in deletedPointers)
            {
                var p = v.Value;
                var pointerEventData = GetPointerData(p);
                ProcessTouchPress(pointerEventData, false, true);
                RemovePointerData(pointerEventData);
            }
            deletedPointers.Dispose();


        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (pointers.IsCreated)
            {
                pointers.Dispose();
            }
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

        bool GetPointerData(int id, out SCIPPointerEventData pointerEventData)
        {
            if (!m_PointerData.TryGetValue(id, out var ped))
            {
                pointerEventData = new SCIPPointerEventData(eventSystem);
                pointerEventData.pointerId = id;
                m_PointerData.Add(id, pointerEventData);
                return true;
            }
            pointerEventData = (SCIPPointerEventData)ped;
            return false;

        }

        SCIPPointerEventData GetPointerData(SCIPPointerData input)
        {
            var created = GetPointerData(input.Id, out var pointerData);
            pointerData.Reset();

            var pressed = created;

            if (created)
            {
                pointerData.position = input.Position;
            }

            if (pressed)
            {
                pointerData.delta = Vector2.zero;
            }
            else
            {
                pointerData.delta = (Vector2)input.Position - pointerData.position;
            }

            pointerData.position = input.Position;

            pointerData.button = PointerEventData.InputButton.Left;

            //if (input.phase == TouchPhase.Canceled)
            //{
            //    pointerData.pointerCurrentRaycast = new RaycastResult();
            //}
            //else
            {
                eventSystem.RaycastAll(pointerData, m_RaycastResultCache);

                var raycast = FindFirstRaycast(m_RaycastResultCache);
                pointerData.pointerCurrentRaycast = raycast;
                m_RaycastResultCache.Clear();
            }

            return pointerData;
        }


    }

    public sealed class SCIPPointerEventData : PointerEventData
    {
        public SCIPPointerEventData(EventSystem eventSystem) : base(eventSystem)
        {
        }
    }
    struct SCIPPointerData
    {
        static readonly SharedStatic<int> SharedIdContainer = SharedStatic<int>.GetOrCreate<ComputeTouchscreenEventJob>();
        public readonly int Id;
        public float2 Position, DeltaPosition;


        public SCIPPointerData(float2 position) : this(Interlocked.Increment(ref SharedIdContainer.Data), position) { }

        public SCIPPointerData(int id, float2 position) : this()
        {
            Id = id;
            Position = position;
        }
    }
    [BurstCompile]
    struct ComputeTouchscreenEventJob : IJob
    {
        [ReadOnly] public NativeArray<SCIPPointerData> CurrentPointers;
        public NativeList<float2> NewPositions;
        public NativeList<SCIPPointerData> NewPointers, AlivedPointers;
        public NativeParallelHashMap<int, SCIPPointerData> DeletedPointers;


        public void Execute()
        {
            var alivedPointersCount = math.min(NewPositions.Length, CurrentPointers.Length);
            if (AlivedPointers.Capacity < alivedPointersCount)
            {
                AlivedPointers.Capacity = alivedPointersCount;
            }

            if (CurrentPointers.Length > NewPositions.Length)//No new pointers
            {
                for (int i = 0; i < CurrentPointers.Length; i++)
                {
                    var pointer = CurrentPointers[i];
                    DeletedPointers.Add(pointer.Id, pointer);
                }



                for (int i = 0; i < NewPositions.Length; i++)
                {
                    var newPosition = NewPositions[i];
                    var nearestDistanceSq = float.MaxValue;
                    var matchedPointer = default(SCIPPointerData);
                    for (int j = 0; j < CurrentPointers.Length; j++)
                    {
                        var obj = CurrentPointers[j];
                        if (DeletedPointers.ContainsKey(obj.Id))
                        {
                            var distanceSq = math.distancesq(newPosition, obj.Position);
                            if (distanceSq < nearestDistanceSq)
                            {
                                matchedPointer = obj;
                                nearestDistanceSq = distanceSq;
                            }
                        }
                    }
                    DeletedPointers.Remove(matchedPointer.Id);
                    matchedPointer.DeltaPosition = newPosition - matchedPointer.Position;
                    matchedPointer.Position = newPosition;

                    AlivedPointers.AddNoResize(matchedPointer);

                }
            }
            else//No deleted pointers
            {
                for (int i = 0; i < CurrentPointers.Length; i++)
                {
                    var pointer = CurrentPointers[i];
                    var nearestDistanceSq = float.PositiveInfinity;
                    var nearestPositionIndex = 0;
                    for (int j = 0; j < NewPositions.Length; j++)
                    {
                        var newPosition = NewPositions[j];
                        var distanceSq = math.distancesq(newPosition, pointer.Position);
                        if (distanceSq < nearestDistanceSq)
                        {
                            nearestDistanceSq = distanceSq;
                            nearestPositionIndex = j;
                        }
                    }
                    var nearestPosition = NewPositions[nearestPositionIndex];
                    NewPositions.RemoveAtSwapBack(nearestPositionIndex);
                    pointer.DeltaPosition = nearestPosition - pointer.Position;
                    pointer.Position = nearestPosition;
                    AlivedPointers.AddNoResize(pointer);
                }
                if (NewPointers.Capacity < NewPositions.Length)
                {
                    NewPointers.Capacity = NewPositions.Length;
                }
                for (int i = 0; i < NewPositions.Length; i++)
                {
                    var newPosition = NewPositions[i];

                    NewPointers.AddNoResize(new SCIPPointerData(newPosition));
                }
            }
        }


    }


}
