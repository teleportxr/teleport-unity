﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace teleport
{
	/// <summary>
	/// A class to store the connection between Unity Assets and Teleport asset paths.
	/// </summary>
	public class SceneResourcePathManager : MonoBehaviour, ISerializationCallbackReceiver
	{
		//Used to serialise dictionary, so it can be refilled when the object is deserialised.
		[SerializeField, HideInInspector] UnityEngine.Object[] sceneResourcePaths_keys;
		[SerializeField, HideInInspector] string[] sceneResourcePaths_values;

		[NonSerialized]
		static Dictionary<Scene,SceneResourcePathManager> sceneResourcePathManagers=new Dictionary<Scene, SceneResourcePathManager> ();

		//STATIC FUNCTIONS
		public static Dictionary<Scene, SceneResourcePathManager> GetSceneResourcePathManagers()
		{
			return sceneResourcePathManagers;
		}
		static public SceneResourcePathManager GetSceneResourcePathManager(Scene scene)
		{
			if(scene==null|| scene.path==null)
				return null;
			if(sceneResourcePathManagers.TryGetValue(scene,out SceneResourcePathManager res))
			{
				if(res!=null)
					return res;
			}
			SceneResourcePathManager sceneResourcePathManager=null;
			var objs = scene.GetRootGameObjects();
			foreach (var o in objs)
			{
				SceneResourcePathManager m = o.GetComponentInChildren<SceneResourcePathManager>();
				if (m)
				{
					sceneResourcePathManager=m;
					break;
				}
			}
			if (sceneResourcePathManager == null)
				sceneResourcePathManager = FindObjectOfType<SceneResourcePathManager>();
			if (sceneResourcePathManager == null)
			{
				var tempObject = new GameObject("SceneResourcePathManager");
				//Add Components
				tempObject.AddComponent<SceneResourcePathManager>();
				sceneResourcePathManager = tempObject.GetComponent<SceneResourcePathManager>();
			}
			sceneResourcePathManagers[scene]= sceneResourcePathManager;
			return sceneResourcePathManager;
		}

		[NonSerialized]
		private Dictionary<UnityEngine.Object,string> sceneResourcePaths=new Dictionary<UnityEngine.Object, string>();

		public Dictionary<UnityEngine.Object, string> GetResourcePathMap()
		{
			return sceneResourcePaths;
		}
		public UnityEngine.Object GetResourceFromPath(string path)
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			int idx=Array.FindIndex(sceneResourcePaths_values, element => element==path);
			if(idx>=0)
			{
				return sceneResourcePaths_keys[idx];
			}
			return null;
		}
		static public void ClearAll()
		{
			// At least get the one in the current scene.
			GetSceneResourcePathManager(SceneManager.GetActiveScene());
			foreach (var s in sceneResourcePathManagers)
			{
				s.Value.Clear();
			}
		}

		public void Clear()
		{
			sceneResourcePaths.Clear();
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
		public void CheckForDuplicates()
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				for(int j=i+1;j< sceneResourcePaths_keys.Length;j++)
				{
					if (sceneResourcePaths_values[i] == sceneResourcePaths_values[j])
					{
						sceneResourcePaths.Remove(sceneResourcePaths_keys[j]);
						Debug.LogWarning($"Removed duplicate resource path {sceneResourcePaths_values[j]} for {sceneResourcePaths_keys[j].name} and {sceneResourcePaths_keys[j].name}.");
					}
				}
			}
		}
		/// <summary>
		/// Convert a Unity-style asset path to a Teleport standardized path.
		/// </summary>
		/// <param name="file_name"></param>
		/// <param name="path_root"></param>
		/// <returns>The standardized path.</returns>
		static public string StandardizePath(string file_name,string path_root)
		{
			if (file_name == null)
			{
				return "";
			}
			string p = file_name;
			p=p.Replace(".","_-_");
			p=p.Replace(",","_--_");
			p=p.Replace(" ","___");
			p=p.Replace('\\','/');
			// Replace legacy paths that include hashes. These are not valid generic URL characters, use tildes instead.
			p = p.Replace('#', '~');
			if (path_root.Length>0)
				p=p.Replace(path_root, "" );
			if(p.Length>240)
			{
				string q="long_path/"+p.GetHashCode().ToString();
				UnityEngine.Debug.LogWarning("Shortening too-long path "+p+" to "+q);
				return q;
			}
			return p;
		}
		/// <summary>
		/// Convert a Teleport standardized path to a Unity asset path.
		/// </summary>
		/// <param name="file_name"></param>
		/// <param name="path_root"></param>
		/// <returns>The path.</returns>
		static public string UnstandardizePath(string file_name, string path_root)
		{
			if (file_name == null)
			{
				return "";
			}
			string p = file_name;
			p = p.Replace("___"," ");
			p = p.Replace("_--_", ",");
			p = p.Replace("_-_", ".");
			if (path_root.Length > 0)
			{ 
				if(!path_root.EndsWith("/"))
					path_root+= "/";
				p = p.Replace(path_root, "");
				p=path_root+p;
			}
			int tilde= p.IndexOf("~~");
			if (tilde >= 0)
			{
				p = p.Substring(0, tilde);
			}
			return p;
		}
		public void SetResourcePath(UnityEngine.Object o,string p)
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				if (sceneResourcePaths_keys[i]!=o&&sceneResourcePaths_values[i] == p)
				{
					if(sceneResourcePaths_keys[i]==null)
					{
						// already have this path, but for a deleted object. Let's remove that. But although C# is able to leave null keys lying around,
						// it WON'T let you remove them!
						//sceneResourcePaths.Remove(null);
						sceneResourcePaths_values[i]="";
					}
					else
					{
						Debug.LogError($"Trying to add duplicate resource path {p} for {o.GetType().Name} {o.name}, but already present for {sceneResourcePaths_keys[i].GetType().Name}  {sceneResourcePaths_keys[i].name}.");
						return;
					}
				}
			}
			sceneResourcePaths[o]= StandardizePath(p,"");
