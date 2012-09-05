using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Serialization;
using System.Linq;

[Serializer(typeof(Vector3))]
public class SerializeVector3 : SerializerExtensionBase<Vector3>
{
	public override IEnumerable<object> Save (Vector3 target)
	{
		return new object[] { target.x, target.y, target.z};
	}
	
	public override object Load (object[] data, object instance)
	{
#if US_LOGGING
		Radical.Log ("Vector3: {0},{1},{2}", data [0], data [1], data [2]);
#endif
		return new UnitySerializer.DeferredSetter (d => {
			return new Vector3 ((float)data [0], (float)data [1], (float)data [2]);
		}
		);
	}
}


[Serializer(typeof(AnimationState))]
public class SerializeAnimationState : SerializerExtensionBase<AnimationState>
{
	public override IEnumerable<object> Save (AnimationState target)
	{
		return new object[] { target.name};
	}
	
	public override object Load (object[] data, object instance)
	{
		var uo = UnitySerializer.DeserializingObject;
		return new UnitySerializer.DeferredSetter (d => {
			var p = uo.GetType ().GetProperty ("animation").GetGetMethod ();
			if (p != null) {
				var animation = (Animation)p.Invoke (uo, null);
				if (animation != null) {
					return animation [(string)data [0]];
					
				}
			}
			return null;
		});
	}
}

[Serializer(typeof(WaitForSeconds))]
public class SerializeWaitForSeconds : SerializerExtensionBase<WaitForSeconds>
{
	
	public override IEnumerable<object> Save (WaitForSeconds target)
	{
		var seconds = target.GetType().GetProperty("m_seconds").GetGetMethod().Invoke(target, null);
		return new object[] {seconds};
		
	}
	
	public override object Load (object[] data, object instance)
	{
		return base.Load (data, instance);
	}
}

[Serializer(typeof(Quaternion))]
public class SerializeQuaternion : SerializerExtensionBase<Quaternion>
{
	public override IEnumerable<object> Save (Quaternion target)
	{
		
		return new object[] { target.x, target.y, target.z, target.w};
	}
	
	public override object Load (object[] data, object instance)
	{
		return new UnitySerializer.DeferredSetter (d => new Quaternion ((float)data [0], (float)data [1], (float)data [2], (float)data [3]));
	}
}

[Serializer(typeof(Bounds))]
public class SerializeBounds : SerializerExtensionBase<Bounds>
{
	public override IEnumerable<object> Save (Bounds target)
	{
		return new object[] { target.center.x, target.center.y, target.center.z, target.size.x, target.size.y, target.size.z };
	}

	public override object Load (object[] data, object instance)
	{
		return new Bounds (
				new Vector3 ((float)data [0], (float)data [1], (float)data [2]),
			    new Vector3 ((float)data [3], (float)data [4], (float)data [5]));
	}
}

public abstract class ComponentSerializerExtensionBase<T> : IComponentSerializer where T: Component
{
	public abstract IEnumerable<object> Save (T target);
	
	
	public abstract void LoadInto(object[] data, T instance);
	
	#region IComponentSerializer implementation
	public byte[] Serialize (Component component)
	{
		return UnitySerializer.Serialize(Save((T)component).ToArray());
		
	}

	public void Deserialize (byte[] data, Component instance)
	{
		object[] dataArray;
		dataArray = UnitySerializer.Deserialize<object[]>(data);
		LoadInto(dataArray, (T)instance);
	}
	#endregion
}

public class SerializerExtensionBase<T> : ISerializeObjectEx
{
	#region ISerializeObject implementation
	public object[] Serialize (object target)
	{
		return Save ((T)target).ToArray ();
	}

	public object Deserialize (object[] data, object instance)
	{
		return Load (data, instance);
	}
	#endregion
	
	public virtual IEnumerable<object> Save (T target)
	{
		return new object[0];
	}
	
	public virtual object Load (object[] data, object instance)
	{
		return null;
	}
	
	#region ISerializeObjectEx implementation
	public bool CanSerialize (Type targetType, object instance)
	{
		if (instance == null)
			return true;
		return CanBeSerialized (targetType, instance);
	}
	#endregion
	
