
using IOT_FE.Model;
using IOT_FE.Services.Api;
using IOT_FE.Views.Shared;
using Microsoft.Maui.Layouts;
using System.Diagnostics;
using ILayout = Microsoft.Maui.ILayout;

namespace IOT_FE
{
    public interface IDeviceNotificationService
    {
        Task<List<DeviceStatusModel>> GetAllDevicesAsync();
        Task<List<DeviceStatusModel>> GetOpenDevicesAsync();
        Task<List<DeviceStatusModel>> GetClosedDevicesAsync();
        Task<DeviceStatusModel> CheckDeviceStatusAsync(string deviceId);
        Task ShowInAppNotificationAsync(SignalRNotification notification);
        Task HandleNotificationAsync(SignalRNotification notification);
    }

    public class DeviceNotificationService : IDeviceNotificationService
    {
        private readonly IBinApi  _binApi;
        private readonly ISignalRService _signalRService;
        private TopNotificationView _currentNotificationView;
        private bool _isShowing = false;

        public DeviceNotificationService(IBinApi binApi, ISignalRService signalRService)
        {
            _binApi = binApi;
            _signalRService = signalRService;

            Debug.WriteLine(" DeviceNotificationService: Handlers disabled to prevent duplicates");
        }

        public async Task<List<DeviceStatusModel>> GetAllDevicesAsync()
        {
            try
            {
                return await _binApi.GetAllDevicesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to get all devices: {ex.Message}");
                return new List<DeviceStatusModel>();
            }
        }

        public async Task<List<DeviceStatusModel>> GetOpenDevicesAsync()
        {
            try
            {
                return await _binApi.GetOpenDevicesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to get open devices: {ex.Message}");
                return new List<DeviceStatusModel>();
            }
        }

        public async Task<List<DeviceStatusModel>> GetClosedDevicesAsync()
        {
            try
            {
                return await _binApi.GetClosedDevicesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to get closed devices: {ex.Message}");
                return new List<DeviceStatusModel>();
            }
        }

