using System.Net;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MediaFrontJapan.SCIP
{
    [RequireComponent(typeof(Graphic))]
    sealed class UST10LXScanPlaneConfig : MonoBehaviour, IDragHandler
    {
        internal static int ConfigingCount;
        [SerializeField] SCIPScanPlane scanPlane = default;
        [SerializeField] InputField scaleInputField = default, angleInputField = default;
        [SerializeField] InputField addressInputField = default;
        [SerializeField] GameObject clampDistancesDrawer = default;
        [SerializeField] Graphic graphic;

        void Reset()
        {
            graphic = GetComponent<Graphic>();
        }

        void Awake()
        {
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
        }

        void OnDestroy()
        {
            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
        }

        void OnAfterInputUpdate()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                enabled = !enabled;
            }
        }

        void OnEnable()
        {
            ConfigingCount++;
            scaleInputField.gameObject.SetActive(true);
            angleInputField.gameObject.SetActive(true);
            addressInputField.gameObject.SetActive(true);
            scaleInputField.text = scanPlane.Scale.ToString();
            angleInputField.text = scanPlane.Angle.ToString();
            addressInputField.text = scanPlane.Address.ToString();
            if (clampDistancesDrawer)
            {
                clampDistancesDrawer.SetActive(true);
            }
            graphic.enabled = true;
        }

        void OnDisable()
        {
            ConfigingCount--;
            scaleInputField.gameObject.SetActive(false);
            angleInputField.gameObject.SetActive(false);
            addressInputField.gameObject.SetActive(false);
            if (clampDistancesDrawer)
            {
                clampDistancesDrawer.SetActive(false);
            }

            scanPlane.SaveSettings();
            graphic.enabled = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }
            switch (eventData.button)
            {
                case PointerEventData.InputButton.Left:
                    {
                        scanPlane.Offset += eventData.delta / scanPlane.RootCanvas.transform.localScale;
                    }
                    break;
                case PointerEventData.InputButton.Right:
                    {
                        DrawClampDistance(eventData.position - eventData.delta, eventData.position);
                    }
                    break;
            }
        }

        void DrawClampDistance(float2 fromScreenPoint, float2 toScreenPoint)
        {
            if (!TryGetDrawRange(fromScreenPoint, toScreenPoint, out var fromSubIndex, out var toSubIndex, out var fromIndex, out var toIndex, out var fromDistance, out var toDistance))
            {
                return;
            }

            DrawDistanceLine(scanPlane.ClampDistances, fromSubIndex, toSubIndex, fromIndex, toIndex, fromDistance, toDistance);
            scanPlane.NotifyClampDistancesChanged();
        }

        bool TryGetDrawRange(float2 fromScreenPoint, float2 toScreenPoint, out float fromSubIndex, out float toSubIndex, out int fromIndex, out int toIndex, out float fromDistance, out float toDistance)
        {
            fromSubIndex = default;
            toSubIndex = default;
            fromIndex = default;
            toIndex = default;
            fromDistance = default;
            toDistance = default;

            var transformation = scanPlane.Client.Transformation;
            if (transformation is null)
            {
                return false;
            }

            var from = scanPlane.ScreenToLocalPoint(fromScreenPoint);
            var to = scanPlane.ScreenToLocalPoint(toScreenPoint);

            var fromRad = math.atan2(from.y, from.x);
            var toRad = math.atan2(to.y, to.x);
            if (math.distance(fromRad, toRad) > math.PI)
            {
                return false;
            }

            fromSubIndex = transformation.RadianToIndex(fromRad);
            toSubIndex = transformation.RadianToIndex(toRad);
            fromDistance = math.length(from);
            toDistance = math.length(to);
            if (fromSubIndex > toSubIndex)
            {
                (fromSubIndex, toSubIndex) = (toSubIndex, fromSubIndex);
                (fromDistance, toDistance) = (toDistance, fromDistance);
            }

            fromIndex = (int)math.ceil(math.max(0, fromSubIndex));
            toIndex = (int)math.floor(math.min(transformation.AMAX, toSubIndex));
            return fromIndex <= toIndex;
        }

        static void DrawDistanceLine(NativeArray<int> distances, float fromSubIndex, float toSubIndex, int fromIndex, int toIndex, float fromDistance, float toDistance)
        {
            if (!distances.IsCreated)
            {
                return;
            }

            var indexSpan = toSubIndex - fromSubIndex;
            if (math.abs(indexSpan) < 0.0001f)
            {
                var clampedDistance = (int)(toDistance * 1000);
                for (int index = fromIndex; index <= toIndex; index++)
                {
                    distances[index] = clampedDistance;
                }

                return;
            }

            for (int index = fromIndex; index <= toIndex; index++)
            {
                var t = (index - fromSubIndex) / indexSpan;
                distances[index] = (int)(math.lerp(fromDistance, toDistance, t) * 1000);
            }
        }

        public void TrySetScale(string text)
        {
            if (float.TryParse(text, out var scale))
            {
                scanPlane.Scale = scale;
            }
        }
        public void TrySetAngle(string text)
        {
            if (float.TryParse(text, out var angle))
            {
                scanPlane.Angle = angle;
            }
        }

        public void TrySetAdress(string text)
        {
            if (IPAddress.TryParse(text, out _))
            {
                scanPlane.Address = text;
            }
        }
    }
}