	public virtual bool CanBeSerialized (Type targetType, object instance)
	{
		return true;
	}


}

[ComponentSerializerFor(typeof(BoxCollider))]
public class SerializeBoxCollider : ComponentSerializerExtensionBase<BoxCollider>
{
	public override IEnumerable<object> Save (BoxCollider target)
	{
		return new object[] {target.isTrigger, target.size.x,target.size.y,target.size.z, target.center.x, target.center.y,target.center.z, target.enabled};
	}
	public override void LoadInto (object[] data, BoxCollider instance)
	{
		instance.isTrigger = (bool)data[0];
		instance.size = new Vector3((float)data[1], (float)data[2], (float)data[3]);
		instance.center = new Vector3((float)data[4], (float)data[5], (float)data[6]);
		instance.enabled = (bool)data[7];
	}
}

[ComponentSerializerFor(typeof(Terrain))]
public class SerializeTerrain : ComponentSerializerExtensionBase<Terrain>
{
	public override IEnumerable<object> Save (Terrain target)
	{
		return new object[] { target.enabled};
	}
	
	public override void LoadInto (object[] data, Terrain instance)
	{
		instance.enabled = (bool)data[0];
	}
}

[ComponentSerializerFor(typeof(WheelCollider))]
[ComponentSerializerFor(typeof(MeshCollider))]
[ComponentSerializerFor(typeof(TerrainCollider))]
public class SerializeCollider : ComponentSerializerExtensionBase<Collider>
{
	public override IEnumerable<object> Save (Collider target)
	{
		return new object[] {target.isTrigger, target.enabled };
	}
	
	public override void LoadInto (object[] data, Collider instance)
	{
		instance.isTrigger = (bool)data[0];
		instance.enabled = (bool)data[1];
		
	}
}

[ComponentSerializerFor(typeof(CapsuleCollider))]
public class SerializeCapsuleCollider : ComponentSerializerExtensionBase<CapsuleCollider>
{
	public override IEnumerable<object> Save (CapsuleCollider target)
	{
		return new object[] {target.isTrigger, target.radius, target.center.x, target.center.y,target.center.z, target.height, target.enabled };
	}
	public override void LoadInto (object[] data, CapsuleCollider instance)
	{
		instance.isTrigger = (bool)data[0];
		instance.radius = (float)data[1];
		instance.center = new Vector3((float)data[2], (float)data[3], (float)data[4]);
		instance.height = (float)data[5];
		instance.enabled = (bool)data[6];
		
	}
}

[ComponentSerializerFor(typeof(SphereCollider))]
public class SerializeSphereCollider : ComponentSerializerExtensionBase<SphereCollider>
{
	public override IEnumerable<object> Save (SphereCollider target)
	{
		return new object[] {target.isTrigger, target.radius, target.center.x, target.center.y,target.center.z, target.enabled };
	}
	public override void LoadInto (object[] data, SphereCollider instance)
	{
		instance.isTrigger = (bool)data[0];
		instance.radius = (float)data[1];
		instance.center = new Vector3((float)data[2], (float)data[3], (float)data[4]);
		instance.enabled = (bool)data[5];
		
	}
}


[Serializer(typeof(Texture2D))]
public class SerializeTexture2D : SerializerExtensionBase<Texture2D>
{
	
	public override IEnumerable<object> Save (Texture2D target)
	{
		if (target.GetInstanceID () >= 0) {
			return new object[] { true, SaveGameManager.GetAssetId (target) };
		} else {
			return new object [] { 
				false,
				target.anisoLevel,
				target.filterMode,
				target.format,
				target.height,
				target.mipMapBias,
				target.mipmapCount,
				target.name,
				target.texelSize,
				target.width,
				target.wrapMode,
				target.GetPixels32 ()
			};
		}
	}
	
	public override object Load (object[] data, object instance)
	{
		if ((bool)data [0]) {
			return SaveGameManager.GetAsset ((SaveGameManager.AssetReference)data [1]);
		}
		Texture2D t;
		t = new Texture2D ((int)data [9], (int)data [4], (TextureFormat)data [3], (int)data [6] > 0 ? true : false);
		t.anisoLevel = (int)data [1];
		t.filterMode = (FilterMode)data [2];
		t.mipMapBias = (float)data [5];
		t.name = (string)data [7];
		t.wrapMode = (TextureWrapMode)data [10];
		t.SetPixels32 ((Color32[])data [11]);
		t.Apply ();

		return t;
	}
	
