using System;
using System.Collections.Generic;
namespace EternalSteam
{
    public sealed class ResourceBank : IResourceBank
    {
        readonly Dictionary<string,double> amounts=new(),capacities=new();
        public double Amount(string id) => amounts.TryGetValue(id,out var value)?value:0;
        public double Capacity(string id) => capacities.TryGetValue(id,out var value)?value:0;
        public void AddCapacity(string id,double value)
        {
            if(string.IsNullOrWhiteSpace(id) || !double.IsFinite(value)) throw new ArgumentException("Invalid resource capacity.");
            capacities[id]=Math.Max(0,Capacity(id)+value);
            // Removing storage preserves stock; no further production until below capacity.
        }
        public bool Exchange(string input,double cost,string output,double gain)
        {
            if(string.IsNullOrWhiteSpace(output) || !double.IsFinite(cost) || cost<0 || !double.IsFinite(gain) || gain<=0) return false;
            if(cost>0 && (string.IsNullOrWhiteSpace(input) || Amount(input)<cost)) return false;
            double after=Amount(output)+gain-(input==output?cost:0);
            if(after>Capacity(output)) return false;
            if(cost>0) amounts[input]=Amount(input)-cost;
            amounts[output]=Amount(output)+gain; return true;
        }
    }
}
