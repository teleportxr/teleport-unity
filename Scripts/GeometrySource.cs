﻿//Standard Shader Inspector Code:
//https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/StandardShaderGUI.cs

using avs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
//using static RootMotion.BipedNaming;
using static teleport.GeometrySource;
using static UnityEngine.Rendering.DebugUI;
using uid = System.UInt64;

namespace avs
{
	public enum NodeDataType : byte
	{
		Invalid,
		None,
		Mesh,
		Light,
		TextCanvas,
		SubScene,
		Skeleton,
		Link
	};

	public enum PrimitiveMode : UInt32
	{
		POINTS, LINES, TRIANGLES, LINE_STRIP, TRIANGLE_STRIP
	};

	//! The standard glTF attribute semantics.
	public enum AttributeSemantic : UInt32
	{
		//Name				Accessor Type(s)	Component Type(s)					Description
		POSITION,			//"VEC3"			5126 (FLOAT)						XYZ vertex positions
		NORMAL,				//"VEC3"			5126 (FLOAT)						Normalized XYZ vertex normals
		TANGENT,			//"VEC4"			5126 (FLOAT)						XYZW vertex tangents where the w component is a sign value(-1 or +1) indicating handedness of the tangent basis
		TEXCOORD_0,			//"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the first set
		TEXCOORD_1,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		TEXCOORD_2,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		TEXCOORD_3,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		TEXCOORD_4,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		TEXCOORD_5,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		TEXCOORD_6,         //"VEC2"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	UV texture coordinates for the second set
		COLOR_0,			//"VEC3"
							//"VEC4"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized	RGB or RGBA vertex color
		JOINTS_0,			//"VEC4"			5121 (UNSIGNED_BYTE)
							//					5123 (UNSIGNED_SHORT)				See Skinned Mesh Attributes
		WEIGHTS_0,			//"VEC4"			5126 (FLOAT)
							//					5121 (UNSIGNED_BYTE) normalized
							//					5123 (UNSIGNED_SHORT) normalized
		TANGENTNORMALXZ,	//"VEC2"			UNSIGNED_INT						Simul: implements packed tangent-normal xz. Actually two VEC4's of BYTE.
							//					SIGNED_SHORT
		COUNT				//														This is the number of elements in enum class AttributeSemantic{};
							//														Must always be the last element in this enum class. 
	};