	public override bool CanBeSerialized (Type targetType, object instance)
	{
		var obj = instance as Texture2D;
		if (!obj) {
			return false;
		}
		if (obj.GetInstanceID () < 0 || SaveGameManager.GetAssetId (instance).index != -1) {
			return true;
		}
		return false;
	}
}

[Serializer(typeof(Material))]
public class SerializeMaterial : SerializerExtensionBase<Material>
{
	public override IEnumerable<object> Save (Material target)
	{
		if (target.GetInstanceID () >= 0) {
			return new object[] { true, SaveGameManager.GetAssetId (target) };
		} else {
			return new object [] { false, target.shader.name, target.name,  target.renderQueue, SerializeRenderer.Store.GetValues (target) };
		}
		
	}
	
	public override object Load (object[] data, object instance)
	{
		
		if ((bool)data [0]) {
			return SaveGameManager.GetAsset ((SaveGameManager.AssetReference)data [1]);
		}
		Material m;
		var shdr = Shader.Find ((string)data [1]);
		m = new Material (shdr);
		m.name = (string)data [2];
		m.renderQueue = (int)data [3];
		SerializeRenderer.Store.SetValues (m, (List<StoreMaterials.StoredValue>)data [4]);
		return m;
		
	}
	
	public override bool CanBeSerialized (Type targetType, object instance)
	{
		
		var obj = instance as Material;
		if (!obj)
			return false;
		if ((Shader.Find (obj.shader.name) != null && obj.GetInstanceID () < 0 && SerializeRenderer.Store != null) || SaveGameManager.GetAssetId (instance).index != -1) {
			return true;
		}
		return false;
	}
}

[SubTypeSerializer(typeof(Texture))]
[Serializer(typeof(AudioClip))]
[SubTypeSerializer(typeof(Mesh))]
[Serializer(typeof(AnimationClip))]
public class SerializeAssetReference : SerializerExtensionBase<object>
{
	public override IEnumerable<object> Save (object target)
	{
		return new object [] { SaveGameManager.GetAssetId (target) };
	}
	
	public override bool CanBeSerialized (Type targetType, object instance)
	{
		
		return instance == null || SaveGameManager.GetAssetId (instance).index != -1;
	}
	
	public override object Load (object[] data, object instance)
	{
		return SaveGameManager.GetAsset ((SaveGameManager.AssetReference)data [0]);
	}
}

[SubTypeSerializer(typeof(ScriptableObject))]
public class SerializeScriptableObjectReference : SerializerExtensionBase<object>
{
	public override IEnumerable<object> Save (object target)
	{
		var id = SaveGameManager.GetAssetId(target);
		if(id.index == -1)
		{
			return new object[] { true, target.GetType().AssemblyQualifiedName, UnitySerializer.SerializeForDeserializeInto(target) };
		}
		else
		{
			return new object[] { false, id };
		}
		
	}
	
	public override bool CanBeSerialized (Type targetType, object instance)
	{
		
		return instance != null;
	}
	
	public override object Load (object[] data, object instance)
	{
		if((bool)data[0])
		{
			var newInstance = ScriptableObject.CreateInstance(Type.GetType((string)data[1]));
			UnitySerializer.DeserializeInto((byte[])data[2], newInstance);
			return newInstance;
		}
		else
		{
			return SaveGameManager.GetAsset ((SaveGameManager.AssetReference)data [1]);
		}
		
	}
}



/// <summary>
/// Store a reference to a game object, first checking whether it is really another game
/// object and not a prefab
/// </summary>
[Serializer(typeof(GameObject))]
public class SerializeGameObjectReference : SerializerExtensionBase<GameObject>
{
	static SerializeGameObjectReference ()
	{
		UnitySerializer.CanSerialize += (tp) => {
			return !(
				typeof(Bounds).IsAssignableFrom (tp) ||
				typeof(MeshFilter).IsAssignableFrom (tp) 
				
				);
		};
	}
	
