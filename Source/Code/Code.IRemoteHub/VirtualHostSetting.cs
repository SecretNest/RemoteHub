using System;
using System.Collections.Generic;
using System.Text;

namespace SecretNest.RemoteHub
{
    /// <summary>
    /// Represents a setting of a virtual host.
    /// </summary>
    public class VirtualHostSetting
    {
        /// <summary>
        /// Gets the priority of this host in this virtual host.
        /// </summary>
        public int Priority { get; }
        /// <summary>
        /// Gets the weight of this host in this virtual host.
        /// </summary>
        public int Weight { get; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Weight;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var target = obj as VirtualHostSetting;
            if (target == null) return false;

            return target.Priority == Priority && target.Weight == Weight;
        }

        /// <summary>
        /// Initializes an instance of VirtualHostSetting
        /// </summary>
        /// <param name="priority">Priority of this host in this virtual host.</param>
        /// <param name="weight">Weight of this host in this virtual host.</param>
        public VirtualHostSetting(int priority, int weight)
        {
            Priority = priority;
            Weight = weight;
        }
    }
}
