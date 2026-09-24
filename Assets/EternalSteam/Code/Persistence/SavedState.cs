using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace EternalSteam
{
    // Explicit opt-in scalar state. Reflection is used only at save/load boundaries, never per frame.
    [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
    public sealed class SavedAttribute:Attribute
    {
        public readonly double Minimum,Maximum;
        public SavedAttribute(double minimum=double.MinValue,double maximum=double.MaxValue){Minimum=minimum;Maximum=maximum;}
    }
    public interface IPendingExecution {bool HasPendingExecution {get;}}
    [Serializable] public sealed class SavedValue {public string name,text;public Vector3 vector;public int[] integers;public long[] longs;}
    [Serializable] public sealed class SavedState
    {
        public string type;public List<SavedValue> values=new();
        static IEnumerable<MemberInfo> Members(object target)=>target.GetType().GetMembers(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Where(m=>m.GetCustomAttribute<SavedAttribute>()!=null).OrderBy(m=>m.Name,StringComparer.Ordinal);
        static Type Kind(MemberInfo m)=>m is FieldInfo f?f.FieldType:((PropertyInfo)m).PropertyType;
        static object Read(MemberInfo m,object target)=>m is FieldInfo f?f.GetValue(target):((PropertyInfo)m).GetValue(target);
        public static SavedState Capture(object target)
        {
            var result=new SavedState{type=target.GetType().FullName};
            foreach(var member in Members(target)){
                var v=Read(member,target);var item=new SavedValue{name=member.Name};
                if(v is Vector3 vector)item.vector=vector;
                else if(v is int[] ints)item.integers=(int[])ints.Clone();
                else if(v is long[] longs)item.longs=(long[])longs.Clone();
                else if(v is List<int> list)item.integers=list.ToArray();
                else if(v is float f)item.text=f.ToString("R",CultureInfo.InvariantCulture);
                else if(v is double d)item.text=d.ToString("R",CultureInfo.InvariantCulture);
                else item.text=Convert.ToString(v,CultureInfo.InvariantCulture);
                result.values.Add(item);
            }return result;
        }
        object Parse(MemberInfo member,SavedValue value)
        {
            var t=Kind(member);var limits=member.GetCustomAttribute<SavedAttribute>();
            if(t==typeof(Vector3)){if(!float.IsFinite(value.vector.x)||!float.IsFinite(value.vector.y)||!float.IsFinite(value.vector.z))throw new InvalidOperationException("Invalid saved vector");return value.vector;}
            if(t==typeof(int[]))return value.integers!=null?(int[])value.integers.Clone():throw new InvalidOperationException("Missing integers");
            if(t==typeof(long[]))return value.longs!=null?(long[])value.longs.Clone():throw new InvalidOperationException("Missing longs");
            if(t==typeof(List<int>))return new List<int>(value.integers??throw new InvalidOperationException("Missing list"));
            if(t==typeof(string))return value.text;
            if(t.IsEnum){var parsed=Enum.Parse(t,value.text);if(!Enum.IsDefined(t,parsed))throw new InvalidOperationException("Invalid saved enum");return parsed;}
            var number=Convert.ChangeType(value.text,t,CultureInfo.InvariantCulture);
            if(t!=typeof(bool)){double d=Convert.ToDouble(number,CultureInfo.InvariantCulture);if(!double.IsFinite(d)||d<limits.Minimum||d>limits.Maximum)throw new InvalidOperationException("Invalid saved number: "+member.Name);}
            return number;
        }
        public void Validate(object target)
        {
            if(target==null||type!=target.GetType().FullName||values==null)throw new InvalidOperationException("Saved module mismatch");
            var members=Members(target).ToArray();if(values.Count!=members.Length||values.Any(v=>v==null)||values.Select(v=>v.name).Distinct().Count()!=values.Count)throw new InvalidOperationException("Saved fields mismatch");
            foreach(var member in members)Parse(member,values.Single(v=>v.name==member.Name));
        }
        public void Restore(object target)
        {
            Validate(target);
            foreach(var member in Members(target)){var value=Parse(member,values.Single(v=>v.name==member.Name));if(member is FieldInfo f)f.SetValue(target,value);else ((PropertyInfo)member).SetValue(target,value);}
        }
    }
}
