﻿using System;
using System.Runtime.InteropServices;
using UnityEngine;
using uid = System.UInt64;

namespace teleport
{
	public struct TeleportServerDll
	{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		public const string name="TeleportServer";
#else
		public const string name="TeleportServer.so";
#endif
	}
}
namespace avs
{
	using InputID = UInt16;
	public enum InputType : byte
	{
		//IsEvent=1,
		//IsReleaseEvent = 2,
		//IsInteger =4,
		//IsFloat =8,
		BooleanState=4,
		FloatState=8,
		BooleanEvent=4|1,
		//ReleaseEvent = 4 | 1| 2,
		FloatEvent =8|1
	};
	//
	// Summary:
	//     The type of a Light.
	public enum LightType : byte
	{
		Spot = 0,
		Directional = 1,
		Point = 2,
		Area = 3,
		Disc = 4
	}
	public enum LogSeverity
	{
		Never=0,
		Debug,
		Info,
		Warning,
		Error,
		Critical,
		Num_LogSeverity
	}

	public enum AxesStandard : byte
	{
		NotInitialised = 0,
		RightHanded = 1,
		LeftHanded = 2,
		YVertical = 4,
		EngineeringStyle = 8 | RightHanded,
		GlStyle = 16 | RightHanded,
		UnrealStyle = 32 | LeftHanded,
		UnityStyle = 64 | LeftHanded | YVertical,
	}

	public enum RateControlMode : byte
	{
		RC_CONSTQP = 0, /*< Constant QP mode */
		RC_VBR = 1, /*< Variable bitrate mode */
		RC_CBR = 2 /*< Constant bitrate mode */
	};

	public enum VideoCodec : byte
	{
		Any = 0,
		Invalid = 0,
		H264, /*!< H264 */
		HEVC /*!< HEVC (H265) */
	};

	public enum AudioCodec
	{
		Any = 0,
		Invalid = 0,
		PCM
	};
	/*
	public enum AnimationTimeControl
	{
		[InspectorName("Animation Time")] ANIMATION_TIME,
		[InspectorName("Right Controller Trigger")] CONTROLLER_0_TRIGGER,
		[InspectorName("Left Controller Trigger")] CONTROLLER_1_TRIGGER
	};*/

	public static class DataTypes
	{
		public static LightType UnityToTeleport(UnityEngine.LightType unity)
		{
			return (LightType)unity;
		}
		public static void ConvertViewProjectionMatrix(AxesStandard toStandard, ref Matrix4x4 m)
		{
			var y = m.GetColumn(1);
			var z = m.GetColumn(2);
			// The source has y and z messed about in the input, but converts to a correct xy position with z=depth.
			// So we swap and flip columns to get to the correct matrix.
			if(toStandard == AxesStandard.GlStyle)
			{
				m.SetColumn(2, -z);// { position.x, position.y, -position.z };
			}
			if(toStandard == AxesStandard.EngineeringStyle)
			{
				m.SetColumn(1, z);
				m.SetColumn(2, y);// { position.x, position.z, position.y };
			}
		}
		// A view matrix converts from xyz in world/object space into xy,z in view space, where z is depth. So even in different object space systems,
		// the final frame is the same.
		public static void ConvertViewMatrix(AxesStandard toStandard, ref Matrix4x4 m)
		{
			var y = m.GetColumn(1);
			var z = m.GetColumn(2);
			// The source has y and z messed about in the input, but converts to a correct xy position with z=depth.
			// So we swap and flip columns to get to the correct matrix.
			if(toStandard == AxesStandard.GlStyle)
			{
				m.SetColumn(2, -z);// { position.x, position.y, -position.z };
			}
			if(toStandard == AxesStandard.EngineeringStyle)
			{
				m.SetColumn(1, z);
				m.SetColumn(2, y);// { position.x, position.z, position.y };
			}
		}