	public override IEnumerable<object> Save (GameObject target)
	{
		return new object[] { target.GetId (), UniqueIdentifier.GetByName (target.gameObject.GetId ()) != null /* Identify a prefab */ };
	}
	
	public override object Load (object[] data, object instance)
	{
		if (instance != null) {
			return instance;
		}
#if US_LOGGING
		if (!((bool)data [1])) {
			Radical.Log ("[[Disabled, will not be set]]");
		}
		Radical.Log ("GameObject: {0}", data [0]);
#endif
		
		return instance ?? new UnitySerializer.DeferredSetter ((d) => {
			return UniqueIdentifier.GetByName ((string)data [0]) ;
		}) { enabled = (bool)data [1]};
	}
	
}

[ComponentSerializerFor(typeof(NavMeshAgent))]
public class SerializeNavMeshAgent : IComponentSerializer
{
	public class StoredInfo
	{
		public bool hasPath, offMesh;
		public float x, y, z, speed, angularSpeed, height, offset, acceleration;
	}
	
	#region IComponentSerializer implementation
	public byte[] Serialize (Component component)
	{
		var agent = (NavMeshAgent)component;
		return UnitySerializer.Serialize (new StoredInfo {
			x=agent.destination.x, 
			y = agent.destination.y, 
			z = agent.destination.z, 
		    speed = agent.speed,
			acceleration = agent.acceleration,
			angularSpeed = agent.angularSpeed,
			height = agent.height,
			offset = agent.baseOffset,
			hasPath = agent.hasPath,
			offMesh = agent.isOnOffMeshLink
		
		});
	}

	public void Deserialize (byte[] data, Component instance)
	{
		Loom.QueueOnMainThread (() => {
			var agent = (NavMeshAgent)instance;
			var si = UnitySerializer.Deserialize<StoredInfo> (data);
			var path = new NavMeshPath ();
			agent.speed = si.speed;
			agent.angularSpeed = si.angularSpeed;
			agent.height = si.height;
			agent.baseOffset = si.offset;
			if (si.hasPath && !agent.isOnOffMeshLink) {
				agent.CalculatePath (new Vector3 (si.x, si.y, si.z), path);
				agent.SetPath (path);
			}
		}, 0.1f);
	
		
	}
	#endregion
	
}

[ComponentSerializerFor(typeof(Camera))]
public class SerializeCamera : IComponentSerializer
{
	public class CameraData
	{
		public float fieldOfView;
		public float nearClipPlane;
		public float farClipPlane;
		public float depth;
		
	}
	#region IComponentSerializer implementation
	public byte[] Serialize (Component component)
	{
		var camera = (Camera)component;
		var cd = new CameraData { fieldOfView = camera.fieldOfView, depth = camera.depth, nearClipPlane = camera.nearClipPlane, farClipPlane = camera.farClipPlane };
		return UnitySerializer.Serialize(cd);
	}

	public void Deserialize (byte[] data, Component instance)
	{
		var cd = UnitySerializer.Deserialize<CameraData>(data);
		var camera = (Camera) instance;
		camera.fieldOfView = cd.fieldOfView;
		camera.nearClipPlane = cd.nearClipPlane;
		camera.farClipPlane = cd.farClipPlane;
		camera.depth = cd.depth;
	}
	#endregion
}

[ComponentSerializerFor(typeof(Rigidbody))]
public class SerializeRigidBody : IComponentSerializer
{
	public class RigidBodyInfo
	{
		public bool isKinematic;
		public bool useGravity, freezeRotation, detectCollisions, useConeFriction;
		public Vector3 velocity, position, angularVelocity, centerOfMass, inertiaTensor;
		public Quaternion rotation, inertiaTensorRotation;
		public float drag, angularDrag, mass, sleepVelocity, sleepAngularVelocity, maxAngularVelocity;
		public RigidbodyConstraints constraints;
		public CollisionDetectionMode collisionDetectionMode;
		public RigidbodyInterpolation interpolation;
		public int solverIterationCount;
		public RigidBodyInfo()
		{
		}
		public RigidBodyInfo(Rigidbody source)
		{
			isKinematic = source.isKinematic;
			useGravity = source.useGravity;
			freezeRotation = source.freezeRotation;
			detectCollisions = source.detectCollisions;
			useConeFriction = source.useConeFriction;
			velocity = source.velocity;
			position = source.position;
			rotation = source.rotation;
			angularVelocity = source.angularVelocity;
			centerOfMass = source.centerOfMass;
			inertiaTensor = source.inertiaTensor;
			inertiaTensorRotation = source.inertiaTensorRotation;
			drag = source.drag;
			angularDrag = source.angularDrag;
			mass = source.mass;
			sleepVelocity = source.sleepVelocity;
			sleepAngularVelocity = source.sleepAngularVelocity;
			maxAngularVelocity = source.maxAngularVelocity;
			constraints = source.constraints;
			collisionDetectionMode = source.collisionDetectionMode;
			interpolation = source.interpolation;
			solverIterationCount = source.solverIterationCount;
		}
		
