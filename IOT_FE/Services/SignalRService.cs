using IOT_FE.Model;
using IOT_FE.Services.Api;
using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IOT_FE
{
    public interface ISignalRService
    {
        Task StartConnectionAsync();
        Task StopConnectionAsync();
        Task UpdateDeviceStatusAsync(string deviceId, bool isAppOpen);
        event Action<SignalRNotification> OnNotificationReceived;
        event Action<SignalRNotification> OnRealTimeNotification;
        bool IsConnected { get; }
        string ConnectionId { get; }
    }

    public class SignalRService : ISignalRService
    {
        private HubConnection _hubConnection;
        private readonly IBinApi _binApi;
        private readonly string _hubUrl;
        private bool _isConnected;
        private string _connectionId = string.Empty;

        public event Action<SignalRNotification> OnNotificationReceived;
        public event Action<SignalRNotification> OnRealTimeNotification;

        public bool IsConnected => _isConnected && _hubConnection?.State == HubConnectionState.Connected;
        public string ConnectionId => _connectionId;

        public SignalRService(IBinApi binApi)
        {
            _binApi = binApi;


            //_hubUrl = "http://192.168.11.129:5162/publicnotificationhub";
            //_hubUrl = "https://vietstockapi.nguyenlethanhphong.io.vn/publicnotificationhub";
            //  _hubUrl = "https://3c6767c9a594.ngrok-free.app/publicnotificationhub";
            _hubUrl = "https://iotapi.nguyenlethanhphong.io.vn/publicnotificationhub";


            InitializeConnection();
        }

        private void InitializeConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            Debug.WriteLine(" Setting up SignalR listeners...");

            //  Lắng nghe sự kiện từ server với object
            _hubConnection.On<object>("ReceiveNotification", (message) =>
            {
                Debug.WriteLine($"Received notification: {message}");
                var notification = ParseNotification(message);
                if (notification != null)
                {
                    OnNotificationReceived?.Invoke(notification);
                }
            });

            _hubConnection.On<object>("ReceiveRealTimeNotification", (message) =>
            {
                Debug.WriteLine($" Received real-time notification: {message}");
                var notification = ParseNotification(message);
                if (notification != null)
                {
                    OnRealTimeNotification?.Invoke(notification);
                }
            });

            //  Thêm listener cho tất cả notification methods có thể từ server
            _hubConnection.On<object>("SendNotificationToDevice", (message) =>
            {
                Debug.WriteLine($" Received device notification: {message}");
                var notification = ParseNotification(message);
                if (notification != null)
                {
                    OnRealTimeNotification?.Invoke(notification);
                }
            });

            _hubConnection.On<string>("DeviceConnected", (deviceId) =>
            {
                Debug.WriteLine($" Device connected: {deviceId}");
            });

            _hubConnection.On<string>("DeviceDisconnected", (deviceId) =>
            {
                Debug.WriteLine($" Device disconnected: {deviceId}");
            });

            //lắng nghe sự kiện bin alert từ server với object
            _hubConnection.On<object>("ReceiveBinAlert", (message) =>
            {
                Debug.WriteLine($"🚮 Received bin alert: {message}");
                var notification = ParseNotification(message);
                if (notification != null)
                {
                    OnRealTimeNotification?.Invoke(notification);
                }
            });


            // Handle connection events
            _hubConnection.Reconnecting += (error) =>
            {
                Debug.WriteLine($" SignalR reconnecting: {error?.Message}");
                _isConnected = false;
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += async (connectionId) =>
            {
                Debug.WriteLine($" SignalR reconnected: {connectionId}");
                _isConnected = true;
                _connectionId = connectionId ?? string.Empty;

                // Không tự động cập nhật trạng thái khi kết nối lại
                // var deviceId = GetDeviceId();
                // await UpdateDeviceStatusAsync(deviceId, true);
            };

            _hubConnection.Closed += (error) =>
            {
                Debug.WriteLine($" SignalR connection closed: {error?.Message}");
                _isConnected = false;
                _connectionId = string.Empty;
                return Task.CompletedTask;
            };

            Debug.WriteLine(" SignalR listeners setup completed!");
        }

        //  Fix: Rename và implement đúng method ParseNotification
        private SignalRNotification ParseNotification(object message)
        {
            try
            {
                Debug.WriteLine($" ParseNotification starting...");

                if (message == null)
                {
                    Debug.WriteLine(" Message is null");
                    return null;
                }

                Debug.WriteLine($" Message type: {message.GetType().FullName}");

                string jsonString = null;

                if (message is string str)
                {
                    jsonString = str;
                    Debug.WriteLine($" Approach 1 (string): {str}");
                }
                else
                {
                    jsonString = JsonSerializer.Serialize(message, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    Debug.WriteLine($" Approach 2 (serialize): {jsonString}");
                }

                if (string.IsNullOrEmpty(jsonString))
                {
                    Debug.WriteLine(" JSON string is empty");
                    return CreateFallbackNotification(message);
                }

                //  Try with flexible number handling
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                var notification = JsonSerializer.Deserialize<SignalRNotification>(jsonString, options);

                if (notification != null)
                {
                    Debug.WriteLine($" Successfully parsed notification:");
                    Debug.WriteLine($"  Title: {notification.Title}");
                    Debug.WriteLine($"  Body: {notification.Body}");
                    Debug.WriteLine($"  FillLevel: {notification.FillLevel}");
                    Debug.WriteLine($"  ImageUrl: {notification.ImageUrl}");
                    Debug.WriteLine($"  Type: {notification.Type}");

                    return notification;
                }
                else
                {
                    Debug.WriteLine(" Deserialized to null");
                    return CreateFallbackNotification(message);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" ParseNotification exception: {ex.Message}");
                Debug.WriteLine($" Exception details: {ex}");
                return CreateFallbackNotification(message);
            }
        }

        //  Fix: Add missing CreateFallbackNotification method
        private SignalRNotification CreateFallbackNotification(object message)
        {
            Debug.WriteLine(" Creating fallback notification...");

            var fallback = new SignalRNotification
            {
                Title = " Thông báo mới",
                Body = message?.ToString() ?? "Có thông báo mới",
                FillLevel = 0,
                Type = "new_article"
            };

            Debug.WriteLine($" Fallback created: {fallback.Title} - {fallback.Body}");
            return fallback;
        }

        public async Task StartConnectionAsync()
        {
            await StartConnectionAsync(false); // Không cập nhật trạng thái khi khởi tạo
        }

        public async Task StartConnectionAsync(bool updateDeviceStatus = false)
        {
            try
            {
                Debug.WriteLine($" Starting SignalR connection...");
                Debug.WriteLine($" Hub URL: {_hubUrl}");

                if (_hubConnection.State == HubConnectionState.Disconnected)
                {
                    await _hubConnection.StartAsync();
                    _isConnected = true;
                    _connectionId = _hubConnection.ConnectionId ?? string.Empty;

                    Debug.WriteLine($" SignalR connected successfully!");
                    Debug.WriteLine($" Connection ID: {_connectionId}");
                    Debug.WriteLine($" Connection State: {_hubConnection.State}");

                    // Chỉ cập nhật trạng thái thiết bị nếu được yêu cầu rõ ràng
                    if (updateDeviceStatus)
                    {
                        var deviceId = GetDeviceId();
                        Debug.WriteLine($" Registering device: {deviceId.Substring(0, Math.Min(8, deviceId.Length))}...");
                        await UpdateDeviceStatusAsync(deviceId, true);
                    }
                    else
                    {
                        Debug.WriteLine(" Skipping device status update");
                    }
                }
                else
                {
                    Debug.WriteLine($" Connection already in state: {_hubConnection.State}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to start SignalR connection: {ex.Message}");
                Debug.WriteLine($" Full exception: {ex}");
                _isConnected = false;
            }
        }

        public async Task StopConnectionAsync()
        {
            try
            {
                if (_hubConnection.State == HubConnectionState.Connected)
                {
                    // Update device status to closed before disconnecting
                    var deviceId = GetDeviceId();
                    await UpdateDeviceStatusAsync(deviceId, false);

                    await _hubConnection.StopAsync();
                    _isConnected = false;
                    _connectionId = string.Empty;
                    Debug.WriteLine(" SignalR connection stopped");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to stop SignalR connection: {ex.Message}");
            }
        }

        public async Task UpdateDeviceStatusAsync(string deviceId, bool isAppOpen)
        {
            try
            {
                Debug.WriteLine($"🔧 UpdateDeviceStatus: Device={deviceId.Substring(0, Math.Min(8, deviceId.Length))}..., Open={isAppOpen}, Connected={IsConnected}");

                if (IsConnected)
                {
                    // Send to SignalR Hub
                    await _hubConnection.InvokeAsync("UpdateDeviceStatus", deviceId, isAppOpen);
                    Debug.WriteLine($"📤 Device status sent via SignalR: {deviceId.Substring(0, Math.Min(8, deviceId.Length))}... - {(isAppOpen ? "Open" : "Closed")}");
                }
                else
                {
                    Debug.WriteLine($" SignalR not connected, skipping hub update");
                }

                // Also update via REST API as backup
                var request = new DeviceStatusUpdateRequest
                {
                    DeviceId = deviceId,
                    IsAppOpen = isAppOpen
                };

                await _binApi.UpdateDeviceStatusAsync(request);
                Debug.WriteLine($"📤 Device status sent via API: {deviceId.Substring(0, Math.Min(8, deviceId.Length))}... - {(isAppOpen ? "Open" : "Closed")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to update device status: {ex.Message}");
                Debug.WriteLine($" Exception details: {ex}");
            }
        }

        private string GetDeviceId()
        {
            const string key = "device_id";
            string deviceId = Preferences.Get(key, null);
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Set(key, deviceId);
            }
            return deviceId;
        }
    }
}