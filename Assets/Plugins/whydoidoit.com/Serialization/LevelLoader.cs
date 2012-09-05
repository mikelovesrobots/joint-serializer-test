using UnityEngine;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;
using Serialization;

//Do not add this script to your own classes! This is created internally
[AddComponentMenu("Storage/Internal/Level Loader (Internal use only, do not add this to your objects!)")]
public class LevelLoader : MonoBehaviour
{
	public LevelSerializer.LevelData Data;
	public static LevelLoader Current;
	
	public delegate void SerializedObjectDelegate(GameObject gameObject, ref bool cancel);
	public delegate void SerializedComponentDelegate(GameObject gameObject, string componentName, ref bool cancel);
	public delegate void CreateObjectDelegate(GameObject prefab, ref bool cancel);
	
	public static event CreateObjectDelegate CreateGameObject = delegate {};
	public static event SerializedObjectDelegate OnDestroyObject = delegate {};
	public static event SerializedObjectDelegate LoadData = delegate {};
	public static event SerializedComponentDelegate LoadComponent = delegate {};
	public static event Action<Component> LoadedComponent = delegate{};
	
	public Action<List<GameObject>> whenCompleted  = delegate {
		
	};
	
	static Texture2D pixel;
	float alpha = 1;
	bool loading = true;
	public bool showGUI = true;
	

	void Awake()
	{
		Current = this;
		if(pixel==null)
		{
			pixel = new Texture2D(1,1);
		}
	}
	
	void OnGUI()
	{
		if(!showGUI)
			return;
		if(!loading && Event.current.type == EventType.repaint)
		{
			alpha = Mathf.Clamp01(alpha - 0.02f);
		}
		else if(alpha == 0)
		{
			GameObject.Destroy(gameObject);
		}
		if(alpha != 0)
		{
			pixel.SetPixel(0,0,new Color(1,1,1,alpha));
			pixel.Apply();
			GUI.DrawTexture(new Rect(0,0,Screen.width, Screen.height), pixel, ScaleMode.StretchToFill);
		}
		
	}
	
	void OnLevelWasLoaded(int level)
	{
		StartCoroutine(Load());
	}
		
	public bool DontDelete = false;
	public GameObject Last;
	
	Dictionary<string, int> indexDictionary = new Dictionary<string, int>();
	
	
	void SetActive(GameObject go, bool active)
	{
		go.active =active;
		foreach(var c in go.transform.Cast<Transform>())
		{
			if(c.GetComponent<StoreInformation>() == null)
			{
				SetActive(c.gameObject, active);
			}
		}
	}
	
	public IEnumerator Load()
	{
		yield return StartCoroutine(Load(5));
	}
	
