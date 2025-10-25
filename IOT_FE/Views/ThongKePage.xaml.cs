using IOT_FE.Services;
using IOT_FE.Services.Api;
using Microcharts;
using Microsoft.Maui.Controls;
using Refit;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IOT_FE.Views
{
    public partial class ThongKePage : ContentPage
    {
        private readonly IBinApi _binApi;
        private readonly ISignalRService _signalRService;
        private string currentChartRange = "week"; // Biến lưu trạng thái range hiện tại
        private bool isInitialized = false; // Cờ kiểm tra xem biểu đồ đã được tải lần đầu chưa

        public ThongKePage(IBinApi binApi, ISignalRService signalRService)
        {
            InitializeComponent();
            _binApi = binApi;
            _signalRService = signalRService;

            // 1. Tải dữ liệu ban đầu (chỉ thống kê)
            LoadDataAsync(false);

            // 2. Kết nối SignalR
            ConnectSignalR();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // 1. Tải thống kê (luôn luôn)
            LoadDataAsync(false);

            if (!isInitialized)
            {
                // 2. CHỈ TẢI BIỂU ĐỒ LẦN ĐẦU
                LoadDataAsync(true);
                isInitialized = true;
            }
        }

        private async Task LoadDataAsync(bool loadCharts)
        {
            // Bọc toàn bộ logic tải dữ liệu trong Main Thread để đảm bảo an toàn UI
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadStatisticsAsync();

                if (loadCharts)
                {
                    await LoadCharts(currentChartRange);
                }
            });
        }

        private async void ConnectSignalR()
        {
            try
            {
                await _signalRService.StartConnectionAsync();
                _signalRService.OnRealTimeNotification += async (notification) =>
                {
                    if (notification != null && notification.Type == "bin_update")
                    {
                        // Cập nhật thống kê khi có tín hiệu Real-time (không tải lại biểu đồ)
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await LoadStatisticsAsync();
                        });
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SignalR connection failed: {ex.Message}");
            }
        }

        // --- HÀM Xử lý sự kiện khi người dùng bấm nút làm mới ---
        private async void RefreshButton_Clicked(object sender, EventArgs e)
        {
            if (sender is Button refreshButton)
            {
                refreshButton.IsEnabled = false; // Tắt nút trong khi đang tải
            }

            // Tải lại cả thống kê và biểu đồ
            await LoadDataAsync(true);

            if (sender is Button reEnabledButton)
            {
                reEnabledButton.IsEnabled = true; // Bật lại nút
            }
        }

        // --- HÀM Xử lý sự kiện thay đổi range của biểu đồ ---
        private async void RangeControl_SelectionChanged(object sender, string newRange)
        {
            // Cập nhật biến trạng thái range và tải lại biểu đồ NGAY LẬP TỨC
            currentChartRange = newRange;
            await LoadDataAsync(true); // Tải lại cả 2
        }

        // ---------------------------------------------------------------------------------


        private async Task LoadStatisticsAsync()
        {
            try
            {
                var data = await _binApi.GetStatisticsAsync();
                // BỎ COMMENT ĐỂ CẬP NHẬT UI
                if (AverageFillLabel != null) AverageFillLabel.Text = $"{data.AverageFillLevel:F1}%";
                if (OpenCountLabel != null) OpenCountLabel.Text = $"{data.OpenCountToday} lần";
                if (Over80Label != null) Over80Label.Text = $"{data.Over80Count} lần";
            }
            catch (Exception ex)
            {
                // Xử lý lỗi
                if (AverageFillLabel != null) AverageFillLabel.Text = "Lỗi tải dữ liệu";
                if (OpenCountLabel != null) OpenCountLabel.Text = ex.Message;
                if (Over80Label != null) Over80Label.Text = "—";
            }
        }

        private async Task LoadCharts(string range)
        {
            // Logic tải biểu đồ giữ nguyên
            try
            {
                Console.WriteLine($"[DEBUG] Gọi API thống kê range={range}");
                var res = await _binApi.GetChartSummaryAsync(range);

                if (res == null || res.Labels == null || res.AvgFill == null || res.OpenCount == null || res.Over80Count == null)
                {
                    return;
                }

                int count = Math.Min(res.Labels.Count, Math.Min(res.AvgFill.Count, Math.Min(res.OpenCount.Count, res.Over80Count.Count)));
                if (count == 0) return;

                // Xử lý trường hợp 1 điểm dữ liệu
                if (count == 1)
                {
                    res.Labels.Add("");
                    res.AvgFill.Add(res.AvgFill[0]);
                    res.OpenCount.Add(res.OpenCount[0]);
                    res.Over80Count.Add(res.Over80Count[0]);
                    count = 2;
                }

                // === Biểu đồ 1: Mức đầy trung bình ===
                var avgEntries = new List<ChartEntry>();
                for (int i = 0; i < count; i++)
                {
                    avgEntries.Add(new ChartEntry(res.AvgFill[i])
                    {
                        Label = string.IsNullOrEmpty(res.Labels[i]) ? "" : res.Labels[i],
                        ValueLabel = $"{res.AvgFill[i]}%",
                        Color = SKColor.Parse("#4B3CFA")
                    });
                }

                if (AvgFillChart != null)
                {
                    AvgFillChart.Chart = new LineChart
                    {
                        Entries = avgEntries,
                        LineSize = 5,
                        PointMode = PointMode.Circle,
                        PointSize = 8,
                        LabelTextSize = 35,
                        ValueLabelTextSize = 40,
                        LabelOrientation = Orientation.Horizontal,
                        ValueLabelOrientation = Orientation.Horizontal,
                        BackgroundColor = SKColor.Parse("#FAFAFA")
                    };
                    await MainThread.InvokeOnMainThreadAsync(() => AvgFillChart.InvalidateMeasure());
                }

                // === Biểu đồ 2: Số lần mở nắp ===
                var openEntries = new List<ChartEntry>();
                for (int i = 0; i < count; i++)
                {
                    openEntries.Add(new ChartEntry(res.OpenCount[i])
                    {
                        Label = string.IsNullOrEmpty(res.Labels[i]) ? "" : res.Labels[i],
                        ValueLabel = $"{res.OpenCount[i]}",
                        Color = SKColor.Parse("#009688")
                    });
                }
                if (OpenChart != null)
                {
                    OpenChart.Chart = new BarChart
                    {
                        Entries = openEntries,
                        LabelTextSize = 35,
                        ValueLabelTextSize = 40,
                        Margin = 60,
                        LabelOrientation = Orientation.Horizontal,
                        ValueLabelOrientation = Orientation.Horizontal,
                        BackgroundColor = SKColor.Parse("#FAFAFA")
                    };
                    await MainThread.InvokeOnMainThreadAsync(() => OpenChart.InvalidateMeasure());
                }

                // === Biểu đồ 3: Số lần vượt mức 80% ===
                var overEntries = new List<ChartEntry>();
                for (int i = 0; i < count; i++)
                {
                    overEntries.Add(new ChartEntry(res.Over80Count[i])
                    {
                        Label = string.IsNullOrEmpty(res.Labels[i]) ? "" : res.Labels[i],
                        ValueLabel = $"{res.Over80Count[i]}",
                        Color = SKColor.Parse("#E53935")
                    });
                }
                if (Over80Chart != null)
                {
                    Over80Chart.Chart = new BarChart
                    {
                        Entries = overEntries,
                        LabelTextSize = 35,
                        ValueLabelTextSize = 40,
                        Margin = 60,
                        LabelOrientation = Orientation.Horizontal,
                        ValueLabelOrientation = Orientation.Horizontal,
                        BackgroundColor = SKColor.Parse("#FAFAFA")
                    };
                    await MainThread.InvokeOnMainThreadAsync(() => Over80Chart.InvalidateMeasure());
                }

                Console.WriteLine("[DEBUG] ✅ Chart render thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] LoadCharts EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
