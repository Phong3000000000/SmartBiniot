using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using IOT_FE.Helpers;
using IOT_FE.Model;
using IOT_FE.Services.Api;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Platform;
using Plugin.Firebase.CloudMessaging;
using System.Diagnostics;
using Debug = System.Diagnostics.Debug;

namespace IOT_FE
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize
                                   | ConfigChanges.Orientation
                                   | ConfigChanges.UiMode
                                   | ConfigChanges.ScreenLayout
                                   | ConfigChanges.SmallestScreenSize
                                   | ConfigChanges.Density)]
    [IntentFilter(new[] { "FLUTTER_NOTIFICATION_CLICK" }, Categories = new[] { "android.intent.category.DEFAULT" })] 
    public class MainActivity : MauiAppCompatActivity
    {
        private ISignalRService _signalRService;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // ✅ Tạo notification channel trước khi xử lý intent
            CreateNotificationChannelIfNeeded();

            // ✅ Xử lý intent Firebase push khi app mở từ noti
            HandleIntent(Intent);
        }

        protected override async void OnResume()
        {
            base.OnResume();
            System.Diagnostics.Debug.WriteLine("MainActivity OnResume");

            try
            {
                // 👉 Lấy SignalR service từ DI container
                _signalRService ??= ServiceHelper.GetService<ISignalRService>();

                if (_signalRService != null)
                {
                    // 👉 Nếu chưa kết nối thì kết nối SignalR
                    if (!_signalRService.IsConnected)
                    {
                        await _signalRService.StartConnectionAsync();
                    }

                    // 👉 Gửi trạng thái thiết bị = Open
                    var deviceId = GetDeviceId();
                    await _signalRService.UpdateDeviceStatusAsync(deviceId, true);
                    Debug.WriteLine($"Device {deviceId} marked as OPEN (OnResume)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainActivity OnResume error: {ex.Message}");
            }
        }

        protected override async void OnPause()
        {
            base.OnPause();
            Debug.WriteLine("🟡 OnPause CALLED");

            try
            {
                var deviceId = GetDeviceId();

                if (_signalRService?.IsConnected == true)
                {
                    Debug.WriteLine($"📤 Sending CLOSED via SignalR for {deviceId}");
                    await _signalRService.UpdateDeviceStatusAsync(deviceId, false);
                }
                else
                {
                    Debug.WriteLine("⚠️ SignalR not connected, fallback to REST");
                    var binApi = ServiceHelper.GetService<IBinApi>();
                    await binApi.UpdateDeviceStatusAsync(new DeviceStatusUpdateRequest
                    {
                        DeviceId = deviceId,
                        IsAppOpen = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" OnPause error: {ex.Message}");
            }
        }



        protected override async void OnDestroy()
        {
            base.OnDestroy();
            Debug.WriteLine("MainActivity OnDestroy");

            try
            {
                if (_signalRService?.IsConnected == true)
                {
                    var deviceId = GetDeviceId();
                    await _signalRService.UpdateDeviceStatusAsync(deviceId, false);
                    Debug.WriteLine($"Device {deviceId} marked as CLOSED (OnDestroy)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainActivity OnDestroy error: {ex.Message}");
            }
        }


        protected override void OnNewIntent(Intent intent)
        {
            base.OnNewIntent(intent);

            // ✅ Xử lý khi người dùng bấm vào notification khi app đang chạy ngầm
            HandleIntent(intent);
        }

        private void HandleIntent(Intent intent)
        {
            try
            {
                FirebaseCloudMessagingImplementation.OnNewIntent(intent);

                if (intent?.Action == "FLUTTER_NOTIFICATION_CLICK")  // ✅ kiểm tra đúng action
                {
                    Debug.WriteLine("📣 App được mở từ thông báo Firebase");

                    if (intent?.Extras != null)
                    {
                        foreach (var key in intent.Extras.KeySet())
                        {
                            var value = intent.Extras.GetString(key);
                            Debug.WriteLine($"🔑 {key}: {value}");
                        }

                        // 👉 Có thể điều hướng đến trang mong muốn tại đây
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Lỗi xử lý intent: {ex.Message}");
            }
        }


        private void CreateNotificationChannelIfNeeded()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                CreateNotificationChannel();
            }
        }

        private void CreateNotificationChannel()
        {
            var channelId = $"{PackageName}.general";
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);
            notificationManager.CreateNotificationChannel(channel);

            FirebaseCloudMessagingImplementation.ChannelId = channelId;
            // Nếu bạn có icon push riêng thì set ở đây:
            // FirebaseCloudMessagingImplementation.SmallIconRef = Resource.Drawable.ic_push_small;
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
