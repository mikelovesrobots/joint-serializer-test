using UnityEngine;
using System.Collections;

public class CreateChildSphere : MonoBehaviour {
	
	public Transform prefab;
	
	// Use this for initialization
	void Start () {
		if(!LevelSerializer.IsDeserializing)
		{
			if(Random.value < 0.4)
			{
				var sphere = Instantiate(prefab, transform.position + Random.onUnitSphere * 3, Quaternion.identity) as Transform;
				sphere.parent = transform;
				
			}
		}
	}
	
	
}
