﻿using Cinemachine;
using UnityEngine;

namespace Scripts.Gameplay.Camera
{
    using Camera = UnityEngine.Camera;

    public class SetTargetForCameraController : MonoBehaviour
    {
        private const string MinimapCameraTag = "Minimap Camera";

        private void Awake()
        {
            SetTargetToCinemachineBrain();
            SetTargetToCameraController();

            Destroy(this);
        }

        private void SetTargetToCinemachineBrain()
        {
            var cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
            if (cinemachineBrain != null)
            {
                cinemachineBrain.ActiveVirtualCamera.Follow = transform;
            }
        }

        private void SetTargetToCameraController()
        {
            var minimapCamera =
                GameObject.FindGameObjectWithTag(MinimapCameraTag);
            if (minimapCamera != null)
            {
                var cameraController =
                    minimapCamera.GetComponent<CameraController>();
                if (cameraController != null)
                {
                    cameraController.SetTarget(transform);
                }
            }
        }
    }
}