		public static void ConvertTransformMatrix(AxesStandard toStandard, ref Matrix4x4 m)
		{
			var y = m.GetColumn(1);
			var z = m.GetColumn(2);
			// The source has y and z messed about in the input, but converts to a correct xy position with z=depth.
			// So we swap and flip columns to get to the correct matrix.
			if(toStandard == AxesStandard.GlStyle)
			{
				m.SetColumn(2, -z);// { position.x, position.y, -position.z };
			}
			if(toStandard == AxesStandard.EngineeringStyle)
			{
				m.SetColumn(1, z);
				m.SetColumn(2, y);// { position.x, position.z, position.y };
			}
			var ry = m.GetRow(1);
			var rz = m.GetRow(2);
			if(toStandard == AxesStandard.GlStyle)
			{
				m.SetRow(2, -rz);
			}
			if(toStandard == AxesStandard.EngineeringStyle)
			{
				m.SetRow(1, rz);
				m.SetRow(2, ry);
			}
		}
		private static float copysign(float sizeval, float signval)
		{
			return Mathf.Sign(signval) == 1 ? Mathf.Abs(sizeval) : -Mathf.Abs(sizeval);
		}
		public static Quaternion GetRotation(this Matrix4x4 matrix)
		{
			Quaternion q = new Quaternion();
			q.w = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 + matrix.m11 + matrix.m22)) / 2;
			q.x = Mathf.Sqrt(Mathf.Max(0, 1 + matrix.m00 - matrix.m11 - matrix.m22)) / 2;
			q.y = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 + matrix.m11 - matrix.m22)) / 2;
			q.z = Mathf.Sqrt(Mathf.Max(0, 1 - matrix.m00 - matrix.m11 + matrix.m22)) / 2;
			q.x = copysign(q.x, matrix.m21 - matrix.m12);
			q.y = copysign(q.y, matrix.m02 - matrix.m20);
			q.z = copysign(q.z, matrix.m10 - matrix.m01);
			return q;
		}

		public static Vector3 GetPosition(this Matrix4x4 matrix)
		{
			var x = matrix.m03;
			var y = matrix.m13;
			var z = matrix.m23;

			return new Vector3(x, y, z);
		}

		public static Vector3 GetScale(this Matrix4x4 m)
		{
			var x = Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02);
			var y = Mathf.Sqrt(m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12);
			var z = Mathf.Sqrt(m.m20 * m.m20 + m.m21 * m.m21 + m.m22 * m.m22);

			return new Vector3(x, y, z);
		}
	}

	//We have to declare our own vector types, as .NET and Unity have different layouts.
	public struct Vector2
	{
		public float x, y;

		public Vector2(float x, float y) => (this.x, this.y) = (x, y);
		public Vector2(System.Numerics.Vector2 netVector) => (x, y) = (netVector.X, netVector.Y);
		public Vector2(UnityEngine.Vector2 unityVector) => (x, y) = (unityVector.x, unityVector.y);

		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if(!(obj is Vector2))
			{
				return false;
			}
			return (this == ((Vector2)obj));
		}

		public static implicit operator Vector2(System.Numerics.Vector2 netVector) => new Vector2(netVector.X, netVector.Y);
		public static implicit operator Vector2(UnityEngine.Vector2 unityVector) => new Vector2(unityVector.x, unityVector.y);

		public static implicit operator System.Numerics.Vector2(Vector2 vector) => new System.Numerics.Vector2(vector.x, vector.y);
		public static implicit operator UnityEngine.Vector2(Vector2 vector) => new UnityEngine.Vector2(vector.x, vector.y);
		public static bool operator ==(Vector2 lhs, Vector2 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y;
		}

		public static bool operator !=(Vector2 lhs, Vector2 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y;
		}
	}

	public struct Vector3
	{
		public float x, y, z;

		public Vector3(float x, float y, float z) => (this.x, this.y, this.z) = (x, y, z);
		public Vector3(System.Numerics.Vector3 netVector) => (x, y, z) = (netVector.X, netVector.Y, netVector.Z);
		public Vector3(UnityEngine.Vector3 unityVector) => (x, y, z) = (unityVector.x, unityVector.y, unityVector.z);

		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				hash = hash * 23 + z.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if(!(obj is Vector3))
			{
				return false;
			}
			return (this == ((Vector3)obj));
		}

		public override string ToString()
		{
			return string.Format("({0:0.0}, {1:0.0}, {2:0.0})", x, y, z);
		}

		public static implicit operator Vector3(System.Numerics.Vector3 netVector) => new Vector3(netVector.X, netVector.Y, netVector.Z);
		public static implicit operator Vector3(UnityEngine.Vector3 unityVector) => new Vector3(unityVector.x, unityVector.y, unityVector.z);
		public static implicit operator Vector3(UnityEngine.Color unityColour) => new Vector3(unityColour.r, unityColour.g, unityColour.b);

		public static implicit operator System.Numerics.Vector3(Vector3 vector) => new System.Numerics.Vector3(vector.x, vector.y, vector.z);
		public static implicit operator UnityEngine.Vector3(Vector3 vector) => new UnityEngine.Vector3(vector.x, vector.y, vector.z);
		public static implicit operator UnityEngine.Color(Vector3 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z);

		public static bool operator ==(Vector3 lhs, Vector3 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
		}

		public static bool operator !=(Vector3 lhs, Vector3 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z;
		}
	}

	public struct Vector4
	{
		public float x, y, z, w;

		public Vector4(float x, float y, float z, float w) => (this.x, this.y, this.z, this.w) = (x, y, z, w);
		public Vector4(System.Numerics.Vector4 netVector) => (x, y, z, w) = (netVector.X, netVector.Y, netVector.Z, netVector.W);
		public Vector4(UnityEngine.Vector4 unityVector) => (x, y, z, w) = (unityVector.x, unityVector.y, unityVector.z, unityVector.w);


		// We should implement a hash code. The GetHashCode method provides this hash code for algorithms that need quick checks of object equality.
		public override int GetHashCode()
		{
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				// Suitable nullity checks etc, of course :)
				hash = hash * 23 + x.GetHashCode();
				hash = hash * 23 + y.GetHashCode();
				hash = hash * 23 + z.GetHashCode();
				hash = hash * 23 + w.GetHashCode();
				return hash;
			}
		}
		public override bool Equals(object obj)
		{
			if(!(obj is Vector4))
			{
				return false;
			}
			return (this == ((Vector4)obj));
		}
		public override string ToString()
		{
			return string.Format("({0:0.0}, {1:0.0}, {2:0.0}, {3:0.0})", x, y, z, w);
		}

		public static implicit operator Vector4(System.Numerics.Vector4 netVector) => new Vector4(netVector.X, netVector.Y, netVector.Z, netVector.W);
		public static implicit operator Vector4(UnityEngine.Vector4 unityVector) => new Vector4(unityVector.x, unityVector.y, unityVector.z, unityVector.w);
		public static implicit operator Vector4(UnityEngine.Color unityColour) => new Vector4(unityColour.r, unityColour.g, unityColour.b, unityColour.a);
		public static implicit operator Vector4(UnityEngine.Quaternion quaternion) => new Vector4(quaternion.x, quaternion.y, quaternion.z, quaternion.w);

		public static implicit operator System.Numerics.Vector4(Vector4 vector) => new System.Numerics.Vector4(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Vector4(Vector4 vector) => new UnityEngine.Vector4(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Color(Vector4 vector) => new UnityEngine.Color(vector.x, vector.y, vector.z, vector.w);
		public static implicit operator UnityEngine.Quaternion(Vector4 vector) => new UnityEngine.Quaternion(vector.x, vector.y, vector.z, vector.w);

		public static bool operator ==(Vector4 lhs, Vector4 rhs)
		{
			return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.w == rhs.w;
		}

		public static bool operator !=(Vector4 lhs, Vector4 rhs)
		{
			return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z || lhs.w != rhs.w;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public class Transform
	{
		public Vector3 position = new Vector3(0, 0, 0);
		public Vector4 rotation = new Vector4(0, 0, 0, 1);
		public Vector3 scale = new Vector3(1, 1, 1);

		//Converts a UnityEngine transform to a libavstream transform using the local transform data.
		public static Transform FromLocalUnityTransform(UnityEngine.Transform unityTransform)
		{
			Transform transform = new Transform();
			transform.position = unityTransform.localPosition;
			transform.rotation = unityTransform.localRotation;
			transform.scale = unityTransform.localScale;
			return transform;
		}

		//Converts a UnityEngine transform to a libavstream transform using the global transform data.
		public static Transform FromGlobalUnityTransform(UnityEngine.Transform unityTransform)
		{
			Transform transform = new Transform();
			transform.position = unityTransform.position;
			transform.rotation = unityTransform.rotation;
			transform.scale = unityTransform.lossyScale;
			return transform;
		}

		//Converts a UnityEngine Matrix4x4 to a libavstream transform 
		public static implicit operator Transform(UnityEngine.Matrix4x4 m)
		{
			Transform transform = new Transform();
			transform.position = m.GetPosition();
			transform.rotation = m.GetRotation();
			transform.scale = m.GetScale();
			return transform;
		}
	}

	public struct NetworkStats
	{
		/*! Total bytes sent. */
		public UInt64 bytesSent;
		/*! Number of sent network packets. */
		public UInt64 networkPacketsSent;
		/*! Available bandwidth */
		public double bandwidth;
		/*! Average bandwidth used  */
		public double avgBandwidthUsed;
		/*! Minimum bandwidth used */
		public double minBandwidthUsed;
		/*! Maximum bandwidth used */
		public double maxBandwidthUsed;
    };

    public struct ClientNetworkState
    {
		public float server_to_client_latency_ms;
		public float client_to_server_latency_ms;
		public avs.SignalingState signalingState;
		public avs.StreamingState streamingState;
	};

	public struct DisplayInfo
    {
		public Int32 width;
		public Int32 height;
		public float framerate;
    };

    public struct VideoEncoderStats
	{
		/*! Total video frames submitted to the encoder. */
		public UInt64 framesSubmitted;
		/*! Number of video frames submitted to the encoder per second. */
		public float framesSubmittedPerSec;
		/*! Total video frames encoded. */
		public UInt64 framesEncoded;
		/*! Number of video frames encoded per second. */
		public float framesEncodedPerSec;
	};

	public struct VideoEncodeCapabilities
	{
		/*! Max encode level supported. */
		public uint maxEncodeLevel;
		/*! Determines whether encoder supports asynchronous encoding. */
		public uint isAsyncCapable;
		/*! Determines whether encoder supports 10-bit encoding. */
		public uint is10BitCapable;
		/*! Determines whether encoder supports YUV444 as output format. */
		public uint isYUV444Capable;
		/*! Determines whether encoder supports asynchronous encoding. */
		public uint isAlphaLayerSupported;
		/*! Minimum input width supported. */
		public uint minWidth;
		/*! Maximum output width supported. */
		public uint maxWidth;
		/*! Minimum input height supported. */
		public uint minHeight;
		/*! Maximum output height supported. */
		public uint maxHeight;
	};

	public struct Mat4x4
	{
		//[Row, Column]
		float m00, m01, m02, m03;
		float m10, m11, m12, m13;
		float m20, m21, m22, m23;
		float m30, m31, m32, m33;

		public Mat4x4(float m00, float m01, float m02, float m03,
						float m10, float m11, float m12, float m13,
						float m20, float m21, float m22, float m23,
						float m30, float m31, float m32, float m33)
		{
			this.m00 = m00;
			this.m01 = m01;
			this.m02 = m02;
			this.m03 = m03;

			this.m10 = m10;
			this.m11 = m11;
			this.m12 = m12;
			this.m13 = m13;

			this.m20 = m20;
			this.m21 = m21;
			this.m22 = m22;
			this.m23 = m23;

			this.m30 = m30;
			this.m31 = m31;
			this.m32 = m32;
			this.m33 = m33;
		}

		public static implicit operator Mat4x4(Matrix4x4 matrix)
		{
			return new Mat4x4
			(
				matrix.m00, matrix.m01, matrix.m02, matrix.m03,
				matrix.m10, matrix.m11, matrix.m12, matrix.m13,
				matrix.m20, matrix.m21, matrix.m22, matrix.m23,
				matrix.m30, matrix.m31, matrix.m32, matrix.m33
			);
		}
	}

    public enum SignalingState : byte
    {
        START,			// Received a WebSocket connection.
		REQUESTED,		// Got an initial connection request message
		ACCEPTED,		// Accepted the connection request. Create a ClientData for this client if not already extant.
		STREAMING,		// Completed signaling, now use streaming state. 
		INVALID
    };
    public enum StreamingState : byte
	{
		UNINITIALIZED = 0,
		NEW_UNCONNECTED,
		CONNECTING,
		CONNECTED,
		DISCONNECTED,
		FAILED,
		CLOSED,
		ERROR_STATE
	};
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
	public struct Handshake
	{
		public UInt32 display_width;
		public UInt32 display_height;
		public float MetresPerUnit;
		public float FOV;
		public UInt32 udpBufferSize;
		public UInt32 maxBandwidthKpS;
		public AxesStandard axesStandard;
		public byte framerate;                  // In hertz
		[MarshalAs(UnmanagedType.U1)] public bool usingHands;             //Whether to send the hand nodes to the client.
		[MarshalAs(UnmanagedType.U1)] public bool isVR;
		public UInt64	resourceCount;				//< Number of resources the client has, these are appended to the handshake.
		public UInt32	maxLightsSupported;			//< Maximum number of lights the client can render.
		public Int32	minimumPriority;			//< Minimum priority of node the client will render.
	}; 
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct InputDefinitionInterop
	{
		public InputID inputID;
		public InputType inputType;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct InputEventBinary
	{
		public UInt32 eventID;
		public InputID inputID;
		[MarshalAs(UnmanagedType.U1)] public bool activated;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct InputEventAnalogue
	{
		public UInt32 eventID;
		public InputID inputID;
		public float value;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct InputEventMotion
	{
		public UInt32 eventID;
		public InputID inputID;
		public avs.Vector2 motion;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct InputState
	{
		public UInt16 numBinaryStates;
		public UInt16 numAnalogueStates;
		public UInt16 numBinaryEvents;
		public UInt16 numAnalogueEvents;
		public UInt16 numMotionEvents;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct Pose
	{
		public Vector4 orientation;
		public Vector3 position;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct PoseDynamic
	{
		public Vector4 orientation;
		public Vector3 position;
		public Vector3 velocity;
		public Vector3 angularVelocity;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct MovementUpdate
	{
		public Int64 time_since_server_start_us;
		[MarshalAs(UnmanagedType.U1)] public bool isGlobal;

		public uid nodeID;
		public avs.Vector3 position;
		public avs.Vector4 rotation; 
		public avs.Vector3 scale;

		public avs.Vector3 velocity;
		public avs.Vector3 angularVelocityAxis;
		public float angularVelocityAngle;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct NodeUpdateEnabledState
	{
		public uid nodeID;
		[MarshalAs(UnmanagedType.U1)] public bool enabled;
	}
	//int animLayer,AnimationClip playingClip,float transitionFromTimestampS, float animTimeAtTimestamp, float speed
	[StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
	public struct ApplyAnimation
	{
		public Int32 animationLayer;				//< Each layer has at most two anims, a current and a next, with a transition between if needed.
		public Int64 timestampUs;					//< Timestamp for the state we are specifying, in microseconds since the session's start timestamp.
		public uid nodeID;							//< ID of the node the animation is playing on.
		public uid animationID;						//< ID of the animation that is now playing.
		public float animTimeAtTimestamp;			//< At the given timestamp, where in the animation should we be, in seconds?
		public float speedUnitsPerSecond;           //< At the given timestamp, how fast is the animation playing, where 1.0F is the default (1 unit=1 second).
		[MarshalAs(UnmanagedType.U1)] public bool loop;
	}
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct NodeRenderState
	{
		public avs.Vector4 lightmapScaleOffset;
		public uid globalIlluminationTextureUid;
		public byte lightmapTextureCoordinate ;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct SceneCaptureCubeTagData
	{
		public Int64 timestamp;

		public uint id;
		public Transform cameraTransform;
		public uint lightCount;
		public float diffuseAmbientScale;
		[MarshalAs(UnmanagedType.ByValArray)]
		public LightTagData[] lights;
	};

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct LightTagData
	{
		public Transform worldTransform;			// 11*4	=44
		public Vector4 color;						// 4*4	=16		60
		public float range;							// 4			64
		public float spotAngle;						// 4			68
		public LightType lightType;					// 1			69
		public Vector3 position;                    // 4*3 = 12		81
		public Vector4 orientation;                 // 4*4 = 16		97
		public Matrix4x4 shadowProjectionMatrix;    // 4*16 = 64	161
		public Matrix4x4 worldToShadowMatrix;       // 4*16 = 64	225
		public Vector2Int texturePosition;          // 4*2 = 8		233
		public int textureSize;						// 4			237
		public uid uid;								// 8			245
														// Total is 245
	};
}

namespace teleport
{
	/*! Graphics API device handle type. */
	public enum GraphicsDeviceType
	{
		Invalid = 0,
		Direct3D11 = 1,
		Direct3D12 = 2,
		OpenGL = 3,
		Vulkan = 4
	}

	public struct VideoEncodeParams
	{
		public Int32 encodeWidth;
		public Int32 encodeHeight;
		public GraphicsDeviceType deviceType;
		public IntPtr deviceHandle;
		public IntPtr inputSurfaceResource;
	}

	public struct AudioSettings
	{
		public avs.AudioCodec codec;
		public UInt32 sampleRate;
		public UInt32 bitsPerSample;
		public UInt32 numChannels;
	}

	public enum ControlModel : UInt32
	{
		NONE = 0,
		SERVER_ORIGIN_CLIENT_LOCAL = 2
	}
}