		public void Configure(Rigidbody body)
		{
			body.isKinematic = true;
			body.freezeRotation = freezeRotation;
			body.useGravity  = useGravity;
			body.detectCollisions = detectCollisions;
			body.useConeFriction = useConeFriction;
			if(centerOfMass != Vector3.zero)
				body.centerOfMass = centerOfMass;
			body.drag = drag;
			body.angularDrag = angularDrag;
			body.mass = mass;
			body.rotation = rotation;
			body.sleepVelocity = sleepVelocity;
			body.sleepAngularVelocity = sleepAngularVelocity;
			body.maxAngularVelocity= maxAngularVelocity;
			body.constraints = constraints;
			body.collisionDetectionMode = collisionDetectionMode;
			body.interpolation = interpolation;
			body.solverIterationCount = solverIterationCount;
			body.isKinematic = isKinematic;
			if(!isKinematic)
			{
				body.velocity = velocity;
				body.useGravity  = useGravity;
				body.angularVelocity = angularVelocity;
				if(inertiaTensor != Vector3.zero)
					body.inertiaTensor = inertiaTensor;
				if(inertiaTensorRotation != zero)
					body.inertiaTensorRotation = inertiaTensorRotation;
			}
			
		}
	}
					
	static Quaternion zero = new Quaternion(0,0,0,0);
	
	#region IComponentSerializer implementation
	public byte[] Serialize (Component component)
	{
		return UnitySerializer.Serialize( new RigidBodyInfo((Rigidbody)component));
		
	}

	public void Deserialize (byte[] data, Component instance)
	{
		var info = UnitySerializer.Deserialize<RigidBodyInfo>(data);
		info.Configure((Rigidbody)instance);
		UnitySerializer.AddFinalAction(()=>{
			info.Configure((Rigidbody)instance);
		});
	}
    #endregion
	
}

[SerializerPlugIn]
public class RagePixelSupport
{
	static RagePixelSupport()
	{
		new SerializePrivateFieldOfType("RagePixelSprite", "animationPingPongDirection");
		new SerializePrivateFieldOfType("RagePixelSprite", "myTime");
	}
}

[ComponentSerializerFor(typeof(Renderer))]
[ComponentSerializerFor(typeof(ClothRenderer))]
[ComponentSerializerFor(typeof(LineRenderer))]
[ComponentSerializerFor(typeof(TrailRenderer))]
[ComponentSerializerFor(typeof(ParticleRenderer))]
[ComponentSerializerFor(typeof(SkinnedMeshRenderer))]
[ComponentSerializerFor(typeof(MeshRenderer))]
public class SerializeRenderer : IComponentSerializer
{
	public static StoreMaterials Store;
	
	public class StoredInformation
	{
		public bool Enabled;
		public List<Material> materials = new List<Material> ();
	}
	
	
	
	#region IComponentSerializer implementation
	public byte[] Serialize (Component component)
	{
		using (new UnitySerializer.SerializationSplitScope()) {
			var renderer = (Renderer)component;
			var si = new StoredInformation ();
			si.Enabled = renderer.enabled;
			if ((Store = renderer.GetComponent<StoreMaterials> ()) != null) {
				
				si.materials = renderer.materials.ToList ();
			}
			var data = UnitySerializer.Serialize (si);
			Store = null;
			return data;
			
		}
	}

