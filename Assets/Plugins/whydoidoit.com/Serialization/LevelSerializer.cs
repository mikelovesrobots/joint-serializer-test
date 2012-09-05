//http://www.whydoidoit.com
//Copyright (C) 2012 Mike Talbot
//
//Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


using UnityEngine;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;
using Serialization;
using System.Reflection;
using System.IO.Compression;
using System.IO;
using SevenZipRadical;
using SevenZipRadical.Compression.LZMA;
using Object = UnityEngine.Object;

/// <summary>
///   Declares a class that serializes a derivation of Component
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ComponentSerializerFor : Attribute
{
    public Type SerializesType;

    public ComponentSerializerFor(Type serializesType)
    {
        SerializesType = serializesType;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SerializerPlugIn : Attribute
{
}


[AttributeUsage(AttributeTargets.Class)]
public class SuspendLevelSerialization : Attribute
{
}

public interface IComponentSerializer
{
    /// <summary>
    ///   Serialize the specified component to a byte array
    /// </summary>
    /// <param name='component'> Component to be serialized </param>
    byte[] Serialize(Component component);

    /// <summary>
    ///   Deserialize the specified data into the instance.
    /// </summary>
    /// <param name='data'> The data that represents the component, produced by Serialize </param>
    /// <param name='instance'> The instance to target </param>
    void Deserialize(byte[] data, Component instance);
}

public interface IControlSerialization
{
    bool ShouldSave();
}

public interface IControlSerializationEx : IControlSerialization
{
    bool ShouldSaveWholeObject();
}


public class LevelSerializer
{
    #region Delegates

    public delegate void StoreQuery(GameObject go, ref bool store);

    #endregion

    #region SerializationModes enum

    public enum SerializationModes
    {
        SerializeWhenFree,
        CacheSerialization
    }

    #endregion

    private static Dictionary<string, GameObject> allPrefabs = new Dictionary<string, GameObject>();
    public static HashSet<string> IgnoreTypes = new HashSet<string>();

    public static Dictionary<Type, IComponentSerializer> CustomSerializers =
        new Dictionary<Type, IComponentSerializer>();

    public static int lastFrame;

    /// <summary>
    ///   The name of the player.
    /// </summary>
    public static string PlayerName = string.Empty;

    public static bool SaveResumeInformation = true;
    private static int _suspensionCount;
    private static SaveEntry _cachedState;

    /// <summary>
    ///   The serialization caching mode
    /// </summary>
    public static SerializationModes SerializationMode = SerializationModes.CacheSerialization;

    /// <summary>
    ///   The max games that will be stored.
    /// </summary>
    public static int MaxGames = 20;

    /// <summary>
    ///   The saved games.
    /// </summary>
    public static Lookup<string, List<SaveEntry>> SavedGames = new Index<string, List<SaveEntry>>();

    //Stop cases
    private static readonly List<Type> _stopCases = new List<Type>();
    public static bool IsDeserializing;
    private static readonly List<object> createdPlugins = new List<object>();
    public static bool useCompression = true;
    private static readonly ProgressHelper progressHelper = new ProgressHelper();

    static LevelSerializer()
    {
        //Basic plug in configuration and special cases
        _stopCases.Add(typeof (PrefabIdentifier));
        UnitySerializer.AddPrivateType(typeof (AnimationClip));

        //Other initialization
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            UnitySerializer.ScanAllTypesForAttribute(
                (tp, attr) => createdPlugins.Add(Activator.CreateInstance(tp)), asm, typeof (SerializerPlugIn));
            UnitySerializer.ScanAllTypesForAttribute(
                (tp, attr) =>
                    {
                        CustomSerializers[((ComponentSerializerFor) attr).SerializesType] =
                            Activator.CreateInstance(tp) as IComponentSerializer;
                    }, asm,
                typeof (ComponentSerializerFor));
        }


        AllPrefabs =
            Resources.FindObjectsOfTypeAll(typeof (GameObject)).Cast<GameObject>()
                .Where(go =>
                           {
                               var pf = go.GetComponent<PrefabIdentifier>();
                               return pf != null && !pf.IsInScene();
                           })
                .Distinct(CompareGameObjects.Instance)
                .ToDictionary(go => go.GetComponent<PrefabIdentifier>().ClassId, go => go);


        try
        {
            var stored = PlayerPrefs.GetString("_Save_Game_Data_");
            if (!string.IsNullOrEmpty(stored))
            {
                SavedGames =
                    UnitySerializer.Deserialize<Lookup<string, List<SaveEntry>>>(Convert.FromBase64String(stored));
            }
            if (SavedGames == null)
            {
                SavedGames = new Index<string, List<SaveEntry>>();
            }
        }
        catch
        {
            SavedGames = new Index<string, List<SaveEntry>>();
        }
    }

    public static Dictionary<string, GameObject> AllPrefabs
    {
        get
        {
            if (Time.frameCount != lastFrame)
            {
                allPrefabs = allPrefabs.Where(p => p.Value).ToDictionary(p => p.Key, p => p.Value);
                lastFrame = Time.frameCount;
            }
            return allPrefabs;
        }
        set { allPrefabs = value; }
    }

    public static bool CanResume
    {
        get { return !string.IsNullOrEmpty(PlayerPrefs.GetString(PlayerName + "__RESUME__")); }
    }

    /// <summary>
    ///   Gets a value indicating whether this instance is suspended.
    /// </summary>
    /// <value> <c>true</c> if this instance is suspended; otherwise, <c>false</c> . </value>
    public static bool IsSuspended
    {
        get { return _suspensionCount > 0; }
    }

    /// <summary>
    ///   Gets the serialization suspension count.
    /// </summary>
    /// <value> The suspension count. </value>
    public static int SuspensionCount
    {
        get { return _suspensionCount; }
    }


    /// <summary>
    ///   Occurs when the level was deserialized
    /// </summary>
    public static event Action Deserialized = delegate { };

    /// <summary>
    ///   Occurs when the level was serialized.
    /// </summary>
    public static event Action GameSaved = delegate { };

    /// <summary>
    ///   Occurs when suspending serialization.
    /// </summary>
    public static event Action SuspendingSerialization = delegate { };

    /// <summary>
    ///   Occurs when resuming serialization.
    /// </summary>
    public static event Action ResumingSerialization = delegate { };

    internal static void InvokeDeserialized()
    {
        _suspensionCount = 0;
        if (Deserialized != null)
        {
            Deserialized();
        }
        foreach (var go in Object.FindObjectsOfType(typeof (GameObject)).Cast<GameObject>())
        {
            go.SendMessage("OnDeserialized", null, SendMessageOptions.DontRequireReceiver);
        }
    }

    public static event StoreQuery Store;

    /// <summary>
    ///   Resume for a stored game state that wasn't directly saved
    /// </summary>
    public static void Resume()
    {
        var data = PlayerPrefs.GetString(PlayerName + "__RESUME__");
        if (!string.IsNullOrEmpty(data))
        {
            var se = UnitySerializer.Deserialize<SaveEntry>(Convert.FromBase64String(data));
            se.Load();
        }
    }

    /// <summary>
    ///   Create a resumption checkpoint
    /// </summary>
    public static void Checkpoint()
    {
        SaveGame("Resume", false, PerformSaveCheckPoint);
    }

    private static void PerformSaveCheckPoint(string name, bool urgent)
    {
        var newGame = CreateSaveEntry(name, urgent);
        PlayerPrefs.SetString(PlayerName + "__RESUME__", Convert.ToBase64String(UnitySerializer.Serialize(newGame)));
    }

    /// <summary>
    ///   Suspends the serialization. Must resume as many times as you suspend
    /// </summary>
    public static void SuspendSerialization()
    {
        if (_suspensionCount == 0)
        {
            SuspendingSerialization();
            if (SerializationMode == SerializationModes.CacheSerialization)
            {
                _cachedState = CreateSaveEntry("resume", true);
                if (SaveResumeInformation)
                {
                    PlayerPrefs.SetString(PlayerName + "__RESUME__",
                                          Convert.ToBase64String(UnitySerializer.Serialize(_cachedState)));
                }
            }
        }
        _suspensionCount++;
    }

    /// <summary>
    ///   Resumes the serialization. Must be balanced with calls to SuspendSerialization
    /// </summary>
    public static void ResumeSerialization()
    {
        _suspensionCount--;
        if (_suspensionCount == 0)
        {
            ResumingSerialization();
        }
    }

    /// <summary>
    ///   Ignores the type of component when saving games.
    /// </summary>
    /// <param name='typename'> Typename of the component to ignore </param>
    public static void IgnoreType(string typename)
    {
        IgnoreTypes.Add(typename);
    }

    public static void UnIgnoreType(string typename)
    {
        IgnoreTypes.Remove(typename);
    }

    /// <summary>
    ///   Ignores the type of component when saving games.
    /// </summary>
    /// <param name='tp'> The type of the component to ignore </param>
    public static void IgnoreType(Type tp)
    {
        if (tp.FullName != null)
        {
            IgnoreTypes.Add(tp.FullName);
        }
    }

    /// <summary>
    ///   Creates a saved game for the current position
    /// </summary>
    /// <returns> The new save entry. </returns>
    /// <param name='name'> A name for the save entry </param>
    /// <param name='urgent'> An urgent save will store the current state, even if suspended. In this case it is likely that clean up will be necessary by handing Deserialized messages or responding to the LevelSerializer.Deserialized event </param>
    public static SaveEntry CreateSaveEntry(string name, bool urgent)
    {
        return new SaveEntry()
                   {
                       Name = name,
                       When = DateTime.Now,
                       Level = Application.loadedLevelName,
                       Data = SerializeLevel(urgent)
                   };
    }

    /// <summary>
    ///   Saves the game.
    /// </summary>
    /// <param name='name'> The name to use for the game </param>
    public static void SaveGame(string name)
    {
        SaveGame(name, false, null);
    }

    public static void SaveGame(string name, bool urgent, Action<string, bool> perform)
    {
        perform = perform ?? PerformSave;
        //See if we need to serialize later
        if (!urgent && (IsSuspended && SerializationMode == SerializationModes.SerializeWhenFree))
        {
            //Are we already waiting for serialization to occur
            if (GameObject.Find("/SerializationHelper") != null)
            {
                return;
            }
            //Create a helper
            var go = new GameObject("SerializationHelper");
            var helper = go.AddComponent(typeof (SerializationHelper)) as SerializationHelper;
            helper.gameName = name;
            helper.perform = perform;
            return;
        }

        perform(name, urgent);
    }

    private static void PerformSave(string name, bool urgent)
    {
        var newGame = CreateSaveEntry(name, urgent);
        SavedGames[PlayerName].Insert(0, newGame);


        while (SavedGames.Count > MaxGames)
        {
            SavedGames[PlayerName].RemoveAt(SavedGames.Count - 1);
        }

        SaveDataToPlayerPrefs();

        PlayerPrefs.SetString(PlayerName + "__RESUME__", Convert.ToBase64String(UnitySerializer.Serialize(newGame)));

        GameSaved();
    }


    /// <summary>
    ///   Saves the stored game data to player prefs.
    /// </summary>
    public static void SaveDataToPlayerPrefs()
    {
        PlayerPrefs.SetString("_Save_Game_Data_", Convert.ToBase64String(UnitySerializer.Serialize(SavedGames)));
    }

    /// <summary>
    ///   Registers the calling assembly as one providing serialization extensions.
    /// </summary>
    public static void RegisterAssembly()
    {
        UnitySerializer.ScanAllTypesForAttribute(
            (tp, attr) =>
                {
                    CustomSerializers[((ComponentSerializerFor) attr).SerializesType] =
                        Activator.CreateInstance(tp) as IComponentSerializer;
                }, Assembly.GetCallingAssembly(),
            typeof (ComponentSerializerFor));
    }

    /// <summary>
    ///   Adds the prefab path.
    /// </summary>
    /// <param name='path'> A resource path that contains prefabs to be created for the game </param>
    public static void AddPrefabPath(string path)
    {
        foreach (var pair in Resources.LoadAll(path, typeof (GameObject))
            .Cast<GameObject>()
            .Where(go => go.GetComponent<UniqueIdentifier>() != null)
            .ToDictionary(go => go.GetComponent<UniqueIdentifier>().ClassId, go => go).Where(
                pair => !AllPrefabs.ContainsKey(pair.Key)))
        {
            AllPrefabs.Add(pair.Key, pair.Value);
        }
    }
	
	public static void DontCollect()
	{
		_collectionCount++;
	}
	
	public static void Collect()
	{
		_collectionCount--;
	}
	
	static int _collectionCount = 0;
	
	public static bool ShouldCollect
	{
		get
		{
			return _collectionCount <= 0;
		}
	}

    /// <summary>
    ///   Serializes the level to a string
    /// </summary>
    /// <returns> The level data as a string </returns>
    /// <exception>Is thrown when the serization was suspended
    ///   <cref>SerizationSuspendedException</cref>
    /// </exception>
    public static string SerializeLevel()
    {
        return SerializeLevel(false);
    }

    /// <summary>
    ///   Serializes the level.
    /// </summary>
    /// <returns> The level stored as a string. </returns>
    /// <param name='urgent'> Whether to ignore an suspension of serialization </param>
    /// <exception cref='SerializationSuspendedException'>Is thrown when the serialization was suspended and urgent was not specified</exception>
    public static string SerializeLevel(bool urgent)
    {
        if (IsSuspended && !urgent)
        {
            if (SerializationMode == SerializationModes.CacheSerialization)
            {
                return _cachedState.Data;
            }
            else
            {
                throw new SerializationSuspendedException();
            }
        }
        //Try to get as much memory as possible
		Resources.UnloadUnusedAssets();
        if(ShouldCollect) GC.Collect();
        var data = SerializeLevel(false, null);
        //Free up memory that has been used during serialization
        if(ShouldCollect) GC.Collect();
        if (useCompression)
        {
            return Convert.ToBase64String(
                SevenZipRadicalHelper.Compress(data, progressHelper)
                );
        }
        else
        {
            return "NOCOMPRESSION" + Convert.ToBase64String(data);
        }
    }

    public static void RaiseProgress(string section, float complete)
    {
        Progress(section, complete);
    }

    public static bool HasParent(UniqueIdentifier i, string id)
    {
        var scan = UniqueIdentifier.GetByName(i.Id).transform;
        while (scan != null)
        {
            UniqueIdentifier ui;
            if ((ui = scan.GetComponent<UniqueIdentifier>()) != null)
            {
                if (id == ui.Id)
                {
                    return true;
                }
            }
            scan = scan.parent;
        }
        return false;
    }

    private static void GetComponentsInChildrenWithClause(Transform t, List<StoreInformation> components)
    {
        foreach (var c in t.Cast<Transform>())
        {
            var s = c.GetComponent<StoreInformation>();
            if (s != null)
            {
                if (!(s is PrefabIdentifier))
                {
                    components.Add(s);
                    GetComponentsInChildrenWithClause(c, components);
                }
            }
            else
            {
                GetComponentsInChildrenWithClause(c, components);
            }
        }
    }

    /// <summary>
    ///   Internal function
    /// </summary>
    public static List<StoreInformation> GetComponentsInChildrenWithClause(GameObject go)
    {
        var components = new List<StoreInformation>();
        GetComponentsInChildrenWithClause(go.transform, components);
        return components;
    }

    public static event Action<string, float> Progress = delegate { };

    /// <summary>
    ///   Serializes the level to a byte array, with an optional root item. The root item
    ///   and its children, if specified, will be the only things saved
    /// </summary>
    /// <returns> The level data as a byte array </returns>
    /// <param name='urgent'> Whether to save even if serialization is suspended </param>
    /// <param name='id'> Identifier (or null) of an object to be the root of the data serialization </param>
    public static byte[] SerializeLevel(bool urgent, string id)
    {
        LevelData ld;


        using (new Radical.Logging())
        {
            //First we need to know the name of the last level loaded
            using (new UnitySerializer.SerializationScope())
            {
                ld = new LevelData()
                         {
                             //The level to reload
                             Name = Application.loadedLevelName
                         };
                //All of the currently active uniquely identified objects
                ld.StoredObjectNames = UniqueIdentifier
                    .AllIdentifiers
                    .Where(i => string.IsNullOrEmpty(id) || i.Id == id || HasParent(i, id))
                    .Select(i => i.gameObject)
                    .Where(go => go != null)
                    .Where(go =>
                               {
                                   var shouldSerialize = go.FindInterface<IControlSerializationEx>();
                                   return shouldSerialize == null || shouldSerialize.ShouldSaveWholeObject();
                               })
                    .Where(go =>
                               {
                                   if (Store == null)
                                   {
                                       return true;
                                   }
                                   var result = true;
                                   Store(go, ref result);
                                   return result;
                               })
                    .Select(n =>
                                {
                                    try
                                    {
                                        var si = new StoredItem()
                                                     {
                                                         Active = n.active,
                                                         Components =
                                                             n.GetComponents<Component>().Select(
                                                                 c => c.GetType().AssemblyQualifiedName).Distinct().
                                                             ToDictionary(v => v, v => true),
                                                         Name = n.GetComponent<UniqueIdentifier>().Id,
                                                         GameObjectName = n.name,
                                                         ParentName =
                                                             (n.transform.parent == null ||
                                                              n.transform.parent.GetComponent<UniqueIdentifier>() ==
                                                              null)
                                                                 ? null
                                                                 : (n.transform.parent.GetComponent<UniqueIdentifier>().
                                                                       Id),
                                                         ClassId = n.GetComponent<PrefabIdentifier>() != null
                                                                       ? n.GetComponent<PrefabIdentifier>().ClassId
                                                                       : string.Empty
                                                     };
                                        if (n.GetComponent<StoreInformation>())
                                        {
                                            n.SendMessage("OnSerializing", SendMessageOptions.DontRequireReceiver);
                                        }
                                        var pf = n.GetComponent<PrefabIdentifier>();
                                        if (pf != null)
                                        {
                                            var components = GetComponentsInChildrenWithClause(n);
                                            si.Children = components.GroupBy(c => c.ClassId).ToDictionary(c => c.Key,
                                                                                                          c =>
                                                                                                          c.Select(
                                                                                                              i => i.Id)
                                                                                                              .ToList());
                                        }
                                        return si;
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogWarning("Failed to serialize status of " + n.name + " with error " +
                                                         e.ToString());
                                        return null;
                                    }
                                })
                    .Where(si => si != null)
                    .ToList();

                //All of the data for the items to be stored

                var toBeProcessed = UniqueIdentifier
                    .AllIdentifiers
                    .Where(o => o.GetComponent<StoreInformation>() != null || o.GetComponent<PrefabIdentifier>() != null)
                    .Where(i => string.IsNullOrEmpty(id) || i.Id == id)
                    .Where(i => i != null)
                    .Select(i => i.gameObject)
                    .Where(i => i != null)
                    .Where(go =>
                               {
                                   var shouldSerialize = go.FindInterface<IControlSerializationEx>();
                                   return shouldSerialize == null || shouldSerialize.ShouldSaveWholeObject();
                               })
                    .Distinct()
                    .Where(go =>
                               {
                                   if (Store == null)
                                   {
                                       return true;
                                   }
                                   var result = true;
                                   Store(go, ref result);

                                   return result;
                               })
                    .SelectMany(o => o.GetComponents<Component>())
                    .Where(c =>
                               {
                                   if (c == null)
                                   {
                                       return false;
                                   }
                                   var tp = c.GetType();
                                   return (!(c is IControlSerialization) || (c as IControlSerialization).ShouldSave()) &&
                                          !tp.IsDefined(typeof (DoNotSerialize), false) &&
                                          !tp.IsDefined(typeof (DontStoreAttribute), true)
                                          && !IgnoreTypes.Contains(tp.FullName);
                               })
                    .Select(c => new
                                     {
                                         Identifier =
                                     (StoreInformation) c.gameObject.GetComponent(typeof (StoreInformation)),
                                         Component = c
                                     })
                    .Where(cp =>
                           (cp.Identifier.StoreAllComponents ||
                            cp.Identifier.Components.Contains(cp.Component.GetType().FullName)))
                    .OrderBy(cp => cp.Identifier.Id)
                    .ThenBy(cp => cp.Component.GetType().AssemblyQualifiedName).ToList();
                var processed = 0;
                ld.StoredItems = toBeProcessed
                    .Select(cp =>
                                {
                                    try
                                    {
                                        if (Radical.IsLogging())
                                        {
                                            Radical.Log("<{0} : {1} - {2}>", cp.Component.gameObject.GetFullName(),
                                                        cp.Component.GetType().Name,
                                                        cp.Component.GetComponent<UniqueIdentifier>().Id);
                                            Radical.IndentLog();
                                        }
                                        var sd = new StoredData()
                                                     {
                                                         Type = cp.Component.GetType().AssemblyQualifiedName,
                                                         ClassId = cp.Identifier.ClassId,
                                                         Name = cp.Component.GetComponent<UniqueIdentifier>().Id
                                                     };

                                        if (CustomSerializers.ContainsKey(cp.Component.GetType()))
                                        {
                                            sd.Data = CustomSerializers[cp.Component.GetType()].Serialize(cp.Component);
                                        }
                                        else
                                        {
                                            sd.Data = UnitySerializer.SerializeForDeserializeInto(cp.Component);
                                        }
                                        if (Radical.IsLogging())
                                        {
                                            Radical.OutdentLog();
                                            Radical.Log("</{0} : {1}>", cp.Component.gameObject.GetFullName(),
                                                        cp.Component.GetType().Name);
                                        }
                                        processed++;
                                        Progress("Storing", (float) processed/(float) toBeProcessed.Count);
                                        return sd;
                                    }
                                    catch (Exception e)
                                    {
                                        processed++;
                                        Debug.LogWarning("Failed to serialize data (" +
                                                         cp.Component.GetType().AssemblyQualifiedName + ") of " +
                                                         cp.Component.name + " with error " + e.ToString());
                                        return null;
                                    }
                                })
                    .Where(s => s != null)
                    .ToList();
            }
        }

        var data = UnitySerializer.Serialize(ld);
        return data;
    }
	
	
	public static void LoadNow(object data)
	{
		LoadNow(data, false, true, null);
	}
	
	public static void LoadNow(object data, bool dontDeleteExistingItems)
	{
		LoadNow(data, dontDeleteExistingItems, true, null);
	}
	
	public static void LoadNow(object data, bool dontDeleteExistingItems, bool showLoadingGUI)
	{
		LoadNow(data, dontDeleteExistingItems, showLoadingGUI, null);
	}
	
	public static void LoadNow(object data, bool dontDeleteExistingItems, bool showLoadingGUI, Action<LevelLoader> complete)
	{
		byte[] levelData = null;
		if(data is byte[])
		{
			levelData = (byte[])data;
		}
		if(data is string)
		{
			levelData = Convert.FromBase64String((string)data);
		}
		if(levelData == null)
		{
			throw new ArgumentException("data parameter must be either a byte[] or a base64 encoded string");
		}
		//Create a level loader
		var l = new GameObject();
		var loader = l.AddComponent<LevelLoader>();
		loader.showGUI = showLoadingGUI;
		var ld = UnitySerializer.Deserialize<LevelSerializer.LevelData> (levelData);
		loader.Data = ld;
		loader.DontDelete = dontDeleteExistingItems;
		//Get the loader to do its job
		loader.StartCoroutine(PerformLoad(loader, complete));
	}
	
	static IEnumerator PerformLoad(LevelLoader loader, Action<LevelLoader> complete)
	{
		yield return loader.StartCoroutine(loader.Load());
		if(complete != null)
			complete(loader);
	}
	
	
    /// <summary>
    ///   Loads the saved level.
    /// </summary>
    /// <param name='data'> The data describing the level to load </param>
    public static LevelLoader LoadSavedLevel(string data)
    {
        IsDeserializing = true;
        LevelData ld;
        if (data.StartsWith("NOCOMPRESSION"))
        {
            ld = UnitySerializer.Deserialize<LevelData>(Convert.FromBase64String(data.Substring(13)));
        }
        else
        {
            ld =
                UnitySerializer.Deserialize<LevelData>(SevenZipRadicalHelper.Decompress(Convert.FromBase64String(data),
                                                                                        progressHelper));
        }

        SaveGameManager.Loaded();
        var go = new GameObject();
        Object.DontDestroyOnLoad(go);
        var loader = go.AddComponent<LevelLoader>();
        loader.Data = ld;

        Application.LoadLevel(ld.Name);
        return loader;
    }

    #region Nested type: CompareGameObjects

    private class CompareGameObjects : IEqualityComparer<GameObject>
    {
        #region IEqualityComparer[GameObject] implementation

        public bool Equals(GameObject x, GameObject y)
        {
            return System.String.Compare(x.GetComponent<PrefabIdentifier>().ClassId, y.GetComponent<PrefabIdentifier>().ClassId, System.StringComparison.Ordinal) == 0;
        }

        public int GetHashCode(GameObject obj)
        {
            return obj.GetComponent<PrefabIdentifier>().ClassId.GetHashCode();
        }

        #endregion

        public static readonly CompareGameObjects Instance = new CompareGameObjects();
    }

    #endregion

    #region Nested type: LevelData

    public class LevelData
    {
        //The name of the level that was saved
        public string Name;
        //A set of all of the unique object names on the level
        //The data that was saved for the level
        public List<StoredData> StoredItems;
        public List<StoredItem> StoredObjectNames;
    }

    #endregion

    #region Nested type: ProgressHelper

    private class ProgressHelper : ICodeProgress
    {
        #region ICodeProgress implementation

        public void SetProgress(long inSize, long outSize)
        {
            RaiseProgress("Compression",
                          Mathf.Clamp01((float) (inSize/(float) SevenZipRadicalHelper.dataProcessingSize)));
        }

        #endregion
    }

    #endregion

    #region Nested type: SaveEntry

    /// <summary>
    ///   A saved game entry
    /// </summary>
    public class SaveEntry
    {
        /// <summary>
        ///   The data about the saved game
        /// </summary>
        public string Data;

        /// <summary>
        ///   The name of the unity scene
        /// </summary>
        public string Level;

        /// <summary>
        ///   The name provided for the saved game.
        /// </summary>
        public string Name;

        /// <summary>
        ///   The time that the game was saved
        /// </summary>
        public DateTime When;

        /// <summary>
        ///   Initializes a new instance of the <see cref="LevelSerializer.SaveEntry" /> class.
        /// </summary>
        /// <param name='contents'> The string representing the data of the saved game (use .ToString()) </param>
        public SaveEntry(string contents)
        {
            UnitySerializer.DeserializeInto(Convert.FromBase64String(contents), this);
        }

        public SaveEntry()
        {
        }

        /// <summary>
        ///   Gets the caption.
        /// </summary>
        /// <value> The caption which is a combination of the name, the level and the time that the game was saved </value>
        public string Caption
        {
            get { return string.Format("{0} - {1} - {2:g}", Name, Level, When); }
        }

        /// <summary>
        ///   Load this saved game
        /// </summary>
        public void Load()
        {
            LoadSavedLevel(Data);
        }

        /// <summary>
        ///   Delete this saved game
        /// </summary>
        public void Delete()
        {
            var owner = SavedGames.FirstOrDefault(p => p.Value.Contains(this));
            if (owner.Value != null)
            {
                owner.Value.Remove(this);
                SaveDataToPlayerPrefs();
            }
        }

        /// <summary>
        ///   Returns a <see cref="System.String" /> that represents the current <see cref="LevelSerializer.SaveEntry" />.
        /// </summary>
        /// <returns> A <see cref="System.String" /> that represents the current <see cref="LevelSerializer.SaveEntry" /> . </returns>
        public override string ToString()
        {
            return Convert.ToBase64String(UnitySerializer.Serialize(this));
        }
    }

    #endregion

    #region Nested type: SerializationHelper

    /// <summary>
    ///   Checks for the ability to serialize
    /// </summary>
    public class SerializationHelper : MonoBehaviour
    {
        public string gameName;
        public Action<string, bool> perform;

        private void Update()
        {
            //Check to see if we are still suspended
            if (IsSuspended == false)
            {
                if (perform != null)
                {
                    perform(gameName, false);
                }
                DestroyImmediate(gameObject);
            }
        }
    }

    #endregion

    #region Nested type: SerializationSuspendedException

    public class SerializationSuspendedException : Exception
    {
        public SerializationSuspendedException() : base("Serialization was suspended: " + _suspensionCount + " times")
        {
        }
    }

    #endregion

    #region Nested type: StoredData

    public class StoredData
    {
        public string ClassId;
        public byte[] Data;
        public string Name;
        public string Type;
    }

    #endregion

    #region Nested type: StoredItem

    public class StoredItem
    {
        public bool Active;
        public List<string> ChildIds = new List<string>();
        public Dictionary<string, List<string>> Children = new Dictionary<string, List<string>>();
        public string ClassId;
        public Dictionary<string, bool> Components;

        [DoNotSerialize] public GameObject GameObject;
        public string GameObjectName;
        public string Name;
        public string ParentName;

        public override string ToString()
        {
            return string.Format("{0}  child of {2} - ({1})", Name, ClassId, ParentName);
        }
    }

    #endregion
}


[ComponentSerializerFor(typeof (Animation))]
public class SerializeAnimations : IComponentSerializer
{
    #region Nested type: StoredState

    public class StoredState
    {
        public byte[] data;
        public string name;
    }

    #endregion

    #region IComponentSerializer implementation

    public byte[] Serialize(Component component)
    {
        return
            UnitySerializer.Serialize(
                ((Animation) component).Cast<AnimationState>().Select(
                    a => new StoredState() {data = UnitySerializer.SerializeForDeserializeInto(a), name = a.name}).
                    ToList());
    }

    public void Deserialize(byte[] data, Component instance)
    {
        var animation = (Animation) instance;
        animation.Stop();
        var list = UnitySerializer.Deserialize<List<StoredState>>(data);
        foreach (var entry in list)
        {
            if (entry.name.Contains(" - Queued Clone"))
            {
                var newState = animation.PlayQueued(entry.name.Replace(" - Queued Clone", ""));
                UnitySerializer.DeserializeInto(entry.data, newState);
            }
            else
            {
                UnitySerializer.DeserializeInto(entry.data, animation[entry.name]);
            }
        }
    }

    #endregion
}

public static class FieldSerializer
{
    public static void SerializeFields(Dictionary<string, object> storage, object obj, params string[] names)
    {
        var tp = obj.GetType();
        foreach (var name in names)
        {
            var fld = tp.GetField(name,
                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                  BindingFlags.SetField);
            if (fld != null)
            {
                storage[name] = fld.GetValue(obj);
            }
        }
    }

    public static void DeserializeFields(Dictionary<string, object> storage, object obj)
    {
        var tp = obj.GetType();
        foreach (var p in storage)
        {
            var fld = tp.GetField(p.Key,
                                  BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                  BindingFlags.SetField);
            if (fld != null)
            {
                fld.SetValue(obj, p.Value);
            }
        }
    }
}