﻿using IOT_BE.Models;

namespace IOT_BE.Services
{
    public interface IDeviceStatusService
    {
        Task UpdateDeviceStatusAsync(string deviceId, string connectionId, bool isAppOpen);
        Task<bool> IsDeviceAppOpenAsync(string deviceId);
        Task<List<string>> GetOpenDevicesAsync();
        Task<List<string>> GetClosedDevicesAsync();
        Task RemoveDeviceByConnectionAsync(string connectionId);
        Task<List<DeviceStatus>> GetAllDeviceStatusesAsync();
    }
}