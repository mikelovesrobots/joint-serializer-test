// /* ------------------
//
//       (c) whydoidoit.com 2012
//           by Mike Talbot 
//     ------------------- */
// 
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using Serialization;

[ExecuteInEditMode]
[AddComponentMenu("Storage/Save Game Manager")]
public class SaveGameManager : MonoBehaviour
{
	private static SaveGameManager instance;
	public static SaveGameManager Instance
	{
		get
		{
			if(instance == null)
			{
				instance = GameObject.FindObjectsOfType(typeof(GameObject))
					.Cast<GameObject>()
					.Where(g=>g.GetComponent<SaveGameManager>() != null)
					.Select(g=>g.GetComponent<SaveGameManager>())
					.FirstOrDefault();
				if(instance!=null)
					instance.GetAllReferences();
			}
			
			return instance;
			 
		}
		set
		{
			instance = value;
		}
	}
	
	public static bool hasRun;
	
	public static void Loaded()
	{
		_cached = null;
		//Instance.Reference.Clear();
	}

	[Serializable]
	public class StoredEntry
	{
		public GameObject gameObject;
		public string Id = Guid.NewGuid().ToString();
	}
	
	[HideInInspector]
	public StoredReferences Reference;
	private static StoredReferences _cached;
	
	
	
	//private static Dictionary<string, StoredEntry> _cached = new Dictionary<string, StoredEntry>();
	private static List<Action> _initActions = new List<Action>();

	
	public GameObject GetById(string id)
	{
		var se = Instance.Reference[id];
		return se != null ? se.gameObject : null;
	}
	
	
	
	public void SetId(GameObject gameObject, string id)
	{
		var rr = Instance.Reference[gameObject] ?? Instance.Reference[id];
		if(rr != null)
		{
			Instance.Reference.Remove(rr.gameObject);
			rr.Id = id;
			rr.gameObject = gameObject;
		} else
		{
			rr =new StoredEntry { gameObject = gameObject, Id = id };
			
		}
		Instance.Reference[rr.Id] = rr;
	}

	public static string GetId(GameObject gameObject)
	{
		if(Instance == null || gameObject == null)
			return string.Empty;
		
		var entry = Instance.Reference[gameObject];
		if(entry != null)
			return entry.Id;
		if(Application.isLoadingLevel && !Application.isPlaying)
		{
			return null;
		}
		entry = new StoredEntry { gameObject = gameObject};
		Instance.Reference[entry.Id] = entry;
		return entry.Id;
	}
	
	private bool hasWoken;
	
	public static void Initialize(Action a)
	{
		if(Instance != null && Instance.hasWoken)
		{
			a();
		}
		else
		{
			_initActions.Add(a);
		}
	}
	
	private static Index<string, Index<string, List<object>>> _assetStore = new Index<string, Index<string, List<object>>>();
	
	public static Index<string, Index<string, List<object>>> assetStore
	{
		get
		{
			if(_assetStore.Count == 0)
			{
				Instance.GetAllReferences();
			}
			ProcessReferences();
			return _assetStore;
		}
	}
	
	static void ProcessReferences()
	{
		foreach(var l in _assetStore.Values.ToList())
		{
			foreach(var k in l.Keys.ToList())
			{
				l[k] = l[k].Where(c=>c!=null).ToList();
				if(l[k].Count==0)
				{
					l.Remove(k);
				}
			}
		}
	}
	
	public static void RefreshReferences()
	{
		if(Instance != null)
			Instance.GetAllReferences();
	}
	
	public delegate void AllowAssetDelegate(UnityEngine.Object asset, ref bool allow);
	public static event AllowAssetDelegate FilterAssetReference = delegate {};
	
	void GetAllReferences()
	{
		var assets = Resources.FindObjectsOfTypeAll(typeof(AnimationClip))
			.Concat(Resources.FindObjectsOfTypeAll(typeof(AudioClip)))
			.Concat(Resources.FindObjectsOfTypeAll(typeof(Mesh)))
			.Concat(Resources.FindObjectsOfTypeAll(typeof(Material)))
			.Concat(Resources.FindObjectsOfTypeAll(typeof(Texture)))
			.Concat(Resources.FindObjectsOfTypeAll(typeof(ScriptableObject)))
			.Where(g=>g!=null && !string.IsNullOrEmpty(g.name) && g.GetInstanceID()>0 )
			.Distinct()
			.ToList();
		_assetStore.Clear();
		foreach(var a in assets)
		{
			var allowAsset = true;
			FilterAssetReference(a, ref allowAsset);
			if(allowAsset)
				_assetStore[a.GetType().Name][a.name].Add(a);
		}
		
	}
	
	public class AssetReference
	{
		public string name;
		public string type;
		public int index;
	}
	
	public static AssetReference GetAssetId(object obj)
	{
		var item = obj as UnityEngine.Object;
		if(item == null)
		{
			return new AssetReference { index = -1};
		}
		var index = assetStore[obj.GetType().Name][item.name].IndexOf(obj);
		if(index != -1)
		{
			return new AssetReference { name = item.name, type = obj.GetType().Name, index = index};
		}
		return new AssetReference { index = -1};
	}

	
	public static object GetAsset(AssetReference id) 
	{
		if(id.index == -1)
			return null;
		try
		{
			return assetStore[id.type][id.name][id.index];
		}
		catch
		{
			return null;
		}
	}
	
	void OnDestroy()
	{
		DestroyImmediate(Reference);
	}
	
	void GetAllInactiveGameObjects()
	{
		var items = Reference.AllReferences.Select(g=>g.transform);
		RecurseAddInactive(items);
	}
	
	void RecurseAddInactive(IEnumerable<Transform> items)
	{
		foreach(var child in items)
		{
			if(child.GetComponent<UniqueIdentifier>()!=null)
			{
				if(!child.gameObject.active)
				{
					GetId(child.gameObject);
				}
			}
			RecurseAddInactive(child.Cast<Transform>());
		}
	}
		
		
		
	
	void Awake()
	{
		if(Reference == null)
			Reference = ScriptableObject.CreateInstance<StoredReferences>();
		GetAllReferences();
		if(Application.isEditor)
		{
			GetAllInactiveGameObjects();
		}
		if(Instance != null && Instance != this)
			Destroy(Instance.gameObject);
		Instance = this;
		hasWoken = true;
		if(Application.isPlaying && !hasRun)
		{
			_cached = Reference;
			hasRun = true;
		}
		else if(!Application.isPlaying ) {
			hasRun = false;
			if(_cached != null && _cached.Count > 0)
				Reference = _cached.Alive();
		}
		if(_initActions.Count > 0)
		{
			foreach(var a in _initActions)
			{
				a();
			}
			_initActions.Clear();
		}

		
	}
}