#if UNITY_EDITOR
			UnityEditor.EditorUtility.SetDirty(this);
#endif
		}
		// Fallback for objects not found in the AssetDatabase.
		public static string GetNonAssetResourcePath(UnityEngine.Object obj, GameObject owner)
		{
			string resourcePath = SceneReferenceManager.GetGameObjectPath(owner) + "_" + obj.GetType().ToString() + "_" + obj.name;
			resourcePath = StandardizePath(resourcePath, "Assets/");
			// Is this path unique?
			int n = 0;
			string resourcePathRoot = resourcePath;
			var scene = SceneManager.GetActiveScene();
			if (owner.scene != null)
				scene = owner.scene;
			var resourcePathManager = GetSceneResourcePathManager(scene);
			var otherObj = resourcePathManager.GetResourceFromPath(resourcePath);
			while (otherObj != null && otherObj != obj)
			{
				n++;
				resourcePath = resourcePathRoot + n;
				otherObj = resourcePathManager.GetResourceFromPath(resourcePath);
			}
			resourcePathManager.SetResourcePath(obj, resourcePath);
			return resourcePath;
		}
		public string GetResourcePath(UnityEngine.Object o)
		{
			string path="";
			if(sceneResourcePaths ==null)
				sceneResourcePaths = new Dictionary<UnityEngine.Object, string>();
			sceneResourcePaths.TryGetValue(o,out path);
			if (path != null)
			{
				string p=StandardizePath(path,"");
				if(p!=path)
				{
					sceneResourcePaths[o]=path=p;
				}
			}
			return path;
		}
		///INHERITED FUNCTIONS

		public void OnBeforeSerialize()
		{
			sceneResourcePaths_keys = sceneResourcePaths.Keys.ToArray();
			sceneResourcePaths_values = sceneResourcePaths.Values.ToArray();
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
			{
				if(sceneResourcePaths_keys[i]==null)
				{ 
					var keys= sceneResourcePaths_keys.ToList();
					var vals = sceneResourcePaths_values.ToList();
					keys.RemoveAt(i);
					vals.RemoveAt(i);
					sceneResourcePaths_keys=keys.ToArray();
					sceneResourcePaths_values = vals.ToArray();
					i--;
				}
			}
		}

		public void OnAfterDeserialize()
		{
			if(sceneResourcePaths_keys!=null)
			for (int i = 0; i < sceneResourcePaths_keys.Length; i++)
				{
					UnityEngine.Object obj = sceneResourcePaths_keys[i];
					string value= sceneResourcePaths_values[i];
					// Replace legacy paths that include hashes. These are not valid generic URL characters, use tildes instead.
					value = value.Replace("#", "~");
					sceneResourcePaths[obj] = value;
			}
		}

	}
}