	public enum MaterialExtensionIdentifier : UInt32
	{
		SIMPLE_GRASS_WIND
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Attribute
	{
		public AttributeSemantic semantic;
		public UInt64 accessor;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct PrimitiveArray
	{
		public UInt64 attributeCount;
		public Attribute[] attributes;
		public UInt64 indices_accessor;
		public uid material;
		public PrimitiveMode primitiveMode;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Accessor
	{
		public enum DataType : UInt32
		{
			SCALAR=1,
			VEC2,
			VEC3,
			VEC4,
			MAT4
		};
		public enum ComponentType : UInt32
		{
			FLOAT,
			DOUBLE,
			HALF,
			UINT,
			USHORT,
			UBYTE,
			INT,
			SHORT,
			BYTE
		};
		public DataType type;
		public ComponentType componentType;
		public UInt64 count;
		public UInt64 bufferView;
		public UInt64 byteOffset;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct BufferView
	{
		public UInt64 buffer;
		public UInt64 byteOffset;
		public UInt64 byteLength;
		public UInt64 byteStride;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct GeometryBuffer
	{
		public UInt64 byteLength;
		public byte[] data;
	};

	public struct MaterialExtension
	{

	}

	//Just copied the Unreal texture formats, this will likely need changing.
	public enum TextureFormat : UInt32
	{
		INVALID,
		G8,
		BGRA8,
		BGRE8,
		RGBA16,
		RGBA16F,
		RGBA8,
		RGBE8,
		D16F,
		D24F,
		D32F,
		RGBAFloat,
		MAX
	}

	public enum TextureCompression: UInt32
	{
		UNCOMPRESSED = 0,
		BASIS_COMPRESSED_DEPRECATED,
		PNG,
		KTX
	}
	public enum RoughnessMode : byte
	{
		CONSTANT = 0,
		MULTIPLY_SMOOTHNESS,
		MULTIPLY_ROUGHNESS
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class TextureAccessor
	{
		public uid index = 0;
		public byte texCoord = 0;

		public Vector2 tiling = new Vector2(1.0f, 1.0f);
		public float strength = 1.0f;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class PBRMetallicRoughness
	{
		public TextureAccessor baseColorTexture = new TextureAccessor();
		public Vector4 baseColorFactor = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

		public TextureAccessor metallicRoughnessTexture = new TextureAccessor();
		public float metallicFactor = 1.0f;
		public float roughOrSmoothMultiplier=-1.0f;
		public float roughOffset = 1.0f;
	}

	/// <summary>
	/// Node properties for sending from C# to C++ dll.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Node
	{
		public IntPtr name;

		public Transform localTransform;

		[MarshalAs(UnmanagedType.I1)]
		public bool stationary;
		public uid ownerClientId;
		public NodeDataType dataType;
		public uid parentID;
		public uid dataID;
		public uid skeletonNodeID;	// The node whose dataID is the skeleton asset.

		public Vector4 lightColour;
		public Vector3 lightDirection;
		public float lightRadius;
		public float lightRange;
		public byte lightType;

		public UInt64 numJoints;
		public Int32[] jointIndices;

		public UInt64 numAnimations;
		public uid[] animationIDs;

		public UInt64 numMaterials;
		public uid[] materialIDs;

		public NodeRenderState renderState;

		public int priority;

		public IntPtr url;
		public IntPtr query_url;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Node2
	{
		public IntPtr name;

		public Transform localTransform;
		//public Transform globalTransform;

		[MarshalAs(UnmanagedType.I1)]
		public bool stationary;
		public uid ownerClientId;
		public NodeDataType dataType;
		public uid parentID;
		public uid dataID;
		public uid skeletonNodeID;  // The node whose dataID is the skeleton asset.

		public Vector4 lightColour;
		public Vector3 lightDirection;
		public float lightRadius;
		public float lightRange;
		public byte lightType;

		public UInt64 numJoints;
		public Int32[] jointIndices;

		public UInt64 numAnimations;
		public uid[] animationIDs;

		public UInt64 numMaterials;
		public uid[] materialIDs;

		public NodeRenderState renderState;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class Mesh
	{
		public IntPtr name;
		
		public IntPtr path;

		public Int64 numPrimitiveArrays;
		public PrimitiveArray[] primitiveArrays;

		public Int64 numAccessors;
		public uid[] accessorIDs;
		public Accessor[] accessors;

		public Int64 numBufferViews;
		public uid[] bufferViewIDs;
		public BufferView[] bufferViews;

		public Int64 numBuffers;
		public uid[] bufferIDs;
		public GeometryBuffer[] buffers;

		//The number of inverseBindMatrices MUST be greater than or equal to the number of joints referenced in the vertices.
		public uid inverseBindMatricesAccessor;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Skeleton
	{
		public IntPtr name;
		
		public IntPtr path;

		public Int64 numBones;
		public uid[] boneIDs;

		public avs.Transform rootTransform;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Texture
	{
		public IntPtr name;
		
		public IntPtr path;

		public uint width;
		public uint height;
		public uint depth;
		public uint arrayCount;
		public uint mipCount;

		[MarshalAs(UnmanagedType.U4)]
		public TextureFormat format;
		[MarshalAs(UnmanagedType.U4)]
		public TextureCompression compression;
		[MarshalAs(UnmanagedType.I1)]
		public bool compressed;

		public uint dataSize;
		public IntPtr data;

		public float valueScale;

		[MarshalAs(UnmanagedType.I1)]
		public bool cubemap;
	}


	public enum MaterialMode : byte
	{
		UNKNOWN=0,
		OPAQUE,
		TRANSPARENT
	};
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class Material
	{
		public IntPtr name;

		public IntPtr path;
		[MarshalAs(UnmanagedType.I8)]
		public MaterialMode materialMode = MaterialMode.UNKNOWN;
		public PBRMetallicRoughness pbrMetallicRoughness = new PBRMetallicRoughness();
		public TextureAccessor normalTexture = new TextureAccessor();
		public TextureAccessor occlusionTexture = new TextureAccessor();
		public TextureAccessor emissiveTexture = new TextureAccessor();
		public Vector3 emissiveFactor = new Vector3(0.0f, 0.0f, 0.0f);
		[MarshalAs(UnmanagedType.I1)]
		public bool doubleSided = false;
		[MarshalAs(UnmanagedType.I1)]
		public byte lightmapTexCoordIndex = 0;
		public UInt64 extensionAmount;
		public MaterialExtensionIdentifier[] extensionIDs;
		public MaterialExtension[] extensions;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class Material1
	{
		public IntPtr name;
		
		public IntPtr path;
		[MarshalAs(UnmanagedType.I8)]
		public MaterialMode materialMode = MaterialMode.UNKNOWN;
		public PBRMetallicRoughness pbrMetallicRoughness = new PBRMetallicRoughness();
		public TextureAccessor normalTexture = new TextureAccessor();
		public TextureAccessor occlusionTexture = new TextureAccessor();
		public TextureAccessor emissiveTexture = new TextureAccessor();
		public Vector3 emissiveFactor = new Vector3(0.0f, 0.0f, 0.0f);
		[MarshalAs(UnmanagedType.I1)]
		public bool doubleSided = false;
		[MarshalAs(UnmanagedType.I1)]
		public byte lightmapTexCoordIndex = 0;
		public UInt64 extensionAmount;
		public MaterialExtensionIdentifier[] extensionIDs;
	}
}

namespace teleport
{
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct Glyph
	{
	   public UInt16 x0,y0,x1,y1; // coordinates of bounding box in bitmap
	   public float xOffset,yOffset,xAdvance;
	   public float xOffset2,yOffset2;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct InteropFontMap
	{
		public int size;
		public int numGlyphs;
		public Glyph [] fontGlyphs;
	}
	//! Struct to pass a font atlas back to the engine.
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class InteropFontAtlas
	{
		
		public IntPtr font_path;
		public int numMaps;
		public InteropFontMap [] fontMaps;
	};
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public class InteropTextCanvas
	{
		public IntPtr text;
		
		public IntPtr font;
		public int pointSize;
		public float lineHeight;
		public avs.Vector4 colour= new avs.Vector4(0.0f,0.0f,0.0f,0.0f);
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
	public struct TextureExtractionData
	{
		public uid id;
		public UnityEngine.Texture unityTexture;
		public avs.Texture textureData;
		public TextureConversion textureConversion;
		public UnityEngine.GameObject gameObject;	// owner, for fallback path.
	}
}

namespace teleport
{
	/// <summary>
	/// A singleton class obtained with GeometrySource.GetGeometrySource();
	/// </summary>
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class GeometrySource : ScriptableObject, ISerializationCallbackReceiver
	{
		//Meta data on a resource loaded from disk.
		private struct LoadedResource
		{
			public uid id;      // ID of the resource generated by the dll on load.
			public IntPtr name; // Name of asset that this resource relates to.
			public Int64 lastModified;
		}

		[Flags]
		public enum ForceExtractionMask
		{
			FORCE_NOTHING = 0,

			FORCE_NODES = 1,
			FORCE_HIERARCHIES = 2,
			FORCE_SUBRESOURCES = 4,
			
			FORCE_TEXTURES = 16,

			FORCE_EVERYTHING = -1,

			//Combinations
			FORCE_NODES_AND_HIERARCHIES = FORCE_NODES | FORCE_HIERARCHIES,
			FORCE_NODES_HIERARCHIES_AND_SUBRESOURCES = FORCE_NODES | FORCE_HIERARCHIES | FORCE_SUBRESOURCES | FORCE_TEXTURES
		}
		[Flags]
		public enum TextureConversion
		{
			CONVERT_NOTHING = 0,
			CONVERT_TO_METALLICROUGHNESS_BLUEGREEN = 1
		}


		#region DLLImports
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_BadCall();

		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_SetCachePath(string name);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_SetHttpRoot(string h);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_DeleteUnmanagedArray(in IntPtr unmanagedArray);

		[DllImport(TeleportServerDll.name)]
		private static extern uid Server_GenerateUid();
		[DllImport(TeleportServerDll.name)]
		public static extern uid Server_GetOrGenerateUid(string path);
		[DllImport(TeleportServerDll.name)]
		public static extern uid Server_PathToUid(string path);
        [DllImport(TeleportServerDll.name)]
		public static extern UInt64 Server_UidToPath(uid u, StringBuilder path, UInt64 stringsize);
		[DllImport(TeleportServerDll.name)]
		private static extern UInt64 Server_EnsurePathResourceIsLoaded(string path);

		[DllImport(TeleportServerDll.name)]
		private static extern void Server_SaveGeometryStore();
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_LoadGeometryStore(out UInt64 meshAmount, out IntPtr loadedMeshes, out UInt64 textureAmount, out IntPtr loadedTextures, out UInt64 numMaterials, out IntPtr loadedMaterials);
	
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_ClearGeometryStore();
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_CheckGeometryStoreForErrors();

		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_StoreNode(uid id, avs.Node node);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_StoreSkeleton(uid id, avs.Skeleton skeleton);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_StoreMesh(uid id,
												string path,
												Int64 lastModified,
												[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(MeshMarshaler))] avs.Mesh mesh,
												 [MarshalAs(UnmanagedType.I1)] avs.AxesStandard extractToStandard,  [MarshalAs(UnmanagedType.I1)] bool verify);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_StoreMaterial(uid id, 
												string path, Int64 lastModified, avs.Material material);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_StoreTexture(uid id, 
												string path, Int64 lastModified, avs.Texture texture
												, [MarshalAs(UnmanagedType.I1)] bool genMips
												, [MarshalAs(UnmanagedType.I1)] bool highQualityUASTC
												, [MarshalAs(UnmanagedType.I1)] bool forceOverwrite
												);
		
		[DllImport(TeleportServerDll.name)]
		private static extern uid Server_StoreFont(  string  ttf_path,string  relative_assetPath, Int64 lastModified, int size);
		[DllImport(TeleportServerDll.name)]
		private static extern uid Server_StoreTextCanvas(string relative_assetPath,teleport.InteropTextCanvas interopTextCanvas);

		[DllImport(TeleportServerDll.name)]
		private static extern void Server_StoreShadowMap(uid id, 
												string path, Int64 lastModified, avs.Texture shadowMap);

		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_IsNodeStored(uid id);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_IsSkeletonStored(uid id);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_IsMeshStored(uid id);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_IsMaterialStored(uid id);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_IsTextureStored(uid id);
		[DllImport(TeleportServerDll.name)]
		private static extern bool Server_EnsureResourceIsLoaded(uid u);

		[DllImport(TeleportServerDll.name)]
		private static extern void Server_RemoveNode(uid id);

		[DllImport(TeleportServerDll.name)]
		public static extern UInt64 Server_GetNumberOfTexturesWaitingForCompression();
		[DllImport(TeleportServerDll.name)]

		public static extern void Server_GetMessageForNextCompressedTexture(string str,UInt64 strlen);
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_CompressNextTexture();
		[DllImport(TeleportServerDll.name)]
		private static extern void Server_SetCompressionLevels(byte compressionStrength, byte compressionQuality);
		
		[DllImport(TeleportServerDll.name)]
		public static extern bool Server_GetFontAtlas( string path, InteropFontAtlas interopFontAtlas);


		[DllImport(TeleportServerDll.name)]
		public static extern int Server_GetStructSize(string name);
		#endregion

		#region CustomSerialisation
		//Dictionaries aren't serialised, so to serialise them I am saving the data to key value arrays. 
		public UnityEngine.Object[] sessionResourceUids_keys = new UnityEngine.Object[0];
		public uid[] sessionResourceUids_values = new uid[0];
		#endregion

		public List<TextureExtractionData> texturesWaitingForExtraction = new List<TextureExtractionData>();
		public bool treatTransparentAsDoubleSided=true;
		[HideInInspector] public string compressedTexturesFolderPath;
		private ComputeShader textureShader;

		private const string RESOURCES_PATH = "Assets/Resources/";
		private const string TELEPORT_VR_PATH = "TeleportVR/";

		private readonly Dictionary<UnityEngine.Object, uid> sessionResourceUids = new Dictionary<UnityEngine.Object, uid>(); //<Resource, ResourceID>
		private readonly Dictionary<string, uid> skeletonUids = new Dictionary<string, uid>(); //<path, ResourceID>
		private readonly Dictionary<uid, UnityEngine.GameObject> sessionNodes = new Dictionary<uid, UnityEngine.GameObject>(); //<Resource, ResourceID>
		public Dictionary<uid, UnityEngine.GameObject> GetSessionNodes()
		{
			return sessionNodes;
		}
		public Dictionary<UnityEngine.Object, uid> GetSessionResourceUids()
		{
			return sessionResourceUids;
		}
		//private bool isAwake = false;

		[SerializeField]
		private List<AnimationClip> processedAnimations = new List<AnimationClip>(); //AnimationClips stored in the GeometrySource that we need to add an event to.
		private AnimationEvent teleportAnimationHook = new AnimationEvent();		//Animation event that is added to animation clips, so we can detect when a new animation starts playing.


		private static GeometrySource geometrySource = null;
		public static void CheckDllImports()
		{// TODO: Add try-catch blocks to all Server_ calls and Client_ calls.
		}
		public static GeometrySource GetGeometrySource()
		{
			if (geometrySource == null)
			{
				geometrySource = Resources.Load<GeometrySource>(TELEPORT_VR_PATH + nameof(GeometrySource));
				CheckDllImports();
			}

#if UNITY_EDITOR
			if (geometrySource == null)
			{
				TeleportSettings.EnsureAssetPath(RESOURCES_PATH + TELEPORT_VR_PATH);
				string unityAssetPath = RESOURCES_PATH + TELEPORT_VR_PATH + nameof(GeometrySource) + ".asset";

				geometrySource = CreateInstance<GeometrySource>();
				AssetDatabase.CreateAsset(geometrySource, unityAssetPath);
				AssetDatabase.SaveAssets();

				Server_ClearGeometryStore();
				UnityEngine.Debug.LogWarning($"Geometry Source asset created with path \"{unityAssetPath}\"!");
			}	
#endif

			return geometrySource;
		}

		public void EliminateDuplicates()
		{
			sessionResourceUids_keys = sessionResourceUids.Keys.ToArray();
			sessionResourceUids_values = sessionResourceUids.Values.ToArray();
			for (int i = 0; i < sessionResourceUids_keys.Length; i++)
			{
				for (int j = i + 1; j < sessionResourceUids_keys.Length; j++)
				{
					if (sessionResourceUids_values[i] == sessionResourceUids_values[j])
					{
						UnityEngine.Debug.LogWarning("Duplicate Uid "+ sessionResourceUids_values [i]+ "found for objects " + sessionResourceUids_keys[i].name+" and " + sessionResourceUids_keys[j]+".");
						sessionResourceUids.Remove(sessionResourceUids_keys[j]);
					}
				}
			}
		}
		public void OnBeforeSerialize()
		{
			EliminateDuplicates();
			sessionResourceUids_keys = sessionResourceUids.Keys.ToArray();
			sessionResourceUids_values = sessionResourceUids.Values.ToArray();
			sessionResourceUids.Clear();
		}

		public void OnAfterDeserialize()
		{
			if (sessionResourceUids_keys.Length == sessionResourceUids_values.Length)
			{
				for(int i=0;i<sessionResourceUids_keys.Length;i++)
				{
					sessionResourceUids[sessionResourceUids_keys[i]]=sessionResourceUids_values[i];
				}
			}
			EliminateDuplicates();
		}

		public void Awake()
		{
			//We already have a GeometrySource, don't load from disk again and break the IDs.
			if(geometrySource != null)
			{
				return;
			}
		#if UNITY_EDITOR
			string shaderGUID = AssetDatabase.FindAssets("ExtractTextureData t:ComputeShader")[0];
			textureShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID));
		#endif
			// Do this ASAP to prevent infinite loop.
			geometrySource =this;
			LoadFromDisk();

			CreateAnimationHook();

		//	isAwake = true;
		}

		public void OnEnable()
		{
			//Initialise static instance in GeometrySource when it is enabled after a hot-reload.
			GetGeometrySource();
			var teleportSettings= TeleportSettings.GetOrCreateSettings();
			compressedTexturesFolderPath = teleportSettings.cachePath;// + "/basis_textures/";

			//Remove nodes that have been lost due to level change.
			var pairsToDelete = sessionResourceUids.Where(pair => pair.Key == null).ToArray();
			foreach (var pair in pairsToDelete)
			{
				Server_RemoveNode(pair.Value);
				sessionResourceUids.Remove(pair.Key);
			}

		}

		bool SetGeometryCachePath(string cachePath)
		{
			bool valid = Server_SetCachePath(cachePath);
			if (!valid)
			{
				UnityEngine.Debug.LogError("Failed to set the Geometry Cache Path to: " + cachePath);
				UnityEngine.Debug.LogError("Unable to Save/Load caching from/to disk.");
			}
			return valid;
		}
		public bool SetHttpRoot(string r)
		{
			bool valid = Server_SetHttpRoot(r);
			if (!valid)
			{
				UnityEngine.Debug.LogError("Failed to set the HTTP Root Path to: " + r);
				UnityEngine.Debug.LogError("Unable to serve files via http.");
			}
			return valid;
		}

		public void SaveToDisk()
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (!SetGeometryCachePath(teleportSettings.cachePath))
				return;

			Server_SaveGeometryStore();
		}

		public void LoadFromDisk()
		{
			var teleportSettings = TeleportSettings.GetOrCreateSettings();
			if (!SetGeometryCachePath(teleportSettings.cachePath))
				return;
			sessionResourceUids.Clear();

			//Load data from files.
			// These are the dll-side resource definitions.
			Server_LoadGeometryStore(out UInt64 numMeshes, out IntPtr loadedMeshes, out UInt64 numTextures, out IntPtr loadedTextures, out UInt64 numMaterials, out IntPtr loadedMaterials);
	
			// Assign new IDs to the loaded resources.
			AddToProcessedResources<UnityEngine.Mesh>((int)numMeshes, loadedMeshes);
			AddToProcessedResources<UnityEngine.Texture>((int)numTextures, loadedTextures);
			AddToProcessedResources<UnityEngine.Material>((int)numMaterials, loadedMaterials);

			//Delete unmanaged memory.
			Server_DeleteUnmanagedArray(loadedMeshes);
			Server_DeleteUnmanagedArray(loadedTextures);
			Server_DeleteUnmanagedArray(loadedMaterials);
		}

		public void ClearData()
		{
			sessionResourceUids.Clear();
			texturesWaitingForExtraction.Clear();
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene s = SceneManager.GetSceneAt(i);
				var g=s.GetRootGameObjects();
				foreach(var gameObject in g)
				{
					var mt=gameObject.GetComponentsInChildren<teleport.MeshTracker>();
					foreach(var t in mt)
					{
						UnityEngine.Object.DestroyImmediate(t);
					}
				}
			}
            SceneResourcePathManager.ClearAll();
			Server_ClearGeometryStore();
		}
		// The resource path is the path that will be used by remote clients to identify the resource object.
		// For Unity it will consist of the asset filename (relative to the Assets folder), followed if necessary by the subobject name within the file.
		// We will use forward slashes, and a double forward slash will separate the filename from the subobject name where present.
		public static bool GetResourcePath(UnityEngine.Object obj, out string path,bool force)
		{
#if UNITY_EDITOR
			// If in Editor and not playing, let's force a regeneration of the path, in case there are code changes that modify the paths.
			if (!Application.isPlaying)
				force=true;
#endif
			var resourcePathManager = SceneResourcePathManager.GetSceneResourcePathManager(SceneManager.GetActiveScene());
			path = resourcePathManager.GetResourcePath(obj);
#if UNITY_EDITOR
			if (!force && path != null && path.Length > 0)
				return true;
			long localId = 0;
			string guid;
			bool result = UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out guid, out localId);
			if (!result)
			{
				return (path!=null&&path.Length>0);
			}
			path = UnityEditor.AssetDatabase.GetAssetPath(obj);
			// Problem is, Unity can bundle a bunch of individual meshes/materials/etc in one asset file, but use them completely independently.
			// We can't therefore just use the file name.
			// For now at least, let's not do this for textures.

			// The double ~ is used to separate the source file name from the subobject name.
			// see https://stackoverflow.com/questions/1856785/characters-allowed-in-a-url
			// - we want the path to be an allowable HTTP url so we can server resources via that protocol.
			if (obj.GetType() != typeof(UnityEngine.Texture))
			{
				string filename = Path.GetFileName(path);
				path = Path.GetDirectoryName(path);
				path = Path.Combine(path, filename);
				path +="~~"+obj.name;
			}
			// Further disambiguation is needed for meshes because Unity discards internal structure from fbx files
			// so more than one mesh with the same name can appear as part of the same asset.
			if (obj.GetType() == typeof(UnityEngine.Mesh))
			{
				path +="_" + localId;
			}
			// Need something unique. Within default and editor resources are thousands of assets, often with clashing names.
			// So here, we use the localId's to distinguish them.
			if (path.Contains("unity default resources"))
			{
				path += "~~" + obj.name + "_" + localId;
			}
			if (path.Contains("unity editor resources"))
			{
				path="";
				// we can't use these.
				return false;
			}
			// dots and commas are undesirable in URL's. Therefore we replace them.
			path = SceneResourcePathManager.StandardizePath(path, "Assets/");
			resourcePathManager.SetResourcePath(obj,path);
			return true;
#else
			if (path != null&&path.Length > 0)
				return true;
			return false;
#endif
		}
		//Add a resource to the GeometrySource; for external extractors.
		//	resource : Resource that will be tracked by the GeometrySource.
		//	resourceID : ID of the resource on the unmanaged side.
		//Returns whether the operation succeeded; it will fail if 'resource' is null or 'resourceID' is zero.
		public bool AddResource(UnityEngine.Object resource, uid resourceID)
		{
			if(resource == null)
			{
				UnityEngine.Debug.LogError("Failed to add the resource to the GeometrySource! Attempted to add a null resource!");
				return false;
			}

			if(resourceID == 0)
			{
				UnityEngine.Debug.LogError($"Failed to add the resource \"{resource.name}\" to the GeometrySource! Attempted to add a resource with an ID of zero!");
				return false;
			}

			//We only want to add the animation clip to the list once.
			if(resource is AnimationClip && !sessionResourceUids.TryGetValue(resource, out uid _))
			{
				AnimationClip animation = (AnimationClip)resource;
				processedAnimations.Add(animation);

				//If this animation was extracted in play mode then we need to add the event.
				if(Application.isPlaying)
				{
					teleportAnimationHook.objectReferenceParameter = animation;
					animation.AddEvent(teleportAnimationHook);
				}
			}

			sessionResourceUids[resource] = resourceID;
			return true;
		}

		//Returns whether the GeometrySource has processed the resource.
		public bool HasResource(UnityEngine.Object resource)
		{
			return sessionResourceUids.ContainsKey(resource);
		}

		public bool HasNode(uid nodeID)
		{
			return Server_IsNodeStored(nodeID);
		}
		//Returns the ID of the resource if it has been processed, or zero if the resource has not been processed or was passed in null.
		public uid FindResourceID(UnityEngine.Object resource)
		{
			if (!resource)
			{
				return 0;
			}

			sessionResourceUids.TryGetValue(resource, out uid nodeID);
			return nodeID;
		}
		public uid FindOrAddResourceID(UnityEngine.Object resource)
		{
			uid u= FindResourceID(resource);
			if(u!=0)
			{
				return u;
			}
			// The resource is not yet in the locally processed list. Therefore, we will try to find it in the mapping of resources and paths.
			string path;
			if(!GetResourcePath(resource,out path,true))
				return 0;
			u= Server_EnsurePathResourceIsLoaded(path);
			return u;
		}
		public uid FindOrAddNodeID(UnityEngine.GameObject gameObject)
		{
			uid nodeID = geometrySource.AddNode(gameObject);
			if (nodeID == 0)
			{
				nodeID=AddNode(gameObject);
			}
			return nodeID;
		}
		//Returns the ID of the resource if it has been processed, or zero if the resource has not been processed or was passed in null.
		public uid[] FindResourceIDs(UnityEngine.Object[] resources)
		{
			if (resources==null||resources.Length==0)
			{
				return null;;
			}
			uid [] uids=new uid[resources.Length];
			for(int i=0;i<resources.Length;i++) 
			{
				sessionResourceUids.TryGetValue(resources[i], out uids[i]);
			}
			return uids;
		}

		public UnityEngine.Object FindResource(uid resourceID)
		{
			return (resourceID == 0) ? null : sessionResourceUids.FirstOrDefault(x => x.Value == resourceID).Key;
		}
		/// <summary>
		/// Get all streamable objects in all loaded scenes.
		/// </summary>
		public List<GameObject> GetStreamableNodes()
		{
			List<GameObject> streamableObjects = new List<GameObject>();
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene scene = SceneManager.GetSceneAt(i);
				var sceneStr = GetStreamableNodes(scene);
				streamableObjects.AddRange(sceneStr);
            }
			return streamableObjects;

        }
		/// <summary>
		/// Get all the game objects that can be streamed, root and otherwise.
		/// </summary>
        public List<GameObject> GetStreamableNodes(Scene scene)
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();

			//Find all GameObjects in open scenes that have the StreamableNode.
			List<GameObject> streamableObjects=new List<GameObject>();
			if(SceneManager.sceneCount==0)
				return streamableObjects;
			{
				var objs = scene.GetRootGameObjects();
				foreach (var o in objs)
				{
					UnityEngine.Transform[] transforms = o.GetComponentsInChildren<UnityEngine.Transform>();
					foreach (var t in transforms)
					{
						if(t.gameObject.GetComponent<StreamableNode>() != null)
							streamableObjects.Add(t.gameObject);
					}
				}
			}
			return streamableObjects;
		}
		/// <summary>
		/// Get all the root game objects that can be streamed.
		/// </summary>
		public List<GameObject> GetStreamableRoots(Scene scene)
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			List<GameObject> streamableRoots= new List<GameObject>();
			if (SceneManager.sceneCount == 0)
				return streamableRoots;
			var objs = scene.GetRootGameObjects();
			foreach (var o in objs)
			{
				teleport.StreamableRoot[] roots = o.GetComponentsInChildren<teleport.StreamableRoot>();
				foreach (var t in roots)
				{
					streamableRoots.Add(t.gameObject);
				}
			}
			return streamableRoots;
		}
		/// <summary>
		/// Adds animations events to all extracted AnimationClips, so we can detect when they start playing.
		/// </summary>
		public void AddAnimationEventHooks()
		{
			foreach(AnimationClip clip in processedAnimations)
			{
				teleportAnimationHook.objectReferenceParameter = clip;
				clip.AddEvent(teleportAnimationHook);
			}
		}
		public static UnityEngine.Transform GetTopmostSkeletonRoot(SkinnedMeshRenderer skinnedMeshRenderer)
		{
			if(!skinnedMeshRenderer.rootBone)
				return null;
			UnityEngine.Transform t = skinnedMeshRenderer.rootBone.transform;
			teleport.SkeletonRoot r=t.GetComponentInParent<teleport.SkeletonRoot>();
			if(r)
				return r.gameObject.transform;
			UnityEngine.Transform meshParent =skinnedMeshRenderer.transform.parent;
			var p= t;
			while (p != null)
			{
				if(p.parent==meshParent)
					return p;
				p=p.parent;
			}
			return t;
		}
		///GEOMETRY EXTRACTION
		public uid AddNode(GameObject gameObject, ForceExtractionMask forceMask = ForceExtractionMask.FORCE_NOTHING, bool isChildExtraction = false,bool verify=false)
		{
			if(!gameObject)
			{
				UnityEngine.Debug.LogError("Failed to extract node from GameObject! Passed GameObject was null!");
				return 0;
			}
			StreamableProperties streamableProperties = gameObject.GetComponent<StreamableProperties>();
			//Just return the ID; if we have already processed the GameObject, the node can be found on the unmanaged side,
			//we are not forcing an extraction of nodes, and we are not forcing an extraction on the hierarchy of a node.
			if (!sessionResourceUids.TryGetValue(gameObject, out uid nodeID) ||! Server_IsNodeStored(nodeID) ||
					(
						!isChildExtraction && (forceMask & ForceExtractionMask.FORCE_NODES) == ForceExtractionMask.FORCE_NODES ||
						(isChildExtraction && (forceMask & ForceExtractionMask.FORCE_HIERARCHIES) == ForceExtractionMask.FORCE_HIERARCHIES)
					)
				)
			{
				bool isRoot=(gameObject.GetComponent<StreamableRoot>() != null);
				avs.Node extractedNode = new avs.Node();
				// If this is a root, should we treat it as parentless? This would lead to problems with the positioning.
				if (isRoot&&gameObject.transform.parent!=null)
				{
					extractedNode.parentID = FindResourceID(gameObject.transform.parent.gameObject);
				}
				else if(gameObject.transform.parent != null)
				{
					extractedNode.parentID = FindResourceID(gameObject.transform.parent.gameObject);
				}
				extractedNode.name = Marshal.StringToCoTaskMemUTF8(gameObject.name);
				teleport.StreamableRoot teleport_Streamable = gameObject.GetComponentInParent<teleport.StreamableRoot>();
	#if UNITY_EDITOR
				// if it's not stationary, it will need a StreamableProperties component to let us know, because isStatic is always false in builds for Unity.
				if (!gameObject.isStatic)
				{
					if (streamableProperties == null)
					{
						streamableProperties=gameObject.AddComponent<StreamableProperties>();
					}
				}
				if (streamableProperties!=null&&streamableProperties.isStationary != gameObject.isStatic)
					streamableProperties.isStationary = gameObject.isStatic;
	#endif
				if (teleport_Streamable != null)
				{
					extractedNode.priority = teleport_Streamable.priority;
				}
				extractedNode.stationary = streamableProperties ? streamableProperties.isStationary : true;
				extractedNode.ownerClientId = teleport_Streamable != null ? teleport_Streamable.OwnerClient : 0;
				if(extractedNode.parentID!=0)
					extractedNode.localTransform = avs.Transform.FromLocalUnityTransform(gameObject.transform);
				else
					extractedNode.localTransform = avs.Transform.FromGlobalUnityTransform(gameObject.transform);
				extractedNode.dataType = avs.NodeDataType.Invalid;
				teleport.SkeletonRoot skeletonRoot = gameObject.GetComponent<teleport.SkeletonRoot>();
				if (skeletonRoot)
				{
					nodeID =AddSkeleton(gameObject.transform,  forceMask);
					extractedNode.dataType=avs.NodeDataType.Skeleton;
					skeletonUids.TryGetValue(skeletonRoot.assetPath, out extractedNode.dataID);

					Animator animator =gameObject.GetComponentInParent<Animator>();
					//Animator component usually appears on the parent GameObject, so we need to use that instead for searching the children.

					if (animator)
					{
	#if UNITY_EDITOR
						extractedNode.animationIDs = AnimationExtractor.AddAnimations(animator, forceMask);
	#endif
						AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
						List<uid> anim_uids=new List<uid>();
						foreach (var clip in animationClips)
						{
							sessionResourceUids.TryGetValue(clip, out uid animID);
							if(animID != 0)
								anim_uids.Add(animID);
							else
							{
								GetResourcePath(clip, out string resourcePath, true);
								// Not a resource, don't extract.
								animID = GeometrySource.Server_GetOrGenerateUid(resourcePath);
								if (animID != 0)
									anim_uids.Add(animID);
								else
								{
									UnityEngine.Debug.LogError("Failed to get a uid for animation clip "+clip.name);
								}
							}
						}
						extractedNode.animationIDs=anim_uids.ToArray();
						extractedNode.numAnimations = (ulong)extractedNode.animationIDs.Length;
					}
					else
					{
						extractedNode.numAnimations = (ulong)0;
					}
				}
				extractedNode.url=IntPtr.Zero;
				extractedNode.query_url = IntPtr.Zero;

				nodeID = nodeID == 0 ? Server_GenerateUid() : nodeID;
				sessionResourceUids[gameObject] = nodeID;
				sessionNodes[nodeID] = gameObject;
				if (extractedNode.dataType == avs.NodeDataType.Invalid)
				{
					ExtractNodeMeshData(gameObject, ref extractedNode, forceMask, verify);
				}
				if (extractedNode.dataType == avs.NodeDataType.Invalid)
				{
					SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
					if (skinnedMeshRenderer&&skinnedMeshRenderer.enabled&&skinnedMeshRenderer.rootBone)
					{
						ExtractNodeSkinnedMeshData(gameObject, ref extractedNode, forceMask, verify);
						// Map from bones to joints, i.e. which of the skeleton's bones does 
						// this particular skinned mesh use as joints?
						var skeletonRootTransform = GetTopmostSkeletonRoot(skinnedMeshRenderer);
						List<UnityEngine.Transform> bones = new List<UnityEngine.Transform>();
						GetBones(skeletonRootTransform, bones);
						extractedNode.numJoints=(UInt64)skinnedMeshRenderer.bones.Length;
						extractedNode.jointIndices=new int[extractedNode.numJoints];
						for (int i = 0; i < skinnedMeshRenderer.bones.Length; i++)
						{
							extractedNode.jointIndices[i] = bones.IndexOf(skinnedMeshRenderer.bones[i]);
						}
					}
				}
				if(extractedNode.dataType == avs.NodeDataType.Invalid)
				{
					ExtractNodeLightData(gameObject, ref extractedNode, forceMask);
				}
				teleport.Link link = gameObject.GetComponent<teleport.Link>();
				if (link!=null)
				{
					ExtractNodeLinkData(gameObject, ref extractedNode, forceMask);
				}
			
				var textCanvas=gameObject.GetComponent<teleport.TextCanvas>();
				if (textCanvas)
				{
					uid text_canvas_uid=ExtractTextCanvas(textCanvas);
					if(text_canvas_uid!=0)
					{
						extractedNode.dataType=avs.NodeDataType.TextCanvas;
						extractedNode.dataID=text_canvas_uid;
					}
				}
				if (Marshal.SizeOf<avs.NodeRenderState>() != Server_GetStructSize("NodeRenderState"))
				{
					UnityEngine.Debug.LogError("Mismatching struct size for NodeRenderState. C#(" + Marshal.SizeOf<avs.NodeRenderState>() + ") != Dll(" + Server_GetStructSize("NodeRenderState") + "). Update C# and dll.");
					return 0;
				}
				
				if (Marshal.SizeOf<avs.Node>() != Server_GetStructSize("InteropNode"))
				{
					UnityEngine.Debug.LogError("Mismatching struct size for InteropNode. C#(" + Marshal.SizeOf<avs.Node>() + ") != Dll(" + Server_GetStructSize("InteropNode") + "). Update C# and dll.");
					return 0;
				}
				//Store extracted node.
				if(!Server_StoreNode(nodeID, extractedNode))
				{
					UnityEngine.Debug.LogError($"Failed to store node {nodeID} {gameObject.name}.");
					return 0;
				}
			}
			teleport.StreamableNode streamableNode = gameObject.GetComponent<teleport.StreamableNode>();
			if(streamableNode!=null)
			{
				streamableNode.UpdateNodeID();
				if(nodeID!=streamableNode.nodeID)
				{
					UnityEngine.Debug.LogError("Node "+gameObject.name+", Id mismatch: " + nodeID+","+ streamableNode.nodeID);
				}
			}
			else
			{
				UnityEngine.Debug.LogWarning("Node "+gameObject.name+" has no StreamableNode.");
			}
			if (!streamableProperties||streamableProperties.includeChildren)
				AddChildNodes(gameObject, forceMask, verify);
			return nodeID;
		}
		public uid AddMesh(UnityEngine.Mesh mesh, GameObject gameObject, ForceExtractionMask forceMask,bool verify)
		{
			if(!mesh)
			{
				UnityEngine.Debug.LogError("Mesh extraction failure! Null mesh passed to AddMesh(...) in GeometrySource!");
				return 0;
			}
			// Make sure there's a path for this mesh.
			GetResourcePath(mesh, out string resourcePath, (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_SUBRESOURCES);
			// Not a resource in the AssetDatabase generate the path some other way.
			if(resourcePath==null||resourcePath=="")
			{
				resourcePath= SceneResourcePathManager.GetNonAssetResourcePath(mesh, gameObject);
			}
			if (resourcePath == null || resourcePath == "")
			{
				return 0;
			}
			//We can't extract an unreadable mesh.
			if (!mesh.isReadable)
			{
				UnityEngine.Debug.LogWarning($"Failed to extract mesh \"{mesh.name}\"! Mesh is unreadable!");
				return 0;
			}

			//Just return the ID; if we have already processed the mesh, the mesh can be found on the unmanaged side, and we are not forcing extraction.
			if(sessionResourceUids.TryGetValue(mesh, out uid meshID))
			{
				if (Server_IsMeshStored(meshID))
				{
					if ((forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
					{
						return meshID;
					}
				}
			}
			bool running = Application.isPlaying;
			//resourcePath = avs.AxesStandard.EngineeringStyle.ToString().Replace("Style", "").ToLower() + "/" + resourcePath;
			meshID = meshID == 0 ? Server_GetOrGenerateUid(resourcePath) : meshID;
			sessionResourceUids[mesh] = meshID;
			// only compress if not running - too slow...
			// Actually, let's ONLY extract offline. 
			if (!running)
			{
				ExtractMeshData(avs.AxesStandard.EngineeringStyle, mesh, meshID,  verify);
				ExtractMeshData(avs.AxesStandard.GlStyle, mesh, meshID,   verify);
				return meshID;
			}
			else
			{
				if (!sessionResourceUids.TryGetValue(mesh, out meshID))
				{
 					UnityEngine.Debug.LogError("Mesh missing! Mesh " + mesh.name + " was not in sessionResourceUids.");
					return 0;
				}
				if(!Server_IsMeshStored(meshID))
				{
					UnityEngine.Debug.LogError("Mesh missing! Mesh "+mesh.name+" was not stored dll-side.");
					Server_IsMeshStored(meshID);
					return 0;
				}
			}
			return meshID;
		}

		public enum UnityBlendMode
		{
			Opaque,
			Cutout,
			Fade,   // Old school alpha-blending mode, fresnel does not affect amount of transparency
			Transparent // Physically plausible transparency mode, implemented as alpha pre-multiply
		}
		public uid AddMaterial(UnityEngine.Material material, GameObject gameObject, ForceExtractionMask forceMask)
		{
			if(!material)
			{
				return 0;
			}

			//Just return the ID; if we have already processed the material, the material can be found on the unmanaged side, and we are not forcing an update.
			if(sessionResourceUids.TryGetValue(material, out uid materialID) && Server_IsMaterialStored(materialID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
			{
				return materialID;
			}

			if(!GetResourcePath(material, out string resourcePath, (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_SUBRESOURCES))
			{
				// Create a path based on gameObject if none can be created from the material itself.
				resourcePath = SceneResourcePathManager.GetNonAssetResourcePath(material, gameObject);
			}
			if (materialID == 0)
			{
				materialID =  Server_GetOrGenerateUid(resourcePath) ;
				//Debug.Log("Generated uid "+materialID+ " for "+resourcePath);
			}
			else
			{
				uid id_from_dll = Server_GetOrGenerateUid(resourcePath);
                if (materialID != id_from_dll)
				{
					UnityEngine.Debug.LogError("Uid mismatch for object "+material.name+" at path "+resourcePath+". Id from list was "+ materialID+", but Id from path "+resourcePath+" was "+id_from_dll);
					sessionResourceUids.Remove(material);
				}
			}
			sessionResourceUids[material] = materialID;

			avs.Material extractedMaterial = new avs.Material();
			extractedMaterial.name = Marshal.StringToCoTaskMemUTF8( material.name);

			//Albedo/Diffuse
			extractedMaterial.materialMode=avs.MaterialMode.OPAQUE;
			if (material.HasProperty("_Mode"))
			{
				UnityBlendMode blendMode =(UnityBlendMode)material.GetFloat("_Mode");
				switch (blendMode)
				{
					case UnityBlendMode.Fade:
					case UnityBlendMode.Transparent:
						extractedMaterial.materialMode=avs.MaterialMode.TRANSPARENT;
						break;
					default:
						break;
				}
			}
			if (material.renderQueue > (int)RenderQueue.GeometryLast)
			{
				extractedMaterial.materialMode = avs.MaterialMode.TRANSPARENT;
			}
			extractedMaterial.pbrMetallicRoughness.baseColorTexture.index = AddTexture(material.mainTexture, gameObject, forceMask);
			extractedMaterial.pbrMetallicRoughness.baseColorTexture.tiling = material.mainTextureScale;
			extractedMaterial.pbrMetallicRoughness.baseColorFactor = material.HasProperty("_Color") ? material.color.linear: new Color(1.0f, 1.0f, 1.0f, 1.0f);

			//Metallic-Roughness

			UnityEngine.Texture metallicRoughness = null;
			if(material.HasProperty("_MetallicGlossMap"))
			{
				metallicRoughness = material.GetTexture("_MetallicGlossMap");
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.index = AddTexture(metallicRoughness, gameObject,forceMask, TextureConversion.CONVERT_TO_METALLICROUGHNESS_BLUEGREEN);
				extractedMaterial.pbrMetallicRoughness.metallicRoughnessTexture.tiling = material.mainTextureScale;
			}
			if (metallicRoughness)
			{
				// We convert the smoothness texture to a roughness texture. So we have no need to offset roughness.
				extractedMaterial.pbrMetallicRoughness.metallicFactor = metallicRoughness ? 1.0f : (material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0.0f); //Unity doesn't use the factor when the texture is set.

				float glossMapScale = material.HasProperty("_GlossMapScale") ? glossMapScale = material.GetFloat("_GlossMapScale") : 1.0f;
				// unity has smoothness=glossMapScale*lookup.smoothness;
				// we want roughness= 1.0-smoothness
				//					= 1.0-glossMapScale*(1.0-lookup.roughness)
				//					= (1.0-glossMapScale)+glossMapScale*lookup.roughness.
				extractedMaterial.pbrMetallicRoughness.roughOrSmoothMultiplier = glossMapScale;
				extractedMaterial.pbrMetallicRoughness.roughOffset = 1.0F - glossMapScale;
			}
			else
			{
				extractedMaterial.pbrMetallicRoughness.metallicFactor = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0.0f; //Unity doesn't use this factor when the texture is set.
				float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.0f;
				extractedMaterial.pbrMetallicRoughness.roughOrSmoothMultiplier = 0;
				extractedMaterial.pbrMetallicRoughness.roughOffset = 1.0f-smoothness;
			}
			
			//Normal

			if(material.HasProperty("_BumpMap"))
			{
				UnityEngine.Texture normal = material.GetTexture("_BumpMap");
				extractedMaterial.normalTexture.index = AddTexture(normal, gameObject,forceMask);
				extractedMaterial.normalTexture.tiling = material.mainTextureScale;
			}
			extractedMaterial.normalTexture.strength = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1.0f;

			//Occlusion

			if(material.HasProperty("_OcclusionMap"))
			{
				UnityEngine.Texture occlusion = material.GetTexture("_OcclusionMap");
				extractedMaterial.occlusionTexture.index = AddTexture(occlusion, gameObject,forceMask);
				extractedMaterial.occlusionTexture.tiling = material.mainTextureScale;
			}
			extractedMaterial.occlusionTexture.strength = material.HasProperty("_OcclusionStrength") ? material.GetFloat("_OcclusionStrength") : 1.0f;

			//Emission

			//Extract emission properties only if emission is active.
			if(!material.globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.EmissiveIsBlack))
			{
				if(material.HasProperty("_EmissionMap"))
				{
					UnityEngine.Texture emission = material.GetTexture("_EmissionMap");
					if(emission!=null)
					{ 
						extractedMaterial.emissiveTexture.index = AddTexture(emission, gameObject,forceMask);
						extractedMaterial.emissiveTexture.tiling = material.mainTextureScale;
					}
				}
				extractedMaterial.emissiveFactor = material.HasProperty("_BumpScale") ? (avs.Vector3)material.GetColor("_EmissionColor").linear : new avs.Vector3(0.0f, 0.0f, 0.0f);
			}
			// UNITY: For baked lightmaps, you must place lightmap UVs in the Mesh.uv2. This channel is also called “UV1”.
			extractedMaterial.lightmapTexCoordIndex=1;
			int rq= material.renderQueue;
			if(rq<0&& material.shader!=null)
				rq=(int)material.shader.renderQueue;
			extractedMaterial.doubleSided=treatTransparentAsDoubleSided?(rq==(int)RenderQueue.Transparent):false;

			//Extensions
			extractedMaterial.extensionAmount = 0;
			extractedMaterial.extensionIDs = null;
			extractedMaterial.extensions = null;

#if UNITY_EDITOR
			//long fileId=0;
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(material, out string guid);
			extractedMaterial.path = Marshal.StringToCoTaskMemUTF8(resourcePath);
			if (Marshal.SizeOf<avs.TextureAccessor>() != Server_GetStructSize("TextureAccessor"))
			{
				UnityEngine.Debug.LogError("Mismatching struct size for TextureAccessor. C#(" + Marshal.SizeOf<avs.TextureAccessor>() + ") != Dll(" + Server_GetStructSize("TextureAccessor") + "). Update C# and dll.");
				return 0;
			}
			if (Marshal.SizeOf<avs.PBRMetallicRoughness>() != Server_GetStructSize("PBRMetallicRoughness"))
			{
				UnityEngine.Debug.LogError("Mismatching struct size for PBRMetallicRoughness. C#(" + Marshal.SizeOf<avs.PBRMetallicRoughness>() + ") != Dll(" + Server_GetStructSize("PBRMetallicRoughness") + "). Update C# and dll.");
				return 0;
			}
			if (Marshal.SizeOf<avs.Material>()!= Server_GetStructSize("InteropMaterial"))
			{
				UnityEngine.Debug.LogError("Mismatching struct size for InteropMaterial. C#("+ Marshal.SizeOf<avs.Material>()+") != Dll("+ Server_GetStructSize("InteropMaterial") + "). Update C# and dll.");
				return 0;
			}
			Server_StoreMaterial(materialID, resourcePath, GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid.Substring(0,32))), extractedMaterial);
#endif

			return materialID;
		}

		public string GenerateCompressedFilePath(string textureAssetPath,avs.TextureCompression textureCompression)
		{
			string compressedFilePath = textureAssetPath; //Use editor file location as unique name; this won't work out of the Unity Editor.
			//compressedFilePath = compressedFilePath.Replace("/", "#"); //Replace forward slashes with hashes.
			int idx = compressedFilePath.LastIndexOf('.');
			if (idx >= 0)
				compressedFilePath = compressedFilePath.Remove(idx); //Remove file extension.
			string folderPath = compressedTexturesFolderPath;
			//Create directory if it doesn't exist.
			if (!Directory.Exists(folderPath))
			{
				Directory.CreateDirectory(folderPath);
			}
			compressedFilePath = folderPath +"/" +compressedFilePath;
			if(textureCompression ==avs.TextureCompression.BASIS_COMPRESSED_DEPRECATED)
				return "";
			else if (textureCompression == avs.TextureCompression.PNG)
				compressedFilePath += ".png";
			else if (textureCompression == avs.TextureCompression.KTX)
				compressedFilePath += ".ktx";
			else return "";
			return compressedFilePath;
		}
	#if UNITY_EDITOR
		private List<RenderTexture> renderTextures = new List<RenderTexture>();
		public bool ExtractTextures(bool forceOverwrite)
		{
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			//According to the Unity docs, we need to call Release() on any render textures we are done with.
			//Resize the array, instead of simply creating a new one, as we want to keep the same render textures for quicker debugging.
			int rendertextureIndex=0;
			avs.TextureCompression avsTextureCompression = avs.TextureCompression.KTX;
			//Extract texture data with a compute shader, rip the data, and store in the native C++ plugin.
			for (int i = 0; i < geometrySource.texturesWaitingForExtraction.Count; i++)
			{
				TextureExtractionData textureExtractionData= geometrySource.texturesWaitingForExtraction[i];
				bool highQualityUASTC = false;
				UnityEngine.Texture sourceTexture = textureExtractionData.unityTexture;

				if (EditorUtility.DisplayCancelableProgressBar($"Extracting Textures ({i + 1} / {geometrySource.texturesWaitingForExtraction.Count})", $"Processing \"{sourceTexture.name}\".", (float)(i + 1) / geometrySource.texturesWaitingForExtraction.Count))
				{
					EditorUtility.ClearProgressBar();
					return false;
				}
				UnityEngine.TextureFormat textureFormat = UnityEngine.TextureFormat.RGBA32;
				bool isCubemap = false;
				int arraySize = 1;
				int numImages=1;
				if (sourceTexture.GetType() == typeof(Texture2D))
				{
					var sourceTexture2D = (Texture2D)sourceTexture;
					textureFormat = sourceTexture2D.format;
				}
				else if (sourceTexture.GetType() == typeof(Texture3D))
				{
					var sourceTexture3D = (Texture3D)sourceTexture;
					textureFormat = sourceTexture3D.format;
				}
				else if (sourceTexture.GetType() == typeof(Cubemap))
				{
					var sourceCubemap = (Cubemap)sourceTexture;
					textureFormat = sourceCubemap.format;
					isCubemap = true;
					arraySize = 6;
				}
				else if (sourceTexture.GetType() == typeof(RenderTexture))
				{
					var sourceRenderTexture = (RenderTexture)sourceTexture;
					textureFormat = ConvertRenderTextureFormatToTextureFormat(sourceRenderTexture.format);
					if(sourceRenderTexture.dimension==UnityEngine.Rendering.TextureDimension.Cube
						|| sourceRenderTexture.dimension == UnityEngine.Rendering.TextureDimension.CubeArray)
					{ 
						isCubemap = true;
						arraySize = 6;
					}
				}
				int mipCount= (int)textureExtractionData.textureData.mipCount;
				numImages =arraySize*mipCount;
				int targetWidth = sourceTexture.width;
				int targetHeight = sourceTexture.height;
				int [] scale = {1,1};
				while (Math.Max(targetWidth, targetHeight) > teleportSettings.serverSettings.maximumTextureSize)
				{
					targetWidth = (targetWidth + 1) / 2;
					targetHeight = (targetHeight + 1) / 2;
					scale[0] *= 2;
					scale[1] *= 2;
				}
				int maxSize= (1 << (mipCount - 1));
				while (maxSize > targetWidth || maxSize > targetHeight)
				{
					mipCount--;
					maxSize = (1 << (mipCount - 1));
					textureExtractionData.textureData.mipCount=(uint)mipCount;
				}
				int w = targetWidth, h = targetHeight;
				int n = 0;
				
				//If we always created a new render texture, then reloading would lose the link to the render texture in the inspector.
				for (int m = 0; m < mipCount; m++)
				{ 
					for (int j=0;j<arraySize;j++)
					{
						RenderTexture rt;
						if (rendertextureIndex+n>=renderTextures.Count)
						{
							rt = new RenderTexture(w, h, 0);
							renderTextures.Add(rt);
						}
						else
						{ 
							rt=renderTextures[rendertextureIndex + n];
							if(rt&&rt.width==w&&rt.height==h)
								rt.Release();
							else
							{
								rt = new RenderTexture(w, h, 0);
								renderTextures[rendertextureIndex + n]=rt;
							}
						}
						RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;
						switch (textureFormat)
						{
							case UnityEngine.TextureFormat.RGBAFloat:
								rtFormat = RenderTextureFormat.ARGBFloat;
								highQualityUASTC = false;
								break;
							case UnityEngine.TextureFormat.DXT5:
								avsTextureCompression = avs.TextureCompression.PNG;
								break;
							case UnityEngine.TextureFormat.BC6H:
								rtFormat = RenderTextureFormat.ARGBHalf;
								avsTextureCompression = avs.TextureCompression.KTX;
								highQualityUASTC = true;
								break;
							case UnityEngine.TextureFormat.RGBAHalf:
								// Basis can't compress half format, so convert to 32-bit float.
								rtFormat = RenderTextureFormat.ARGBHalf;
								highQualityUASTC = true;
								avsTextureCompression = avs.TextureCompression.KTX;
								break;
							case UnityEngine.TextureFormat.RGBA32:
								rtFormat = RenderTextureFormat.ARGB32;
								break;
							case UnityEngine.TextureFormat.ARGB32:
								rtFormat = RenderTextureFormat.ARGB32;
								break;
							default:
								break;
						}
						if (rt.width!=w||rt.height!=h||rt.format!=rtFormat)
						{
							rt.Release();
						}
						rt.format= rtFormat;
						rt.width = w;
						rt.height = h;
						rt.depth = 0;
						rt.enableRandomWrite = true;
						rt.name = $"{textureExtractionData.unityTexture.name} ({textureExtractionData.id}) {n}";
						rt.Create();
						n++;
					}
					w = (w + 1) / 2;
					h = (h + 1) / 2;
				}
				bool isNormal = false;
				//Normal maps need to be extracted differently; i.e. convert from DXT5nm format.
				string path = AssetDatabase.GetAssetPath(sourceTexture);
				AssetImporter aa =AssetImporter.GetAtPath(path);
				TextureImporter textureImporter=null;
				if (aa&&aa.GetType()==typeof(TextureImporter))
				{ 
					textureImporter = (TextureImporter)aa;
					if (textureImporter != null)
					{
						textureImporter.isReadable = true;
						TextureImporterType textureType = textureImporter ? textureImporter.textureType : TextureImporterType.Default;
						isNormal = textureType == UnityEditor.TextureImporterType.NormalMap;
					}
				}
				else
				{
				//	UnityEngine.Debug.LogError("Texture "+sourceTexture.name+" has no importer.");
				}
				if (isNormal || textureImporter != null && textureImporter.GetDefaultPlatformTextureSettings().textureCompression == TextureImporterCompression.CompressedHQ)
					highQualityUASTC = true;
				if (highQualityUASTC)
					avsTextureCompression = avs.TextureCompression.KTX;
				bool flipY=true;
				int[] offsets = { 0, 0 };
				if (flipY)
				{
					scale[1] *= -1;
				}
				string shaderName = isCubemap?"ExtractCubeFace":isNormal ? "ExtractNormalMap" : "ExtractTexture";
				if(textureExtractionData.textureConversion == TextureConversion.CONVERT_TO_METALLICROUGHNESS_BLUEGREEN)
					shaderName= "ConvertRoughnessMetallicGreenBlue";
				shaderName += UnityEditor.PlayerSettings.colorSpace == ColorSpace.Gamma ? "Gamma" : "Linear";
				// make as many copies of textureShader as we have images!!!
				List<ComputeShader> textureShaders=new List<ComputeShader>();
				List<RenderTexture> textureArray = new List<RenderTexture>();
				n = 0;
				string shaderGUID = AssetDatabase.FindAssets($"ExtractTextureData t:ComputeShader")[0];
				var assetpath= UnityEditor.AssetDatabase.GUIDToAssetPath(shaderGUID);
				ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetpath);

				if (shader==null)
				{
					UnityEngine.Debug.LogError("shader " + shaderName + " not found.");
					continue;
				}
				int kernelHandle = shader.FindKernel(shaderName);
				if(kernelHandle<0)
				{
					UnityEngine.Debug.LogError("shader " + shaderName + " not found.");
					continue;
				}
				// Here we will use a shader to extract from the source texture into the target which is readable.
				// Unity stores all textures FLIPPED in the y direction. So the shader must handle re-flipping the images back to normal.
				// But when unity encodes a png, it re-flips it. So we only do this reversal when the texture will be sent direct.
				for (int j = 0; j < arraySize; j++)
				{
					offsets[1] = flipY ? sourceTexture.height - 1 : 0;
					for (int m = 0; m < mipCount; m++)
					{
						textureShaders.Add(shader);
						shader.SetInts("Scale", scale);
						shader.SetInts("Offset", offsets);
						shader.SetTexture(kernelHandle, "Source", sourceTexture);
						if (isCubemap)
							shader.SetTexture(kernelHandle, "SourceCube", sourceTexture);
						var rt = renderTextures[rendertextureIndex + m * arraySize + j];
						shader.SetTexture(kernelHandle, "Result", rt);
						shader.SetInt("ArrayIndex", j);
						shader.SetInt("Face", j);
						shader.SetInt("Mip",m);
						int NID= Shader.PropertyToID("N");
						shader.SetInt(NID, n);
						shader.Dispatch(kernelHandle, (rt.width +7)/ 8, (rt.height+7)/ 8, 1);
						
						textureArray.Add(rt);
						n++;
						offsets[1] = (offsets[1] + 1) / 2 - 1;
					}
				}
				if (!SystemInfo.IsFormatSupported(renderTextures[rendertextureIndex].graphicsFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample))
				{
					UnityEngine.Debug.LogError("Format "+ renderTextures[rendertextureIndex].graphicsFormat+" of texture " + sourceTexture.name + " is not supported.");
					rendertextureIndex+=arraySize;
					continue;
				}
				//Rip data from render texture, and store in GeometryStore.
				ExtractAndStoreTexture(sourceTexture, textureExtractionData.gameObject, textureArray, textureExtractionData.textureData, targetWidth, targetHeight, avsTextureCompression, highQualityUASTC, forceOverwrite);
				rendertextureIndex+=arraySize;
			}

			for (int i = rendertextureIndex; i < renderTextures.Count; i++)
			{
				if (renderTextures[i])
				{
					renderTextures[i].Release();
				}
			}
			geometrySource.CompressTextures();
			geometrySource.texturesWaitingForExtraction.Clear();
			EditorUtility.ClearProgressBar();
			return true;
		}
		class SubresourceImage
		{
			public byte[] bytes;
		}

		void ExtractAndStoreTexture(UnityEngine.Texture sourceTexture, GameObject gameObject,List<RenderTexture> renderTextures, avs.Texture textureData,int targetWidth, int targetHeight, avs.TextureCompression avsTextureCompression, bool highQualityUASTC,  bool forceOverwrite)
		{
			int arraySize=1;
			if(sourceTexture.GetType()==typeof(Cubemap)|| sourceTexture.GetType() == typeof(RenderTexture)&&
				(sourceTexture.dimension== UnityEngine.Rendering.TextureDimension.Cube|| sourceTexture.dimension == UnityEngine.Rendering.TextureDimension.CubeArray))
			{ 
				textureData.cubemap=true;
				arraySize=6;
			}
			textureData.data=(IntPtr)0;
			List<SubresourceImage> subresourceImages=new List<SubresourceImage>();
			
			if (textureData.mipCount * arraySize != renderTextures.Count)
			{
				UnityEngine.Debug.LogError("Image count mismatch");
				return;
			}
			int n=0;
			for (int i=0;i<arraySize;i++)
			{
				int w = targetWidth;
				int h = targetHeight;
				for (int j = 0; j < textureData.mipCount; j++)
				{
					SubresourceImage subresourceImage=new SubresourceImage();
					subresourceImages.Add(subresourceImage);
					var rt= renderTextures[n];
					//Read pixel data into Texture2D, so that it can be read.
					Texture2D readTexture = new Texture2D(w, h, rt.graphicsFormat
								, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

					RenderTexture oldActive = RenderTexture.active;

					RenderTexture.active = rt;
					readTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
					readTexture.Apply();

					RenderTexture.active = oldActive;

					textureData.width = (uint) targetWidth;
					textureData.height = (uint) targetHeight;
					bool pngCompatibleFormat=false;
					if (readTexture.format == UnityEngine.TextureFormat.RGBAFloat)
					{
						Color[] pixelData = readTexture.GetPixels();
						textureData.format = avs.TextureFormat.RGBAFloat;
						int floatSize = Marshal.SizeOf<float>();
						int imageSize= Marshal.SizeOf<Color>() * pixelData.Length;
						var srcBytes = MemoryMarshal.Cast<Color, byte>(pixelData);
						subresourceImage.bytes=srcBytes.ToArray();
					}
					else if (readTexture.format == UnityEngine.TextureFormat.RGBAHalf)
					{
						byte[] pixelData = readTexture.GetRawTextureData();

						textureData.format = avs.TextureFormat.RGBA16F;
						int bytesPerPixel =8;
						int imageSize = pixelData.Length * (int)bytesPerPixel;
						subresourceImage.bytes = new byte[imageSize];
						subresourceImage.bytes = pixelData;
					}
					else
					{
						pngCompatibleFormat = true;
						Color32[] pixelData = readTexture.GetPixels32();

						textureData.format = avs.TextureFormat.RGBA8;
						int imageSize =pixelData.Length * Marshal.SizeOf<Color32>();
						subresourceImage.bytes = new byte[imageSize];
						var srcBytes = MemoryMarshal.Cast<Color32, byte>(pixelData);
						subresourceImage.bytes = srcBytes.ToArray();
					}
					string textureAssetPath = AssetDatabase.GetAssetPath(sourceTexture).Replace("Assets/","");
					string basisFile = geometrySource.GenerateCompressedFilePath(textureAssetPath, avs.TextureCompression.KTX);
					string dirPath = System.IO.Path.GetDirectoryName(basisFile);
					if (!Directory.Exists(dirPath))
					{
						Directory.CreateDirectory(dirPath);
					}
					textureData.compression = avs.TextureCompression.KTX;
					if (pngCompatibleFormat&&highQualityUASTC)
					{
						avsTextureCompression =avs.TextureCompression.PNG;
						highQualityUASTC=false;
					}
					float valueScale = 1.0f;
					textureData.valueScale = valueScale;
					textureData.compressed=false;
					textureData.compression=avsTextureCompression;
					n++;
					w = (w + 1) / 2;
					h = (h + 1) / 2;
				}
			}


			// Now we will write the subresource images into textureData, prepending with the subresource count.
			// let's have a uint16 here, N , then a list of N size_t offsets.
			// Therefore the memory we need is (subresource memory)+sizeof(uint16)+N*uint32
			int memoryRequired = Marshal.SizeOf<UInt16>();
			List<UInt32> offsets=new List<UInt32>();
			memoryRequired += Marshal.SizeOf<UInt32>()* subresourceImages.Count;
			foreach (SubresourceImage subresourceImage in subresourceImages)
			{
				offsets.Add((UInt32)memoryRequired);
				memoryRequired +=subresourceImage.bytes.Length;
			}
			textureData.dataSize = (uint)(memoryRequired);
			textureData.data = Marshal.AllocCoTaskMem((int)textureData.dataSize);
			IntPtr targetPtr = textureData.data;
			UInt16 [] numImages = {((UInt16)subresourceImages.Count)};
			byte[] numImagesBytes = MemoryMarshal.Cast<UInt16, byte>(numImages).ToArray();
			Marshal.Copy(numImagesBytes, 0, targetPtr, Marshal.SizeOf<UInt16>());
			targetPtr += Marshal.SizeOf<UInt16>();
			byte[] offsetsBytes = MemoryMarshal.Cast<UInt32, byte>(offsets.ToArray()).ToArray();
			Marshal.Copy(offsetsBytes, 0, targetPtr, offsetsBytes.Length);
			targetPtr += offsetsBytes.Length;
			foreach (SubresourceImage subresourceImage in subresourceImages)
			{
				Marshal.Copy(subresourceImage.bytes, 0, targetPtr, (int)subresourceImage.bytes.Length);
				targetPtr += subresourceImage.bytes.Length;
			}
			geometrySource.AddTextureData(sourceTexture, gameObject, textureData, highQualityUASTC, forceOverwrite);
			Marshal.FreeCoTaskMem(textureData.data);
		}
		static string basisUExe = "";
		/// <summary>
		/// Launch the application with some options set.
		/// </summary>
		static public void LaunchBasisUExe(string srcPng)
		{
			if (basisUExe == "")
			{
				string rootPath = Application.dataPath;
				// Because Basis is broken for UASTC when run internally, we instead call it directly here.
				string[] files = Directory.GetFiles(rootPath, "basisu.exe", SearchOption.AllDirectories);
				if (files.Length > 0)
				{
					basisUExe = files[0];
				}
				else
				{
					UnityEngine.Debug.LogError("Failed to find basisu.exe for UASTC texture.");
					return;
				}
			}

			// Use ProcessStartInfo class

			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.CreateNoWindow = true;
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardOutput = true;
			startInfo.FileName = basisUExe;
			//startInfo.WindowStyle = ProcessWindowStyle.Hidden;
			string outputPath = System.IO.Path.GetDirectoryName(srcPng);
			startInfo.Arguments = "-uastc -uastc_rdo_m -no_multithreading -debug -stats -output_path \"" + outputPath + "\" \"" + srcPng + "\"";

			try
			{
				startInfo.WorkingDirectory = outputPath;
				UnityEngine.Debug.Log(basisUExe + " " + startInfo.Arguments);
				// Start the process with the info we specified.
				// Call WaitForExit and then the using statement will close.
				var exeProcess = new System.Diagnostics.Process();
				exeProcess.StartInfo = startInfo;
				exeProcess.Start();
				string output = "";
				do
				{
					string line = exeProcess.StandardOutput.ReadLine();
					output += line;
#if UNPACK_COMPRESSED_UASTC
					UnityEngine.Debug.Log("Basis: " + line);
					// do something with line
#endif
				} while (!exeProcess.HasExited);
				exeProcess.WaitForExit();
				int exitCode = exeProcess.ExitCode;
				if (exitCode != 0)
				{
					UnityEngine.Debug.LogError("Basis failed for "+srcPng+", with exit code " + exitCode);
					StreamWriter outputFile = new StreamWriter(srcPng + ".out");
					outputFile.Write(output);
					outputFile.Close();
				}
			}
			catch
			{
				UnityEngine.Debug.LogError("Basis failed");
				// Log error.
			}
#if UNPACK_COMPRESSED_UASTC
			try
			{
				startInfo.WorkingDirectory = outputPath + "\\unpack";
				startInfo.FileName = basis;
				var basisFile = srcPng.Replace(".png", ".basis");
				startInfo.Arguments = "-unpack -no_ktx -etc1_only -debug -stats \"" + basisFile + "\"";
				UnityEngine.Debug.Log(basis + " "+ startInfo.Arguments);
				var exeProcess = new Process();
				exeProcess.StartInfo= startInfo;
				exeProcess.Start();
				StreamWriter outputFile = new StreamWriter(basisFile + ".out");
				while (!exeProcess.StandardOutput.EndOfStream)
				{
					string line = exeProcess.StandardOutput.ReadLine();
					outputFile.WriteLine(line);
					UnityEngine.Debug.Log("Basis: " + line);
					// do something with line
				};
				exeProcess.WaitForExit();
				int exitCode = exeProcess.ExitCode;
				if (exitCode != 0)
				{
					UnityEngine.Debug.LogError("Basis decompress exit code " + exitCode);
				}
			}
			catch
			{
				// Log error.
			}
#endif
		}
#endif
		public void AddTextureData(UnityEngine.Texture texture, GameObject gameObject,avs.Texture textureData, bool highQualityUASTC, bool forceOverwrite )
		{
			if(!sessionResourceUids.TryGetValue(texture, out uid textureID))
			{
				UnityEngine.Debug.LogError(texture.name + " had its data extracted, but is not in the dictionary of processed resources!");
				return;
			}

			GetResourcePath(texture, out string resourcePath, false);
#if UNITY_EDITOR
			if(resourcePath==null||resourcePath == "")
			{
				resourcePath = SceneResourcePathManager.GetNonAssetResourcePath(texture, gameObject);
			}
			if (resourcePath == null || resourcePath == "")
			{
				UnityEngine.Debug.LogError("No resource path for texture: "+texture.name + "!");
				return;
			}
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(texture, out string guid);
			string textureAssetPath = AssetDatabase.GetAssetPath(texture).Replace("Assets/","");
			long lastModified = GetAssetWriteTimeUTC(textureAssetPath);
			bool genMips=false;
			int unmanagedSize = Server_GetStructSize("InteropTexture");
			int managedSize = (int)Marshal.SizeOf(typeof(avs.Texture));

			if (managedSize != unmanagedSize)
			{
				UnityEngine.Debug.LogError($"{nameof(avs.Texture)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!\n"
					+ $"This usually means that your TeleportServer.dll (or .so) is out of sync with the Unity plugin C# code.\n" +
					$"One or both of these needs to be updated.");
			}
			Server_StoreTexture(textureID,  resourcePath, lastModified, textureData,  genMips, highQualityUASTC, forceOverwrite);
#endif
		}
		class SubresourceImage16
		{
			public short[] bytes;
		}/*
 * Convert a 32-bit floating-point number in IEEE single-precision format to a 16-bit floating-point number in
 * IEEE half-precision format, in bit representation.
 *
 * @note The implementation relies on IEEE-like (no assumption about rounding mode and no operations on denormals)
 * floating-point operations and bitcasts between integer and floating-point variables.
 
		public static unsafe float fp32_from_bits(UInt32 value)
		{
			return *(float*)(&value);
		}
		public static unsafe UInt32 fp32_to_bits(float value)
		{
			return *(UInt32*)(&value);
		}
		short ConvertFloatToFloat16(float f)
		{
			float scale_to_inf = fp32_from_bits((UInt32)0x77800000); // exponent is 112
			float scale_to_zero = fp32_from_bits((UInt32)0x08800000); // exponent is -110
			// If 15 < e then inf, otherwise e += 2
			float basebase = (Math.Abs(f) * scale_to_inf) * scale_to_zero;

			UInt32 w = fp32_to_bits(f);
			// Right shift 1, so the sign bit is dropped, only exponent and mantissa are left
			UInt32 shl1_w = w + w;
			UInt32 sign = w & (UInt32)(0x80000000);
			UInt32 bias = shl1_w & (UInt32)(0xFF000000); // Get the exponent bits/value of fp32
			if (bias < (UInt32)(0x71000000))
			{ // 113 - 127 = -14, if true e is less than -14, then it is -14
				bias = (UInt32)(0x71000000);
			}

			// bias >> 1 move exponent bits to the bit positions they are supposed to be and assume sign to be positive
			// 0x07800000 (0000 0111 1000 000 ...) --> exponent 15 = 15 << 23
			// fp32_from_bits((bias >> 1) + (UInt32)(0x07800000)) --> exponent + 15
			// base: exponent + 2
			// To make the exponent the same as bigger operand, namely, exponent + 15, the base needes to be
			// shifted right 13 bits, the result of exponent would be exponent + 15, which
			// is the biased exponent in fp16
			// If true exponent is less than -14, then bias = -14, + (UInt32)(0x07800000) --> bias is fixed
			// at 1, base exponent is exponent e + 2, if e is -15, e + 2 is -13, so need to right shifted 14
			// bits, if e is -16, 15 bits.
			basebase = fp32_from_bits((bias >> 1) + (UInt32)(0x07800000)) + basebase;
			UInt32 bits = fp32_to_bits(basebase);
			// exp_bits needs to be right shifted 13 bits to shift from bit27 in fp32 to bit14 in fp16, droping 3 exponent bits away
			// If input exponent is 142 (1000 1110) (142 - 127 = 15), 142 + 15 = 1001 1101, discarding top 3 bits
			// 1 1101 --> 5 bits = 29
			// mantissa_bits takes bit10 also, which should be 1 when input exponent is 142, so 1 1101 + 1 = 1 1110 = 30
			// true exponent for 30 in fp16 is 30 - 15 = 15 which is the same as 142 in fp32
			// So droping top 3 bits is the same as substracting 128, and later adding mantissa_bits adds
			// 1 in exponent, so the whole effect of exponent + 15 - 128 + 1 = exponent - 127 + 15, which
			// is the formular to change exponent biased in fp32 to exponent biased in fp16
			UInt32 exp_bits = (bits >> 13) & (UInt32)(0x00007C00);// Get the 5-bit exponents 0111 1100
			UInt32 mantissa_bits = bits & (UInt32)(0x00000FFF);
			UInt32 nonsign = exp_bits + mantissa_bits;
			return (short)((sign >> 16) | (shl1_w > (UInt32)(0xFF000000) ? (UInt16)(0x7E00) : nonsign));
		}*/
		public void AddFp16TestTexture()
		{
#if UNITY_EDITOR
			string resourcePath ="TestFloat16Texture";
			string textureAssetPath = resourcePath;
			int unmanagedSize = Server_GetStructSize("InteropTexture");
			int managedSize = (int)Marshal.SizeOf(typeof(avs.Texture));
			if (managedSize != unmanagedSize)
			{
				UnityEngine.Debug.LogError($"{nameof(avs.Texture)} struct size mismatch between unmanaged code({unmanagedSize}) and managed code({managedSize})!\n"
					+ $"This usually means that your TeleportServer.dll (or .so) is out of sync with the Unity plugin C# code.\n" +
					$"One or both of these needs to be updated.");
			}
			uid textureID=Server_GetOrGenerateUid(resourcePath);
			avs.Texture avsTexture=new avs.Texture();
			string name=resourcePath;
			avsTexture.name=Marshal.StringToCoTaskMemUTF8(name);
			avsTexture.path = Marshal.StringToCoTaskMemUTF8(resourcePath);
			avsTexture.width=4;
			avsTexture.height=4;
			avsTexture.depth=1;
			avsTexture.arrayCount=1;
			avsTexture.mipCount=1;

			avsTexture.format=avs.TextureFormat.RGBA16F;
			avsTexture.compression=avs.TextureCompression.KTX;
			avsTexture.compressed =false;

			avsTexture.valueScale=1.0F;
			avsTexture.cubemap=false;

			List<SubresourceImage16> subresourceImages = new List<SubresourceImage16>();
			subresourceImages.Add(new SubresourceImage16());

			UInt16[] numImages = { ((UInt16)1) };
			byte[] numImagesBytes = MemoryMarshal.Cast<UInt16, byte>(numImages).ToArray();

			int memoryRequired = Marshal.SizeOf<UInt16>();
			List<UInt32> offsets = new List<UInt32>();
			memoryRequired += Marshal.SizeOf<UInt32>() * 1;
			foreach (SubresourceImage16 subresourceImage in subresourceImages)
			{
				offsets.Add((UInt32)memoryRequired);
				subresourceImage.bytes=new short[4*2*avsTexture.width * avsTexture.height];
				for(UInt32 i = 0; i < subresourceImage.bytes.Length/4; i++)
				{
					subresourceImage.bytes[i*4+0]=15360;//ConvertFloatToFloat16(1.0F);
					subresourceImage.bytes[i * 4 + 1] = 0;//ConvertFloatToFloat16(0.0F);
					subresourceImage.bytes[i * 4 + 2] = 15360;//ConvertFloatToFloat16(1.0F);
					subresourceImage.bytes[i * 4 + 3] = 15360;//ConvertFloatToFloat16(1.0F);
				}
				memoryRequired += subresourceImage.bytes.Length;
			}
			avsTexture.data = Marshal.AllocCoTaskMem((int)memoryRequired);
			IntPtr targetPtr = avsTexture.data;
			Marshal.Copy(numImagesBytes, 0, targetPtr, Marshal.SizeOf<UInt16>());
			targetPtr += Marshal.SizeOf<UInt16>();

			byte[] offsetsBytes = MemoryMarshal.Cast<UInt32, byte>(offsets.ToArray()).ToArray();
			Marshal.Copy(offsetsBytes, 0, targetPtr, offsetsBytes.Length);
			targetPtr += offsetsBytes.Length;
			foreach (SubresourceImage16 subresourceImage in subresourceImages)
			{
				Marshal.Copy(subresourceImage.bytes, 0, targetPtr, (int)subresourceImage.bytes.Length);
				targetPtr += subresourceImage.bytes.Length;
			}
			avsTexture.dataSize=(uint)memoryRequired;
			Server_StoreTexture(textureID, resourcePath, 0, avsTexture, false, true, true);
			Marshal.FreeCoTaskMem(avsTexture.data);
#endif
		}

		//Extract bone and add to processed resources; will also add parent and child bones.
		//	bone : The bone we are extracting.
		//	boneList : Array of bones; usually taken from skinned mesh renderer.
		public void CreateBone(UnityEngine.Transform bone, ForceExtractionMask forceMask, Dictionary<UnityEngine.Transform,uid> boneIDs, List<uid> jointIDs,List<avs.Node> avsNodes)
		{
			uid boneID=0;
			sessionResourceUids.TryGetValue(bone, out boneID);
			if (boneID!=0&&jointIDs.Contains(boneID))
				return;
			if (boneID == 0)
            {

                boneID = boneIDs[bone];
            }
            else
            {
				if(boneID!=boneIDs[bone])
                {
					UnityEngine.Debug.LogError("Bone id's don't match");
					return;
                }
            }
            if (boneIDs.ContainsKey(bone))
            {
                //Just return the ID; if we have already processed the transform and the bone can be found on the unmanaged side.
                if (boneID != 0 && Server_IsNodeStored(boneID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
                {
                    return ;
                }

                //Add to sessionResourceUids first to prevent a stack overflow from recursion. 
                boneID = boneID == 0 ? Server_GenerateUid() : boneID;
                sessionResourceUids[bone] = boneID;

                // The parent of the bone might NOT be in the skeleton! Unity's avatar system can skip objects in the hierarchy.
                // So we must skip transforms to find the parent that's actually in the list of bones!
                uid parentID = 0;
				avs.Transform avsTransform = avs.Transform.FromLocalUnityTransform(bone);

				if (bone != boneIDs.First().Key)
                {
					UnityEngine.Transform parent = bone.parent;
                    while (parent.parent != null && parent.parent != boneIDs.First().Key && !boneIDs.ContainsKey(parent))
					{
						// We want the transform of the bone relative to its skeleton parent, not its Unity GameObject parent.
						avsTransform.rotation = parent.localRotation * avsTransform.rotation;
						avsTransform.position = parent.localRotation * avsTransform.position + parent.localPosition;
						parent = parent.parent;
					}
                    parentID = FindResourceID(parent);
                    if (parentID == 0)
                    {
						UnityEngine.Debug.Log("Unable to extract bone: parent not found for " + bone.name);
                        return;
                    }
				}
                // There should be only ONE bone with parent id 0.
                //parentID = parentID == 0 ? AddBone(parent,  forceMask, bones, jointIDs) : parentID;

                avs.Node boneNode = new avs.Node();
                boneNode.priority = 0;
                boneNode.name = Marshal.StringToCoTaskMemUTF8(bone.name);
                boneNode.parentID = parentID;
                boneNode.localTransform = avsTransform;

                boneNode.dataType = avs.NodeDataType.None;

                // don't fill the children yet. We probably don't have their id's.
				avsNodes.Add(boneNode);
            
                sessionResourceUids[bone] = boneID;
                jointIDs.Add(boneID);
            }
			return;
		}
		static public void GetBones(UnityEngine.Transform rootBone, List<UnityEngine.Transform> bones)
		{
			bones.Add(rootBone);
			for (int i = 0; i < rootBone.childCount; i++)
			{
				var child = rootBone.GetChild(i);
				GetBones(child, bones);
			}
		}
		// Ensure that every bone in a hierarchy has: a) a uid, and b) a StreamableNode
		public void BuildBoneNodeList(UnityEngine.Transform rootBone, Dictionary<UnityEngine.Transform, uid> boneIDs)
		{
			uid parentID=0;
			if(rootBone.parent!=null)
			{
				sessionResourceUids.TryGetValue(rootBone.parent.gameObject,out parentID);
			}
			sessionResourceUids.TryGetValue(rootBone.gameObject, out uid boneID);
            if (boneID == 0)
            {
				boneID=Server_GenerateUid();
                sessionResourceUids[rootBone.gameObject] = boneID;
            }
			boneIDs[rootBone] =boneID;

			if(rootBone.GetComponent<StreamableNode>() == null)
				rootBone.gameObject.AddComponent<StreamableNode>();

			ulong numChildren = (ulong)rootBone.childCount;
			for (uint i =0; i < numChildren; i++)
            {
				var child= rootBone.GetChild((int)i);
				BuildBoneNodeList(child, boneIDs);
			}
		}
		public void CompressTextures()
		{
			UInt64 totalTexturesToCompress = Server_GetNumberOfTexturesWaitingForCompression();

			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			Server_SetCompressionLevels(teleportSettings.serverSettings.compressionLevel, teleportSettings.serverSettings.qualityLevel);
		}

		private void AddChildNodes(GameObject gameObject, ForceExtractionMask forceMask,bool verify)
		{
			//Extract children of node, through transform hierarchy.
			// Cheating, but to help with skeletons, extract skinned mesh children first.
			for(int i = 0; i < gameObject.transform.childCount; i++)
			{
				GameObject child = gameObject.transform.GetChild(i).gameObject;
				if(child.GetComponent<SkinnedMeshRenderer>()!=null)
					 AddNode(child, forceMask, true, verify);
			}
			for (int i = 0; i < gameObject.transform.childCount; i++)
			{
				GameObject child = gameObject.transform.GetChild(i).gameObject;
				if (child.GetComponent<SkinnedMeshRenderer>() == null)
					AddNode(child, forceMask, true, verify);
			}
		}

		private void ExtractNodeMaterials(UnityEngine.Material[] sourceMaterials, GameObject gameObject,ref avs.Node extractTo, ForceExtractionMask forceMask)
		{
			List<uid> materialIDs = new List<uid>();
			foreach(UnityEngine.Material material in sourceMaterials)
			{
				uid materialID = AddMaterial(material,  gameObject, forceMask);

				if(materialID != 0)
				{
					materialIDs.Add(materialID);
					
					//Check GeometryStore to see if material actually exists, if it doesn't then there is a mismatch between the managed code data and the unmanaged code data.
					if(!Server_IsMaterialStored(materialID))
					{
						UnityEngine.Debug.LogError($"Missing material {material.name}({materialID}), which was added to \"{extractTo.name}\".");
					}
				}
				else
				{
					UnityEngine.Debug.LogWarning($"Received 0 for ID of material on game object: {extractTo.name}");
				}
			}
			extractTo.numMaterials = (ulong)materialIDs.Count;
			extractTo.materialIDs = materialIDs.ToArray();
		}
		public uid ExtractTextCanvas(TextCanvas textCanvas)
		{
			if (!textCanvas.font)
				return 0;

			GetResourcePath(textCanvas.font, out string resourcePath, false);
#if UNITY_EDITOR
			string fontAssetPath = AssetDatabase.GetAssetPath(textCanvas.font).Replace("Assets/", "");
			long lastModified = GetAssetWriteTimeUTC(fontAssetPath);
			string font_ttf_path = System.IO.Directory.GetParent(Application.dataPath).ToString().Replace("\\","/") +"/Assets/"+ fontAssetPath;
		
			uid font_uid= Server_StoreFont(font_ttf_path,resourcePath,lastModified,textCanvas.size);
		#else
			uid font_uid= Server_GetOrGenerateUid(resourcePath);
		#endif
			string canvasAssetPath="Text/"+textCanvas.name;
			teleport.InteropTextCanvas interopTextCanvas=new teleport.InteropTextCanvas();
			interopTextCanvas.text=Marshal.StringToCoTaskMemUTF8(textCanvas.text);
			if(interopTextCanvas.text==null)
				return 0;
			interopTextCanvas.font=Marshal.StringToCoTaskMemUTF8(resourcePath);
			interopTextCanvas.pointSize=textCanvas.size;
			interopTextCanvas.lineHeight=textCanvas.lineHeight;
			interopTextCanvas.colour=textCanvas.colour;
			
			// We store the canvas text. This is a pure text asset, treated as a mutable resource - i.e. it can be modified in real time.
			uid u= Server_StoreTextCanvas(canvasAssetPath,interopTextCanvas);
			return u;
		}
		private bool ExtractNodeMeshData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask, bool verify)
		{
			MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
			MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
			if(!meshFilter || !meshRenderer || !meshRenderer.enabled)
			{
				return false;
			}
			extractTo.renderState.lightmapScaleOffset=meshRenderer.lightmapScaleOffset;
			UnityEngine.Texture[] giTextures = GlobalIlluminationExtractor.GetTextures();
			if(meshRenderer.lightmapIndex>=0&&meshRenderer.lightmapIndex<0xFFFE)
			{
				if(giTextures != null)
				{ 
					// If this index not found? These errors are not fatal, but should not occur.
					if(meshRenderer.lightmapIndex>=giTextures.Length)
					{
						UnityEngine.Debug.LogError($"For GameObject \"{gameObject.name}\", lightmap "+ meshRenderer.lightmapIndex + " was not listed in GlobalIlluminationExtractor!");
					}
					else
					{
						var lightmap_texture= giTextures[meshRenderer.lightmapIndex];
						extractTo.renderState.globalIlluminationTextureUid = FindResourceID(lightmap_texture);
						if (extractTo.renderState.globalIlluminationTextureUid == 0)
						{
							extractTo.renderState.globalIlluminationTextureUid=AddTexture(lightmap_texture, gameObject, forceMask);
							if (extractTo.renderState.globalIlluminationTextureUid == 0)
							{
								UnityEngine.Debug.LogError($"For GameObject \"{gameObject.name}\", lightmap " + meshRenderer.lightmapIndex + " was not found in GeometrySource!");
							}
						}
					}
				}
				else
				{
					UnityEngine.Debug.LogError($"For GameObject \"{gameObject.name}\", lightmap " + meshRenderer.lightmapIndex + " was not found in giTextures!");
				}
			}
			//Extract mesh used on node.
			SceneReferenceManager sceneReferenceManager= SceneReferenceManager.GetSceneReferenceManager(gameObject.scene);
			if (sceneReferenceManager == null)
			{
				UnityEngine.Debug.LogError($"Failed to get SceneReferenceManager for GameObject \"{gameObject.name}\"!");
				return false;
			}
			UnityEngine.Mesh mesh = sceneReferenceManager.GetMeshFromGameObject(gameObject);
			if(mesh&&(mesh.hideFlags&UnityEngine.HideFlags.DontSave)== UnityEngine.HideFlags.DontSave)
			{
				// This is ok, it just means we're extracting one of the standard meshes: sphere, cube, etc.
				//Debug.Log("GeometrySource.ExtractNodeMeshData extracting "+gameObject.name+" with HideFlags!");
			}
			MeshTracker meshTracker = gameObject.GetComponent<MeshTracker>();
			if (mesh == null)
			{
				// This is ok, there might be no mesh at all.
				//UnityEngine.Debug.LogError($"Failed GeometrySource.ExtractNodeMeshData for GameObject \"{gameObject.name}\"!");
				if (meshTracker != null)
				{
					// We're running, so just retrieve from the mesh tracker.
					extractTo.dataID = Server_PathToUid( meshTracker.resourcePath);
					extractTo.dataType=avs.NodeDataType.Mesh;
					return true;
				}
				else
				{
					return false;
				}
			}
			extractTo.dataID = AddMesh(mesh,  gameObject ,forceMask, verify);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				// This is ok.
				return false;
			}
			extractTo.dataType = avs.NodeDataType.Mesh;

			ExtractNodeMaterials(meshRenderer.sharedMaterials, gameObject,ref extractTo, forceMask);
			
			return true;
		}

		private bool ExtractNodeSkinnedMeshData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask, bool verify)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
			extractTo.renderState.lightmapScaleOffset = skinnedMeshRenderer.lightmapScaleOffset;
			var giTextures = GlobalIlluminationExtractor.GetTextures();
			if (skinnedMeshRenderer.lightmapIndex >= 0 && skinnedMeshRenderer.lightmapIndex < 0xFFFE && skinnedMeshRenderer.lightmapIndex < giTextures.Length)
				extractTo.renderState.globalIlluminationTextureUid = FindResourceID(giTextures[skinnedMeshRenderer.lightmapIndex]);
			//Extract mesh used on node.
			SceneReferenceManager sceneReferenceManager = SceneReferenceManager.GetSceneReferenceManager(gameObject.scene);
			extractTo.dataType = avs.NodeDataType.Mesh;
			// Add the skeleton. If it's shared between multiple meshes, it may be already there.
			var skeletonRootTransform = GetTopmostSkeletonRoot(skinnedMeshRenderer);
			extractTo.skeletonNodeID = AddSkeleton(skeletonRootTransform, forceMask);

			UnityEngine.Mesh mesh=null;
			var meshTracker = gameObject.GetComponent<MeshTracker>();
			if (sceneReferenceManager)
			{ 
				mesh =sceneReferenceManager.GetMeshFromGameObject(gameObject);
			}
			else if(gameObject.GetComponent<MeshFilter>() != null)
			{
				mesh=gameObject.GetComponent<MeshFilter>().mesh;
			}
			else if (meshTracker != null)
			{
				mesh=meshTracker.mesh;
			}
			else
			{
				var sceneName = gameObject.scene != null && gameObject.scene.name != null ? gameObject.scene.name : "null";
				UnityEngine.Debug.LogError($"Failed to get mesh for GameObject: {gameObject.name} in scene: {sceneName}");
				return false;
			}
			extractTo.dataID = AddMesh(mesh, gameObject,forceMask, verify);

			//Can't create a node with no data.
			if(extractTo.dataID == 0)
			{
				UnityEngine.Debug.LogError($"Failed to extract skinned mesh data from GameObject: {gameObject.name}");
				return false;
			}
			ExtractNodeMaterials(skinnedMeshRenderer.sharedMaterials,  gameObject, ref extractTo, forceMask);

			return true;
		}
		private bool ExtractNodeLinkData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask)
		{
			teleport.Link link=gameObject.GetComponent<teleport.Link>();
			if(!link)
				return false;
			if (!sessionResourceUids.TryGetValue(link, out extractTo.dataID))
			{
				extractTo.dataID = Server_GenerateUid();
			}
			sessionResourceUids[link] = extractTo.dataID;
			extractTo.dataType = avs.NodeDataType.Link;
			extractTo.url= Marshal.StringToCoTaskMemUTF8(link.url);
			extractTo.query_url = Marshal.StringToCoTaskMemUTF8(link.query_url);
			return true;
		}
		private bool ExtractNodeLightData(GameObject gameObject, ref avs.Node extractTo, ForceExtractionMask forceMask)
		{
			Light light = gameObject.GetComponent<Light>();
			if(!light || !light.isActiveAndEnabled)
			{
				return false;
			}

			if(!sessionResourceUids.TryGetValue(light, out extractTo.dataID))
			{
				extractTo.dataID = Server_GenerateUid();
			}
			sessionResourceUids[light] = extractTo.dataID;
			extractTo.dataType = avs.NodeDataType.Light;

			Color lightColour = light.color;
			TeleportSettings teleportSettings = TeleportSettings.GetOrCreateSettings();
			extractTo.lightColour = (teleportSettings.colorSpace == ColorSpace.Linear) ? lightColour.linear * light.intensity : lightColour.gamma * light.intensity;
			extractTo.lightType = (byte)light.type;
			extractTo.lightRange = light.range ;
			extractTo.lightRadius = light.range / 5.0F;
			// For Unity, lights point along the X-axis, so:
			extractTo.lightDirection = new avs.Vector3(0, 0, 1.0F);

			return true;
		}
		//! Add the skeleton associated with the specified SkinnedMeshRenderer.
		//! Multiple Skinned Meshes can share the same skeleton, and we only need to add each one once.
		//! This fn returns the id of the skeleton Node. The skeleton asset has its own id.
		private uid AddSkeleton(UnityEngine.Transform skeletonRootTransform, ForceExtractionMask forceMask)
		{
			// In unity, this isn't really an asset, it has no location on disk.
			uid skeletonRootNodeID = 0;
			uid skeletonAssetID=0;
			teleport.SkeletonRoot skeletonRoot= skeletonRootTransform.gameObject.GetComponent<teleport.SkeletonRoot>();
			if (skeletonRoot == null|| forceMask!= ForceExtractionMask.FORCE_NOTHING)
			{
				if(skeletonRoot == null)
					skeletonRoot=skeletonRootTransform.gameObject.AddComponent<teleport.SkeletonRoot>();
				skeletonRoot.assetPath="";
				if (skeletonRootTransform.gameObject.scene!=null)
					skeletonRoot.assetPath+= skeletonRootTransform.gameObject.scene.name+".";
				skeletonRoot.assetPath+= skeletonRootTransform.name;
			}
			//Just return the ID; if we have already processed the skeleton and the skeleton can be found on the unmanaged side.
			if (sessionResourceUids.TryGetValue(skeletonRootTransform.gameObject, out skeletonRootNodeID) )
			{
				if(Server_IsNodeStored(skeletonRootNodeID) && (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_NOTHING)
				{
					return skeletonRootNodeID;
				}
			}
			skeletonUids.TryGetValue(skeletonRoot.assetPath, out skeletonAssetID);
			skeletonAssetID = skeletonAssetID == 0 ? Server_GenerateUid() : skeletonAssetID;
			skeletonUids[skeletonRoot.assetPath] = skeletonAssetID;

			avs.Skeleton skeleton = new avs.Skeleton();
			skeleton.name = Marshal.StringToCoTaskMemUTF8(skeletonRootTransform.name);
			skeleton.path = Marshal.StringToCoTaskMemUTF8(skeletonRoot.assetPath);

			List<uid> jointIDs = new List<uid>();
			HashSet<UnityEngine.Transform> done=new HashSet<UnityEngine.Transform>();
			Dictionary<UnityEngine.Transform,uid> boneIDs=new Dictionary<UnityEngine.Transform, uid>();
			// First, make sure every bone has a uid.
			// Note: we can end up here with MORE joint ID's than there are "bones" in the Skinned Mesh Renderer, because
			// Unity is allowed to skip over non-animated nodes. We must obtain the full hierarchy.
			BuildBoneNodeList(skeletonRootTransform, boneIDs);
			if(boneIDs.Count==0)
				return 0;
			skeletonRootNodeID = boneIDs[skeletonRootTransform];

			// TODO: uid's may be a bad way to do this because it only applies to one skeleton...
			skeleton.boneIDs = boneIDs.Values.ToArray();
			skeleton.numBones = boneIDs.Count;

			if(skeletonRootTransform.parent)
				skeleton.rootTransform = avs.Transform.FromLocalUnityTransform(skeletonRootTransform.parent);

			Server_StoreSkeleton(skeletonAssetID, skeleton);
			return skeletonRootNodeID;
		}

		private avs.Mat4x4[] ExtractInverseBindMatrices(UnityEngine.Mesh mesh)
		{
			avs.Mat4x4[] bindMatrices = new avs.Mat4x4[mesh.bindposes.Length];

			for(int i = 0; i < mesh.bindposes.Length; i++)
			{
				bindMatrices[i] = mesh.bindposes[i];
			}

			return bindMatrices;
		}
		public void FindBadMeshes(UnityEngine.GameObject g,bool fixit=false)
		{
			var meshes=g.GetComponentsInChildren<MeshFilter>();
			foreach(var m in meshes)
			{
				var mesh = m.sharedMesh;
				bool meshbad=false;
				if(!mesh.isReadable)
					continue;
				List<int> badVertices=new List<int>();
				for (int i = 0; i < mesh.vertices.Length; i++)
				{
					var v=mesh.vertices[i];
					bool isbad = false;
					isbad |= (Single.IsNaN(v.x) || Single.IsInfinity(v.x));
					isbad |= (Single.IsNaN(v.y) || Single.IsInfinity(v.y));
					isbad |= (Single.IsNaN(v.z) || Single.IsInfinity(v.z));
					if(isbad)
					{
						//UnityEngine.Debug.LogWarning("Found bad vertex position "+i);
						if(fixit)
							mesh.vertices[i] = new UnityEngine.Vector3(0, 0, 0);
					}
					meshbad|=isbad;
				}
				for (int i = 0; i < mesh.normals.Length; i++)
				{
					var v = mesh.normals[i];
					bool isbad = false;
					isbad |= (Single.IsNaN(v.x) || Single.IsInfinity(v.x));
					isbad |= (Single.IsNaN(v.y) || Single.IsInfinity(v.y));
					isbad |= (Single.IsNaN(v.z) || Single.IsInfinity(v.z));
					if (isbad)
					{
						//UnityEngine.Debug.LogWarning("Found bad vertex normal " + i);
						if (fixit)
							mesh.normals[i]=new UnityEngine.Vector3(0,0,1.0f);
					}
					meshbad |= isbad;
				}
				UnityEngine.Vector2[] uv0=mesh.uv;
				UnityEngine.Vector2[] uv1=mesh.uv2;
				UnityEngine.Vector2[][] uvs={uv0,uv1};
				for (int i = 0; i < uvs.Length; i++)
				{
					var uv= uvs[i];
					for (int j = 0; j < uv.Length; j++)
					{
						var v = uv[j];
						bool isbad = false;
						isbad |= (Single.IsNaN(v.x) || Single.IsInfinity(v.x));
						isbad |= (Single.IsNaN(v.y) || Single.IsInfinity(v.y));
						if (isbad)
						{
							//UnityEngine.Debug.LogWarning("Found bad texcoord " + i);
							if (fixit)
								uv[j] = new UnityEngine.Vector2(0, 0);
						}
						meshbad |= isbad;
					}
				}
				if (meshbad)
				{
					if(fixit)
					{
						mesh.uv=uvs[0];
						mesh.uv2=uvs[1];
						mesh.RecalculateNormals();
						mesh.UploadMeshData(false);
					}
					UnityEngine.Debug.LogError("Found bad data in mesh of game object "+g.name,g);
				}
			}
		}
		private void ExtractMeshData(avs.AxesStandard extractToBasis, UnityEngine.Mesh mesh, uid meshID,bool verify,bool merge=true)
		{
			UInt64 localId=1;
			avs.PrimitiveArray[] primitives = new avs.PrimitiveArray[mesh.subMeshCount];
			Dictionary<UInt64, avs.Accessor> accessors = new Dictionary<UInt64, avs.Accessor>(6);
			Dictionary<UInt64, avs.BufferView> bufferViews = new Dictionary<UInt64, avs.BufferView>(6);
			Dictionary<UInt64, avs.GeometryBuffer> buffers = new Dictionary<UInt64, avs.GeometryBuffer>(5);

			UInt64 positionAccessorID = localId++;
			UInt64 normalAccessorID = localId++;
			UInt64 tangentAccessorID = localId++;
			UInt64 uv0AccessorID = localId++;
			// Let's always generate an accessor for uv2. But if there are no uv's there we'll just point it at uv0.
			UInt64 uv2AccessorID = localId++;//mesh.uv2.Length != 0 ? localId++ : 0;
			UInt64 jointAccessorID = 0;
			UInt64 weightAccessorID = 0;

			//Generate IDs for joint and weight accessors if the model has bones.
			if(mesh.boneWeights.Length != 0)
			{
				jointAccessorID = localId++;
				weightAccessorID = localId++;
			}
			UInt64 inverseBindMatricesID =0;
			if (mesh.bindposes.Length > 0)
			{
				inverseBindMatricesID = localId++;
			}

			//Position Buffer:
			{
				CreateMeshBufferAndView(extractToBasis, mesh.vertices, buffers, bufferViews,ref localId, out UInt64 positionViewID);

				accessors.Add
				(
					positionAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC3,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.vertexCount,
						bufferView = positionViewID,
						byteOffset = 0
					}
				);
			}

			//Normal Buffer
			{
				CreateMeshBufferAndView(extractToBasis, mesh.normals, buffers, bufferViews, ref localId, out UInt64 normalViewID);

				accessors.Add
				(
					normalAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC3,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.normals.Length,
						bufferView = normalViewID,
						byteOffset = 0
					}
				);
			}

			//Tangent Buffer
			if(mesh.tangents.Length>0)
			{
				CreateMeshBufferAndView(extractToBasis, mesh.tangents, buffers, bufferViews, ref localId, out UInt64 tangentViewID);

				accessors.Add
				(
					tangentAccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC4,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.tangents.Length,
						bufferView = tangentViewID,
						byteOffset = 0
					}
				);
			}
			bool have_uv2 = false;

			//UVs/Tex-Coords Buffer (All UVs are combined into a single buffer, with a view for each UV):
			{
				//Two floats per Vector2 * four bytes per float.
				int stride = 2 * 4;

				avs.GeometryBuffer uvBuffer = new avs.GeometryBuffer();
				uvBuffer.byteLength = (ulong)((mesh.uv.Length + mesh.uv2.Length) * stride);
				uvBuffer.data = new byte[uvBuffer.byteLength];
				int uv0Size = mesh.uv.Length * stride;
				//Buffer.BlockCopy(mesh.uv, 0, uvBuffer.data, 0, uv0Size);
				//Buffer.BlockCopy(mesh.uv2, 0, uvBuffer.data, uv0Size, mesh.uv2.Length*stride);
				//BitConverter.GetBytes(mesh.uv).CopyTo(uvBuffer.data,0);
				//Get byte data from first UV channel.
				for (int i = 0; i < mesh.uv.Length; i++)
				{
					BitConverter.GetBytes(mesh.uv[i].x).CopyTo(uvBuffer.data, i * stride + 0);
					BitConverter.GetBytes(mesh.uv[i].y).CopyTo(uvBuffer.data, i * stride + 4);
				}

				//Get byte data from second UV channel.
				for(int i = 0; i < mesh.uv2.Length; i++)
				{
					BitConverter.GetBytes(mesh.uv2[i].x).CopyTo(uvBuffer.data, uv0Size + i * stride + 0);
					BitConverter.GetBytes(mesh.uv2[i].y).CopyTo(uvBuffer.data, uv0Size + i * stride + 4);
				}

				UInt64 uvBufferID = localId++;
				UInt64 uv0ViewID = localId++;
				UInt64 uv2ViewID = mesh.uv2.Length != 0 ? localId++ : 0;

				buffers.Add(uvBufferID, uvBuffer);

				//Buffer view for first UV channel.
				bufferViews.Add
				(
					uv0ViewID,
					new avs.BufferView
					{
						buffer = uvBufferID,
						byteOffset = 0,
						byteLength = (ulong)(mesh.uv.Length * stride),
						byteStride = (ulong)stride
					}
				);

				if(mesh.uv2.Length != 0)
				{
					//Buffer view for second UV channel.
					bufferViews.Add
					(
						uv2ViewID,
						new avs.BufferView
						{
							buffer = uvBufferID,
							byteOffset = (ulong)(mesh.uv.Length * stride), //Offset is length of first UV channel.
							byteLength = (ulong)(mesh.uv2.Length * stride),
							byteStride = (ulong)stride
						}
					);
				}

				//Accessor for first UV channel.
				accessors.Add
				(
					uv0AccessorID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.VEC2,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.uv.Length,
						bufferView = uv0ViewID,
						byteOffset = 0
					}
				);
				if (mesh.uv2.Length != 0)
				{
					//Accessor for second UV channel.
					accessors.Add
					(
						uv2AccessorID,
						new avs.Accessor
						{
							type = avs.Accessor.DataType.VEC2,
							componentType = avs.Accessor.ComponentType.FLOAT,
							count = (ulong)mesh.uv2.Length,
							bufferView = uv2ViewID,
							byteOffset = 0
						}
					);
					have_uv2 = true;
				}
				else
				{
					// If not present, use the regular uv's for channel 2:
					accessors.Add
					(
						uv2AccessorID,
						new avs.Accessor
						{
							type = avs.Accessor.DataType.VEC2,
							componentType = avs.Accessor.ComponentType.FLOAT,
							count = (ulong)mesh.uv.Length,
							bufferView = uv0ViewID,
							byteOffset = 0
						}
					);
					have_uv2=true;
				}
			}

			//Joint and Weight Buffers
			if(mesh.boneWeights.Length != 0)
			{
				//Four bytes per int * four ints per BoneWeight.
				int jointStride = 4 * 4;
				//Four bytes per float * four floats per boneweight
				int weightStride = 4 * 4;

				///Buffers
				avs.GeometryBuffer jointBuffer = new avs.GeometryBuffer();
				jointBuffer.byteLength = (ulong)(mesh.boneWeights.Length * jointStride);
				jointBuffer.data = new byte[jointBuffer.byteLength];

				avs.GeometryBuffer weightBuffer = new avs.GeometryBuffer();
				weightBuffer.byteLength = (ulong)(mesh.boneWeights.Length * weightStride);
				weightBuffer.data = new byte[weightBuffer.byteLength];
				int minBoneIndex=0xFFFFFF;
				int maxBoneIndex=0;
				for(int i = 0; i < mesh.boneWeights.Length; i++)
				{
					BoneWeight boneWeight = mesh.boneWeights[i];
					maxBoneIndex=Math.Max(maxBoneIndex, boneWeight.boneIndex0);
					maxBoneIndex = Math.Max(maxBoneIndex, boneWeight.boneIndex1);
					maxBoneIndex = Math.Max(maxBoneIndex, boneWeight.boneIndex2);
					maxBoneIndex = Math.Max(maxBoneIndex, boneWeight.boneIndex3);
					minBoneIndex = Math.Min(minBoneIndex, boneWeight.boneIndex0);
					minBoneIndex = Math.Min(minBoneIndex, boneWeight.boneIndex1);
					minBoneIndex = Math.Min(minBoneIndex, boneWeight.boneIndex2);
					minBoneIndex = Math.Min(minBoneIndex, boneWeight.boneIndex3);
					BitConverter.GetBytes((int)boneWeight.boneIndex0).CopyTo(jointBuffer.data, i * jointStride + 00 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex1).CopyTo(jointBuffer.data, i * jointStride + 01 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex2).CopyTo(jointBuffer.data, i * jointStride + 02 * 4);
					BitConverter.GetBytes((int)boneWeight.boneIndex3).CopyTo(jointBuffer.data, i * jointStride + 03 * 4);

					BitConverter.GetBytes(boneWeight.weight0).CopyTo(weightBuffer.data, i * weightStride + 00 * 4);
					BitConverter.GetBytes(boneWeight.weight1).CopyTo(weightBuffer.data, i * weightStride + 01 * 4);
					BitConverter.GetBytes(boneWeight.weight2).CopyTo(weightBuffer.data, i * weightStride + 02 * 4);
					BitConverter.GetBytes(boneWeight.weight3).CopyTo(weightBuffer.data, i * weightStride + 03 * 4);
				}

				UInt64 jointBufferID = localId++;
				UInt64 weightBufferID = localId++;
				buffers.Add(jointBufferID, jointBuffer);
				buffers.Add(weightBufferID, weightBuffer);

				///Buffer Views
				avs.BufferView jointBufferView = new avs.BufferView
				{
					buffer = jointBufferID,
					byteOffset = 0,
					byteLength = jointBuffer.byteLength,
					byteStride = (ulong)jointStride
				};

				avs.BufferView weightBufferView = new avs.BufferView
				{
					buffer = weightBufferID,
					byteOffset = 0,
					byteLength = weightBuffer.byteLength,
					byteStride = (ulong)weightStride
				};

				UInt64 jointBufferViewID = localId++;
				UInt64 weightBufferViewID = localId++;
				bufferViews.Add(jointBufferViewID, jointBufferView);
				bufferViews.Add(weightBufferViewID, weightBufferView);

				// Accessors
				avs.Accessor jointAccessor = new avs.Accessor
				{
					type = avs.Accessor.DataType.VEC4,
					componentType = avs.Accessor.ComponentType.INT,
					count = (ulong)mesh.boneWeights.Length,
					bufferView = jointBufferViewID,
					byteOffset = 0
				};

				avs.Accessor weightAccessor = new avs.Accessor
				{
					type = avs.Accessor.DataType.VEC4,
					componentType = avs.Accessor.ComponentType.FLOAT,
					count = (ulong)mesh.boneWeights.Length,
					bufferView = weightBufferViewID,
					byteOffset = 0
				};

				accessors.Add(jointAccessorID, jointAccessor);
				accessors.Add(weightAccessorID, weightAccessor);
			}

			if(mesh.bindposes.Length > 0)
			{
				CreateMeshBufferAndView(extractToBasis, mesh.bindposes, buffers, bufferViews, ref localId, out UInt64 invBindViewID);

				accessors.Add
				(
					inverseBindMatricesID,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.MAT4,
						componentType = avs.Accessor.ComponentType.FLOAT,
						count = (ulong)mesh.bindposes.Length,
						bufferView = invBindViewID,
						byteOffset = 0
					}
				);
			}
			//Oculus Quest OVR rendering uses USHORTs for non-instanced meshes.
			int indexBufferStride = (extractToBasis == avs.AxesStandard.GlStyle || mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16) ? 2 : 4;

			//Index Buffer
			CreateIndexBufferAndView(indexBufferStride, mesh.triangles, buffers, bufferViews, ref localId, out UInt64 indexViewID);

			//Create Attributes
			for(int i = 0; i < primitives.Length; i++)
			{
				primitives[i].attributeCount = (ulong)(4 + (have_uv2 ? 1 : 0) + (mesh.boneWeights.Length != 0 ? 2 : 0));
				primitives[i].attributes = new avs.Attribute[primitives[i].attributeCount];
				primitives[i].primitiveMode = avs.PrimitiveMode.TRIANGLES;

				primitives[i].attributes[0] = new avs.Attribute { accessor = positionAccessorID, semantic = avs.AttributeSemantic.POSITION };
				primitives[i].attributes[1] = new avs.Attribute { accessor = normalAccessorID, semantic = avs.AttributeSemantic.NORMAL };
				primitives[i].attributes[2] = new avs.Attribute { accessor = tangentAccessorID, semantic = avs.AttributeSemantic.TANGENT };
				primitives[i].attributes[3] = new avs.Attribute { accessor = uv0AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_0 };

				int additionalAttributes = 0;
				if(have_uv2)//mesh.uv2.Length != 0) always add uv2s.
				{
					primitives[i].attributes[4 + additionalAttributes] = new avs.Attribute { accessor = uv2AccessorID, semantic = avs.AttributeSemantic.TEXCOORD_1 };
					additionalAttributes += 1;
				}

				if(mesh.boneWeights.Length != 0)
				{
					primitives[i].attributes[4 + additionalAttributes] = new avs.Attribute { accessor = jointAccessorID, semantic = avs.AttributeSemantic.JOINTS_0 };
					primitives[i].attributes[5 + additionalAttributes] = new avs.Attribute { accessor = weightAccessorID, semantic = avs.AttributeSemantic.WEIGHTS_0 };
					additionalAttributes += 2;
				}

				primitives[i].indices_accessor = localId++;
				accessors.Add
				(
					primitives[i].indices_accessor,
					new avs.Accessor
					{
						type = avs.Accessor.DataType.SCALAR,
						componentType = (indexBufferStride == 2) ? avs.Accessor.ComponentType.USHORT : avs.Accessor.ComponentType.UINT,
						count = mesh.GetIndexCount(i),
						bufferView = indexViewID,
						byteOffset = (UInt64)mesh.GetIndexStart(i) * (UInt64)indexBufferStride
					}
				);
			}

#if UNITY_EDITOR
			SceneReferenceManager.GetGUIDAndLocalFileIdentifier(mesh, out string guid);
			GetResourcePath(mesh, out string resourcePath, false);
			
			long last_modified=0;
			// If it's one of the default resources, we must generate a prop
			if (!guid.Contains("0000000000000000e000000000000000")|| resourcePath.Contains("unity default resources"))
			{
				last_modified = GetAssetWriteTimeUTC(AssetDatabase.GUIDToAssetPath(guid.Substring(0,32)).Replace("Assets/", ""));
			}
			var avsMesh= new avs.Mesh()
			{
				name = Marshal.StringToCoTaskMemUTF8(mesh.name),
				path = Marshal.StringToCoTaskMemUTF8(resourcePath),

				numPrimitiveArrays = primitives.Length,
				primitiveArrays = primitives,

				numAccessors = accessors.Count,
				accessorIDs = accessors.Keys.ToArray(),
				accessors = accessors.Values.ToArray(),

				numBufferViews = bufferViews.Count,
				bufferViewIDs = bufferViews.Keys.ToArray(),
				bufferViews = bufferViews.Values.ToArray(),

				numBuffers = buffers.Count,
				bufferIDs = buffers.Keys.ToArray(),
				buffers = buffers.Values.ToArray(),
				inverseBindMatricesAccessor = inverseBindMatricesID
			};
			if (!Server_StoreMesh
			(
				meshID,
				resourcePath,
				last_modified,
				avsMesh,
				extractToBasis
				, verify
			))
			{
				UnityEngine.Debug.LogError("Failed to store mesh: \""+ mesh.name+"\" with id "+meshID);
			}
#endif
		}

		private void CreateIndexBufferAndView(int stride, in int[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, ref UInt64 localId, out UInt64 bufferViewID)
		{
			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Convert to ushort for stride 2.
			if (stride == 2)
			{
				int faceCount= data.Length/3;
				if (faceCount * 3 != data.Length)
				{
					UnityEngine.Debug.LogError($"Index buffer count is not a multiple of 3!");
					for (int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes((ushort)data[i]).CopyTo(newBuffer.data, i * stride);
					}
				}
				else
				{
					for (int i = 0; i < faceCount; i++)
					{
						BitConverter.GetBytes((ushort)data[i * 3 + 2]).CopyTo(newBuffer.data, (i * 3 + 0) * stride);
						BitConverter.GetBytes((ushort)data[i * 3 + 1]).CopyTo(newBuffer.data, (i * 3 + 1) * stride);
						BitConverter.GetBytes((ushort)data[i * 3 + 0]).CopyTo(newBuffer.data, (i * 3 + 2) * stride);
					}
				}
				// In GLTF, which standard we adopt, when a mesh primitive uses any triangle-based topology (i.e., triangles, triangle strip, or triangle fan),
				// the determinant of the transform defines the winding order of that primitive.
				// If the determinant is a positive value, the winding order triangle faces is counterclockwise, otherwise clockwise.
				// So here we flip the order.
			}
			//Convert to uint for stride 4.
			else
			{
				if(stride != 4)
				{
					UnityEngine.Debug.LogError($"CreateIndexBufferAndView(...) received invalid stride of {stride}! Extracting as uint!");
					stride=4;
				}
				int faceCount = data.Length / 3;
				if (faceCount * 3 != data.Length)
				{ 
					for (int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes((uint)data[i]).CopyTo(newBuffer.data, i * stride);
					}
				}
				else
				{
					for (int i = 0; i < faceCount; i++)
					{
						BitConverter.GetBytes((uint)data[i * 3 + 2]).CopyTo(newBuffer.data, (i * 3 + 0) * stride);
						BitConverter.GetBytes((uint)data[i * 3 + 1]).CopyTo(newBuffer.data, (i * 3 + 1) * stride);
						BitConverter.GetBytes((uint)data[i * 3 + 0]).CopyTo(newBuffer.data, (i * 3 + 2) * stride);
					}
				}
			}

			UInt64 bufferID = localId++;
			bufferViewID = localId++;

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private void CreateMeshBufferAndView(avs.AxesStandard extractToBasis, in UnityEngine.Vector3[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, ref UInt64 localId, out UInt64 bufferViewID)
		{
			//Three floats per Vector3 * four bytes per float.
			int stride = 3 * 4;

			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Get byte data from each Vector3, and copy into buffer.
			switch(extractToBasis)
			{
				case avs.AxesStandard.GlStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(-data[i].z).CopyTo(newBuffer.data, i * stride + 8);
					}

					break;
				case avs.AxesStandard.EngineeringStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 8);
					}

					break;
				default:
					UnityEngine.Debug.LogError("Attempted to extract mesh buffer data with unsupported axes standard of:" + extractToBasis);
					break;
			}

			UInt64 bufferID = localId++;
			bufferViewID = localId++;

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private void CreateMeshBufferAndView(avs.AxesStandard extractToBasis, in UnityEngine.Vector4[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, ref UInt64 localId, out UInt64 bufferViewID)
		{
			//Four floats per Vector4 * four bytes per float.
			int stride = 4 * 4;

			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Get byte data from each Vector4, and copy into buffer.
			switch(extractToBasis)
			{
				case avs.AxesStandard.GlStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(-data[i].z).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes(data[i].w).CopyTo(newBuffer.data, i * stride + 12);
					}

					break;
				case avs.AxesStandard.EngineeringStyle:
					for(int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].x).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].z).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(data[i].y).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes(data[i].w).CopyTo(newBuffer.data, i * stride + 12);
					}

