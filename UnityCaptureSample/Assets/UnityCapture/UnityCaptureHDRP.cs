/*
  UnityCaptureHDRP
  Copyright (c) 2022 Amari Noa

  Feature contributors:
    Amari Noa (Added support for Unity HDRP)


  // =====
  Based on Unity Capture
  https://github.com/schellingb/UnityCapture
  Copyright (c) 2018 Bernhard Schelling

  Feature contributors:
    Brandon J Matthews (low-level interface for custom texture capture)


  // =====
  Based on UnityCam
  https://github.com/mrayy/UnityCam
  Copyright (c) 2016 MHD Yamen Saraiji

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
     claim that you wrote the original software. If you use this software
     in a product, an acknowledgment in the product documentation would be
     appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
     misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class UnityCaptureHDRP : MonoBehaviour
{
    public enum ECaptureDevice { CaptureDevice1 = 0, CaptureDevice2 = 1, CaptureDevice3 = 2, CaptureDevice4 = 3, CaptureDevice5 = 4, CaptureDevice6 = 5, CaptureDevice7 = 6, CaptureDevice8 = 7, CaptureDevice9 = 8, CaptureDevice10 = 9 }
    public enum EResizeMode { Disabled = 0, LinearResize = 1 }
    public enum EMirrorMode { Disabled = 0, MirrorHorizontally = 1 }
    public enum ECaptureSendResult { SUCCESS = 0, WARNING_FRAMESKIP = 1, WARNING_CAPTUREINACTIVE = 2, ERROR_UNSUPPORTEDGRAPHICSDEVICE = 100, ERROR_PARAMETER = 101, ERROR_TOOLARGERESOLUTION = 102, ERROR_TEXTUREFORMAT = 103, ERROR_READTEXTURE = 104, ERROR_INVALIDCAPTUREINSTANCEPTR = 200 };

    public enum ERenderTextureDepth { Disabled = 0, D16 = 16, D24 = 24 , D32 = 32 }

    [SerializeField] [Tooltip("Capture device index")] public ECaptureDevice CaptureDevice = ECaptureDevice.CaptureDevice1;
    [SerializeField] [Tooltip("Scale image if Unity and capture resolution don't match (can introduce frame dropping, not recommended)")] public EResizeMode ResizeMode = EResizeMode.Disabled;
    [SerializeField] [Tooltip("How many milliseconds to wait for a new frame until sending is considered to be stopped")] public int Timeout = 1000;
    [SerializeField] [Tooltip("Mirror captured output image")] public EMirrorMode MirrorMode = EMirrorMode.Disabled;
    [SerializeField] [Tooltip("Introduce a frame of latency in favor of frame rate")] public bool DoubleBuffering = false;
    [SerializeField] [Tooltip("Check to enable VSync during capturing")] public bool EnableVSync = false;
    [SerializeField] [Tooltip("Set the desired render target frame rate")] public int TargetFrameRate = 60;
    [SerializeField] [Tooltip("Check to disable output of warnings")] public bool HideWarnings = false;

    [SerializeField] [Tooltip("Output resolution width")] public int ResolutionWidth = 1920;
    [SerializeField] [Tooltip("Output resolution height")] public int ResolutionHeight = 1080;
    [SerializeField] [Tooltip("Rendertexture depth buffer")] public ERenderTextureDepth Depth = ERenderTextureDepth.D24;

    private Interface CaptureInterface;

    private Camera Cam;
    private RenderTexture Source;
    private RenderTexture Destination;

    private void ReleaseRenderTextures()
    {
        Source?.Release();
        Destination?.Release();
    }

    private void CreateAndSetRenderTextures()
    {
        ReleaseRenderTextures();

        Source = new RenderTexture(ResolutionWidth, ResolutionHeight, (int)Depth);
        Destination = new RenderTexture(ResolutionWidth, ResolutionHeight, (int)Depth);

        Cam.targetTexture = Source;
    }

    void Awake()
    {
        QualitySettings.vSyncCount = (EnableVSync ? 1 : 0);
        Application.targetFrameRate = TargetFrameRate;

        if (Application.runInBackground == false)
        {
            Debug.LogWarning("Application.runInBackground switched to enabled for capture streaming");
            Application.runInBackground = true;
        }

        Cam = GetComponent<Camera>();
        CreateAndSetRenderTextures();

        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void Start()
    {
        CaptureInterface = new Interface(CaptureDevice);
    }

    void OnDestroy()
    {
        ReleaseRenderTextures();

        CaptureInterface?.Close();
    }

    void Update()
    {
        if (Source)
        {
            if (Source.width != ResolutionWidth || Source.height != ResolutionHeight)
            {
                Debug.Log($"[UnityCaptureHDRP] Output resolution changed. (Cam: {gameObject.name})");
                Debug.Log($"[UnityCaptureHDRP] before: {Source.width}x{Source.height} / after: {ResolutionWidth}x{ResolutionHeight}");

                CreateAndSetRenderTextures();
            }
        }
        else
        {
            CreateAndSetRenderTextures();
        }
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
#if UNITY_EDITOR
        if (!EditorApplication.isPlaying)
        {
            return;
        }
#endif

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (camera != Cam)
        {
            return;
        }

        if (!Source)
        {
            if (!HideWarnings)
            {
                Debug.LogWarning("[UnityCaptureHDRP] Source RenderTexture is null.");
            }
            return;
        }


        Graphics.Blit(Source, Destination);
        switch (CaptureInterface.SendTexture(Source, Timeout, DoubleBuffering, ResizeMode, MirrorMode))
        {
            case ECaptureSendResult.SUCCESS: break;
            case ECaptureSendResult.WARNING_FRAMESKIP: if (!HideWarnings) Debug.LogWarning("[UnityCaptureHDRP] Capture device did skip a frame read, capture frame rate will not match render frame rate."); break;
            case ECaptureSendResult.WARNING_CAPTUREINACTIVE: if (!HideWarnings) Debug.LogWarning("[UnityCaptureHDRP] Capture device is inactive"); break;
            case ECaptureSendResult.ERROR_UNSUPPORTEDGRAPHICSDEVICE: Debug.LogError("[UnityCaptureHDRP] Unsupported graphics device (only D3D11 supported)"); break;
            case ECaptureSendResult.ERROR_PARAMETER: Debug.LogError("[UnityCaptureHDRP] Input parameter error"); break;
            case ECaptureSendResult.ERROR_TOOLARGERESOLUTION: Debug.LogError("[UnityCaptureHDRP] Render resolution is too large to send to capture device"); break;
            case ECaptureSendResult.ERROR_TEXTUREFORMAT: Debug.LogError("[UnityCaptureHDRP] Render texture format is unsupported (only basic non-HDR (ARGB32) and HDR (FP16/ARGB Half) formats are supported)"); break;
            case ECaptureSendResult.ERROR_READTEXTURE: Debug.LogError("[UnityCaptureHDRP] Error while reading texture image data"); break;
            case ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR: Debug.LogError("[UnityCaptureHDRP] Invalid Capture Instance Pointer"); break;
        }
    }

    public class Interface
    {
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static System.IntPtr CaptureCreateInstance(int CapNum);
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static void CaptureDeleteInstance(System.IntPtr instance);
        [System.Runtime.InteropServices.DllImport("UnityCapturePlugin")] extern static ECaptureSendResult CaptureSendTexture(System.IntPtr instance, System.IntPtr nativetexture, int Timeout, bool UseDoubleBuffering, EResizeMode ResizeMode, EMirrorMode MirrorMode, bool IsLinearColorSpace);
        System.IntPtr CaptureInstance;

        public Interface(ECaptureDevice CaptureDevice)
        {
            CaptureInstance = CaptureCreateInstance((int)CaptureDevice);
        }

        ~Interface()
        {
            Close();
        }

        public void Close()
        {
            if (CaptureInstance != System.IntPtr.Zero) CaptureDeleteInstance(CaptureInstance);
            CaptureInstance = System.IntPtr.Zero;
        }

        public ECaptureSendResult SendTexture(Texture Source, int Timeout = 1000, bool DoubleBuffering = false, EResizeMode ResizeMode = EResizeMode.Disabled, EMirrorMode MirrorMode = EMirrorMode.Disabled)
        {
            if (CaptureInstance == System.IntPtr.Zero) return ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR;
            return CaptureSendTexture(CaptureInstance, Source.GetNativeTexturePtr(), Timeout, DoubleBuffering, ResizeMode, MirrorMode, QualitySettings.activeColorSpace == ColorSpace.Linear);
        }
    }
}
