using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IOT_FE.Model
{
    public class DeviceStatusModel
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool IsAppOpen { get; set; }
        public DateTime LastUpdated { get; set; }
        public string ConnectionId { get; set; } = string.Empty;
        public string FcmToken { get; set; } = string.Empty;
    }

    public class DeviceStatusUpdateRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool IsAppOpen { get; set; }
    }
}
