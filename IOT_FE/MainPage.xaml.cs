using AndroidX.ConstraintLayout.Core.Parser;
using IOT_FE.Model;
using IOT_FE.Services.Api;
using Microsoft.Extensions.DependencyInjection;
using Plugin.Firebase.CloudMessaging;
using System.Diagnostics;

namespace IOT_FE
{
    public partial class MainPage : ContentPage
    {
        private readonly IBinApi _binApi;
        private readonly ISignalRService _signalRService; // 👈 thêm dòng này
        double _containerWidth = 0;

        public MainPage(IBinApi binApi)
        {
            InitializeComponent();
            _binApi = binApi;

            var app = (App)App.Current!;
            app.OnAppRealTimeNotification += HandleAppRealtime;

            GetFCMToken();

            ProgressBarContainer.SizeChanged += (s, e) =>
            {
                _containerWidth = ProgressBarContainer.Width;
            };

        }


        private void HandleAppRealtime(SignalRNotification notification)
        {
            Debug.WriteLine($"📡 MainPage nhận realtime từ App: {notification.Type}");

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (notification.Type == "bin_update")
                {
                    await LoadBinStatus();
                }
                else if (notification.Type == "auto_mode")
                {
                    await LoadAutoModeStatus();
                }
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadAutoModeStatus();   //  Khi vào trang → load trạng thái switch
            await LoadBinStatus();        //  Lấy mức đầy lần đầu khi vào
            await LoadManualOpenStatus(); //  Kiểm tra trạng thái mở nắp thủ công
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();


        }




        private async Task LoadBinStatus()
        {
            try
            {
                var result = await _binApi.GetCurrentFillLevel();
                if (result != null)
                {
                    double percent = result.FillLevel;
                    FillLevelLabel.Text = $"{percent:0.0}%";

                    if (_containerWidth > 0)
                        ProgressFillBox.WidthRequest = _containerWidth * (percent / 100.0);

                    if (percent < 50)
                        ProgressFillBox.BackgroundColor = Colors.Green;
                    else if (percent < 80)
                        ProgressFillBox.BackgroundColor = Colors.Gold;
                    else
                        ProgressFillBox.BackgroundColor = Colors.Red;
                }
            }
            catch (Exception ex)
            {
                FillLevelLabel.Text = $"❌ Lỗi gọi API: {ex.Message}";
            }
        }

        // 🟢 Load trạng thái auto-mode từ API
        private async Task LoadAutoModeStatus()
        {
            try
            {
                var response = await _binApi.GetAutoModeStatus();
                AutoModeSwitch.IsToggled = response.Enabled;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không lấy được trạng thái auto-mode:\n{ex.Message}", "OK");
            }
        }

        // Khi người dùng bật/tắt switch
        private async void OnAutoModeToggled(object sender, ToggledEventArgs e)
        {
            bool isAutoMode = e.Value;

            if (isAutoMode)
            {
                AutoModeLabel.Text = "Tự động mở nắp";
                AutoModeLabel.TextColor = Colors.Green;
                AutoModeIcon.Source = "robot_on.png";
            }
            else
            {
                AutoModeLabel.Text = "Tự động mở nắp";
                AutoModeLabel.TextColor = Colors.Gray;
                AutoModeIcon.Source = "robot_off.png";
            }

            await _binApi.SetAutoMode(new AutoModeRequest { Enabled = isAutoMode });
        }
        //Load trang thái mở nắp thủ công từ API
        private async Task LoadManualOpenStatus()
        {
            try
            {
                var response = await _binApi.GetManualOpenStatus();
                ManualOpenSwitch.Toggled -= OnManualOpenToggled; // tránh gọi lệnh ngược
                ManualOpenSwitch.IsToggled = response.Open;
                ManualOpenSwitch.Toggled += OnManualOpenToggled;

                if (response.Open)
                {
                    ManualOpenLabel.TextColor = Colors.Green;
                    ManualOpenIcon.Source = "lid_open.png";
                }
                else
                {

                    ManualOpenLabel.TextColor = Colors.Gray;
                    ManualOpenIcon.Source = "lid_closed.png";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không lấy được trạng thái mở nắp:\n{ex.Message}", "OK");
            }
        }
        // 🟢 Khi người dùng bật/tắt switch mở nắp thủ công
        private async void OnManualOpenToggled(object sender, ToggledEventArgs e)
        {
            bool openLid = e.Value;

            try
            {
                await _binApi.SetManualOpen(new ManualOpenRequest { Open = openLid });

                if (openLid)
                {
                    ManualOpenLabel.Text = "Mở nắp thủ công";
                    ManualOpenLabel.TextColor = Colors.Green;
                    ManualOpenIcon.Source = "lid_open.png"; // bạn chuẩn bị ảnh icon nắp mở
                }
                else
                {
                    ManualOpenLabel.Text = "Mở nắp thủ công";
                    ManualOpenLabel.TextColor = Colors.Gray;
                    ManualOpenIcon.Source = "lid_closed.png";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không gửi được lệnh mở/đóng nắp:\n{ex.Message}", "OK");

                // rollback trạng thái switch nếu lỗi
                ManualOpenSwitch.Toggled -= OnManualOpenToggled;
                ManualOpenSwitch.IsToggled = !openLid;
                ManualOpenSwitch.Toggled += OnManualOpenToggled;
            }
        }

        private void OnRefreshClicked(object sender, EventArgs e)
        {
            _ = LoadBinStatus();
        }

        public string GetDeviceId()
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

        private async void GetFCMToken()
        {
            try
            {
                await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
                var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
                System.Diagnostics.Debug.WriteLine($"FCM token: {token}");

                var deviceId = GetDeviceId();

                var fcmToken = new FcmToken
                {
                    UserId = deviceId,
                    Token = token,
                };

                var response = await _binApi.SaveTokenAsync(fcmToken);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Đã lưu token");
                }
                else
                {
                    Debug.WriteLine($"Lưu token thất bại");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi lấy FCM token: {ex.Message}");
            }
        }
    }
}
