﻿using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections.Generic;

namespace teleport
{
	using uid = System.UInt64;
	using InputID = UInt16;
	public enum BackgroundMode: byte
    {
		NONE=0, COLOUR, TEXTURE, VIDEO
	}
	public enum LightingMode : byte
	{
		NONE = 0, TEXTURE, VIDEO
	}
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	//! Settings structure for the server, to be shared between C# and the C++ dll.
	//! These values are set in the Unity Editor under Project Settings/Teleport XR, and passed as const
	//! to the C++ dll.
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public class ServerSettings
    {
		[Header("SRT")]
		public Int32 requiredLatencyMs = 30;

		[Header("General")]
		public Int32 detectionSphereRadius = 5;
		public Int32 detectionSphereBufferDistance = 5;
		public Int64 throttleKpS = 0;

		[Header("Geometry")]
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingGeometry = true;
		public Int32 geometryTicksPerSecond = 2;
		public Int32 geometryBufferCutoffSize = 1048576; // Byte count we stop encoding nodes at.
		public float confirmationWaitTime = 15; // Seconds to wait before resending a resource.
		public float clientDrawDistanceOffset = 0; // Offset for distance pixels are clipped at for geometry on the client.

		[Header("Video")]
		[MarshalAs(UnmanagedType.U1)] public bool StreamVideo = true;
		[MarshalAs(UnmanagedType.U1)] public bool StreamWebcam = true;
		public Int32 defaultCaptureCubeTextureSize = 512;
		public Int32 webcamWidth = 128;
		public Int32 webcamHeight = 96;
		public Int32 videoEncodeFrequency = 2;
		[MarshalAs(UnmanagedType.U1)] public bool isDeferringOutput = false;
		[MarshalAs(UnmanagedType.U1)] public bool isCullingCubemaps = false;
		public Int32 blocksPerCubeFaceAcross = 2; // The number of blocks per cube face will be this value squared
		public Int32 cullQuadIndex = -1; // This culls a quad at the index. For debugging only
		public Int32 targetFPS = 60;
		public Int32 idrInterval = 0;
		public avs.VideoCodec videoCodec = avs.VideoCodec.HEVC;
		public avs.RateControlMode rateControlMode = avs.RateControlMode.RC_CBR; 
		public Int32 averageBitrate = 40000000;
		public Int32 maxBitrate = 80000000;
		[MarshalAs(UnmanagedType.U1)] public bool useAutoBitRate = false;
		public Int32 vbvBufferSizeInFrames = 1;
		[MarshalAs(UnmanagedType.U1)] public bool useAsyncEncoding = true;
		[MarshalAs(UnmanagedType.U1)] public bool use10BitEncoding = false;
		[MarshalAs(UnmanagedType.U1)] public bool useYUV444Decoding = false;
		[MarshalAs(UnmanagedType.U1)] public bool useAlphaLayerEncoding = false; // Only works on RTX GPUs and later
		[MarshalAs(UnmanagedType.U1)] public bool usePerspectiveRendering = false;
		public Int32 perspectiveWidth = 836;
		public Int32 perspectiveHeight = 836; 
		public float perspectiveFOV = 130;
		[MarshalAs(UnmanagedType.U1)] public bool useDynamicQuality = false;
		public Int32 bandwidthCalculationInterval = 5000;

		[Header("Audio")]
		[MarshalAs(UnmanagedType.U1)] public bool isStreamingAudio = true;
		[MarshalAs(UnmanagedType.U1)] public bool isReceivingAudio = false;

		[Header("Debugging")]
		public Int32 debugStream = 0;
		[MarshalAs(UnmanagedType.U1)] public bool debugNetworkPackets = false;
		[MarshalAs(UnmanagedType.U1)] public bool debugControlPackets = false;
		[MarshalAs(UnmanagedType.U1)] private bool willCacheReset = false;
		[MarshalAs(UnmanagedType.U1)] public bool pipeDllOutputToUnity = false;
		public byte estimatedDecodingFrequency = 10; //An estimate of how frequently the client will decode the packets sent to it; used by throttling.
		
		[Header("Streamed Textures")]
		public Int32 maximumTextureSize= 1024;
		[MarshalAs(UnmanagedType.U1)] public bool useCompressedTextures = true;
		public byte qualityLevel = 1;
		public byte compressionLevel = 1;

		[Header("Camera")]
		[MarshalAs(UnmanagedType.U1)] public bool willDisableMainCamera = false;
		[NonSerialized]
		[MarshalAs(UnmanagedType.U1)] public byte axesStandard = 64|2|4;

		[Header("Lighting")]
		public Int32 defaultSpecularCubemapSize = 64;
		public Int32 defaultSpecularMips = 7;
		public Int32 defaultDiffuseCubemapSize = 64;
		public Int32 defaultLightCubemapSize = 64;
		public Int32 defaultShadowmapSize = 0;
	}
	/// <summary>
	/// A control definition to send to the client.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
	public class ClientControl
	{
		//! path with wildcards to match the client's OpenXR component path.
		public string path;
	}
	/// <summary>
	/// Settings specific to a given client, as decided engine-side.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
	public class ClientSettings
	{
		public Vector2Int videoTextureSize;
		public Vector2Int shadowmapPos;
		public Int32 shadowmapSize;
		public Vector2Int webcamPos;
		public Vector2Int webcamSize; 
		public Int32 captureCubeTextureSize;
		public BackgroundMode backgroundMode;
		public Vector4 backgroundColour;
		public float drawDistance;
		public Int32 minimumNodePriority;
		public uid backgroundTexture;
	}
	/// <summary>
	/// Settings specific to a given client, as decided engine-side.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
	public class ClientDynamicLighting
	{
		public Vector2Int specularPos;
		public Int32 specularCubemapSize;
		public Int32 specularMips;
		public Vector2Int diffusePos;
		public Int32 diffuseCubemapSize;
		public Vector2Int lightPos;
		public Int32 lightCubemapSize;
		public uid specularCubemapTexture=0;
		public uid diffuseCubemapTexture=0;
		public LightingMode lightingMode = LightingMode.TEXTURE;
	}

	/// <summary>
	/// Definition of an input that the client can send the server.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
    public class InputDefinition
	{
		/// <summary>
		/// The name used on this server to identify this control, usually describes its function in the app.
		/// </summary>
		public string name;
		/// <summary>
		/// The type of input - event or state, integer, float etc.
		/// </summary>
		public avs.InputType inputType;
		/// <summary>
		/// A full or partial OpenXR path. The client should try to match this path with one or more of its available inputs.
		/// </summary>
		public string controlPath;
    }
}
