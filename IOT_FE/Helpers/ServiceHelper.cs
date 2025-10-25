using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IOT_FE.Helpers
{
    public static class ServiceHelper
    {
        public static IServiceProvider? Provider { get; set; }

        public static T? GetService<T>()
        {
            System.Diagnostics.Debug.WriteLine($" GetService called for type: {typeof(T).Name}");

            if (Provider == null)
            {
                System.Diagnostics.Debug.WriteLine(" Provider is null");
                return default(T);
            }

            try
            {
                var service = Provider.GetRequiredService<T>();
                System.Diagnostics.Debug.WriteLine($" Service found: {typeof(T).Name}");
                return service;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($" Error getting service {typeof(T).Name}: {ex.Message}");
                return default(T);
            }
        }
    }
}
