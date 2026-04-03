#pragma warning disable CS1702
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MediaFrontJapan.SCIP
{
    sealed class SCIPClient : MonoBehaviour
    {
        [SerializeField] string hostName = "";
        [SerializeField] int portNumber = 10940;
        [SerializeField] int timeoutMilliSeconds = 500;
        [NonSerialized] internal UST10LXScanPlaneSettings Settings;
        [SerializeField] string playerPrefsKey = "";

        CancellationTokenSource onEnableCancellationTokenSource;
        int enableVersion;

        internal SCIPScanTransformation Transformation { get; private set; }
        internal SCIPCapture Capture { get; private set; }
        internal string ConfiguredHostName => hostName;

        internal event Action<SCIPScanTransformation> TransformationChanged;
        internal event Action<SCIPCapture> CaptureChanged;

        void OnEnable()
        {
            if (PlayerPrefs.HasKey(playerPrefsKey))
            {
                Settings = JsonUtility.FromJson<UST10LXScanPlaneSettings>(PlayerPrefs.GetString(playerPrefsKey));
                hostName = Settings.Address;
            }

            onEnableCancellationTokenSource = new CancellationTokenSource();
            OnEnableAsync(++enableVersion, onEnableCancellationTokenSource.Token);
        }

        void OnDisable()
        {
            onEnableCancellationTokenSource?.Cancel();
            onEnableCancellationTokenSource?.Dispose();
            onEnableCancellationTokenSource = null;
        }

        async void OnEnableAsync(int runVersion, CancellationToken cancellationToken)
        {
            Device device = null;
            SCIPScanTransformation transformation = null;
            NativeArray<int> distances = default;

            try
            {
                device = new Device(new EthernetConnectionProvider(hostName, portNumber, timeoutMilliSeconds));
                try
                {
                    await device.ConnectAndRunAsync(cancellationToken);
                }
                catch (Exception e)
                {
                    throw new IOException("Failed to connect to SCIP device. Make sure that you have connected device, configured network adapter settings, and assigned its host name.", e);
                }

                var ppResponse = await device.PP();
                var pp = PPResponse.FromResponse(ppResponse).Value;
                var steps = pp.AMAX - pp.AMIN + 1;
                if (steps != UST10LXConstants.Steps)
                {
                    throw new NotSupportedException($"Only HOKUYO UST-10LX is supported. Expected {UST10LXConstants.Steps} scan steps, but the connected device reported {steps}.");
                }

                var channel = device.MD(new MultiScanParam() { Start = pp.AMIN, End = pp.AMAX, Grouping = 1, Scans = 0, Skips = 0 });
                using (var response = await channel.ReadAsync(cancellationToken))
                {
                    response.Status.ThrowExceptionForStatus();
                }

                transformation = new SCIPScanTransformation(pp.DMIN, pp.DMAX, pp.ARES, pp.AMIN, pp.AMAX, pp.AFRT);
                distances = new NativeArray<int>(pp.AMAX - pp.AMIN + 1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                if (runVersion == enableVersion)
                {
                    Transformation = transformation;
                    TransformationChanged?.Invoke(transformation);
                }

                var capture = new SCIPCapture(transformation, distances);
                while (await channel.WaitToReadAsync(cancellationToken))
                {
                    if (!channel.TryRead(out var response))
                    {
                        continue;
                    }

                    while (channel.TryRead(out var latestResponse))
                    {
                        response.Dispose();
                        response = latestResponse;
                    }

                    using (response)
                    {
                        var (_, encodedDataBlocks) = ScanResponseParser.Parse(response);
                        using (var content = GetContentJob.GetContent(encodedDataBlocks))
                        {
                            GetDistancesJob.GetDistances(content, distances);
                        }
                    }

                    if (runVersion != enableVersion)
                    {
                        break;
                    }

                    Capture = capture;
                    CaptureChanged?.Invoke(capture);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            finally
            {
                if (runVersion == enableVersion)
                {
                    Capture = default;
                    CaptureChanged?.Invoke(default);
                    Transformation = null;
                    TransformationChanged?.Invoke(default);
                }

                if (distances.IsCreated)
                {
                    distances.Dispose();
                }

                transformation?.Dispose();

                if (device is not null)
                {
                    await device.DisposeAsync();
                }
            }
        }
    }

    struct SCIPCapture
    {
        public SCIPScanTransformation Transformation;
        public NativeArray<int> Distances;
        public SCIPCapture(SCIPScanTransformation transformation, NativeArray<int> distances)
        {
            Transformation = transformation;
            Distances = distances;
        }
        public bool IsValid => Distances.IsCreated;
    }

    sealed class SCIPScanTransformation : IDisposable
    {
        public SCIPScanTransformation(int DMIN, int DMAX, int ARES, int AMIN, int AMAX, int AFRT)
        {
            var steps = AMAX - AMIN + 1;
            var distancesToPositions = new NativeArray<float2>(steps, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var oneStepRadian = (math.PI * 2) / ARES;
            var startRadian = oneStepRadian * (AMIN - AFRT);
            new CreateDistancesToPositionsJob()
            {
                DistancesToPositions = distancesToPositions,
                OneStepRadian = oneStepRadian,
                StartRadian = startRadian,
            }.Schedule(steps, 256).Complete();

            DistancesToPositions = distancesToPositions;
            StartRadian = startRadian;
            OneStepRadian = oneStepRadian;
            MinDistance = DMIN;
            MaxDistance = DMAX;
            this.ARES = ARES;
            this.AMIN = AMIN;
            this.AMAX = AMAX;
            this.AFRT = AFRT;
        }
        internal NativeArray<float2> DistancesToPositions;
        internal readonly float StartRadian, OneStepRadian;
        internal readonly int MinDistance, MaxDistance;
        internal readonly int ARES, AMIN, AMAX, AFRT;
        public bool IsValid => DistancesToPositions.IsCreated;
        public float IndexToRadian(float index)
        {
            return StartRadian + OneStepRadian * index;
        }

        public float RadianToIndex(float radian)
        {
            return (radian - StartRadian) / OneStepRadian;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 TransformDistanceToPosition(int distance, int index)
        {
            return DistancesToPositionsJob.DistanceToPosition(distance, index, DistancesToPositions, MinDistance, MaxDistance);
        }
        public void TransformDistancesToPositions(NativeArray<int> distances, NativeArray<float2> result)
        {
            new DistancesToPositionsJob()
            {
                MinDistance = MinDistance,
                MaxDistance = MaxDistance,
                DistancesToPositions = DistancesToPositions,
                Distances = distances,
                Positions = result,

            }.Run(distances.Length);
        }
        public NativeArray<float2> TransformDistancesToPositions(NativeArray<int> distances, Allocator allocator = Allocator.TempJob)
        {
            var positions = new NativeArray<float2>(distances.Length, allocator, NativeArrayOptions.UninitializedMemory);
            TransformDistancesToPositions(distances, positions);
            return positions;
        }
        public void Dispose()
        {
            DistancesToPositions.Dispose();
        }
    }

    #region Jobs
    [BurstCompile]
    struct GetContentJob : IJobParallelFor
    {
        const int MaxBlockContentSize = 64;
        const int MaxBlockSize = MaxBlockContentSize + 2;

        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public unsafe byte* DataPtr;
        [WriteOnly, NoAlias] public NativeArray<byte> Content;
        [NoAlias, NativeDisableUnsafePtrRestriction] public unsafe int* CheckSumErrors;
        public void Execute(int index)
        {
            var srcOffset = index * MaxBlockSize;
            var dstOffset = index * MaxBlockContentSize;
            var blockContentSize = Math.Min(MaxBlockContentSize, Content.Length - dstOffset);
            unsafe
            {
                var srcBlockPtr = DataPtr + srcOffset;
                var checkSum = srcBlockPtr[blockContentSize];
                var error = math.select(1, 0, CheckSum.IsValidData(srcBlockPtr, blockContentSize, checkSum));
                Interlocked.Add(ref *CheckSumErrors, error);
                var dstPtr = (byte*)Content.GetUnsafePtr() + dstOffset;
                UnsafeUtility.MemCpy(dstPtr, srcBlockPtr, blockContentSize);
            }
        }

        public static NativeArray<byte> GetContent(ReadOnlySpan<byte> data, Allocator allocator = Allocator.TempJob)
        {
            ComputeBlockLayout(data.Length, out var fullSizeBlocksCount, out var nonFullSizeBlockContentSize);
            var contentSize = MaxBlockContentSize * fullSizeBlocksCount + nonFullSizeBlockContentSize;
            var blocks = nonFullSizeBlockContentSize == 0 ? fullSizeBlocksCount : fullSizeBlocksCount + 1;

            var content = new NativeArray<byte>(contentSize, allocator, NativeArrayOptions.UninitializedMemory);
            unsafe
            {
                int checkSumErrors = 0;
                fixed (byte* dataPtr = data)
                {
                    new GetContentJob()
                    {
                        DataPtr = dataPtr,
                        Content = content,
                        CheckSumErrors = &checkSumErrors,

                    }.Run(blocks);
                }
                if (checkSumErrors != 0)
                {
                    content.Dispose();
                    throw new FormatException("Data has checksum error!");
                }

                return content;
            }
        }

        static void ComputeBlockLayout(int dataSize, out int fullSizeBlocksCount, out int nonFullSizeBlockContentSize)
        {
            dataSize -= 1;
            fullSizeBlocksCount = dataSize / MaxBlockSize;
            var nonFullSizeBlockSize = dataSize % MaxBlockSize;
            nonFullSizeBlockContentSize = math.select(nonFullSizeBlockSize - 2, 0, nonFullSizeBlockSize == 0);
        }
    }
    [BurstCompile]
    struct GetDistancesJob : IJobParallelForBatch
    {
        [ReadOnly, NoAlias] public NativeArray<byte> Data;
        [WriteOnly, NoAlias] public NativeArray<int> Distances;
        [ReadOnly] public int EncodingChars;
        public void Execute(int startIndex, int count)
        {
            switch (EncodingChars)
            {
                case 2:
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var index = startIndex + i;
                            var dataIndex = index * EncodingChars;
                            uint value = CharEncoding.Decode2(Data.ReinterpretLoad<ushort>(dataIndex));
                            Distances[index] = (int)value;
                        }
                        break;
                    }
                case 3:
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var index = startIndex + i;
                            var dataIndex = index * EncodingChars;
                            uint value = CharEncoding.Decode3((uint)Data.ReinterpretLoad<ushort>(dataIndex) | ((uint)Data[dataIndex + 2] << 16));
                            Distances[index] = (int)value;
                        }
                        break;
                    }
                case 4:
                    {
                        for (int i = 0; i < count; i++)
                        {
                            var index = startIndex + i;
                            var dataIndex = index * EncodingChars;
                            uint value = CharEncoding.Decode4(Data.ReinterpretLoad<uint>(dataIndex));
                            Distances[index] = (int)value;
                        }
                        break;
                    }
                default:
                    {
                        for (int ii = 0; ii < count; ii++)
                        {
                            var index = startIndex + ii;
                            var dataIndex = index * EncodingChars;
                            uint value = 0;
                            var src = Data.GetSubArray(dataIndex, EncodingChars);
                            for (int i = 0; i < src.Length; i++)
                            {
                                value <<= 6;
                                value |= (uint)(src[i] - 0x30);
                            }
                            Distances[index] = (int)value;
                        }
                        break;
                    }
            }
        }

        public static void GetDistances(NativeArray<byte> data, NativeArray<int> distances, int encodingChars)
        {
            new GetDistancesJob()
            {
                Data = data,
                Distances = distances,
                EncodingChars = encodingChars,
            }.RunBatch(distances.Length);
        }
        public static void GetDistances(NativeArray<byte> data, NativeArray<int> distances)
        {
            GetDistances(data, distances, data.Length / distances.Length);
        }
        public static NativeArray<int> GetDistances(NativeArray<byte> data, int encodingChars, Allocator allocator = Allocator.TempJob)
        {
            var distances = new NativeArray<int>(data.Length / encodingChars, allocator, NativeArrayOptions.UninitializedMemory);

            GetDistances(data, distances, encodingChars);

            return distances;
        }
    }

    [BurstCompile]
    internal struct CreateDistancesToPositionsJob : IJobParallelFor
    {
        const float MilliMeterToMeter = 0.001f;
        [ReadOnly] public float StartRadian, OneStepRadian;
        [WriteOnly, NoAlias] public NativeArray<float2> DistancesToPositions;
        public void Execute(int index)
        {
            var radian = StartRadian + (OneStepRadian * index);
            math.sincos(radian, out var s, out var c);
            DistancesToPositions[index] = new float2(c, s) * MilliMeterToMeter;
        }
    }
    [BurstCompile]
    internal struct DistancesToPositionsJob : IJobParallelFor
    {
        [ReadOnly] public int MinDistance, MaxDistance;
        [ReadOnly, NoAlias] public NativeArray<float2> DistancesToPositions;
        [ReadOnly, NoAlias] public NativeArray<int> Distances;
        [WriteOnly, NoAlias] public NativeArray<float2> Positions;
        public void Execute(int index)
        {
            var distance = Distances[index];
            Positions[index] = DistanceToPosition(distance, index, DistancesToPositions, MinDistance, MaxDistance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 DistanceToPosition(int distance, int index, NativeArray<float2> distancesToPositions, int minDistance, int maxDistance)
        {
            var clampedDistance = math.select(distance, minDistance, distance < minDistance);
            clampedDistance = math.select(distance, maxDistance, distance > maxDistance);
            return clampedDistance * distancesToPositions[index];
        }
    }
    #endregion
}