        public async Task<DeviceStatusModel> CheckDeviceStatusAsync(string deviceId)
        {
            try
            {
                return await _binApi.CheckDeviceStatusAsync(deviceId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to check device status: {ex.Message}");
                return new DeviceStatusModel { DeviceId = deviceId, IsAppOpen = false };
            }
        }

        public async Task ShowInAppNotificationAsync(SignalRNotification notification)
        {
            try
            {
                Debug.WriteLine($" Hiển thị thông báo với TopNotificationView: {notification.Title} - {notification.Body}");

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        // Nếu đang hiển thị thông báo khác, bỏ qua
                        if (_isShowing)
                        {
                            Debug.WriteLine(" Đang có thông báo khác, bỏ qua thông báo mới");
                            return;
                        }

                        _isShowing = true;

                        // Tạo thông báo mới
                        _currentNotificationView = new TopNotificationView();

                        // Thêm vào trang hiện tại
                        await ThemVaoTrangHienTai();

                        // Hiển thị thông báo và chờ phản hồi từ người dùng  
                        var nguoiDungBamVao = await _currentNotificationView.ShowNotificationAsync(notification, 5000);

                        // Dọn dẹp
                        await DonDepNotificationView();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($" Lỗi khi hiển thị thông báo: {ex.Message}");
                        Debug.WriteLine($"📋 Chi tiết lỗi: {ex}");

                        // Đảm bảo dọn dẹp khi có lỗi
                        await DonDepNotificationView();

       
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Thất bại khi hiển thị thông báo trong app: {ex.Message}");
                _isShowing = false;
            }
        }

        private async Task ThemVaoTrangHienTai()
        {
            try
            {
                var mainPage = Application.Current?.MainPage;

                if (mainPage is Shell shell)
                {
                    // Lấy trang hiện tại từ Shell
                    var currentPage = shell.CurrentPage as ContentPage;
                    if (currentPage != null)
                    {
                        Debug.WriteLine($"🔍 Current page type: {currentPage.GetType().Name}");
                        Debug.WriteLine($"🔍 Current page content type: {currentPage.Content?.GetType().Name}");

                        if (currentPage.Content is Layout currentLayout)
                        {
                            Debug.WriteLine(" Trang thường - Sử dụng layout trực tiếp");
                            await ThemVaoLayout(currentLayout);
                        }
                        else
                        {
                            Debug.WriteLine(" Shell CurrentPage không có Layout hỗ trợ");
                        }
                    }
                    else
                    {
                        Debug.WriteLine(" Shell CurrentPage không phải ContentPage");
                    }
                }
                else if (mainPage is ContentPage contentPage)
                {

                    if (contentPage.Content is Layout layout)
                    {
                        Debug.WriteLine(" ContentPage thường");
                        await ThemVaoLayout(layout);
                    }
                }
                else
                {
                    Debug.WriteLine($" MainPage không phải Shell hoặc ContentPage: {mainPage?.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Lỗi khi thêm vào trang hiện tại: {ex.Message}");
                throw;
            }
        }

     

        private AbsoluteLayout FindAbsoluteLayoutInView(View view)
        {
            // Nếu chính view này là AbsoluteLayout
            if (view is AbsoluteLayout absoluteLayout)
            {
                return absoluteLayout;
            }

            // Nếu view có Content property (như ContentView)
            if (view is ContentView contentView && contentView.Content != null)
            {
                return FindAbsoluteLayoutInView(contentView.Content);
            }

            // Nếu view là Layout, tìm trong children
            if (view is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    if (child is View childView)
                    {
                        var found = FindAbsoluteLayoutInView(childView);
                        if (found != null)
                            return found;
                    }
                }
            }

            return null;
        }

        private async Task ThemVaoLayout(Layout layout)
        {
            try
            {
                if (layout is Grid grid)
                {
                    // Thêm vào Grid với positioning cụ thể
                    grid.Children.Add(_currentNotificationView);

                    // Đặt ở row đầu tiên và span toàn bộ columns
                    Grid.SetRow(_currentNotificationView, 0);
                    Grid.SetColumn(_currentNotificationView, 0);
                    Grid.SetRowSpan(_currentNotificationView, 1); // Chỉ chiếm 1 row
                    Grid.SetColumnSpan(_currentNotificationView, grid.ColumnDefinitions.Count > 0 ? grid.ColumnDefinitions.Count : 1);

                    Debug.WriteLine($" Đã thêm vào Grid với {grid.RowDefinitions.Count} rows, {grid.ColumnDefinitions.Count} columns");
                }
                else if (layout is StackLayout stackLayout)
                {
                    // Thêm vào đầu StackLayout
                    stackLayout.Children.Insert(0, _currentNotificationView);
                    Debug.WriteLine(" Đã thêm vào StackLayout");
                }
                else if (layout is AbsoluteLayout absoluteLayout)
                {
                    // Thêm vào AbsoluteLayout với positioning chính xác
                    AbsoluteLayout.SetLayoutBounds(_currentNotificationView, new Rect(0, 0, 1, AbsoluteLayout.AutoSize));
                    AbsoluteLayout.SetLayoutFlags(_currentNotificationView, AbsoluteLayoutFlags.WidthProportional | AbsoluteLayoutFlags.XProportional);
                    absoluteLayout.Children.Add(_currentNotificationView);
                    Debug.WriteLine(" Đã thêm vào AbsoluteLayout");
                }
                else if (layout is VerticalStackLayout verticalStackLayout)
                {
                    // Thêm vào đầu VerticalStackLayout
                    verticalStackLayout.Children.Insert(0, _currentNotificationView);
                    Debug.WriteLine(" Đã thêm vào VerticalStackLayout");
                }
                else
                {
                    Debug.WriteLine($" Layout type không được hỗ trợ: {layout.GetType().Name}");

                    // Fallback: thử cast về ILayout và thêm vào
                    if (layout is ILayout iLayout)
                    {
                        iLayout.Add(_currentNotificationView);
                        Debug.WriteLine($" Đã thêm vào ILayout: {layout.GetType().Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Lỗi khi thêm vào layout: {ex.Message}");
                throw;
            }
        }

        private async Task DonDepNotificationView()
        {
            try
            {
                Debug.WriteLine(" Bắt đầu dọn dẹp notification view...");

                if (_currentNotificationView != null)
                {
                    // Xóa khỏi parent layout  
                    var parent = _currentNotificationView.Parent;
                    if (parent is Layout parentLayout)
                    {
                        parentLayout.Children.Remove(_currentNotificationView);
                        Debug.WriteLine($" Đã xóa notification khỏi {parentLayout.GetType().Name}");
                    }
                    else if (parent is ILayout iLayout)
                    {
                        iLayout.Remove(_currentNotificationView);
                        Debug.WriteLine($" Đã xóa notification khỏi ILayout: {parent?.GetType().Name}");
                    }
                    else
                    {
                        Debug.WriteLine($" Parent không phải Layout: {parent?.GetType().Name}");
                    }

                    _currentNotificationView = null;
                }

                _isShowing = false;
                Debug.WriteLine(" Đã dọn dẹp notification view hoàn tất");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Lỗi khi dọn dẹp notification: {ex.Message}");
                _isShowing = false;
            }
        }

    
     

        public async Task HandleNotificationAsync(SignalRNotification notification)
        {
            try
            {
                Debug.WriteLine($" Xử lý thông báo: {notification.Title} - {notification.Body}");

                // Luôn hiển thị thông báo trong app vì thiết bị đang mở
                await ShowInAppNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Thất bại khi xử lý thông báo: {ex.Message}");
            }
        }

        private async void HandleNotificationReceived(SignalRNotification notification)
        {
            try
            {
                Debug.WriteLine($"📨 HandleNotificationReceived: {notification.Title}");
                await HandleNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Lỗi trong HandleNotificationReceived: {ex.Message}");
            }
        }

        private async void HandleRealTimeNotification(SignalRNotification notification)
        {
            try
            {
                Debug.WriteLine($" HandleRealTimeNotification: {notification.Title}");
                await ShowInAppNotificationAsync(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Lỗi trong HandleRealTimeNotification: {ex.Message}");
            }
        }

        private async Task NavigateToArticleAsync(int articleId)
        {
            try
            {
                await Shell.Current.GoToAsync($"detail?articleId={articleId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Thất bại khi điều hướng đến bài viết {articleId}: {ex.Message}");
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