using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TexturePacker.Net
{
    public class ControlsPool
    {
        private readonly Dictionary<Type, List<object>> cache;

        private readonly Dispatcher dispatcher;
        public ControlsPool(Window owner)
        {
            dispatcher = owner.Dispatcher;
            cache = new Dictionary<Type, List<object>>();
        }

        public object GetNewControl(Type type)
        {
            lock (this)
            {
                if (cache.TryGetValue(type, out List<object> cachedItems))
                {
                    if (cachedItems.Count > 0)
                    {
                        object result = cachedItems[^1];
                        cachedItems.RemoveAt(cachedItems.Count - 1);
                    }
                }
            }
            return null;
        }
    }
}
