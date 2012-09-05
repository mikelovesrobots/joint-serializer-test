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
using Serialization;
using System.Collections.Generic;
using System.Reflection;

public class CoroutineReturn
{
	public virtual bool finished { get; set; }

	public virtual bool cancel { get; set; }
}

[SerializeAll]
public class WaitForAnimation : CoroutineReturn
{
	private GameObject _go;
	private string _name;
	private float _time;
	private float _weight;
	[DoNotSerialize]
	private int startFrame;

	public string name
	{
		get
		{
			return _name;
		}
	}
	
	public WaitForAnimation()
	{
	}
	
	public WaitForAnimation(GameObject go, string name)
		: this( go, name, 1f, -1)
	{
	}
	public WaitForAnimation(GameObject go, string name, float time)
		: this( go, name, time, -1)
	{
	}
	public WaitForAnimation(GameObject go, string name, float time, float weight)
	{
		startFrame = Time.frameCount;
		_go = go;
		_name = name;
		_time = Mathf.Clamp01(time);
		_weight = weight;
	}
	
	[DoNotSerialize]
	public override bool finished
	{
		get
		{
			if (LevelSerializer.IsDeserializing)
			{
				return false;
			}
			if (Time.frameCount <= startFrame + 4)
			{
				return false;
			}
			
			var anim = _go.animation[_name];
		
			bool ret = true;
				
			if (anim.enabled)
			{
				
				if (_weight == -1)
				{
					ret = anim.normalizedTime >= _time;
				
				}
				else
				{
					if (_weight < 0.5)
					{
						ret = anim.weight <= Mathf.Clamp01(_weight + 0.001f);
					}
					ret = anim.weight >= Mathf.Clamp01(_weight - 0.001f);
				}
			
			}
			if(!_go.animation.IsPlaying(_name))
			{
				ret = true;
			}
			if(ret)
			{
				if(anim.weight == 0 || anim.normalizedTime == 1)
				{
					anim.enabled = false;
				}
			}
			return ret;
				
		}
		set
		{
			base.finished = value;
		}
	}
	
}

public static class TaskHelpers
{
	public static WaitForAnimation WaitForAnimation(this GameObject go, string name)
	{
		return WaitForAnimation(go, name, 1f);
	}
	public static WaitForAnimation WaitForAnimation(this GameObject go, string name, float time)
	{
		return new WaitForAnimation(go, name, time, -1);
	}

	public static WaitForAnimation WaitForAnimationWeight(this GameObject go, string name)
	{
		return WaitForAnimationWeight(go, name, 0f);
	}
	public static WaitForAnimation WaitForAnimationWeight(this GameObject go, string name, float weight)
	{
		return new WaitForAnimation(go, name, 0, weight);
	}
}

public interface IYieldInstruction
{
	YieldInstruction Instruction { get; }
}

public class RadicalWaitForSeconds : IYieldInstruction
{

	private float _time;
	private float _seconds;
	
	public RadicalWaitForSeconds()
	{
	}
	
	public float TimeRemaining
	{
		get
		{
			return Mathf.Clamp((_time + _seconds) - Time.time, 0, 10000000);
		}
		set
		{
			_time = Time.time;
			_seconds = value;
		}
	}
	
	public RadicalWaitForSeconds(float seconds)
	{
		_time = Time.time;
		_seconds = seconds;
	}
	
	#region IYieldInstruction implementation
	public YieldInstruction Instruction
	{
		get
		{
			return new WaitForSeconds(TimeRemaining);
		}
	}
	#endregion
}

public interface INotifyStartStop
{
	void Stop();

	void Start();
}

public class RadicalRoutine : IDeserialized
{
	
	private bool cancel;
	private IEnumerator extended;
	public IEnumerator enumerator;
	public object Notify;
	public string Method;
	public bool finished;
	public object Target;
	bool isTracking;
	MonoBehaviour _trackedObject;
	public MonoBehaviour trackedObject
	{
		get
		{
			return _trackedObject;
		}
		set
		{
			_trackedObject = value;
			isTracking = _trackedObject != null;
		}
	}

	public event Action Cancelled = delegate {};
	public event Action Finished = delegate {};
	
	public void Cancel()
	{
		cancel = true;
		if (extended is INotifyStartStop)
		{
			(extended as INotifyStartStop).Stop();
		}
	}
	
