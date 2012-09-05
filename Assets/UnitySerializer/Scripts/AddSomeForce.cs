using UnityEngine;
using System.Collections;

public class AddSomeForce : MonoBehaviour {
	
	bool done;
	// Use this for initialization
	void FixedUpdate () {
		if(done) 
			return;
		done = true;
		rigidbody.angularVelocity = Random.insideUnitSphere * 10;
		
	}
	

}
