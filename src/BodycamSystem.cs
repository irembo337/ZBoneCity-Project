using System;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class BodycamSystem
    {
        private const string CameraObjectName = "ZBoneCity_Bodycam_Camera";
        private const string CanvasObjectName = "ZBoneCity_Bodycam_Canvas";
        private const float TeleportResetDistance = 1.45f;
        private const int PainOverlayLayerFallback = 5;
        private readonly AxonBodyOverlay _axonOverlay = new AxonBodyOverlay();
        private Camera? _mainCamera;
        private Camera? _bodycamCamera;
        private GameObject? _bodycamObject;
        private GameObject? _canvasObject;
        private Canvas? _canvas;
        private RectTransform? _canvasRect;
        private CanvasGroup? _canvasGroup;
        private RawImage? _feedImage;
        private RawImage? _noiseImage;
        private RawImage? _lensDirtImage;
        private AspectRatioFitter? _feedAspect;
        private Image? _topBorder;
        private Image? _bottomBorder;
        private RenderTexture? _renderTexture;
        private Texture2D? _noiseTexture;
        private Texture2D? _lensDirtTexture;
        private int _renderWidth;
        private int _renderHeight;
        private int _copiedMainCameraId;
        private uint _noiseSeed = 2166136261u;
        private Vector3 _smoothedPosition;
        private Vector3 _positionVelocity;
        private Quaternion _smoothedRotation = Quaternion.identity;
        private Vector3 _lastTargetPosition;
        private float _previousVerticalVelocity;
        private float _landingKick;
        private float _bobPhase;
        private float _lensBlood;
        private float _lensWater;
        private float _autoExposure = 1f;
        private float _focusResponse = 1f;
        private float _glitchPulse;
        private float _glitchTimer;
        private bool _motionInitialized;
        private bool _targetInitialized;

        public void Update(float deltaTime)
        {
            if (!Config.BodycamEnabled)
            {
                Destroy();
                return;
            }

            if (!CanCreateRuntimeObjects())
            {
                Reset();
                return;
            }

            if (!CaptureMainCamera())
                return;

            EnsureCamera();
            EnsureRenderTexture();
            EnsureCanvas();
            UpdateCanvas(Mathf.Clamp(deltaTime, 0.001f, 0.05f));
        }

        public void LateUpdate(float deltaTime)
        {
            if (!Config.BodycamEnabled)
                return;

            if (!CanCreateRuntimeObjects())
                return;

            if (!CaptureMainCamera())
                return;

            EnsureCamera();
            EnsureRenderTexture();
            UpdateCameraTransform(Mathf.Clamp(deltaTime, 0.001f, 0.05f));
        }

        public void RenderGui()
        {
        }

        public void Reset()
        {
            _mainCamera = null;
            _copiedMainCameraId = 0;
            _lensBlood = 0f;
            _lensWater = 0f;
            _autoExposure = 1f;
            _focusResponse = 1f;
            _glitchPulse = 0f;
            _glitchTimer = 0f;
            ResetMotionState();
            if (_bodycamCamera != null)
                _bodycamCamera.enabled = false;
        }

        public void ResetAxonIdentity()
        {
            _axonOverlay.ResetIdentity();
        }

        public void Destroy()
        {
            if (_bodycamCamera != null)
            {
                _bodycamCamera.targetTexture = null;
                _bodycamCamera.enabled = false;
                _bodycamCamera = null;
            }

            if (_bodycamObject != null)
            {
                UnityEngine.Object.Destroy(_bodycamObject);
                _bodycamObject = null;
            }

            if (_canvasObject != null)
            {
                UnityEngine.Object.Destroy(_canvasObject);
                _canvasObject = null;
            }

            _axonOverlay.Destroy();
            DestroyRenderTexture();
            if (_noiseTexture != null)
            {
                UnityEngine.Object.Destroy(_noiseTexture);
                _noiseTexture = null;
            }

            if (_lensDirtTexture != null)
            {
                UnityEngine.Object.Destroy(_lensDirtTexture);
                _lensDirtTexture = null;
            }

            _canvas = null;
            _canvasRect = null;
            _canvasGroup = null;
            _feedImage = null;
            _noiseImage = null;
            _lensDirtImage = null;
            _feedAspect = null;
            _topBorder = null;
            _bottomBorder = null;
            Reset();
        }

        public void AddLensBlood(float intensity)
        {
            _lensBlood = Mathf.Clamp01(Mathf.Max(_lensBlood, intensity));
        }

        public void AddLensWater(float intensity)
        {
            _lensWater = Mathf.Clamp01(Mathf.Max(_lensWater, intensity));
        }

        private bool CaptureMainCamera()
        {
            Camera? current = Camera.main;
            if (current == null)
                return false;

            if (_mainCamera != current)
            {
                _mainCamera = current;
                _copiedMainCameraId = 0;
                ResetMotionState();
            }

            return true;
        }

        private static bool CanCreateRuntimeObjects()
        {
            MainMod? runtime = MainMod.Runtime;
            return runtime != null && runtime.IsPlayerRigReady();
        }

        private void EnsureCamera()
        {
            if (_mainCamera == null)
                return;

            if (_bodycamObject == null)
            {
                _bodycamObject = new GameObject(CameraObjectName);
                UnityEngine.Object.DontDestroyOnLoad(_bodycamObject);
            }

            if (_bodycamCamera == null)
            {
                _bodycamCamera = _bodycamObject.AddComponent<Camera>();
                _copiedMainCameraId = 0;
            }

            int mainId = _mainCamera.GetInstanceID();
            if (_copiedMainCameraId != mainId)
            {
                _bodycamCamera.CopyFrom(_mainCamera);
                _copiedMainCameraId = mainId;
            }

            _bodycamCamera.enabled = true;
            _bodycamCamera.targetTexture = _renderTexture;
            _bodycamCamera.depth = _mainCamera.depth - 100f;
            _bodycamCamera.fieldOfView = GetPresetFov();
            _bodycamCamera.nearClipPlane = Mathf.Max(0.025f, _mainCamera.nearClipPlane);
            _bodycamCamera.farClipPlane = _mainCamera.farClipPlane;
            _bodycamCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _bodycamCamera.allowMSAA = false;
            ExcludePainOverlayLayer(_bodycamCamera);
            if (_renderHeight > 0)
                _bodycamCamera.aspect = _renderWidth / (float)_renderHeight;
        }

        private static void ExcludePainOverlayLayer(Camera camera)
        {
            int layer = LayerMask.NameToLayer("UI");
            if (layer < 0)
                layer = PainOverlayLayerFallback;
            camera.cullingMask &= ~(1 << layer);
        }

        private void EnsureRenderTexture()
        {
            int width = Mathf.Clamp(Screen.width, 64, 3840);
            int height = Mathf.Clamp(Screen.height, 64, 2160);
            if (_renderTexture != null && _renderWidth == width && _renderHeight == height && _renderTexture.IsCreated())
                return;

            DestroyRenderTexture();
            _renderWidth = width;
            _renderHeight = height;
            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ZBoneCity_Bodycam_RT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _renderTexture.Create();

            if (_bodycamCamera != null)
            {
                _bodycamCamera.targetTexture = _renderTexture;
                _bodycamCamera.aspect = width / (float)height;
            }

            if (_feedImage != null)
                _feedImage.texture = _renderTexture;
            if (_feedAspect != null)
                _feedAspect.aspectRatio = width / (float)height;
        }

        private void DestroyRenderTexture()
        {
            if (_renderTexture == null)
                return;

            if (_bodycamCamera != null && _bodycamCamera.targetTexture == _renderTexture)
                _bodycamCamera.targetTexture = null;
            if (_feedImage != null && _feedImage.texture == _renderTexture)
                _feedImage.texture = null;

            if (_renderTexture.IsCreated())
                _renderTexture.Release();
            UnityEngine.Object.Destroy(_renderTexture);
            _renderTexture = null;
            _renderWidth = 0;
            _renderHeight = 0;
        }

        private void EnsureCanvas()
        {
            if (_canvasObject != null)
                return;

            _canvasObject = new GameObject(CanvasObjectName);
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);
            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32730;
            _canvas.pixelPerfect = false;

            CanvasScaler scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            _canvasGroup = _canvasObject.AddComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.alpha = 1f;

            _canvasRect = _canvasObject.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;

            _feedImage = CreateRawImage("BodycamFeed", _canvasObject.transform);
            _feedImage.texture = _renderTexture;
            _feedImage.color = Color.white;
            Stretch(_feedImage.rectTransform);
            _feedAspect = _feedImage.gameObject.AddComponent<AspectRatioFitter>();
            _feedAspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            _feedAspect.aspectRatio = _renderHeight > 0 ? _renderWidth / (float)_renderHeight : 16f / 9f;

            _noiseImage = CreateRawImage("DigitalNoise", _canvasObject.transform);
            _noiseImage.texture = EnsureNoiseTexture();
            _noiseImage.color = new Color(1f, 1f, 1f, 0f);
            Stretch(_noiseImage.rectTransform);

            _lensDirtImage = CreateRawImage("LensDirt", _canvasObject.transform);
            _lensDirtImage.texture = EnsureLensDirtTexture();
            _lensDirtImage.color = new Color(1f, 1f, 1f, 0f);
            Stretch(_lensDirtImage.rectTransform);

            _topBorder = CreateBorder("TopBorder", true);
            _bottomBorder = CreateBorder("BottomBorder", false);
            _axonOverlay.EnsureCreated(_canvasObject.transform);
        }

        private void UpdateCanvas(float deltaTime)
        {
            if (_canvasObject == null)
                return;

            if (!_canvasObject.activeSelf)
                _canvasObject.SetActive(true);

            if (_feedImage != null && _feedImage.texture != _renderTexture)
                _feedImage.texture = _renderTexture;
            if (_feedAspect != null && _renderHeight > 0)
                _feedAspect.aspectRatio = _renderWidth / (float)_renderHeight;

            ApplyDigitalEffects(deltaTime);
            ApplyBorders();
            _axonOverlay.Update(deltaTime);
        }

        private void UpdateCameraTransform(float deltaTime)
        {
            if (_mainCamera == null || _bodycamObject == null || _bodycamCamera == null)
                return;

            Transform main = _mainCamera.transform;
            Transform bodycam = _bodycamObject.transform;
            BuildTargetPose(main, out Vector3 targetPosition, out Quaternion targetRotation);
            UpdateMotionEstimates(targetPosition, deltaTime, out float horizontalSpeed);

            if (!_motionInitialized || Vector3.Distance(_smoothedPosition, targetPosition) > TeleportResetDistance)
            {
                _smoothedPosition = targetPosition;
                _smoothedRotation = targetRotation;
                _positionVelocity = Vector3.zero;
                _motionInitialized = true;
            }
            else
            {
                float smoothTime = Config.BodycamHelmetCamera ? 0.018f : Mathf.Lerp(0.035f, 0.075f, Mathf.InverseLerp(9f, 1f, GetPresetSmoothing()));
                _smoothedPosition = Vector3.SmoothDamp(_smoothedPosition, targetPosition, ref _positionVelocity, smoothTime, 32f, deltaTime);
                float rotationResponse = Config.BodycamHelmetCamera ? 28f : Mathf.Max(10f, GetPresetSmoothing() * 1.8f);
                float rotationT = 1f - Mathf.Exp(-rotationResponse * deltaTime);
                _smoothedRotation = Quaternion.Slerp(_smoothedRotation, targetRotation, rotationT);
            }

            Vector3 physicalOffset = BuildPhysicalCameraOffset(horizontalSpeed, deltaTime);
            Quaternion physicalRotation = BuildPhysicalCameraRotation(horizontalSpeed);
            bodycam.position = _smoothedPosition + _smoothedRotation * physicalOffset;
            bodycam.rotation = _smoothedRotation * physicalRotation;
            _bodycamCamera.fieldOfView = Mathf.Lerp(_bodycamCamera.fieldOfView, GetPresetFov(), 1f - Mathf.Exp(-18f * deltaTime));
        }

        private static void BuildTargetPose(Transform main, out Vector3 position, out Quaternion rotation)
        {
            Vector3 yawForward = Vector3.ProjectOnPlane(main.forward, Vector3.up);
            if (yawForward.sqrMagnitude < 0.0001f)
                yawForward = Vector3.ProjectOnPlane(main.up, Vector3.up);
            if (yawForward.sqrMagnitude < 0.0001f)
                yawForward = Vector3.forward;
            yawForward.Normalize();

            Quaternion yawRotation = Quaternion.LookRotation(yawForward, Vector3.up);
            if (Config.BodycamHelmetCamera)
            {
                float helmetForward = Mathf.Clamp(GetPresetForwardOffset() * 0.45f, 0.045f, 0.16f);
                position = main.position + main.forward * helmetForward - main.up * 0.015f;
                rotation = main.rotation;
                return;
            }

            float headPitch = Mathf.DeltaAngle(0f, main.eulerAngles.x);
            float headRoll = Mathf.DeltaAngle(0f, main.eulerAngles.z);
            float chestPitch = Mathf.Clamp(headPitch * 0.28f, -16f, 10f);
            float chestRoll = Mathf.Clamp(headRoll * 0.10f, -2.5f, 2.5f);
            float forwardOffset = Mathf.Max(GetPresetForwardOffset(), 0.34f);
            Vector3 lateral = yawRotation * new Vector3(0.035f, -0.015f, 0f);
            position = main.position - Vector3.up * 0.36f + yawForward * forwardOffset + lateral;
            rotation = yawRotation * Quaternion.Euler(chestPitch, 0f, chestRoll);
        }

        private void UpdateMotionEstimates(Vector3 targetPosition, float deltaTime, out float horizontalSpeed)
        {
            horizontalSpeed = 0f;
            if (_targetInitialized)
            {
                Vector3 velocity = (targetPosition - _lastTargetPosition) / Mathf.Max(deltaTime, 0.001f);
                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
                horizontalSpeed = horizontalVelocity.magnitude;
                if (_previousVerticalVelocity < -2.2f && velocity.y > -0.35f)
                    _landingKick = Mathf.Max(_landingKick, Mathf.Clamp(Mathf.Abs(_previousVerticalVelocity) * 0.006f, 0.008f, 0.035f));
                _previousVerticalVelocity = velocity.y;
            }
            else
            {
                _targetInitialized = true;
                _previousVerticalVelocity = 0f;
            }

            _lastTargetPosition = targetPosition;
        }

        private Vector3 BuildPhysicalCameraOffset(float horizontalSpeed, float deltaTime)
        {
            float speed01 = Mathf.InverseLerp(0.25f, 4.5f, horizontalSpeed);
            float frequency = Mathf.Lerp(4.2f, 9.4f, speed01);
            float amplitude = Mathf.Clamp(horizontalSpeed * 0.0038f, 0f, 0.024f);
            if (horizontalSpeed > 3.8f)
                amplitude *= 1.35f;

            _bobPhase += deltaTime * frequency;
            _landingKick = Mathf.MoveTowards(_landingKick, 0f, deltaTime * 0.13f);
            float runtimeShake = GetRuntimeShake();
            float bobY = Mathf.Sin(_bobPhase * Mathf.PI * 2f) * amplitude;
            float bobX = Mathf.Sin(_bobPhase * Mathf.PI * 4f + 0.35f) * amplitude * 0.28f;
            Vector3 shake = new Vector3(
                (Mathf.PerlinNoise(Time.time * 13.1f, 0.2f) - 0.5f) * runtimeShake,
                (Mathf.PerlinNoise(0.4f, Time.time * 11.7f) - 0.5f) * runtimeShake,
                0f);

            return new Vector3(bobX, bobY - _landingKick, 0f) + shake;
        }

        private Quaternion BuildPhysicalCameraRotation(float horizontalSpeed)
        {
            float runtimeShake = GetRuntimeShake();
            float speed01 = Mathf.InverseLerp(0.25f, 4.5f, horizontalSpeed);
            float bobPitch = Mathf.Sin(_bobPhase * Mathf.PI * 2f + 0.6f) * Mathf.Lerp(0.15f, 1.25f, speed01);
            float bobRoll = Mathf.Sin(_bobPhase * Mathf.PI * 2f) * Mathf.Lerp(0.1f, 0.9f, speed01);
            float yawShake = (Mathf.PerlinNoise(Time.time * 9.3f, 2.0f) - 0.5f) * runtimeShake * 7f;
            float pitchShake = (Mathf.PerlinNoise(3.0f, Time.time * 8.4f) - 0.5f) * runtimeShake * 4.5f;
            float landingPitch = -_landingKick * 80f;
            return Quaternion.Euler(bobPitch + pitchShake + landingPitch, yawShake, bobRoll);
        }

        private void ApplyDigitalEffects(float deltaTime)
        {
            float noise = Mathf.Min(Config.BodycamNoiseIntensity, 0.28f);
            float compression = Mathf.Min(Config.BodycamCompressionIntensity, 0.32f);
            if (Config.BodycamPreset == 3)
                noise = Mathf.Max(noise, 0.18f);
            if (Config.BodycamPreset == 2)
                compression = Mathf.Max(compression, 0.14f);

            UpdateCameraImageModel(deltaTime, ref noise, ref compression);

            if (_feedImage != null)
            {
                float exposure = _autoExposure * Mathf.Lerp(1f, 0.92f, compression);
                Color compressedTint = Color.Lerp(Color.white, new Color(0.92f, 0.965f, 1f, 1f), compression);
                _feedImage.color = new Color(
                    Mathf.Clamp01(compressedTint.r * exposure),
                    Mathf.Clamp01(compressedTint.g * exposure),
                    Mathf.Clamp01(compressedTint.b * exposure),
                    1f);
                _feedImage.rectTransform.anchoredPosition = new Vector2(_glitchPulse * 3.0f, -_glitchPulse * 1.6f);
            }

            if (_noiseImage == null)
                return;

            _noiseImage.texture = EnsureNoiseTexture();
            _noiseImage.color = new Color(1f, 1f, 1f, noise * (0.035f + GetRuntimeShake() * 0.08f + _glitchPulse * 0.18f));
            _noiseImage.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.time * 11f) * (0.8f + _glitchPulse * 4f), Mathf.Cos(Time.time * 9f) * (0.8f + _glitchPulse * 3f));

            _lensBlood = Mathf.MoveTowards(_lensBlood, 0f, deltaTime * 0.018f);
            _lensWater = Mathf.MoveTowards(_lensWater, 0f, deltaTime * 0.035f);
            if (_lensDirtImage != null)
            {
                _lensDirtImage.texture = EnsureLensDirtTexture();
                float alpha = Config.Clamp(_lensBlood * 0.55f + _lensWater * 0.22f, 0f, 0.72f);
                _lensDirtImage.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        private void UpdateCameraImageModel(float deltaTime, ref float noise, ref float compression)
        {
            Transform? main = _mainCamera != null ? _mainCamera.transform : null;
            float skyExposure = main == null ? 0.5f : Mathf.Clamp01(Vector3.Dot(main.forward, Vector3.up) * 0.5f + 0.5f);
            float targetExposure = Mathf.Lerp(1.08f, 0.86f, skyExposure);
            _autoExposure = Mathf.Lerp(_autoExposure <= 0f ? 1f : _autoExposure, targetExposure, 1f - Mathf.Exp(-2.4f * deltaTime));

            float speedFocus = Mathf.InverseLerp(0.01f, 0.18f, _positionVelocity.magnitude);
            _focusResponse = Mathf.Lerp(_focusResponse <= 0f ? 1f : _focusResponse, 1f - speedFocus * 0.16f, 1f - Mathf.Exp(-5.5f * deltaTime));
            compression = Mathf.Clamp01(compression + (1f - _focusResponse) * 0.12f);

            _glitchTimer -= deltaTime;
            _glitchPulse = Mathf.MoveTowards(_glitchPulse, 0f, deltaTime * 3.8f);
            if (_glitchTimer <= 0f)
            {
                float stress = MainMod.Runtime?.PlayerManager?.Stress.Normalized ?? 0f;
                float interval = Mathf.Lerp(9f, 2.8f, Mathf.Clamp01(stress + Config.BodycamNoiseIntensity * 0.6f));
                _glitchTimer = interval + UnityEngine.Random.Range(0f, interval * 0.65f);
                if (UnityEngine.Random.value < 0.38f + stress * 0.35f)
                    _glitchPulse = Mathf.Max(_glitchPulse, UnityEngine.Random.Range(0.08f, 0.32f) * (1f + stress));
            }

            noise = Mathf.Clamp01(noise + _glitchPulse * 0.35f);
        }

        private Texture2D? EnsureNoiseTexture()
        {
            if (_noiseTexture != null)
                return _noiseTexture;

            const int size = 96;
            _noiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _noiseTexture.name = "ZBoneCity_Bodycam_Noise";
            _noiseTexture.wrapMode = TextureWrapMode.Repeat;
            _noiseTexture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                _noiseSeed ^= (uint)(i + 0x9e3779b9u);
                _noiseSeed *= 16777619u;
                byte value = (byte)(120 + ((_noiseSeed >> 16) & 0x3f));
                pixels[i] = new Color32(value, value, value, 58);
            }

            _noiseTexture.SetPixels32(pixels);
            _noiseTexture.Apply(false, true);
            return _noiseTexture;
        }

        private Texture2D? EnsureLensDirtTexture()
        {
            if (_lensDirtTexture != null)
                return _lensDirtTexture;

            const int size = 192;
            _lensDirtTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _lensDirtTexture.name = "ZBoneCity_Bodycam_LensDirt";
            _lensDirtTexture.wrapMode = TextureWrapMode.Clamp;
            _lensDirtTexture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            uint seed = 0x811c9dc5u;
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size;
                    seed ^= (uint)(x * 374761393 + y * 668265263);
                    seed *= 16777619u;
                    float edge = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(ny - 0.5f));
                    float edgeMask = Mathf.SmoothStep(0.28f, 0.52f, edge);
                    float smearA = 1f - Mathf.SmoothStep(0.0f, 0.18f, Mathf.Abs((nx * 0.85f + ny * 0.25f) - 0.28f));
                    float smearB = 1f - Mathf.SmoothStep(0.0f, 0.13f, Mathf.Abs((nx * 0.35f - ny * 0.92f) + 0.33f));
                    float dot = ((seed >> 8) & 0xff) / 255f;
                    float alpha = Config.Clamp((smearA * 0.20f + smearB * 0.16f + edgeMask * 0.10f) * (0.45f + dot * 0.55f), 0f, 1f);
                    pixels[y * size + x] = new Color32(96, 0, 8, (byte)Mathf.RoundToInt(alpha * 210f));
                }
            }

            _lensDirtTexture.SetPixels32(pixels);
            _lensDirtTexture.Apply(false, true);
            return _lensDirtTexture;
        }

        private static float GetRuntimeShake()
        {
            HealthManager? manager = MainMod.Runtime?.PlayerManager;
            float medicalShake = manager == null ? 0f : manager.Stress.BodycamShake + manager.PainSystem.ShakeIntensity * 0.10f + manager.Neck.CameraInstability * 0.42f;
            float presetMultiplier = Config.BodycamPreset == 2 ? 1.20f : Config.BodycamPreset == 1 ? 0.70f : 1f;
            return Config.Clamp(medicalShake * Config.BodycamShakeIntensity * presetMultiplier * 0.026f, 0f, 0.055f);
        }

        private static float GetPresetFov()
        {
            return Config.BodycamPreset switch
            {
                1 => Mathf.Max(Config.BodycamFov, 128f),
                2 => Mathf.Clamp(Config.BodycamFov, 100f, 116f),
                3 => Mathf.Clamp(Config.BodycamFov, 105f, 124f),
                _ => Config.BodycamFov
            };
        }

        private static float GetPresetForwardOffset()
        {
            return Config.BodycamPreset switch
            {
                1 => Mathf.Max(Config.BodycamForwardOffset, 0.20f),
                2 => Mathf.Max(Config.BodycamForwardOffset, 0.38f),
                3 => Mathf.Max(Config.BodycamForwardOffset, 0.34f),
                _ => Mathf.Max(Config.BodycamForwardOffset, Config.BodycamHelmetCamera ? 0.08f : 0.34f)
            };
        }

        private static float GetPresetSmoothing()
        {
            return Config.BodycamPreset switch
            {
                1 => Mathf.Max(Config.BodycamSmoothing, 13f),
                2 => Mathf.Clamp(Config.BodycamSmoothing, 7f, 11f),
                3 => Mathf.Max(Config.BodycamSmoothing, 9f),
                _ => Config.BodycamSmoothing
            };
        }

        private void ApplyBorders()
        {
            float height = Mathf.Round(1080f * Config.Clamp(Config.BodycamBorderThickness, 0f, 0.22f));
            bool visible = Config.BodycamShowBorders && height > 0.5f;
            SetBorder(_topBorder, visible, height);
            SetBorder(_bottomBorder, visible, height);
        }

        private void ResetMotionState()
        {
            _smoothedPosition = Vector3.zero;
            _positionVelocity = Vector3.zero;
            _smoothedRotation = Quaternion.identity;
            _lastTargetPosition = Vector3.zero;
            _previousVerticalVelocity = 0f;
            _landingKick = 0f;
            _bobPhase = 0f;
            _motionInitialized = false;
            _targetInitialized = false;
        }

        private static RawImage CreateRawImage(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RawImage image = go.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private Image CreateBorder(string name, bool top)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_canvasObject!.transform, false);
            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.black;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = top ? new Vector2(0f, 1f) : Vector2.zero;
            rect.anchorMax = top ? Vector2.one : new Vector2(1f, 0f);
            rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return image;
        }

        private static void SetBorder(Image? image, bool visible, float height)
        {
            if (image == null)
                return;

            image.enabled = visible;
            if (!visible)
                return;

            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
