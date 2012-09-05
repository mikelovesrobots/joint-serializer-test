#pragma strict

public var prefab : GameObject;

function Start () {
  var first : GameObject = Instantiate(prefab, new Vector3(0,1,0), Quaternion.identity);
  var second : GameObject = Instantiate(prefab, new Vector3(3,3,3), Quaternion.identity);

  var joint : Joint = first.AddComponent(FixedJoint);
  joint.connectedBody = second.rigidbody;
  joint.breakForce = 250;
  joint.breakTorque = 250;

  //var joint : Joint = first.AddComponent(ConfigurableJoint);
  // joint.axis = Vector3.zero;
  // joint.secondaryAxis = Vector3.zero;
  // joint.xMotion = ConfigurableJointMotion.Locked;
  // joint.yMotion = ConfigurableJointMotion.Locked;
  // joint.zMotion = ConfigurableJointMotion.Locked;
  // joint.angularXMotion = ConfigurableJointMotion.Locked;
  // joint.angularYMotion = ConfigurableJointMotion.Locked;
  // joint.angularZMotion = ConfigurableJointMotion.Locked;
}

