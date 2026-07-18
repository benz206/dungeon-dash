using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DungeonDash
{
    public sealed class RenderingBootstrap : MonoBehaviour
    {
        Camera _mainCamera;
        Volume _volume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<RenderingBootstrap>() == null)
                new GameObject("Rendering Bootstrap").AddComponent<RenderingBootstrap>();
        }

        void Update()
        {
            EnsureCameraPostProcessing();
            EnsureVolume();
        }

        void EnsureCameraPostProcessing()
        {
            var camera = Camera.main;
            if (camera == null || camera == _mainCamera) return;
            _mainCamera = camera;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = 1;
        }

        void EnsureVolume()
        {
            if (Application.isBatchMode) return;
            if (_volume != null) return;
            _volume = FindFirstObjectByType<Volume>();
            if (_volume != null) return;

            var profile = Resources.Load<VolumeProfile>("DungeonVolumeProfile");
            if (profile == null) return;

            var volumeObject = new GameObject("Global Volume");
            volumeObject.transform.SetParent(transform, false);
            volumeObject.layer = 0;
            _volume = volumeObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.sharedProfile = profile;
        }
    }
}
