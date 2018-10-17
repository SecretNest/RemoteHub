using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    public class VirtualHostSetting
    {
        public int Priority { get; }
        public int Weight { get; }

        public override int GetHashCode()
        {
            return Weight;
        }

        public override bool Equals(object obj)
        {
            var target = obj as VirtualHostSetting;
            if (target == null) return false;

            return target.Priority == Priority && target.Weight == Weight;
        }

        public VirtualHostSetting(int priority, int weight)
        {
            Priority = priority;
            Weight = weight;
        }
    }
}
