﻿using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using System.Collections.Generic;
using uid = System.UInt64;
using UnityEngine.SceneManagement;
using static UnityEditor.Rendering.CameraUI;

#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Text.RegularExpressions;

namespace teleport
{
	public interface IStreamedGeometryManagement
	{
		public void UpdateStreamedGeometry(Teleport_SessionComponent session, ref List<teleport.StreamableRoot> gainedStreamables, ref List<teleport.StreamableRoot> lostStreamables, List<teleport.StreamableRoot> streamedHierarchies);
		public bool CheckRootCanStream(teleport.StreamableRoot r);
		public string GetLastWarning();
	}

	public struct SessionState
    {
		public UInt64 sessionId;
    };
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoad]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
#endif
	/// <summary>
	/// A singleton component which stores per-server-session state.
	/// </summary>
	[ExecuteInEditMode]
	public class Monitor : MonoBehaviour
	{
		private static bool initialised = false;
		private static teleport.Monitor instance; //There should only be one teleport.Monitor instance at a time.

		//StringBuilders used for constructing log messages from libavstream.
		private static StringBuilder logInfo = new StringBuilder();
		private static StringBuilder logWarning = new StringBuilder();
		private static StringBuilder logError = new StringBuilder();
		private static StringBuilder logCritical = new StringBuilder();

		#region DLLDelegates

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate bool OnClientStoppedRenderingNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate bool OnClientStartedRenderingNode(uid clientID, uid nodeID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetHeadPose(uid clientID, in avs.Pose newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnSetControllerPose(uid clientID, uid index, in avs.PoseDynamic newHeadPose);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInputState(uid clientID, in avs.InputState inputState, in IntPtr binaryStatesPtr, in IntPtr analogueStateasPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnNewInputEvents(uid clientID, UInt16 numbinaryEvents, UInt16 numAnalogueEvents, UInt16 numMotionEvents, in IntPtr binaryEventsPtr, in IntPtr analogueEventsPtr, in IntPtr motionEventsPtr);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnDisconnect(uid clientID);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		public delegate void OnMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData); 

		 [UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void ReportHandshakeFn(uid clientID, in avs.Handshake handshake);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate void OnAudioInputReceived(uid clientID, in IntPtr dataPtr, UInt64 dataSize);

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		delegate Int64 GetUnixTimestampFn();

		#endregion
		public static OnMessageHandler editorMessageHandler=null;
		#region DLLImport
		struct InitialiseState
		{
			public string clientIP;
			public string httpMountDirectory;
			public string certPath;
			public string privateKeyPath;
			public string signalingPorts;

			public OnClientStoppedRenderingNode clientStoppedRenderingNode;
			public OnClientStartedRenderingNode clientStartedRenderingNode;
			public OnSetHeadPose headPoseSetter;
			public OnSetControllerPose controllerPoseSetter;
			public OnNewInputState newInputStateProcessing;
			public OnNewInputEvents newInputEventsProcessing;
			public OnDisconnect disconnect;
			public OnMessageHandler messageHandler;
			public ReportHandshakeFn reportHandshake;
			public OnAudioInputReceived audioInputReceived;
			public GetUnixTimestampFn getUnixTimestamp;
			public Int64 startUnixTimeUs;
		};

		[DllImport(TeleportServerDll.name)]
		static extern void Server_SetMessageHandlerDelegate(OnMessageHandler m);
		
		[DllImport(TeleportServerDll.name)]
		public static extern UInt64 Server_SizeOf(string name);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_Teleport_Initialize(InitialiseState initialiseState);
		[DllImport(TeleportServerDll.name)]
		public static extern bool Server_Teleport_GetSessionState( ref teleport.SessionState sessionState);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_SetConnectionTimeout(Int32 timeout);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_UpdateServerSettings(teleport.ServerSettings newSettings);

		[DllImport(TeleportServerDll.name)]
		public static extern bool Server_UpdateNodeTransform(uid id,avs.Transform tr);
		
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_SetClientPosition(uid clientID, Vector3 pos);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_Tick(float deltaTime);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_EditorTick();
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_Teleport_Shutdown();
		[DllImport(TeleportServerDll.name)]
		private static extern uid Server_GetUnlinkedClientID();

		// Really basic "send it again" function. Sends to all relevant clients. Must improve!
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_ResendNode(uid id);
		#endregion


		[Header("Background")]
		public BackgroundMode backgroundMode = BackgroundMode.COLOUR;
		public LightingMode lightingMode=LightingMode.TEXTURE;
		[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4)]
		public Color BackgroundColour;

		public void onSpecularRenderTextureChange()
        {
			
        }
		public RenderTexture environmentRenderTexture;
	
		public string environmentTextureResourcePath;
		[Tooltip("A cubemap rendertarget. This will be generated from the 'Environment Cubemap' and used for lighting dynamic objects.")]
		//! This will point to a saved asset texture.
		public RenderTexture specularRenderTexture;
		public string specularTextureResourcePath;
		[Tooltip("A cubemap rendertarget. This will be generated from the 'Environment Cubemap' and used for lighting dynamic objects.")]
		//! This will point to a saved asset texture.
		public RenderTexture diffuseRenderTexture;
		public string diffuseTextureResourcePath;
		[Tooltip("Multiplier for generating the specular render texture from the environment cubemap.")]
		public float specularMultiplier=1.0F;
#if UNITY_EDITOR
		//! For generating static cubemaps in the Editor.
		RenderTexture dummyRenderTexture;
		[HideInInspector]
		public Camera dummyCam = null;
		public int envMapSize=64;
        [NonSerialized]
        public bool generateEnvMaps=false;
        [NonSerialized]
        public bool envMapsGenerated = false;
#endif
        //! Create a new session, e.g. when a client connects.
        public delegate Teleport_SessionComponent CreateSession(uid client_id);
		public CreateSession createSessionCallback = DefaultCreateSession;

		[Tooltip("Choose the prefab to be used when a player connects, to represent that player's position and shape.")]
		public GameObject defaultPlayerPrefab;

		private GUIStyle overlayFont = new GUIStyle();
		private GUIStyle clientFont = new GUIStyle();

		private string title = "Teleport";

		[HideInInspector]
		public Teleport_AudioCaptureComponent audioCapture = null;

		public Cubemap environmentCubemap=null;
#if UNITY_EDITOR
		static Monitor()
		{
			UnityEditor.EditorApplication.update += Server_EditorTick;
		}
#endif

		public void SetCreateSessionCallback(CreateSession callback)
		{
			createSessionCallback = callback;
		}
		public static Monitor Instance
		{
			get
			{
				// We only want one instance, so delete duplicates.
				if (instance == null)
				{
					for (int i = 0; i < SceneManager.sceneCount; i++)
					{
						var objs=SceneManager.GetSceneAt(i).GetRootGameObjects();
						foreach(var o in objs)
						{
							var m=o.GetComponentInChildren<teleport.Monitor>();
							if(m)
							{
								instance=m;
								return instance;
							}
						}
					}
					instance = FindObjectOfType<teleport.Monitor>();
					if(instance==null)
					{
						var tempObject= new GameObject("Monitor");
						//Add Components
						tempObject.AddComponent<teleport.Monitor>();
						instance = tempObject.GetComponent<teleport.Monitor>();
					}
				}
				return instance;
			}
		}

		static Int64 startUnixTimeUs = 0;
		static Int64 sessionTimeUs = 0;
		static public Int64 GetSessionTimestampNowUs()
		{
			sessionTimeUs= GetUnixTimestampNowUs()- startUnixTimeUs;
			return sessionTimeUs;
		}
		static public double GetSessionTimestampNowS()
		{
			double sessionTimeS = (double)(GetSessionTimestampNowUs())/1000000.0;
			return sessionTimeS;
		}
		static public Int64 GetUnixTimestampNowUs()
		{
			return (Int64)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()*1000.0);
		}
		static public Int64 GetUnixStartTimestampUs()
		{
			return startUnixTimeUs;
		}


		static float lastFixedTime=0.0f;
		public static Int64 GetServerTimeUs()
        {
			if (lastFixedTime != Time.fixedTime)
            {
				server_unix_time_us = GetSessionTimestampNowUs();
				lastFixedTime = Time.fixedTime;
			}
			return server_unix_time_us;
		}
		static Int64 server_unix_time_us=0;
		void FixedUpdate()
        {
			
		}
		///MONOBEHAVIOUR FUNCTIONS

		private void Awake()
		{
			var g = GeometrySource.GetGeometrySource();
			if (g == null)
				return;
#if UNITY_EDITOR
			if (!UnityEditor.EditorApplication.isPlaying)
				return;
#endif
			// Make sure we have a Teleport Render Pipeline, or we won't get a video stream.
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType() != typeof(TeleportRenderPipelineAsset))
			{
#if UNITY_EDITOR
				//if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)	
				UnityEditor.EditorUtility.DisplayDialog("Warning", "Current rendering pipeline is "+ (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().ToString():"null") + ", not TeleportRenderPipeline.", "OK");
				UnityEditor.EditorApplication.isPlaying = false;
				return;
#else
				title += ": Current rendering pipeline is not TeleportRenderPipeline!";
				Debug.LogError(title);
#endif
			}
			if (g.CheckForErrors() == false)
			{
#if UNITY_EDITOR
				Debug.LogError("GeometrySource.CheckForErrors() failed. Run will not proceed.");
				UnityEditor.EditorUtility.DisplayDialog("Warning", "This scene has errors.", "OK");
				UnityEditor.EditorApplication.isPlaying = false;
				return;
#else
				Debug.LogError("GeometrySource.CheckForErrors() failed. Please check the log.");
#endif
			}

			overlayFont.normal.textColor = Color.yellow;
			overlayFont.fontSize = 14;
			clientFont.fontSize = 14;
			clientFont.normal.textColor = Color.white;

			// Make sure we have a Teleport Render Pipeline, or we won't get a video stream.
			if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType() != typeof(TeleportRenderPipelineAsset))
			{
				title += ": Current rendering pipeline is not TeleportRenderPipeline!";
				Debug.LogError(title);
			}
			
			//We need to add the animation events on play, so we can detect when an animation starts.
			GeometrySource.GetGeometrySource().AddAnimationEventHooks();
		}
		private void OnEnable()
		{
			var mgmt = GetComponent<IStreamedGeometryManagement>();
			if(mgmt == null)
			{
			}
			startUnixTimeUs = GetUnixTimestampNowUs();
			ulong unmanagedSize = Server_SizeOf("ServerSettings");
			ulong managedSize = (ulong)Marshal.SizeOf(typeof(teleport.ServerSettings));
		
			if (managedSize != unmanagedSize)
			{
				Debug.LogError($"teleport.Monitor failed to initialise! {nameof(teleport.ServerSettings)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!\n"
					+$"This usually means that your TeleportServer.dll (or .so) is out of sync with the Unity plugin C# code.\n" +
					$"One or both of these needs to be updated.");
				return;
			}
			if (instance == null)
				instance = this;
			if (instance != this)
			{
				Debug.LogError($"More than one instance of singleton teleport.Monitor.");
				return;
			}
			SceneManager.sceneLoaded += OnSceneLoaded;
			if (!Application.isPlaying)
				return;

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			Server_UpdateServerSettings(teleportSettings.serverSettings);
			InitialiseState initialiseState = new InitialiseState
			{
				clientStoppedRenderingNode = ClientStoppedRenderingNode,
				clientStartedRenderingNode = ClientStartedRenderingNode,
				headPoseSetter = Teleport_SessionComponent.StaticSetHeadPose,
				controllerPoseSetter = Teleport_SessionComponent.StaticSetControllerPose,
				newInputStateProcessing = Teleport_SessionComponent.StaticProcessInputState,
				newInputEventsProcessing = Teleport_SessionComponent.StaticProcessInputEvents,
				disconnect = Teleport_SessionComponent.StaticDisconnect,
				messageHandler = teleportSettings.serverSettings.pipeDllOutputToUnity ? LogMessageHandler : (OnMessageHandler)null,
				signalingPorts = teleportSettings.signalingPorts,
				reportHandshake = ReportHandshake,
				audioInputReceived = Teleport_SessionComponent.StaticProcessAudioInput,
				getUnixTimestamp = GetUnixTimestampNowUs,
				httpMountDirectory = teleportSettings.cachePath,
				clientIP = teleportSettings.clientIP,
				certPath = teleportSettings.certPath,
				privateKeyPath = teleportSettings.privateKeyPath,
				startUnixTimeUs= startUnixTimeUs
			};

			initialised = Server_Teleport_Initialize(initialiseState);
			if(!initialised)
			{
				Debug.LogError($"Teleport_Initialize failed, so server cannot start.");
			}
			if (!GeometrySource.GetGeometrySource().SetHttpRoot(teleportSettings.httpRoot))
			{
				
			}
			// Sets connection timeouts for peers (milliseconds)
			Server_SetConnectionTimeout(teleportSettings.connectionTimeout);

			TeleportSettings settings = TeleportSettings.GetOrCreateSettings();
			// Create audio component
			var audioCaptures = FindObjectsOfType<Teleport_AudioCaptureComponent>();
			if (audioCaptures.Length > 0)
			{
				audioCapture = audioCaptures[0];
			}
			else
			{
				GameObject go = new GameObject("TeleportAudioCapture");
				go.AddComponent<Teleport_AudioCaptureComponent>();
				audioCapture = go.GetComponent<Teleport_AudioCaptureComponent>();
			}

			if(!settings.serverSettings.isStreamingAudio)
			{
				audioCapture.gameObject.SetActive(false);
				// Setting active to false on game obect does not disable audio listener or capture component
				// so they must be disabled directly.
				audioCapture.enabled = false;
				var audioListener=audioCapture.GetComponent<AudioListener>();
				if(audioListener)
					audioListener.enabled = false;
			}
		}

		private void OnDisable()
		{
			Server_SetMessageHandlerDelegate(null);
#if UNITY_EDITOR
			if (dummyCam)
			{
				//DestroyImmediate(dummyCam);
			}
#endif
			SceneManager.sceneLoaded -= OnSceneLoaded;
			if(Application.isPlaying)
				Server_Teleport_Shutdown();
		}
		
		static public void OverrideRenderingLayerMask(GameObject gameObject, uint mask,bool recursive=false)
		{
			Renderer[] renderers;
			if(recursive)
				renderers= gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask= mask;
			}
		}

		static public void SetRenderingLayerMask(GameObject gameObject, uint mask, bool recursive = false)
		{
			Renderer[] renderers;
			if (recursive)
				renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				renderer.renderingLayerMask |= mask;
			}
		}

		static public void UnsetRenderingLayerMask(GameObject gameObject, uint mask, bool recursive = false)
		{
			uint inverse_mask=~mask;
			Renderer[] renderers;
			if (recursive)
				renderers = gameObject.GetComponentsInChildren<Renderer>(true);
			else
				renderers = gameObject.GetComponents<Renderer>();
			foreach (Renderer renderer in renderers)
			{
				// Previously we &'d with the existing mask, but that causes bad behaviour if the mask is left in the wrong state and the object is saved.
				renderer.renderingLayerMask &=  inverse_mask;
			}
		}

		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			// If there's a taghandler, call its OnSceneLoaded.
			if(TagHandler.Instance)
				TagHandler.Instance.OnSceneLoaded( scene,  mode);
			// clear masks corresponding to streamed objects.
			int clientLayer = 25;
			uint streamedClientMask = (uint)(((int)1) << (clientLayer + 1));
			uint invStreamedMask = ~streamedClientMask;

			GameObject[] rootGameObjects = scene.GetRootGameObjects();
			foreach(GameObject gameObject in rootGameObjects)
			{
				SetRenderingLayerMask(gameObject, invStreamedMask);
			}
			// Reset the "origin" for all sessions on the assumption we have changed level.
			foreach(Teleport_SessionComponent sessionComponent in Teleport_SessionComponent.sessions.Values)
			{
				sessionComponent.ResetOrigin();
			}
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			// Ensure that the RootStreamables are added.
			var g1=scene.GetRootGameObjects();
			var mgmt = GetComponent<IStreamedGeometryManagement>();
			foreach (GameObject gameObject in g1)
			{
				var g2=gameObject.GetComponentsInChildren<StreamableRoot>();
				foreach(StreamableRoot r in g2)
				{
					if (r.priority < teleportSettings.defaultMinimumNodePriority)
						continue;
					if (mgmt!= null)
					{
						if(!mgmt.CheckRootCanStream(r))
						{
							Debug.LogWarning(mgmt.GetLastWarning(), r.gameObject);
						}
					}
					// Specify ForceExtractionMask.FORCE_NODES so that new node properties are updated.
					GeometrySource.GetGeometrySource().AddNode(r.gameObject, GeometrySource.ForceExtractionMask.FORCE_NODES);
				}
			}
		}

		private void Update()
		{
			if(initialised&&Application.isPlaying)
			{
				Server_Tick(Time.deltaTime);
				CheckForClients();
			}
#if UNITY_EDITOR
			if (generateEnvMaps)
			{
				GenerateEnvMaps();
				generateEnvMaps=false;
				dummyCam.enabled = true;
			}
#endif
		}