	public void Deserialize (byte[] data, Component instance)
	{
		var renderer = (Renderer)instance;
		renderer.enabled = false;
		UnitySerializer.AddFinalAction (() => {
			Store = renderer.GetComponent<StoreMaterials> ();
			
			using (new UnitySerializer.SerializationSplitScope()) {
				var si = UnitySerializer.Deserialize<StoredInformation> (data);
				if (si == null) {
					return;
				}
				renderer.enabled = si.Enabled;
				if (si.materials.Count > 0) {
					if (Store != null) {
						renderer.materials = si.materials.ToArray ();
					}
			
			
				}
			
			}
			Store = null;
		}
		);
	}
	#endregion
	
}



[SubTypeSerializer(typeof(Component))]
public class SerializeComponentReference: SerializerExtensionBase<Component>
{ 
	

	public override IEnumerable<object> Save (Component target)
	{
		return new object[] { target.gameObject.GetId (), UniqueIdentifier.GetByName (target.gameObject.GetId ()) != null, target.GetType ().AssemblyQualifiedName,"" /* Identify a prefab */ };
	}
	
	public override object Load (object[] data, object instance)
	{
#if US_LOGGING
		if (!((bool)data [1])) {
			Radical.Log ("[[Disabled, will not be set]]");
		}
		Radical.Log ("Component: {0}.{1}", data [0], data [2]);
#endif
		return new UnitySerializer.DeferredSetter ((d) => {
			var item = UniqueIdentifier.GetByName ((string)data [0]);
			return item != null ? item.GetComponent (Type.GetType ((string)data [2])) : null;
		}) { enabled = (bool)data [1]};
	}
}

public class ProvideAttributes : IProvideAttributeList
{
	private string[] _attributes;
	protected bool AllSimple = true;
	
	public ProvideAttributes (string[] attributes)
		: this( attributes, true)
	{
	}

	public ProvideAttributes (string[] attributes, bool allSimple)
	{
		_attributes = attributes;
		AllSimple = allSimple;
	}

	#region IProvideAttributeList implementation
	public IEnumerable<string> GetAttributeList (Type tp)
	{
		return _attributes;
	}
	#endregion

	#region IProvideAttributeList implementation
	public virtual bool AllowAllSimple (Type tp)
	{
		return AllSimple;
	}
	#endregion 
}

[AttributeListProvider(typeof(Camera))]
public class ProvideCameraAttributes : ProvideAttributes
{
	public ProvideCameraAttributes () : base(new string[0])
	{
	}
}

[AttributeListProvider(typeof(Transform))]
public class ProviderTransformAttributes : ProvideAttributes
{
	public ProviderTransformAttributes () : base(new string[] {
		"localPosition",
		"localRotation",
		"localScale"
	}, false)
	{
	}
}


[AttributeListProvider(typeof(Renderer))]
[AttributeListProvider(typeof(Collider))]
[AttributeListProvider(typeof(AudioListener))]
[AttributeListProvider(typeof(ParticleEmitter))]
[AttributeListProvider(typeof(Cloth))]
[AttributeListProvider(typeof(Light))]
[AttributeListProvider(typeof(Joint))]
[AttributeListProvider(typeof(MeshFilter))]
[AttributeListProvider(typeof(TextMesh))]
public class ProviderRendererAttributes : ProvideAttributes
{
	public ProviderRendererAttributes () : base(new string[] {
		"active",
		"text",
		"anchor",
		"alignment",
		"lineSpacing",
		"spring",
		"useSpring",
		"motor",
		"useMotor",
		"limits",
		"useLimits",
		"axis",
		"breakForce",
		"breakTorque",
		"offsetZ",
		"playAutomatically",
		"animatePhysics",
		"tabSize",
		"enabled",
		"isTrigger",
		"emit",
		"minSize",
		"maxSize",
		"minEnergy",
		"maxEnergy",
		"minEmission",
		"maxEmission",
		"rndRotation",
		"rndVelocity",
		"rndAngularVelocity",
		"angularVelocity",
		"emitterVelocityScale",
		"localVelocity",
		"worldVelocity",
		"useWorldVelocity"
			
		
	}, false)
	{
		
	}
}

