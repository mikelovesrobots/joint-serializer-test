#pragma strict

var prefab : GameObject;
var target : Transform;

public var testArrayItems = new String[10];

function Start () {
	testArrayItems[2] = "Hello";
}

function Update () {
	if(LevelSerializer.IsDeserializing)
	   return;
	 if(Time.timeScale == 0)
	    return;
	if(Random.Range(0,100) < 2) {
		
		var direction = target.transform.forward * ((Random.value * 8) + 2);
		direction = direction + target.transform.up * 8;
		direction = direction + ( target.transform.right * ( - 4 + ((Random.value * 8))));
		if(prefab != null)
			Instantiate(prefab, direction, Quaternion.identity);
		
	}
}