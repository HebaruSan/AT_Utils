//  Author:
//       Allis Tauri <allista@gmail.com>
//
//  Copyright (c) 2015 Allis Tauri

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AT_Utils
{
	public static partial class Utils
	{
		public static string ModName = "MyMod";

		/// <summary>
		/// The camel case components matching regexp.
		/// From: http://stackoverflow.com/questions/155303/net-how-can-you-split-a-caps-delimited-string-into-an-array
		/// </summary>
		const string CamelCaseRegexp = "([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))";
		static Regex CCR = new Regex(CamelCaseRegexp);
		public static string ParseCamelCase(string s) { return CCR.Replace(s, "$1 "); }

		public static string FormatVeryBigValue(float value, string unit, string format = "F1")
		{
			string mod = "";
			if(value > 1e24) { value /= 1e24f; mod = "Y"; }
			else if(value > 1e21) { value /= 1e21f; mod = "Z"; }
			else if(value > 1e18) { value /= 1e18f; mod = "E"; }
			else if(value > 1e15) { value /= 1e15f; mod = "P"; }
			else if(value > 1e12) { value /= 1e12f; mod = "T"; }
			else return FormatBigValue(value, unit, format);
			return value.ToString(format)+mod+unit;
		}

		public static string FormatBigValue(float value, string unit, string format = "F1")
		{
			string mod = "";
			if     (value > 1e9) { value /= 1e9f; mod = "G"; }
			else if(value > 1e6) { value /= 1e6f; mod = "M"; }
			else if(value > 1e3) { value /= 1e3f; mod = "k"; }
			return value.ToString(format)+mod+unit;
		}

		public static string FormatSmallValue(float value, string unit, string format = "F1")
		{
			string mod = "";
			if(value > 1e-3) { value *= 1e3f; mod = "m"; }
			else if(value > 1e-6) { value *= 1e6f; mod = "μ"; }
			else if(value > 1e-9) { value *= 1e9f; mod = "n"; }
			return value.ToString(format)+mod+unit;
		}

//		public static string FormatTimeDelta(double value)
//		{
//			var h = 0;
//			if(value > 3600) h
//		}

		public static string Format(string s, params object[] args)
		{
			if(args == null || args.Length == 0) return s;
			convert_args(args);
			for(int i = 0, argsLength = args.Length; i < argsLength; i++)
			{
				var ind = s.IndexOf("{}"); 
				if(ind >= 0) s = s.Substring(0, ind)+"{"+i+"}"+s.Substring(ind+2);
				else s += string.Format(" arg{0}: {{{0}}}", i);
			}
			return string.Format(s.Replace("{}", "[no arg]"), args);
		}

		public static string formatVector(Vector3 v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static string formatVector(Vector3d v)
		{ return string.Format("({0}, {1}, {2}); |v| = {3}", v.x, v.y, v.z, v.magnitude); }

		public static string formatOrbit(Orbit o)
		{
			return Utils.Format(
				"Body:   {}\n" +
				"\trotation: {}s\n" +
				"\tradius:   {}\n" +
				"PeA:    {} m\n" +
				"ApA:    {} m\n" +
				"PeR:    {} m\n" +
				"ApR:    {} m\n" +
				"Ecc:    {}\n" +
				"Inc:    {} deg\n" +
				"LAN:    {} deg\n" +
				"MA:     {} rad\n" +
				"TA:     {} deg\n" +
				"AoP:    {} deg\n" +
				"Period: {} s\n" +
				"epoch:   {}\n" +
				"T@epoch: {} s\n" +
				"T:       {} s\n" +
				"Vel: {} m/s\n" +
				"Pos: {} m\n",
				o.referenceBody.bodyName, o.referenceBody.rotationPeriod,
				FormatBigValue((float)o.referenceBody.Radius, "m"),
				o.PeA, o.ApA,
				o.PeR, o.ApR, 
				o.eccentricity, o.inclination, o.LAN, o.meanAnomaly, o.trueAnomaly, o.argumentOfPeriapsis,
				o.period, o.epoch, o.ObTAtEpoch, o.ObT,
				formatVector(o.vel), formatVector(o.pos));
		}

		public static string formatBounds(Bounds b, string name="")
		{
			return string.Format("Bounds:  {0}\n" +
			                     "Center:  {1}\n" +
			                     "Extents: {2}\n" +
			                     "Min:     {3}\n" +
			                     "Max:     {4}\n" +
			                     "Volume:  {5}", 
			                     name, b.center, b.extents, b.min, b.max,
			                     b.size.x*b.size.y*b.size.z);
		}

		static void convert_args(object[] args)
		{
			for(int i = 0, argsL = args.Length; i < argsL; i++) 
			{
				var arg = args[i];
				if(arg is Vector3) args[i] = formatVector((Vector3)arg);
				else if(arg is Vector3d) args[i] = formatVector((Vector3d)arg);
				else if(arg is Orbit) args[i] = formatOrbit((Orbit)arg);
				else if(arg is Bounds) args[i] = formatBounds((Bounds)arg);
				else if(arg == null) args[i] = "null";
				else args[i] = arg.ToString();
			}
		}

		public static void Log(string msg, params object[] args)
		{ 
			
			msg = string.Format("[{0}: {1:HH:mm:ss.fff}] {2}", ModName, DateTime.Now, msg);
			if(args.Length > 0)
			{
				convert_args(args);
				Debug.Log(string.Format(msg, args)); 
			}
			else Debug.Log(msg);
		}

		public static void Log(this MonoBehaviour mb, string msg, params object[] args)
		{ Utils.Log(string.Format("{0}: {1}", mb.name, msg), args); }

		public static void Log(this Vessel v, string msg, params object[] args)
		{ Utils.Log(string.Format("{0}: {1}", v.vesselName, msg), args); }

		public static void Log(this PartModule pm, string msg, params object[] args)
		{ 
			var vn = pm.vessel == null? "_vessel" : (string.IsNullOrEmpty(pm.vessel.vesselName)? pm.vessel.id.ToString() : pm.vessel.vesselName);
			Utils.Log(string.Format("{0}:{1}:{2}: {3}", vn, pm.part == null? "_part" : pm.part.Title(), pm.moduleName, msg), args); 
		}

		public static void LogF(string msg, params object[] args) { Log(Format(msg, args)); }
		#endregion

		//from http://stackoverflow.com/questions/716399/c-sharp-how-do-you-get-a-variables-name-as-it-was-physically-typed-in-its-dec
		//second answer
		public static string PropertyName<T>(T obj) { return typeof(T).GetProperties()[0].Name; }

		//ResearchAndDevelopment.PartModelPurchased is broken and always returns 'true'
		public static bool PartIsPurchased(string name)
		{
			var info = PartLoader.getPartInfoByName(name);
			if(info == null || HighLogic.CurrentGame == null) return false;
			if(HighLogic.CurrentGame.Mode != Game.Modes.CAREER) return true;
			var tech = ResearchAndDevelopment.Instance.GetTechState(info.TechRequired);
			return tech != null && tech.state == RDTech.State.Available && tech.partsPurchased.Contains(info);
		}

		public static Vector3[] BoundCorners(Bounds b)
		{
			var edges = new Vector3[8];
			Vector3 min = b.min;
			Vector3 max = b.max;
			edges[0] = new Vector3(min.x, min.y, min.z); //left-bottom-back
			edges[1] = new Vector3(min.x, min.y, max.z); //left-bottom-front
			edges[2] = new Vector3(min.x, max.y, min.z); //left-top-back
			edges[3] = new Vector3(min.x, max.y, max.z); //left-top-front
			edges[4] = new Vector3(max.x, min.y, min.z); //right-bottom-back
			edges[5] = new Vector3(max.x, min.y, max.z); //right-bottom-front
			edges[6] = new Vector3(max.x, max.y, min.z); //right-top-back
			edges[7] = new Vector3(max.x, max.y, max.z); //right-top-front
			return edges;
		}
	}

	public class ListDict<K,V> : Dictionary<K, List<V>>
	{
		public void Add(K key, V value)
		{
			List<V> lst;
			if(TryGetValue(key, out lst))
				lst.Add(value);
			else this[key] = new List<V>{value};
		}
	}
}
