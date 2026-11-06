using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>AR Foundation image registration + tracked content. No SIFT descriptor injection.</summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public sealed class RuntimeImageTracker : MonoBehaviour
{
    [SerializeField] string imageFile = "target.png";
    [SerializeField] string imageName = "runtime-target";
    [SerializeField, Min(0.01f)] float physicalWidthMeters = 0.20f;
    [SerializeField] GameObject contentPrefab;
    ARTrackedImageManager manager;
    readonly Dictionary<TrackableId, GameObject> content = new Dictionary<TrackableId, GameObject>();
    readonly HashSet<TrackableId> seen = new HashSet<TrackableId>();
    readonly List<TrackableId> stale = new List<TrackableId>();
    string status = "Waiting for AR image subsystem...";
    bool busy;
    int registered;

    void Awake() { manager = GetComponent<ARTrackedImageManager>(); }
    IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + 20f;
        while (manager.subsystem == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (manager.subsystem == null) { status = "No image subsystem. Enable ARCore/ARKit and check camera permission + initial reference library."; yield break; }
        status = "Ready. Register the image, then point the camera at the printed target.";
    }
    public void RegisterImage() { if (!busy) StartCoroutine(AddImage()); }
    IEnumerator AddImage()
    {
        if (manager.subsystem == null) { status = "AR image subsystem is not ready."; yield break; }
        if (!manager.subsystem.subsystemDescriptor.supportsMutableLibrary) { status = "This provider does not support runtime image libraries."; yield break; }
        if (registered > 0) { status = "Runtime target already registered in this session."; yield break; }
        if (string.IsNullOrWhiteSpace(imageName) || physicalWidthMeters <= 0 || float.IsNaN(physicalWidthMeters) || float.IsInfinity(physicalWidthMeters)) { status = "Invalid image name or physical width."; yield break; }
        var library = manager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
        if (library == null) { status = "Assign a reference image library to ARTrackedImageManager before starting."; yield break; }
        busy = true;
        Texture2D texture = null;
        string path = Path.Combine(Application.streamingAssetsPath, imageFile);
        string uri = path.Contains("://") ? path : new Uri(path).AbsoluteUri;
        using (var request = UnityWebRequestTexture.GetTexture(uri, false))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success) { status = "Image read failed: " + request.error; busy = false; yield break; }
            texture = DownloadHandlerTexture.GetContent(request);
        }
        AddReferenceImageJobState job;
        try { job = library.ScheduleAddImageWithValidationJob(texture, imageName, physicalWidthMeters); }
        catch (Exception e) { status = "Registration rejected: " + e.Message; Destroy(texture); busy = false; yield break; }
        status = "Validating reference image...";
        // Keep the texture alive until the asynchronous native job has finished.
        while (!job.jobHandle.IsCompleted) yield return null;
        job.jobHandle.Complete();
        status = "Image registration: " + job.status;
        if (job.status == AddReferenceImageJobStatus.Success) registered++;
        Destroy(texture);
        busy = false;
    }
    void Update()
    {
        if (manager == null || manager.subsystem == null) return;
        seen.Clear();
        foreach (var image in manager.trackables)
        {
            seen.Add(image.trackableId);
            GameObject instance;
            if (!content.TryGetValue(image.trackableId, out instance))
            {
                instance = contentPrefab != null ? Instantiate(contentPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = "AR content: " + image.referenceImage.name;
                if (contentPrefab == null) { instance.transform.localScale = Vector3.one * 0.04f; Destroy(instance.GetComponent<Collider>()); }
                instance.transform.SetParent(image.transform, false);
                instance.transform.localPosition = Vector3.up * 0.025f;
                instance.transform.localRotation = Quaternion.identity;
                content.Add(image.trackableId, instance);
            }
            // Limited / None must not leave a floating content object visible.
            instance.SetActive(image.trackingState == TrackingState.Tracking);
        }
        stale.Clear();
        foreach (var pair in content) if (!seen.Contains(pair.Key)) { if (pair.Value != null) Destroy(pair.Value); stale.Add(pair.Key); }
        foreach (var id in stale) content.Remove(id);
    }
    void OnDisable() { foreach (var instance in content.Values) if (instance != null) instance.SetActive(false); }
    void OnDestroy() { foreach (var instance in content.Values) if (instance != null) Destroy(instance); }
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(12, 35, Screen.width - 24, 180), GUI.skin.box);
        GUILayout.Label("MetaSonGi AR | Runtime image library");
        GUILayout.Label(status);
        GUI.enabled = !busy && registered == 0;
        if (GUILayout.Button("Register target.png at runtime", GUILayout.Height(45))) RegisterImage();
        GUI.enabled = true;
        GUILayout.Label("Print width: " + physicalWidthMeters.ToString("F2") + "m | tracked images: " + content.Count);
        GUILayout.EndArea();
    }
}