					break;
				default:
					UnityEngine.Debug.LogError("Attempted to extract mesh buffer data with unsupported axes standard of:" + extractToBasis);
					break;
			}

			UInt64 bufferID = localId++;
			bufferViewID = localId++;

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}
		private void CreateMeshBufferAndView(avs.AxesStandard extractToBasis, in Matrix4x4[] data, in Dictionary<uid, avs.GeometryBuffer> buffers, in Dictionary<uid, avs.BufferView> bufferViews, ref UInt64 localId, out UInt64 bufferViewID)
		{
			//16 floats per Matrix4x4 * four bytes per float.
			int stride = 16 * 4;

			avs.GeometryBuffer newBuffer = new avs.GeometryBuffer();
			newBuffer.byteLength = (ulong)(data.Length * stride);
			newBuffer.data = new byte[newBuffer.byteLength];

			//Get byte data from each Vector4, and copy into buffer.
			switch (extractToBasis)
			{
				case avs.AxesStandard.GlStyle:
					for (int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes( data[i].m00).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes( data[i].m01).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(-data[i].m02).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes( data[i].m03).CopyTo(newBuffer.data, i * stride + 12);

						BitConverter.GetBytes( data[i].m10).CopyTo(newBuffer.data, i * stride + 16);
						BitConverter.GetBytes( data[i].m11).CopyTo(newBuffer.data, i * stride + 20);
						BitConverter.GetBytes(-data[i].m12).CopyTo(newBuffer.data, i * stride + 24);
						BitConverter.GetBytes( data[i].m13).CopyTo(newBuffer.data, i * stride + 28);

						BitConverter.GetBytes(-data[i].m20).CopyTo(newBuffer.data, i * stride + 32);
						BitConverter.GetBytes(-data[i].m21).CopyTo(newBuffer.data, i * stride + 36);
						BitConverter.GetBytes( data[i].m22).CopyTo(newBuffer.data, i * stride + 40);
						BitConverter.GetBytes(-data[i].m23).CopyTo(newBuffer.data, i * stride + 44);

						BitConverter.GetBytes( data[i].m30).CopyTo(newBuffer.data, i * stride + 48);
						BitConverter.GetBytes( data[i].m31).CopyTo(newBuffer.data, i * stride + 52);
						BitConverter.GetBytes(-data[i].m32).CopyTo(newBuffer.data, i * stride + 56);
						BitConverter.GetBytes( data[i].m33).CopyTo(newBuffer.data, i * stride + 60);
					}

					break;
				case avs.AxesStandard.EngineeringStyle:
					for (int i = 0; i < data.Length; i++)
					{
						BitConverter.GetBytes(data[i].m00).CopyTo(newBuffer.data, i * stride + 0);
						BitConverter.GetBytes(data[i].m02).CopyTo(newBuffer.data, i * stride + 4);
						BitConverter.GetBytes(data[i].m01).CopyTo(newBuffer.data, i * stride + 8);
						BitConverter.GetBytes(data[i].m03).CopyTo(newBuffer.data, i * stride + 12);

						BitConverter.GetBytes(data[i].m20).CopyTo(newBuffer.data, i * stride + 16);
						BitConverter.GetBytes(data[i].m22).CopyTo(newBuffer.data, i * stride + 20);
						BitConverter.GetBytes(data[i].m21).CopyTo(newBuffer.data, i * stride + 24);
						BitConverter.GetBytes(data[i].m23).CopyTo(newBuffer.data, i * stride + 28);

						BitConverter.GetBytes(data[i].m10).CopyTo(newBuffer.data, i * stride + 32);
						BitConverter.GetBytes(data[i].m12).CopyTo(newBuffer.data, i * stride + 36);
						BitConverter.GetBytes(data[i].m11).CopyTo(newBuffer.data, i * stride + 40);
						BitConverter.GetBytes(data[i].m13).CopyTo(newBuffer.data, i * stride + 44);

						BitConverter.GetBytes(data[i].m30).CopyTo(newBuffer.data, i * stride + 48);
						BitConverter.GetBytes(data[i].m32).CopyTo(newBuffer.data, i * stride + 52);
						BitConverter.GetBytes(data[i].m31).CopyTo(newBuffer.data, i * stride + 56);
						BitConverter.GetBytes(data[i].m33).CopyTo(newBuffer.data, i * stride + 60);
					}

					break;
				default:
					UnityEngine.Debug.LogError("Attempted to extract mesh buffer data with unsupported axes standard of:" + extractToBasis);
					break;
			}

			UInt64 bufferID = localId++;
			bufferViewID = localId++;

			buffers.Add(bufferID, newBuffer);
			bufferViews.Add
			(
				bufferViewID,
				new avs.BufferView
				{
					buffer = bufferID,
					byteOffset = 0,
					byteLength = newBuffer.byteLength,
					byteStride = (ulong)stride
				}
			);
		}

		private static uint GetBytesPerPixel(UnityEngine.TextureFormat format)
		{
			switch (format)
			{
				case UnityEngine.TextureFormat.Alpha8: return 1;
				case UnityEngine.TextureFormat.ARGB4444: return 2;
				case UnityEngine.TextureFormat.RGB24: return 3;
				case UnityEngine.TextureFormat.RGBA32: return 4;
				case UnityEngine.TextureFormat.ARGB32: return 4;
				case UnityEngine.TextureFormat.RGB565: return 2;
				case UnityEngine.TextureFormat.R16: return 2;
				case UnityEngine.TextureFormat.DXT1: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.DXT5: return 1;
				case UnityEngine.TextureFormat.RGBA4444: return 2;
				case UnityEngine.TextureFormat.BGRA32: return 4;
				case UnityEngine.TextureFormat.RHalf: return 2;
				case UnityEngine.TextureFormat.RGHalf: return 4;
				case UnityEngine.TextureFormat.RGBAHalf: return 8;
				case UnityEngine.TextureFormat.RFloat: return 4;
				case UnityEngine.TextureFormat.RGFloat: return 8;
				case UnityEngine.TextureFormat.RGBAFloat: return 16;
				case UnityEngine.TextureFormat.YUY2: return 2;
				case UnityEngine.TextureFormat.RGB9e5Float: return 4;
				case UnityEngine.TextureFormat.BC4: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.BC5: return 1;
				case UnityEngine.TextureFormat.BC6H: return 1;
				case UnityEngine.TextureFormat.BC7: return 1;
				case UnityEngine.TextureFormat.DXT1Crunched: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.DXT5Crunched: return 1;
				case UnityEngine.TextureFormat.PVRTC_RGB2: return 1; //0.25 = 2bits
				case UnityEngine.TextureFormat.PVRTC_RGBA2: return 1; //0.25 = 2bits
				case UnityEngine.TextureFormat.PVRTC_RGB4: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.PVRTC_RGBA4: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.ETC_RGB4: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.EAC_R: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.EAC_R_SIGNED: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.EAC_RG: return 1;
				case UnityEngine.TextureFormat.EAC_RG_SIGNED: return 1;
				case UnityEngine.TextureFormat.ETC2_RGB: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.ETC2_RGBA1: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.ETC2_RGBA8: return 1;
				//These are duplicates of ASTC_RGB_0x0, and not implemented in Unity 2018:
				//case TextureFormat.ASTC_4x4: return 8;
				//case TextureFormat.ASTC_5x5: return 8;
				//case TextureFormat.ASTC_6x6: return 8;
				//case TextureFormat.ASTC_8x8: return 8;
				//case TextureFormat.ASTC_10x10: return 8;
				//case TextureFormat.ASTC_12x12: return 8;
				case UnityEngine.TextureFormat.RG16: return 2;
				case UnityEngine.TextureFormat.R8: return 1;
				case UnityEngine.TextureFormat.ETC_RGB4Crunched: return 1; //0.5 = 4bits
				case UnityEngine.TextureFormat.ETC2_RGBA8Crunched: return 1;
#if UNITY_2019_0_OR_NEWER
			//These are not implemented in Unity 2018:
			case UnityEngine.TextureFormat.ASTC_HDR_4x4: return 8;
			case UnityEngine.TextureFormat.ASTC_HDR_5x5: return 8;
			case UnityEngine.TextureFormat.ASTC_HDR_6x6: return 8;
			case UnityEngine.TextureFormat.ASTC_HDR_8x8: return 8;
			case UnityEngine.TextureFormat.ASTC_HDR_10x10: return 8;
			case UnityEngine.TextureFormat.ASTC_HDR_12x12: return 8;
#endif
				case UnityEngine.TextureFormat.ASTC_4x4: return 8;
				case UnityEngine.TextureFormat.ASTC_5x5: return 8;
				case UnityEngine.TextureFormat.ASTC_6x6: return 8;
				case UnityEngine.TextureFormat.ASTC_8x8: return 8;
				case UnityEngine.TextureFormat.ASTC_10x10: return 8;
				case UnityEngine.TextureFormat.ASTC_12x12: return 8;
#if UNITY_2019_3_OR_OLDER
				case UnityEngine.TextureFormat.ASTC_4x4: return 8;
				case UnityEngine.TextureFormat.ASTC_5x5: return 8;
				case UnityEngine.TextureFormat.ASTC_6x6: return 8;
				case UnityEngine.TextureFormat.ASTC_8x8: return 8;
				case UnityEngine.TextureFormat.ASTC_10x10: return 8;
				case UnityEngine.TextureFormat.ASTC_12x12: return 8;
#endif
				default:
					UnityEngine.Debug.LogWarning("Defaulting to <color=red>8</color> bytes per pixel as format is unsupported: " + format);
					return 8;
			}
		}
		private static UnityEngine.TextureFormat ConvertRenderTextureFormatToTextureFormat(RenderTextureFormat rtf)
		{
			string formatName = Enum.GetName(typeof(RenderTextureFormat), rtf);
			UnityEngine.TextureFormat texFormat = UnityEngine.TextureFormat.RGBA32;
			foreach (UnityEngine.TextureFormat f in Enum.GetValues(typeof(UnityEngine.TextureFormat)))
			{
				string fName = Enum.GetName(typeof(UnityEngine.TextureFormat), f);
				if (fName.Equals(formatName, StringComparison.Ordinal))
				{
					texFormat = f;
					return texFormat;
				}
			}
			switch (rtf)
			{
				case RenderTextureFormat.ARGB64:
					return UnityEngine.TextureFormat.RGBAHalf;
				default:
					break;
			}
			UnityEngine.Debug.LogError("No equivalent TextureFormat found for RenderTextureFormat " + rtf.ToString());
			return texFormat;
		}
		private static uint GetBytesPerPixel(RenderTextureFormat format)
		{
			switch (format)
			{
				case RenderTextureFormat.ARGBFloat: return 16;
				case RenderTextureFormat.ARGB32: return 4;
				case RenderTextureFormat.ARGB64: return 8;
				default:
					UnityEngine.TextureFormat f= ConvertRenderTextureFormatToTextureFormat(format);
					return GetBytesPerPixel(f);
					//UnityEngine.Debug.LogWarning("Defaulting to <color=red>8</color> bytes per pixel as format is unsupported: " + format);
					//return 8;
			}
		}
		public uid AddTexture(UnityEngine.Texture texture,GameObject gameObject, ForceExtractionMask forceMask= ForceExtractionMask.FORCE_NOTHING, TextureConversion textureConversion= TextureConversion.CONVERT_NOTHING)
		{
			if(!texture)
			{
				return 0;
			}
			bool force_recreate_texture = (forceMask & ForceExtractionMask.FORCE_SUBRESOURCES) == ForceExtractionMask.FORCE_SUBRESOURCES;
			//Just return the ID; if we have already processed the texture and the texture can be found on the unmanaged side.
			if (sessionResourceUids.TryGetValue(texture, out uid textureID) && Server_IsTextureStored(textureID) && !force_recreate_texture)
			{
				return textureID;
			}

			GetResourcePath(texture, out string resourcePath,force_recreate_texture);
			uid uid_from_path= Server_GetOrGenerateUid(resourcePath);
			if (textureID == 0)
			{
				textureID = uid_from_path;
			}
			else
			{
				if (textureID != uid_from_path)
				{
					UnityEngine.Debug.LogError("Uid mismatch for texture " + texture.name + " at path " + resourcePath+"."
						+" uid from path was "+uid_from_path+", but stored textureID was "+textureID+".");
					sessionResourceUids.Remove(texture);
					Server_GetOrGenerateUid(resourcePath);
					return 0;
				}
			}
			sessionResourceUids[texture] = textureID;
			//We can't extract textures in play-mode.
			if (Application.isPlaying)
			{
				if (Server_EnsureResourceIsLoaded(textureID))
					return textureID;
				UnityEngine.Debug.LogWarning("Texture <b>" + texture.name + "</b> has not been extracted, but is being used on streamed geometry!");
				return 0;
			}
			if (Server_IsTextureStored(textureID)&&!force_recreate_texture)
			{
				return textureID;
			}
			string name=texture.name;
			if(textureConversion!= TextureConversion.CONVERT_NOTHING)
				name+="_"+textureConversion;
			avs.Texture extractedTexture = new avs.Texture()
			{
				name = Marshal.StringToCoTaskMemUTF8(name),
				path = Marshal.StringToCoTaskMemUTF8(resourcePath),

				width = (uint)texture.width,
				height = (uint)texture.height,
				mipCount = 1,

				format = avs.TextureFormat.INVALID, //Assumed
				compression = avs.TextureCompression.UNCOMPRESSED, //Assumed

				valueScale=1.0F
			};

			switch(texture)
			{
				case Texture2D texture2D:
					extractedTexture.depth = 1;
					extractedTexture.arrayCount = 1;
					extractedTexture.mipCount = (uint)texture2D.mipmapCount;
					break;
				case Texture2DArray texture2DArray:
					extractedTexture.depth = 1;
					extractedTexture.arrayCount = (uint)texture2DArray.depth;
					break;
				case Texture3D texture3D:
					extractedTexture.depth = (uint)texture3D.depth;
					extractedTexture.arrayCount = 1;
					break;
				case Cubemap cubemap:
					extractedTexture.depth = 1;
					extractedTexture.arrayCount = 6;
					extractedTexture.cubemap=true;
					break;
				case RenderTexture renderTexture:
					extractedTexture.depth = 1;
					extractedTexture.arrayCount = 1;
					if (renderTexture.dimension == UnityEngine.Rendering.TextureDimension.Cube)
					{
						//extractedTexture.arrayCount = 6;
						extractedTexture.cubemap = true;
					}
					extractedTexture.mipCount=(uint)renderTexture.mipmapCount;
					break;
				default:
					UnityEngine.Debug.LogError("Passed texture was unsupported type: " + texture.GetType() + "!");
					return 0;
			}

			texturesWaitingForExtraction.Add(new TextureExtractionData { id = textureID, unityTexture = texture, textureData = extractedTexture , textureConversion=textureConversion, gameObject=gameObject });
			return textureID;
		}

		private long GetAssetWriteTimeUTC(string filePath)
		{
			try
			{
				if (!File.Exists(filePath))
					filePath = "Assets/" + filePath;
				return File.GetLastWriteTimeUtc(filePath).ToFileTimeUtc();
			}
			catch(Exception)
			{
				UnityEngine.Debug.LogError("Failed to get last write time for "+filePath);
				return 0;
			}
		}
		bool paused_already=false;
		// Mark resources as already processed.
		//  numResources : Number of resources in loadedResources.
		//  loadedResources : LoadedResource array that was created in unmanaged memory.
		private void AddToProcessedResources<UnityAsset>(int numResources, in IntPtr loadedResources) where UnityAsset : UnityEngine.Object
		{
			try
			{
				int resourceSize = Marshal.SizeOf<LoadedResource>();
				for (int i = 0; i < numResources; i++)
				{
					//Create new pointer to the memory location of the LoadedResource for this index.
					IntPtr resourcePtr = new IntPtr(loadedResources.ToInt64() + i * resourceSize);

					//Marshal data to manaaged types.
					LoadedResource metaResource = Marshal.PtrToStructure<LoadedResource>(resourcePtr);
					if (metaResource.name == null)
					{
						UnityEngine.Debug.LogError("metaResource.name is null");
						continue;
					}
					string name = Marshal.PtrToStringAnsi(metaResource.name);
					string path = GetPathFromUid(metaResource.id);

#if UNITY_EDITOR
					string expectedAssetPath = path;
					expectedAssetPath = SceneResourcePathManager.UnstandardizePath(expectedAssetPath, "");
					if (expectedAssetPath.StartsWith("Library/"))
					{
						expectedAssetPath = "Library/unity default resources";
					}
					// Paths in Library/
					string unityAssetPath = expectedAssetPath;
					//Asset we found in the database.
					UnityAsset asset = null;
					string standardizedUnityAssetPath = SceneResourcePathManager.StandardizePath(unityAssetPath, "Assets/");
					if (expectedAssetPath != unityAssetPath)
					{
						UnityEngine.Debug.LogWarning("Path mismatch for (" + typeof(UnityAsset).ToString() + ")" + name + ": expected[" + expectedAssetPath + "]!=got[" + unityAssetPath + "]");
						if (!paused_already)
						{
							System.Threading.Thread.Sleep(10000);
							paused_already = true;
						}
					}
					UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath("Assets/" + unityAssetPath);
					if (assetsAtPath.Length == 0)
					{
						// Library assets are not under "Assets/"
						assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(unityAssetPath);
					}
					if (assetsAtPath.Length == 0)
					{
						unityAssetPath = SceneResourcePathManager.UnstandardizePath(expectedAssetPath, "");
						do
						{
							assetsAtPath = AssetDatabase.LoadAllAssetsAtPath("Assets/" + unityAssetPath);
							if (assetsAtPath.Length != 0)
								break;
							int slash = unityAssetPath.LastIndexOf('/');
							if (slash < 0)
								break;
							unityAssetPath = unityAssetPath.Substring(0, slash);
						} while (unityAssetPath.Length > 0);
					}
					foreach (UnityEngine.Object unityObject in assetsAtPath)
					{
						if (unityObject)
							if ((unityObject.GetType() == typeof(UnityAsset) || unityObject.GetType().IsSubclassOf(typeof(UnityAsset))) && unityObject.name == name)
							{
								asset = (UnityAsset)unityObject;
								break;
							}
					}
					// Give up on matching the name, is the type correct?
					if (asset == null)
					{
						foreach (UnityEngine.Object unityObject in assetsAtPath)
						{
							if (unityObject)
								if ((unityObject.GetType() == typeof(UnityAsset) || unityObject.GetType().IsSubclassOf(typeof(UnityAsset))))
								{
									asset = (UnityAsset)unityObject;
									break;
								}
						}
					}

					if (asset)
					{
						long lastModified = GetAssetWriteTimeUTC(unityAssetPath);

						//Use the asset as is, if it has not been modified since it was saved.
						if (metaResource.lastModified >= lastModified)
						{
							sessionResourceUids[asset] = metaResource.id;
						}
						else
						{
						//	UnityEngine.Debug.Log($"Asset {typeof(UnityAsset).FullName} \"{name}\" has been modified.");//
						}
					}
					else
					{
						UnityEngine.Debug.LogWarning($"Can't find asset {typeof(UnityAsset).FullName} \"{name}\"!");
					}
#endif
				}
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogWarning("Exception in AddToProcessedResources: " +e.Message);
			}
		}

		private void CreateAnimationHook()
		{
			teleportAnimationHook.functionName = "SetPlayingAnimation";
			teleportAnimationHook.time = 0.0f;
			//Animation clip may be attached to an object that isn't streamed, and we don't want error messages when it hits the event and doesn't have a receiver.
			teleportAnimationHook.messageOptions = SendMessageOptions.DontRequireReceiver;
		}
		public bool CheckForErrors()
		{
			return Server_CheckGeometryStoreForErrors();
		}
		static public string GetPathFromUid(uid u)
		{
			StringBuilder path=new StringBuilder("", 20);
			try
			{ 
				int len=(int)Server_UidToPath(u,  path, 0);
				if (len > 0)
				{
					path = new StringBuilder(len, len);
					Server_UidToPath(u, path, (UInt64)len);
				}

				return path.ToString();
			}
			catch (EntryPointNotFoundException)
			{
				UnityEngine.Debug.LogError("Server_UidToPath not found in dll.");
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogError("Failed in GetPathFromUid. "+e.ToString());
				if (UnityEditor.EditorApplication.isPlaying)
					UnityEditor.EditorApplication.isPlaying=false;
			}
			return "";
		}
	}
}
