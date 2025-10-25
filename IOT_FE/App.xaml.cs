using IOT_FE.Model;
using System.Diagnostics;

namespace IOT_FE
{

    public partial class App : Application
    {
        private ISignalRService _signalRService;
        private IDeviceNotificationService _deviceNotificationService;

        public event Action<SignalRNotification> OnAppRealTimeNotification;

        public App()
        {
            InitializeComponent();

            // 👉 Nếu bạn không có PreloadMenu thì chỉ cần khởi tạo SignalR trực tiếp:
            InitSignalRAndNotifications();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        private async void InitSignalRAndNotifications()
        {
            try
            {
                await Task.Delay(500); // Cho services khởi tạo xong

                var services = Handler?.MauiContext?.Services;
                if (services == null) return;

                _signalRService = services.GetService<ISignalRService>();
                _deviceNotificationService = services.GetService<IDeviceNotificationService>();

                if (_signalRService != null)
                {
                    _signalRService.OnNotificationReceived += OnNotificationReceived;
                    _signalRService.OnRealTimeNotification += OnRealTimeNotificationReceived;
                    _signalRService.OnRealTimeNotification += HandleSignalRRealTime;



                    await _signalRService.StartConnectionAsync();
                    Debug.WriteLine("✅ SignalR service initialized and connected");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ InitSignalR error: {ex.Message}");
            }
        }

        private async void OnNotificationReceived(SignalRNotification notification)
        {
            Debug.WriteLine($"📩 Nhận thông báo: {notification.Title} - {notification.Body}");
            if (_deviceNotificationService != null)
            {
                await _deviceNotificationService.ShowInAppNotificationAsync(notification);
            }
            else
            {
                await ShowNotificationAlertFallback(notification);
            }
        }

        private async void OnRealTimeNotificationReceived(SignalRNotification notification)
        {
            Debug.WriteLine($"⚡ Nhận thông báo realtime: {notification.Title} - {notification.Body}");

            // 🟡 Parse body để lấy mức đầy
            if (notification.Type == "bin_update")
            {
                double fillLevel = notification.FillLevel; // vì nó đã là double rồi
                if (fillLevel < 80)
                {
                    Debug.WriteLine($"⏩ Không hiện popup vì fillLevel = {fillLevel}% < 80%");
                    return; // 👉 Không gọi ShowInAppNotificationAsync nữa
                }
            }

            if (_deviceNotificationService != null)
            {
                await _deviceNotificationService.ShowInAppNotificationAsync(notification);
            }
            else
            {
                await ShowNotificationAlertFallback(notification);
            }
        }




        private void HandleSignalRRealTime(SignalRNotification notification)
        {
            Debug.WriteLine($"⚡ [App] Realtime: {notification.Type} - {notification.Title}");

            if (notification.Type == "bin_update")
            {
                Debug.WriteLine("🟢 App xử lý cập nhật bin");
            }
            else if (notification.Type == "auto_mode")
            {
                Debug.WriteLine("🟡 App xử lý auto_mode");
            }

            // 🟡 BẮN EVENT RA CHO MAINPAGE
            OnAppRealTimeNotification?.Invoke(notification);
        }



        private async Task ShowNotificationAlertFallback(SignalRNotification notification)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Current.MainPage.DisplayAlert(notification.Title, notification.Body, "OK"));
        }

        // 👉 Gửi trạng thái thiết bị khi app chuyển trạng thái
        protected override async void OnSleep()
        {
            base.OnSleep();
            if (_signalRService?.IsConnected == true)
            {
                var deviceId = GetDeviceId();
                await _signalRService.UpdateDeviceStatusAsync(deviceId, false);
                Debug.WriteLine($"😴 Device {deviceId} marked CLOSED (sleep)");
            }
        }

        protected override async void OnResume()
        {
            base.OnResume();
            if (_signalRService != null && !_signalRService.IsConnected)
                await _signalRService.StartConnectionAsync();

            if (_signalRService?.IsConnected == true)
            {
                var deviceId = GetDeviceId();
                await _signalRService.UpdateDeviceStatusAsync(deviceId, true);
                Debug.WriteLine($"🌅 Device {deviceId} marked OPEN (resume)");
            }
        }

        protected override async void OnStart()
        {
            base.OnStart();
            if (_signalRService != null && !_signalRService.IsConnected)
                await _signalRService.StartConnectionAsync();
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