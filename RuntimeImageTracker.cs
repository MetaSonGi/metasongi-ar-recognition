// MetaSonGi AR Recognition — 런타임 이미지 등록 + 추적 콘텐츠 표시
// AR Foundation 6.x / ARCore. XR Origin(AR Tracked Image Manager가 있는 오브젝트)에 붙여 사용합니다.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MetaSonGi.ARRecognition
{
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class RuntimeImageTracker : MonoBehaviour
    {
        [Tooltip("런타임에 등록할 이미지. Texture Import 설정에서 Read/Write를 켜야 합니다.")]
        public Texture2D targetTexture;

        [Tooltip("런타임 등록 이름")]
        public string runtimeImageName = "runtime-target";

        [Tooltip("인쇄·화면에 표시한 실제 가로 길이(미터)")]
        public float physicalWidthMeters = 0.20f;

        [Tooltip("이미지 위에 띄울 콘텐츠. 비우면 기본 큐브를 만듭니다.")]
        public GameObject contentPrefab;

        ARTrackedImageManager _manager;
        readonly Dictionary<TrackableId, GameObject> _contents = new Dictionary<TrackableId, GameObject>();
        string _status = "대기";
        bool _busy;

        public string Status => _status;

        void Awake()
        {
            _manager = GetComponent<ARTrackedImageManager>();
        }

        void OnEnable()
        {
            _manager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        void OnDisable()
        {
            _manager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        /// <summary>등록 가능 여부를 검사하고 사람이 읽을 수 있는 이유를 돌려줍니다.</summary>
        public bool CanRegister(out string reason)
        {
            if (targetTexture == null) { reason = "targetTexture가 비어 있습니다"; return false; }
            if (!targetTexture.isReadable) { reason = "텍스처 Read/Write가 꺼져 있습니다"; return false; }
            if (physicalWidthMeters <= 0f) { reason = "실제 가로 길이는 0보다 커야 합니다"; return false; }
            if (_manager.subsystem == null) { reason = "이미지 추적 서브시스템이 아직 시작되지 않았습니다 (AR 지원 기기·ARCore 설정 확인)"; return false; }
            if (!_manager.descriptor.supportsMutableLibrary) { reason = "이 기기는 런타임 이미지 라이브러리를 지원하지 않습니다"; return false; }
            if (!(_manager.referenceLibrary is MutableRuntimeReferenceImageLibrary)) { reason = "참조 이미지 라이브러리가 런타임 수정 가능 형식이 아닙니다"; return false; }
            reason = null;
            return true;
        }

        /// <summary>UI 버튼 등에서 호출: targetTexture를 라이브러리에 비동기로 추가합니다.</summary>
        public void RegisterTarget()
        {
            if (_busy) return;
            if (!CanRegister(out var reason)) { _status = "실패: " + reason; Debug.LogWarning("[RuntimeImageTracker] " + _status); return; }
            StartCoroutine(RegisterRoutine());
        }

        IEnumerator RegisterRoutine()
        {
            _busy = true;
            _status = "등록 중…";
            var library = (MutableRuntimeReferenceImageLibrary)_manager.referenceLibrary;

            // 이미지 검증을 지원하지 않는 기기에서는 ErrorInvalidImage 없이 Success/ErrorUnknown만 보고됩니다.
            AddReferenceImageJobState job = library.ScheduleAddImageWithValidationJob(targetTexture, runtimeImageName, physicalWidthMeters);

            while (!job.jobHandle.IsCompleted) yield return null;
            job.jobHandle.Complete();

            switch (job.status)
            {
                case AddReferenceImageJobStatus.Success:
                    _status = $"Success — 라이브러리 이미지 {library.count}개";
                    break;
                case AddReferenceImageJobStatus.ErrorInvalidImage:
                    _status = "실패: 특징점이 부족한 이미지입니다 (대비가 강하고 반복이 적은 이미지 사용)";
                    break;
                default:
                    _status = "실패: " + job.status;
                    break;
            }
            Debug.Log("[RuntimeImageTracker] " + _status);
            _busy = false;
        }

        void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var image in args.added) UpdateContent(image);
            foreach (var image in args.updated) UpdateContent(image);
            foreach (var pair in args.removed)
            {
                if (_contents.TryGetValue(pair.Key, out var go))
                {
                    Destroy(go);
                    _contents.Remove(pair.Key);
                }
            }
        }

        void UpdateContent(ARTrackedImage image)
        {
            if (!_contents.TryGetValue(image.trackableId, out var go))
            {
                go = contentPrefab != null ? Instantiate(contentPrefab, image.transform) : CreateDefaultCube(image.transform);
                go.name = "Content-" + image.referenceImage.name;
                _contents[image.trackableId] = go;
            }

            // Limited/None이면 위치를 믿을 수 없으므로 숨김
            bool tracking = image.trackingState == TrackingState.Tracking;
            go.SetActive(tracking);
            if (!tracking) return;

            // 이미지 크기에 맞춰 콘텐츠 크기 조정 (기본 큐브는 이미지 가로의 절반)
            if (contentPrefab == null)
            {
                float edge = Mathf.Max(0.02f, image.size.x * 0.5f);
                go.transform.localScale = Vector3.one * edge;
                go.transform.localPosition = new Vector3(0f, edge * 0.5f, 0f);
            }
        }

        static GameObject CreateDefaultCube(Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            return cube;
        }

        void OnGUI()
        {
            const int w = 520;
            var area = new Rect(20, 40, w, 260);
            GUILayout.BeginArea(area);
            var big = new GUIStyle(GUI.skin.button) { fontSize = 32 };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true };
            GUI.enabled = !_busy;
            if (GUILayout.Button("Register target.png at runtime", big, GUILayout.Height(90))) RegisterTarget();
            GUI.enabled = true;
            GUILayout.Label("상태: " + _status, label);
            GUILayout.Label("추적 중 콘텐츠: " + _contents.Count, label);
            GUILayout.EndArea();
        }
    }
}
