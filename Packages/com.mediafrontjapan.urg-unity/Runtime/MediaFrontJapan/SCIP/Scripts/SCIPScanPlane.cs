using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MediaFrontJapan.SCIP
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SCIPScanPlane : MonoBehaviour
    {
        const int DefaultClampDistanceMilliMeters = 10000;

        [SerializeField] SCIPClient client = default;
        [SerializeField] string playerPrefsKey = "";
        RectTransform rectTransform;
        [NonSerialized] internal UST10LXScanPlaneSettings Settings;
        [SerializeField] internal float objectSize = 0.05f;
        internal NativeArray<int> ClampDistances { get; private set; }
        internal SCIPClient Client => client;
        internal event Action<NativeArray<int>> ClampDistancesChanged;
        internal event Action<NativeArray<float2>> ObjectLocalPositionsChanged;

        public Vector2 Offset
        {
            get => Settings.Offset;
            set
            {
                Settings.Offset = value;
                rectTransform.anchoredPosition = Offset;
            }
        }

        public float Scale
        {
            get => Settings.Scale;
            set
            {
                Settings.Scale = value;
                rectTransform.localScale = new Vector3(value, value, 1);
            }
        }
        public float Angle
        {
            get => Settings.Angle;
            set
            {
                Settings.Angle = value;
                var euler = rectTransform.localEulerAngles;
                euler.z = value;
                rectTransform.localEulerAngles = euler;
            }
        }

        public string Address
        {
            get => Settings.Address;
            set
            {
                Settings.Address = value;
            }
        }

        NativeList<float2> objectLocalPositions;
        public NativeArray<float2> ObjectLocalPositions => objectLocalPositions.IsCreated ? objectLocalPositions.AsArray() : default;

        public JobHandle LocalToScreenPoint(NativeArray<float2> localPositions, NativeArray<float2> screenPositions, JobHandle dependsOn = default)
        {
            if (RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                var worldCamera = RootCanvas.worldCamera;
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

            var localToWorld = (float4x4)transform.localToWorldMatrix;
            var localToWorldXYW = new float3x3(localToWorld.c0.xyw, localToWorld.c1.xyw, localToWorld.c3.xyw);
            return new LocalToScreenSpaceOverlayJob()
            {
                LocalPositions = localPositions,
                ScreenPositions = screenPositions,
                LocalToWorld = localToWorldXYW,
            }.Schedule(localPositions.Length, 256, dependsOn);
        }

        public float2 ScreenToLocalPoint(float2 screenPosition)
        {
            if (RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                throw new NotSupportedException($"Currently, only {RenderMode.ScreenSpaceOverlay} is supported.");
            }

            return math.mul(rectTransform.worldToLocalMatrix, new float4(screenPosition, 0, 1)).xy;
        }

        internal Canvas RootCanvas => GetComponentInParent<Canvas>().rootCanvas;

        void Reset()
        {
            playerPrefsKey = name;
        }

        void Start()
        {
            rectTransform = (RectTransform)transform;
            LoadOrGetDefaultSettings();
            client.CaptureChanged += Reflesh;
            if (client.Capture.IsValid)
            {
                Reflesh(client.Capture);
            }
        }

        void OnDestroy()
        {
            if (client != null)
            {
                client.CaptureChanged -= Reflesh;
            }

            if (ClampDistances.IsCreated)
            {
                ClampDistances.Dispose();
            }

            if (objectLocalPositions.IsCreated)
            {
                objectLocalPositions.Dispose();
            }
        }

        public void LoadOrGetDefaultSettings()
        {
            if (PlayerPrefs.HasKey(playerPrefsKey))
            {
                Settings = JsonUtility.FromJson<UST10LXScanPlaneSettings>(PlayerPrefs.GetString(playerPrefsKey));
            }
            else
            {
                Settings = CreateDefaultSettings();
            }

            NormalizeSettings();

            if (ClampDistances.IsCreated)
            {
                ClampDistances.Dispose();
            }

            ClampDistances = new NativeArray<int>(Settings.ClampDistances, Allocator.Persistent);
            Offset = Settings.Offset;
            Angle = Settings.Angle;
            Scale = Settings.Scale;
            Address = Settings.Address;

            NotifyClampDistancesChanged();
        }

        UST10LXScanPlaneSettings CreateDefaultSettings()
        {
            return new UST10LXScanPlaneSettings(
                CreateDefaultClampDistances(),
                rectTransform.anchoredPosition,
                rectTransform.localScale.x,
                rectTransform.localEulerAngles.z,
                client.ConfiguredHostName);
        }

        void NormalizeSettings()
        {
            Settings ??= CreateDefaultSettings();

            if (string.IsNullOrWhiteSpace(Settings.Address))
            {
                Settings.Address = client.ConfiguredHostName;
            }

            Settings.ClampDistances = NormalizeDistances(Settings.ClampDistances, DefaultClampDistanceMilliMeters);
        }

        public void SaveSettings()
        {
            ClampDistances.CopyTo(Settings.ClampDistances);
            PlayerPrefs.SetString(playerPrefsKey, JsonUtility.ToJson(Settings));
            PlayerPrefs.Save();
        }

        internal void NotifyClampDistancesChanged()
        {
            ClampDistancesChanged?.Invoke(ClampDistances);
        }

        void Reflesh(SCIPCapture capture)
        {
            if (!capture.IsValid | !enabled)
            {
                return;
            }
            if (objectLocalPositions.IsCreated)
            {
                objectLocalPositions.Clear();
            }
            else
            {
                objectLocalPositions = new NativeList<float2>(Allocator.Persistent);
            }

            new GetObjectPositionsJob()
            {
                Distances = capture.Distances,
                DistancesToPositions = capture.Transformation.DistancesToPositions,
                ClampDistances = ClampDistances,
                ObjectPositions = objectLocalPositions,
                MinDistance = capture.Transformation.MinDistance,
                MaxDistance = capture.Transformation.MaxDistance,
                OneStepRadian = capture.Transformation.OneStepRadian,
                ObjectSize = objectSize,

            }.Run();

            ObjectLocalPositionsChanged?.Invoke(ObjectLocalPositions);
        }

        static int[] CreateDefaultClampDistances()
        {
            var clampDistances = new int[UST10LXConstants.Steps];
            Array.Fill(clampDistances, DefaultClampDistanceMilliMeters);
            return clampDistances;
        }

        static int[] NormalizeDistances(int[] source, int defaultValue)
        {
            var normalized = new int[UST10LXConstants.Steps];
            if (defaultValue != 0)
            {
                Array.Fill(normalized, defaultValue);
            }

            if (source != null)
            {
                Array.Copy(source, normalized, Math.Min(source.Length, normalized.Length));
            }

            return normalized;
        }
    }
    [BurstCompile]
    struct GetObjectPositionsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int> Distances;
        [ReadOnly, NoAlias] public NativeArray<float2> DistancesToPositions;
        [ReadOnly, NoAlias] public NativeArray<int> ClampDistances;
        [WriteOnly, NoAlias] public NativeList<float2> ObjectPositions;
        [ReadOnly] public int MinDistance, MaxDistance;
        [ReadOnly] public float OneStepRadian;
        [ReadOnly] public float ObjectSize;
        public void Execute()
        {
            var sizeFactor = ObjectSize * ObjectSize * 0.8f;
            for (int index = 0; index < Distances.Length; index++)
            {
                var distance = Distances[index];
                if (MinDistance <= distance && distance <= MaxDistance
                    && distance <= ClampDistances[index])
                {
                    var sizeInStep = (int)(ObjectSize / (distance * 0.001f * OneStepRadian));

                    var from = math.max(0, index - sizeInStep);
                    var to = math.min(Distances.Length, index + sizeInStep);
                    var nearestPos = GetPosition(index);
                    var pos = new float2();
                    var count = 0;
                    for (int i = from; i < to; i++)
                    {
                        var comparand = Distances[i];

                        if (comparand < distance | (comparand == distance & (i < index)))
                        {
                            goto NextStep;
                        }

                        var comparandPos = GetPosition(i);
                        if (math.distancesq(nearestPos, comparandPos) < sizeFactor)
                        {
                            pos += GetPosition(i);
                            count += 1;
                        }
                    }

                    var localPosition = math.select(pos / count, nearestPos, count == 0);
                    ObjectPositions.Add(localPosition);
                }
            NextStep:;
            }
        }
        float2 GetPosition(int index)
        {
            return DistancesToPositionsJob.DistanceToPosition(Distances[index], index, DistancesToPositions, MinDistance, MaxDistance);
        }
    }
    [BurstCompile]
    struct LocalToScreenSpaceCameraJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float2> LocalPositions;
        [WriteOnly, NoAlias] public NativeArray<float2> ScreenPositions;
        [ReadOnly] public float3x3 LocalToProjection;
        [ReadOnly] public float2 HalfScreenSize;

        public void Execute(int index)
        {
            var localPos = LocalPositions[index];
            var projPos = math.mul(LocalToProjection, new float3(localPos, 1));
            var screenPos = ((projPos.xy / projPos.z) + 1) * HalfScreenSize;
            ScreenPositions[index] = screenPos;
        }
    }
    [BurstCompile]
    struct LocalToScreenSpaceOverlayJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float2> LocalPositions;
        [WriteOnly, NoAlias] public NativeArray<float2> ScreenPositions;
        [ReadOnly] public float3x3 LocalToWorld;
        public void Execute(int index)
        {
            var localPos = LocalPositions[index];
            var screenPos = math.mul(LocalToWorld, new float3(localPos, 1)).xy;
            ScreenPositions[index] = screenPos;
        }
    }

    static class UST10LXConstants
    {
        public const int PortNumber = 10940;
        public const int MaxStep = 1080;
        public const int MinStep = 0;
        public const int Steps = MaxStep - MinStep + 1;
        public const int FrontStep = 540;
        public const int AngleResolution = 1440;
        public const int EncodedScanDistanceSize = 3;
        public const int MaxScanContentSize = EncodedScanDistanceSize * Steps;
    }

    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    sealed class UST10LXScanPlaneSettings
    {
        public int[] ClampDistances;
        public Vector2 Offset;
        public float Scale;
        public float Angle;
        public string Address;
        public UST10LXScanPlaneSettings(int[] clampDistances, Vector2 offset, float scale, float angle, string address)
        {
            ClampDistances = clampDistances;
            Offset = offset;
            Scale = scale;
            Angle = angle;
            Address = address;
        }
    }
}