	public IEnumerator Load (int numberOfFrames)
	{
		//Need to wait while the base level is prepared, it takes 2 frames
		while(numberOfFrames-- > 0)
		{
			yield return new WaitForEndOfFrame();
		}
		if(LevelSerializer.ShouldCollect) GC.Collect();
		
		LevelSerializer.RaiseProgress("Initializing",0);
		
		//Check if we should be deleting missing items
		if(!DontDelete)
		{
			//First step is to remove any items that should not exist according to the saved scene
			foreach (var go in UniqueIdentifier.AllIdentifiers.Where(n=>!Data.StoredObjectNames.Any(sn=>sn.Name == n.Id)).ToList())
			{
				try
				{
					bool cancel = false;
					OnDestroyObject(go.gameObject, ref cancel);
					if(!cancel)
						GameObject.Destroy (go.gameObject);
				}
				catch(Exception e)
				{
					Radical.LogWarning("Problem destroying object " + go.name + " " + e.ToString());
				}
			}
		}
		
		LevelSerializer.RaiseProgress("Initializing",0.25f);
		
		
		//Next we need to instantiate any items that are needed by the stored scene
		foreach(var sto in Data.StoredObjectNames.Where(c=>UniqueIdentifier.GetByName(c.Name) == null && !string.IsNullOrEmpty(c.ClassId) ))
		{
			try
			{
				var pf = LevelSerializer.AllPrefabs[sto.ClassId];
				bool cancel = false;
				CreateGameObject(pf as GameObject, ref cancel);
				if(cancel) continue;
				
				sto.GameObject = Instantiate(pf) as GameObject;
				sto.GameObject.GetComponent<UniqueIdentifier>().Id = sto.Name;
				if(sto.ChildIds.Count > 0)
				{
					var list = sto.GameObject.GetComponentsInChildren<UniqueIdentifier>().ToList();
					for(var i = 0; i < list.Count && i < sto.ChildIds.Count; i++)
					{
						list[i].Id = sto.ChildIds[i];
					}
				}
				if(sto.Children.Count > 0)
				{
					var list = LevelSerializer.GetComponentsInChildrenWithClause(sto.GameObject);
					indexDictionary.Clear();
					foreach(var c in list)
					{
						if(sto.Children.ContainsKey(c.ClassId))
						{
							if(!indexDictionary.ContainsKey(c.ClassId))
							{
								indexDictionary[c.ClassId] = 0;
							}
							c.Id = sto.Children[c.ClassId][indexDictionary[c.ClassId]];
							indexDictionary[c.ClassId] = indexDictionary[c.ClassId] + 1;
						}
					}
					
				}
				
			}
			catch(Exception e)
			{
				Radical.LogWarning("Problem creating " + sto.GameObjectName + " with classID " + sto.ClassId + " " + e.ToString());
			}

		}
		HashSet<GameObject> loadedGameObjects = new HashSet<GameObject>();
		
		LevelSerializer.RaiseProgress("Initializing",0.75f);
		
		
		foreach(var so in Data.StoredObjectNames)
		{
			var go = UniqueIdentifier.GetByName(so.Name);
			if(go == null)
			{
				Radical.LogNow("Could not find " + so.GameObjectName + " " + so.Name);
			}
			else
			{
				loadedGameObjects.Add(go);
				if(so.Components != null && so.Components.Count > 0)
				{
					var all = go.GetComponents<Component>().ToList();
					foreach(var comp in all)
					{
						if(!so.Components.ContainsKey(comp.GetType().AssemblyQualifiedName))
						{
							Destroy(comp);
						}
					}
				}
				SetActive(go, so.Active);
				
			}
		}

		LevelSerializer.RaiseProgress("Initializing",0.85f);
		

		foreach(var go in Data.StoredObjectNames.Where(c=>!string.IsNullOrEmpty(c.ParentName)))
		{
			var parent = UniqueIdentifier.GetByName(go.ParentName);
			var item = UniqueIdentifier.GetByName(go.Name);
			if(item != null && parent != null)
			{
				item.transform.parent = parent.transform;
			}
		}
		
		//Newly created objects should have the time to start
		yield return new WaitForEndOfFrame();
		yield return new WaitForEndOfFrame();

		LevelSerializer.RaiseProgress("Initializing",1f);
		
		
		using (new Radical.Logging())
		{
			var currentProgress = 0;

			using (new UnitySerializer.SerializationScope())
			{
				//Now we restore the data for the items
				foreach (var item in Data.StoredItems.GroupBy(i=>i.Name,(name, cps)=>
					new {
					  Name = name,
					  Components = cps.Where(cp=>cp.Name==name).GroupBy(cp=>cp.Type, (type, components)=>new { Type = type, List = components.ToList() } ).ToList()
				}))
				{
					
#if US_LOGGING
					Radical.Log ("\n*****************\n{0}\n********START**********\n", item.Name);
					Radical.IndentLog ();
#endif
					var go = UniqueIdentifier.GetByName (item.Name);
					if (go == null)
					{
						Radical.LogWarning (item.Name + " was null");
						continue; 
						
					}
					
					
					foreach (var cp in item.Components)
					{
						try
						{
							LevelSerializer.RaiseProgress("Loading", (float)++currentProgress/(float)Data.StoredItems.Count);
								Type type = Type.GetType (cp.Type);
								if (type == null)
								{
									continue;
								}
								Last = go;
								bool cancel = false;
								LoadData(go, ref cancel);
								LoadComponent(go, type.Name, ref cancel);
								if(cancel)
									continue;
								
#if US_LOGGING
								Radical.Log ("<{0}>\n", type.FullName);
								Radical.IndentLog ();
#endif
		
								var list = go.GetComponents (type).Where (c => c.GetType () == type).ToList ();
								//Make sure the lists are the same length
								while (list.Count > cp.List.Count)
								{
									Component.DestroyImmediate (list.Last ());
									list.Remove (list.Last ());
								}
							   	if(type == typeof(NavMeshAgent)) 
								{
									Action perform = ()=>{
										var comp = cp;
										var tp = type;
										var tname = item.Name;
										UnitySerializer.AddFinalAction(()=>{
											var g = UniqueIdentifier.GetByName (tname);
											var nlist = g.GetComponents (tp).Where (c => c.GetType () == tp).ToList ();
											while (nlist.Count < comp.List.Count)
											{
												try
												{
													nlist.Add (g.AddComponent (tp));
												} catch
												{
												}
											}
											list = list.Where (l => l != null).ToList ();
											//Now deserialize the items back in
											for (var i =0; i < nlist.Count; i++)
											{
												if (LevelSerializer.CustomSerializers.ContainsKey (tp))
												{
													LevelSerializer.CustomSerializers [tp].Deserialize (comp.List [i].Data, nlist [i]);
												} else
												{
													UnitySerializer.DeserializeInto (comp.List [i].Data, nlist [i]);
												}
												LoadedComponent(nlist[i]);
											}
										});
									};
									perform();
									
								} else {
									while (list.Count < cp.List.Count)
									{
										try
										{
#if US_LOGGING
										    Radical.Log("Adding component of type " + type.ToString());
#endif
											list.Add (go.AddComponent (type));
										} catch
										{
										}
									}
									list = list.Where (l => l != null).ToList ();
									//Now deserialize the items back in
									for (var i =0; i < list.Count; i++)
									{
										Radical.Log (string.Format ("Deserializing {0} for {1}", type.Name, go.GetFullName ()));
										if (LevelSerializer.CustomSerializers.ContainsKey (type))
										{
											LevelSerializer.CustomSerializers [type].Deserialize (cp.List [i].Data, list [i]);
										} else
										{
											UnitySerializer.DeserializeInto (cp.List [i].Data, list [i]);
										}
										LoadedComponent(list[i]);
									}
								}
#if US_LOGGING
							    Radical.OutdentLog ();
							    Radical.Log ("</{0}>", type.FullName);
#endif
						
							}
							catch(Exception e)
							{
								Radical.LogWarning("Problem deserializing " + cp.Type + " for " + go.name + " " + e.ToString());
							}
						
						
					}
					
#if US_LOGGING				
					Radical.OutdentLog ();
					Radical.Log ("\n*****************\n{0}\n********END**********\n\n", item.Name);
#endif
					
					
				}
		
				//yield return null;
				//Finally we need to fixup any references to other game objects,
				//these have been stored in a list inside the serializer
				//waiting for us to call this.  Vector3s are also deferred until this point
				UnitySerializer.RunDeferredActions ();
				Resources.UnloadUnusedAssets();
				if(LevelSerializer.ShouldCollect) GC.Collect();
			
				yield return null;
				yield return null;
			
				
				UnitySerializer.InformDeserializedObjects ();
			
				//Flag that we aren't deserializing
				LevelSerializer.IsDeserializing = false;
			
				//Tell the world that the level has been loaded
				LevelSerializer.InvokeDeserialized ();
				whenCompleted(loadedGameObjects.ToList());
				loading = false;
				RoomManager.loadingRoom = false;
				//Get rid of the current object that is holding this level loader, it was
				//created solely for the purpose of running this script
				GameObject.Destroy (this.gameObject, 1.1f);
			
			}
		}
	}

}