#if UNITY_EDITOR
		private void GenerateEnvMaps()
		{
			envMapsGenerated = false;
            // we will render this source cubemap into a target that has mips for roughness, and also into a diffuse cubemap.
            // We will save those two cubemaps to disk, and store them as the client dynamic lighting textures.

            if (dummyRenderTexture == null)
			{
				dummyRenderTexture = new RenderTexture(8, 8
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, 1);
			}
			if (dummyCam == null)
			{
				GameObject monitorObject = gameObject;
				monitorObject.TryGetComponent<Camera>(out dummyCam);
				if (dummyCam == null)
					dummyCam=monitorObject.AddComponent<Camera>();
				dummyCam.targetTexture= dummyRenderTexture;
				dummyCam.enabled = true;
			}
			int mips = 21;
			while (mips > 1 && ((1 << mips) > envMapSize))
			{
				mips--;
			}
			string scenePath = SceneManager.GetActiveScene().path;
			if(environmentRenderTexture)
			{
				string path = "";
				if (GeometrySource.GetResourcePath(environmentRenderTexture, out path, true))
				{
					if (path != "")
						environmentTextureResourcePath = path;
				}
			}
			int renderEnvMapSize=Math.Min(1024,environmentCubemap.width);
			// If environment rendertexture is unassigned or not the same size as the env cubemap, recreate it as a saved asset.
			if (environmentRenderTexture == null || environmentRenderTexture.width != renderEnvMapSize ||
				environmentRenderTexture.mipmapCount != mips)
			{
				environmentRenderTexture = new RenderTexture(renderEnvMapSize, renderEnvMapSize
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, mips);
				environmentRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				environmentRenderTexture.useMipMap = true;
				// We will generate the mips with shaders in the render call.
				environmentRenderTexture.autoGenerateMips = false;
				string assetPath = scenePath.Replace(".unity", "/environmentRenderTexture.renderTexture");
				string assetDirectory = scenePath.Replace(".unity", "");
				string parentDirectory = System.IO.Path.GetDirectoryName(scenePath);
				string subDirectory = System.IO.Path.GetFileNameWithoutExtension(scenePath);
				UnityEditor.AssetDatabase.CreateFolder(parentDirectory, subDirectory);
				UnityEditor.AssetDatabase.CreateAsset(environmentRenderTexture, assetPath);
			}
			if (specularRenderTexture)
			{
				string path = "";
				if (GeometrySource.GetResourcePath(specularRenderTexture, out path, true))
				{
					specularTextureResourcePath = path;
				}
			}
			// If specular rendertexture is unassigned or not the same size as the env cubemap, recreate it as a saved asset.
			if (specularRenderTexture == null || specularRenderTexture.width != envMapSize ||
				specularRenderTexture.mipmapCount != mips)
			{
				specularRenderTexture = new RenderTexture(envMapSize, envMapSize
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, mips);
				specularRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				specularRenderTexture.useMipMap = true;
				// We will generate the mips with shaders in the render call.
				specularRenderTexture.autoGenerateMips = false;
				string assetPath = scenePath.Replace(".unity", "/specularRenderTexture.renderTexture");
				string assetDirectory = scenePath.Replace(".unity", "");
				string parentDirectory=System.IO.Path.GetDirectoryName(scenePath);
				string subDirectory = System.IO.Path.GetFileNameWithoutExtension(scenePath);
				UnityEditor.AssetDatabase.CreateFolder(parentDirectory,subDirectory);
				UnityEditor.AssetDatabase.CreateAsset(specularRenderTexture, assetPath);
			}
			if (diffuseRenderTexture)
			{
				string path="";
				if (GeometrySource.GetResourcePath(diffuseRenderTexture, out path, true))
				{
					diffuseTextureResourcePath = path;
				}
			}
			// If diffuse rendertexture is unassigned or not the same size as the env cubemap, recreate it as a saved asset.
			if (diffuseRenderTexture == null || diffuseRenderTexture.width != envMapSize ||
				diffuseRenderTexture.mipmapCount != mips)
			{
				diffuseRenderTexture = new RenderTexture(envMapSize, envMapSize
					, 24, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_UNorm, mips);
				diffuseRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				diffuseRenderTexture.useMipMap = true;
				// We will generate the mips with shaders in the render call.
				diffuseRenderTexture.autoGenerateMips = true;
				string assetPath = scenePath.Replace(".unity", "/diffuseRenderTexture.renderTexture");
				UnityEditor.AssetDatabase.CreateAsset(diffuseRenderTexture, assetPath);
			}
			dummyCam.Render();
			EditorUtility.SetDirty(this);
		}