	public static RadicalRoutine Run(IEnumerator extendedCoRoutine)
	{
		return Run ( extendedCoRoutine, "", null);
	}
	public static RadicalRoutine Run(IEnumerator extendedCoRoutine, string methodName)
	{
		return Run ( extendedCoRoutine, methodName, null);
	}
	public static RadicalRoutine Run(IEnumerator extendedCoRoutine, string methodName, object target)
	{
		var rr = new RadicalRoutine();
		rr.Method = methodName;
		rr.Target = target;
		rr.extended = extendedCoRoutine;
		if (rr.extended is INotifyStartStop)
		{
			(rr.extended as INotifyStartStop).Start();
		}
		rr.enumerator = rr.Execute(extendedCoRoutine);
		RadicalRoutineHelper.Current.Run(rr);
		return rr;
		
	}
	
	public static RadicalRoutine Create(IEnumerator extendedCoRoutine)
	{
		var rr = new RadicalRoutine();
		rr.extended = extendedCoRoutine;
		if (rr.extended is INotifyStartStop)
		{
			(rr.extended as INotifyStartStop).Start();
		}
		rr.enumerator = rr.Execute(extendedCoRoutine);
		return rr;
	}
	
	public void Run()
	{
		Run ( "", null);
	}
	public void Run(string methodName)
	{
		Run ( methodName, null);
	}
	public void Run(string methodName, object target)
	{
		Method = methodName;
		Target = target;
		RadicalRoutineHelper.Current.Run(this);
	}
	
	private IEnumerator Execute(IEnumerator extendedCoRoutine)
	{
		return Execute( extendedCoRoutine, null);
	}
	private IEnumerator Execute(IEnumerator extendedCoRoutine, Action complete)
	{
		var stack = new Stack<IEnumerator>();
		stack.Push(extendedCoRoutine);
		while(stack.Count > 0)
		{
			extendedCoRoutine = stack.Pop();
			while (!cancel && extendedCoRoutine != null && (!isTracking || (trackedObject != null && trackedObject.enabled) ) && (!LevelSerializer.IsDeserializing ? extendedCoRoutine.MoveNext() : true))
			{
				var v = extendedCoRoutine.Current;
				var cr = v as CoroutineReturn;
				if (cr != null)
				{
					if (cr.cancel)
					{
						cancel = true;
						break;
					}
					while (!cr.finished)
					{
						if (cr.cancel)
						{
							cancel = true;
							break;
						}
						yield return null;
					}
					if (cancel)
						break;
				}
				else
				if (v is IYieldInstruction)
				{
					yield return (v as IYieldInstruction).Instruction;
				} if(v is IEnumerator)
				{
					stack.Push(extendedCoRoutine);
					extendedCoRoutine = v as IEnumerator;
				} else if(v is RadicalRoutine)
				{
					var rr = v as RadicalRoutine;
					while(!rr.finished)
						yield return null;
				}
				else
				{
					yield return v;
				}
				
				
			}
		}
		finished = true;
		Cancel();
	
		if (cancel)
		{
			Cancelled();
		}
	
		Finished();
		if (complete != null)
			complete();
		
		
		
	}
	
	
	#region IDeserialized implementation
	public void Deserialized()
	{
		
	}
	#endregion
}

public static class RadicalRoutineExtensions
{
	public class RadicalRoutineBehaviour : MonoBehaviour
	{
	}
	
	public static RadicalRoutine StartExtendedCoroutine(this MonoBehaviour behaviour, IEnumerator coRoutine)
	{
		var routine = RadicalRoutine.Create(coRoutine);
		routine.trackedObject = behaviour;
		routine.Run();
		return routine;
		
	}
	
	public static RadicalRoutine StartExtendedCoroutine(this GameObject go, IEnumerator coRoutine)
	{
		var behaviour = go.GetComponent<MonoBehaviour>() ?? go.AddComponent<RadicalRoutineBehaviour>();
		return behaviour.StartExtendedCoroutine(coRoutine);
	}
	
	public static RadicalRoutine StartExtendedCoroutine(this Component co, IEnumerator coRoutine)
	{
		return co.gameObject.StartExtendedCoroutine(coRoutine);
	}
}