#endif
		private void OnGUI()
		{
			if ( Application.isPlaying)
			{
				int x = 10;
				int y = 0;
				GUI.Label(new Rect(x, y, 100, 20), title, overlayFont);
		
				GUI.Label(new Rect(x,y+=14, 100, 20), string.Format("Accepting signals on ports: {0}", TeleportSettings.GetOrCreateSettings().signalingPorts), overlayFont);
				foreach(var s in Teleport_SessionComponent.sessions)
				{
					s.Value.ShowOverlay(x,y, clientFont);
				}
			}
		}

		private void OnValidate()
		{
			if(Application.isPlaying)
			{
				TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
				Server_UpdateServerSettings(teleportSettings.serverSettings);
			}
		}

		private void CheckForClients()
		{
			uid id = Server_GetUnlinkedClientID();
			if (id == 0)
			{
				return;
			}
			Teleport_SessionComponent session;
			if (Teleport_SessionComponent.sessions.ContainsKey(id))
			{
				Debug.LogWarning($"Setting up SessionComponent for Client {id}. There is already a registered session for that client!");
				session= Teleport_SessionComponent.sessions[id];
			}
			else
			{ 
				session = createSessionCallback(id);
			}
			if (session != null)
			{
				session.StartSession(id);
				var mgmt = GetComponent<IStreamedGeometryManagement>();
				if(session.GeometryStreamingService!=null)	
					session.GeometryStreamingService.streamedGeometryManagement = mgmt;
			}
		}

		public static Teleport_SessionComponent DefaultCreateSession(uid clientID)
		{
			string path = "";
			{
				StringBuilder pathStringBuilder = new StringBuilder("", 25);
				try
				{
					uint newlen = Teleport_SessionComponent.Client_GetSignalingPath(clientID, 25, pathStringBuilder);
					if (newlen > 0)
					{
						pathStringBuilder = new StringBuilder("", (int)newlen + 2);
						Teleport_SessionComponent.Client_GetSignalingPath(clientID, newlen + 1, pathStringBuilder);
					}
				}
				catch (Exception exc)
				{
					Debug.Log(exc.ToString());
				}
				path= pathStringBuilder.ToString();
			}
			var currentSessions = GameObject.FindObjectsByType<Teleport_SessionComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			// We want to use an existing session in the scene if it doesn't have a client.
			// This is useful if the session is placed in the scene instead of spawned.
			#if UNITY_EDITOR
			if (Instance.defaultPlayerPrefab!=null)
			{
				var prefabs = PrefabUtility.FindAllInstancesOfPrefab(Instance.defaultPlayerPrefab);
				currentSessions=new Teleport_SessionComponent[prefabs.Length];
				for(int i=0;i<prefabs.Length;i++)
				{
					currentSessions[i] = prefabs[i].GetComponentInChildren<Teleport_SessionComponent>();
				}
			}
			#endif
			foreach (var s in currentSessions)
			{
				if (!s.Spawned && (s.GetClientID() == 0|| s.GetClientID()==clientID))
				{
					AddMainCamToSession(s);
					return s;
				}
			}

			Teleport_SessionComponent session = null;
			if (Instance.defaultPlayerPrefab == null)
			{
				Instance.defaultPlayerPrefab = Resources.Load("Prefabs/DefaultUser") as GameObject;
			}
			if (Instance.defaultPlayerPrefab == null)
			{
				Debug.LogError("No player prefab available, set Monitor's Default Player Prefab.");
			}
			if (Instance.defaultPlayerPrefab != null)
			{
				var childComponents = Instance.defaultPlayerPrefab.GetComponentsInChildren<Teleport_SessionComponent>();

				if (childComponents.Length != 1)
				{
					Debug.LogError($"Exactly <b>one</b> {typeof(Teleport_SessionComponent).Name} child should exist, but <color=red><b>{childComponents.Length}</b></color> were found for \"{Instance.defaultPlayerPrefab}\"!");
					return null;
				}

				if (childComponents.Length == 0)
				{
					return null;
				}
				Vector3 SpawnPosition = new Vector3(0, 0, 0);
				Quaternion SpawnRotation=Quaternion.identity;
				bool got_metaurl=false;
				if (path.Length>0)
				{
					//string pattern  = @"\?meta_url=/(-?\d+(\.\d+){2,3}/)+$";
					string pattern = @"meta_url=(\d+\,\d+\,\d+)(/\d+\,\d+\,\d+)+";// (\.\d+){2}  (/\d+(\.\d+){2})+";
					Match m = Regex.Match(path, pattern, RegexOptions.IgnoreCase);
					if (m.Success)
					{
						MetaURLScene metaURLScene = MetaURLScene.GetInstance();
						if (metaURLScene == null)
						{
							UnityEngine.Debug.LogError("No MetaURLScene found, but meta_url was specified in the URL.");
						}
						else
						{
							SpawnPosition = metaURLScene.origin;
						float scale= metaURLScene.scaleMetres;
						for (int i=1;i<m.Groups.Count;i++)
						{
							string subgroup=m.Groups[i].Value;
							string subpattern = @"(\d+),(\d+),(\d+)";
							Match submatch = Regex.Match(subgroup, subpattern, RegexOptions.IgnoreCase);
							if(submatch.Success&&submatch.Groups.Count==4) {
								int x= Int32.Parse(submatch.Groups[1].Value);
								int y = Int32.Parse(submatch.Groups[2].Value);
								int z = Int32.Parse(submatch.Groups[3].Value);
								Vector3 addPosition=new Vector3((float)x*scale/100.0F, (float)y * scale / 100.0F, (float)z * scale / 100.0F);
								SpawnPosition+=addPosition;
								scale/=100.0F;
								}
							}
						}
						Debug.Log("Found MetaURL");
						got_metaurl=true;
					}
				}
				if (!got_metaurl)
				{
					var spawners=FindObjectsOfType<teleport.Spawner>();
					if(spawners.Length>0)
					{
						var spawner= spawners[0];
						// If the spawner fails, we can't initialize a session.
						if (!spawner.Spawn(out SpawnPosition, out SpawnRotation))
						{
							Debug.LogError($"spawner.Spawn failed.");
							return null;
						}
					}
				}
				GameObject player = Instantiate(Instance.defaultPlayerPrefab, SpawnPosition, SpawnRotation);
				teleport.StreamableRoot rootStreamable=player.GetComponent<teleport.StreamableRoot>();
				if(rootStreamable==null)
					rootStreamable=player.AddComponent<teleport.StreamableRoot>();
				rootStreamable.ForceInit();
				player.name = "TeleportVR_" +Instance.defaultPlayerPrefab.name+"_"+ Teleport_SessionComponent.sessions.Count + 1;

				session = player.GetComponentsInChildren<Teleport_SessionComponent>()[0];
				session.Spawned = true;

				AddMainCamToSession(session);
			}

			return session;
		}
		public static void DefaultRemoveSession(Teleport_SessionComponent sess)
		{

		}

		static void AddMainCamToSession(Teleport_SessionComponent session)
		{
			if (session.head != null && Camera.main != null && Teleport_SessionComponent.sessions.Count == 0)
			{
				Camera.main.transform.parent = session.head.transform;
				Camera.main.transform.localRotation = Quaternion.identity;
				Camera.main.transform.localPosition = Vector3.zero;
			}
		}

		///DLL CALLBACK FUNCTIONS
		[return: MarshalAs(UnmanagedType.U1)]
		private static bool ClientStoppedRenderingNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj)
			{
				// Possibly node was already deleted.
				return false;
			}

			if(obj.GetType() != typeof(GameObject))
			{
				Debug.LogWarning($"Failed to show node! Resource found for ID {nodeID} was of type {obj.GetType().Name}, when we require a {nameof(GameObject)}!");
				return false;
			}

			GameObject gameObject = (GameObject)obj;

			if(!gameObject.TryGetComponent(out teleport.StreamableRoot streamable))
			{
				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will show the node.
				/*
				Debug.LogWarning($"Failed to show node! \"{gameObject}\" does not have a {nameof(teleport.StreamableRoot)} component!");
				return false;
				*/

				return true;
			}

			streamable.ShowHierarchy();

			return true;
		}

		[return: MarshalAs(UnmanagedType.U1)]
		private static bool ClientStartedRenderingNode(uid clientID, uid nodeID)
		{
			UnityEngine.Object obj = GeometrySource.GetGeometrySource().FindResource(nodeID);
			if(!obj)
			{
				Debug.LogWarning($"Failed to hide node! Could not find a resource with ID {nodeID}!");
				return false;
			}

			if(obj.GetType() != typeof(GameObject))
			{
				Debug.LogWarning($"Failed to hide node! Resource found for ID {nodeID} was of type {obj.GetType().Name}, when we require a {nameof(GameObject)}!");
				return false;
			}

			GameObject gameObject = (GameObject)obj;

			if(!gameObject.TryGetComponent(out teleport.StreamableRoot streamable))
			{
				//We still succeeded in ensuring the GameObject was in the correct state; the hierarchy root will hide the node.
				/*
				Debug.LogWarning($"Failed to hide node! \"{gameObject}\" does not have a {nameof(teleport.StreamableRoot)} component!");
				return false;
				*/
				return true;
			}

			streamable.HideHierarchy();

			return true;
		}

		private static void ReportHandshake(uid clientID,in avs.Handshake handshake)
		{
			var session=Teleport_SessionComponent.sessions[clientID];
			if(session!=null)
			{
				session.ReportHandshake(handshake);
			}
		}

		private static void LogMessageHandler(avs.LogSeverity Severity, string Msg, in IntPtr userData)
		{
			if(Msg.Length<1)
				return;
			if(editorMessageHandler!=null)
				editorMessageHandler(Severity,Msg,userData);
#if UNITY_EDITOR
#else
			switch(Severity)
			{
				case avs.LogSeverity.Debug:
				case avs.LogSeverity.Info:
					logInfo.Append(Msg);

					if(logInfo[logInfo.Length - 1] == '\n')
					{
						Debug.Log(logInfo.ToString());
						logInfo.Clear();
					}

					break;
				case avs.LogSeverity.Warning:
					logWarning.Append(Msg);

					if(logWarning[logWarning.Length - 1] == '\n')
					{
						Debug.LogWarning(logWarning.ToString());
						logWarning.Clear();
					}

					break;
				case avs.LogSeverity.Error:
					logError.Append(Msg);

					if(logError[logError.Length - 1] == '\n')
					{
						Debug.LogError(logError.ToString());
						logError.Clear();
					}

					break;
				case avs.LogSeverity.Critical:
					logCritical.Append(Msg);

					if(logCritical[logCritical.Length - 1] == '\n')
					{
						Debug.LogAssertion(logCritical.ToString());
						logCritical.Clear();
					}

					break;
			}
#endif
		}
		public void ReparentNode(GameObject child, GameObject newParent, Vector3 relativePos, Quaternion relativeRot,bool keepWorldPos)
		{
			GameObject oldParent = child.transform.parent != null ? child.transform.parent.gameObject : null;
			if (newParent != null)
				child.transform.SetParent(newParent.transform, keepWorldPos);
			else
				child.transform.SetParent(null, keepWorldPos);
			if(!keepWorldPos)
			{
				child.transform.localPosition = relativePos;
				child.transform.localRotation= relativeRot;
			}
			teleport.StreamableRoot teleport_Streamable = child.GetComponent<teleport.StreamableRoot>();
			if(!teleport_Streamable)
            {
				Debug.LogError("Reparenting a child that has no teleport_Streamable");
            }
			// Is the new parent owned by a client? If so inform clients of this change:
			Teleport_SessionComponent oldSession=null;
			Teleport_SessionComponent newSession = null;
			teleport.StreamableRoot newParentStreamable=null;
			teleport.StreamableRoot oldParentStreamable=null;
			if (newParent != null)
			{
				// Is the new parent owned by a client? If so inform clients of this change:
				newParentStreamable = newParent.GetComponentInParent<teleport.StreamableRoot>();
				if (newParentStreamable != null && newParentStreamable.OwnerClient != 0)
				{
					newSession = Teleport_SessionComponent.GetSessionComponent(newParentStreamable.OwnerClient);
				}
			}
			if (newSession!=null)
			{
				teleport_Streamable.OwnerClient = newSession.GetClientID();
			}
			else
				teleport_Streamable.OwnerClient = 0;
			if (oldParent != null)
			{
				oldParentStreamable = oldParent.GetComponentInParent<teleport.StreamableRoot>();
				if (oldParentStreamable != null&&oldParentStreamable.OwnerClient != 0)
				{
					oldSession = Teleport_SessionComponent.GetSessionComponent(oldParentStreamable.OwnerClient);
				}
			}
			if (oldSession)
			{
				oldSession.GeometryStreamingService.ReparentNode(child, newParent, relativePos, relativeRot);
			}
			if (newSession)
			{
				newSession.GeometryStreamingService.ReparentNode(child, newParent, relativePos, relativeRot);
			}
			//teleport_Streamable.stageSpaceVelocity=new Vector3(0,0,0);
			//teleport_Streamable.stageSpaceAngularVelocity = new Vector3(0, 0, 0);
		}

		public void ComponentChanged(MonoBehaviour component)
		{
			if (!Application.isPlaying)
				return;
			teleport.StreamableRoot streamable=component.gameObject.GetComponentInParent<teleport.StreamableRoot>();
			if(streamable)
			{
				uid u=streamable.GetUid();
				if(u!=0)
					Server_ResendNode(u);
			}
		}
	}
}