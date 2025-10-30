using IOT_BE.Hubs;
using IOT_BE.Models;
using IOT_BE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace IOT_BE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SmartBinController : ControllerBase
    {
        // Biến static để lưu mức đầy hiện tại
        private static float _currentFillLevel = 0;

        // Thêm biến static để lưu trạng thái mở nắp tự động
        private static bool _autoOpenEnabled = true;

        // Thêm biến static để lưu trạng thái mở nắp thủ công
        private static bool _manualOpenRequested = false;

        // THÊM BIẾN STATIC ĐỂ CHỐNG SPAM THÔNG BÁO ĐẦY (>80%)
        private static bool _isFullAlertSent = false;

        private readonly IFirebaseMessagingService _firebaseService;
        private readonly INotificationService _notificationService;
        private readonly IHubContext<PublicNotificationHub> _hubContext;
        private readonly IDeviceStatusService _deviceStatusService;
        private readonly IOT_BEDbContext _context;

        // Inject các service qua constructor
        public SmartBinController(
          IFirebaseMessagingService firebaseService,
          INotificationService notificationService,
          IHubContext<PublicNotificationHub> hubContext,
          IDeviceStatusService deviceStatusService,
          IOT_BEDbContext context)
        {
            _firebaseService = firebaseService;
            _notificationService = notificationService;
            _hubContext = hubContext;
            _deviceStatusService = deviceStatusService;
            _context = context;
        }

        // API để điều khiển mở/đóng thủ công từ app
        [HttpPost("manual-open")]
        public IActionResult SetManualOpen([FromBody] ManualOpenRequest request)
        {
            _manualOpenRequested = request.Open;
            Console.WriteLine($"Thao tác thủ công: {(_manualOpenRequested ? "MỞ NẮP" : "ĐÓNG NẮP")}");
            return Ok(new { success = true, open = _manualOpenRequested });
        }

        // PI để ESP32 đọc trạng thái mở thủ công hiện tại
        [HttpGet("manual-open")]
        public IActionResult GetManualOpen()
        {
            return Ok(new { open = _manualOpenRequested });
        }

        // Model nhận request mở/đóng từ app
        public class ManualOpenRequest
        {
            public bool Open { get; set; }
        }


        [HttpPost("update")]
        public async Task<IActionResult> UpdateBin([FromBody] BinData data)
        {
            if (data == null)
            {
                return BadRequest("Không có dữ liệu nhận được");
            }

            _currentFillLevel = data.FillLevel;
            Console.WriteLine($"Nhận dữ liệu từ ESP32: {_currentFillLevel}% đầy");


            // Lưu dữ liệu vào database
            data.Timestamp = DateTime.Now;
            await _context.BinData.AddAsync(data);
            await _context.SaveChangesAsync();

            // 1. Gửi tín hiệu realtime xuống tất cả client MAUI
            var signalData = new
            {
                title = "Cập nhật mức đầy",
                body = $"Mức đầy hiện tại: {_currentFillLevel}%",
                type = "bin_update",
                fillLevel = _currentFillLevel
            };

            await _hubContext.Clients.All.SendAsync("ReceiveRealTimeNotification", signalData);

            // 2. Gửi Firebase nếu đầy > 80% (LOGIC CHỐNG SPAM)
            if (_currentFillLevel >= 80)
            {
                // Chỉ gửi thông báo nếu CHƯA có cảnh báo nào được gửi
                if (!_isFullAlertSent)
                {
                    await _notificationService.SendBinFullNotificationAsync(
                        new BinData { FillLevel = _currentFillLevel }
                    );
                    _isFullAlertSent = true; // Đánh dấu là đã gửi cảnh báo
                    Console.WriteLine(">>> CẢNH BÁO: ĐÃ GỬI THÔNG BÁO ĐẦY LẦN ĐẦU");
                }
            }
            else
            {
                // Nếu mức đầy dưới 80%, reset cờ cảnh báo, sẵn sàng cho lần đầy tiếp theo
                _isFullAlertSent = false;
            }

            return Ok(new { message = "Đã nhận thành công", level = _currentFillLevel });
        }


        [HttpGet("current")]
        public IActionResult GetCurrentFillLevel()
        {
            return Ok(new { fillLevel = _currentFillLevel });
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok("API đang hoạt động 👍");
        }

        // API bật/tắt chế độ mở nắp tự động
        [HttpPost("auto-mode")]
        public IActionResult SetAutoMode([FromBody] AutoModeRequest request)
        {
            _autoOpenEnabled = request.Enabled;
            Console.WriteLine($"Trạng thái mở nắp tự động: {(_autoOpenEnabled ? "BẬT" : "TẮT")}");
            return Ok(new { success = true, enabled = _autoOpenEnabled });
        }

        // ESP32 sẽ gọi cái này để kiểm tra trạng thái hiện tại
        [HttpGet("auto-mode")]
        public IActionResult GetAutoMode()
        {
            return Ok(new { enabled = _autoOpenEnabled });
        }

        // API lấy thống kê mức đầy trung bình và số lần mở nắp trong ngày
        [HttpGet("statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            var today = DateTime.Today;

            // Lấy toàn bộ dữ liệu hôm nay, BẮT BUỘC phải sắp xếp theo Timestamp
            var dataToday = _context.BinData
                .Where(x => x.Timestamp.Date == today)
                .OrderBy(x => x.Timestamp)
                .ToList();

            if (!dataToday.Any())
                return Ok(new { AverageFillLevel = 0, OpenCountToday = 0, Over80Count = 0 });

            //  SỬA LỖI ĐẾM SỐ LẦN MỞ NẮP (Chỉ đếm sự kiện chuyển trạng thái Đóng -> Mở)
            int openCount = 0;
            bool wasOpenedPreviously = false;

            // Duyệt qua dữ liệu đã sắp xếp
            foreach (var item in dataToday)
            {
                if (item.IsOpened && !wasOpenedPreviously)
                {
                    // Phát hiện sự kiện nắp MỞ ra (chuyển từ false sang true)
                    openCount++;
                }
                wasOpenedPreviously = item.IsOpened;
            }


            //  Lọc giá trị trùng (chỉ lấy khi FillLevel thay đổi >= 0.5%)
            var uniqueValues = new List<float>();
            float? last = null;
            foreach (var item in dataToday)
            {
                if (last == null || Math.Abs(item.FillLevel - last.Value) >= 0.5f)
                {
                    uniqueValues.Add(item.FillLevel);
                    last = item.FillLevel;
                }
            }

            // Trung bình của giá trị không trùng
            float average = uniqueValues.Count > 0 ? uniqueValues.Average() : 0;

            // Đếm số lần vượt mức 80% (chỉ tính khi vượt từ <80 → >=80 để tránh đếm trùng)
            int over80Count = 0;
            float prev = 0;
            foreach (var item in dataToday)
            {
                if (prev < 80 && item.FillLevel >= 80)
                    over80Count++;
                prev = item.FillLevel;
            }

            return Ok(new
            {
                AverageFillLevel = Math.Round(average, 1),
                OpenCountToday = openCount, // Sử dụng biến đã đếm chính xác
                Over80Count = over80Count
            });
        }

        [Produces("application/json")]
        [HttpGet("chart-summary")]
        public IActionResult GetChartSummary([FromQuery] string range = "week")
        {
            DateTime now = DateTime.Now;
            DateTime startDate;

            // Xác định phạm vi
            if (range == "month")
                startDate = new DateTime(now.Year, now.Month, 1);
            else if (range == "year")
                startDate = new DateTime(now.Year, 1, 1);
            else
                startDate = now.AddDays(-7); // mặc định: tuần

            // Lọc dữ liệu
            var data = _context.BinData
                .Where(x => x.Timestamp >= startDate)
                .ToList();

            if (!data.Any())
                return Ok(new { labels = new string[0], avgFill = new float[0], openCount = new int[0], over80Count = new int[0] });

            // Gom nhóm theo thời gian
            IEnumerable<IGrouping<string, BinData>> grouped;

            if (range == "year")
                grouped = data.GroupBy(x => x.Timestamp.ToString("MM"));
            else if (range == "month")
                grouped = data.GroupBy(x => x.Timestamp.ToString("dd"));
            else
                grouped = data.GroupBy(x => x.Timestamp.ToString("ddd")); // theo thứ

            var result = grouped.Select(g =>
            {
                var list = g.OrderBy(x => x.Timestamp).ToList();

                //  SỬA LỖI ĐẾM SỐ LẦN MỞ NẮP TRONG BIỂU ĐỒ (Chỉ đếm sự kiện chuyển trạng thái Đóng -> Mở)
                int openCount = 0;
                bool wasOpen = false;
                foreach (var i in list)
                {
                    if (i.IsOpened && !wasOpen)
                    {
                        openCount++;
                    }
                    wasOpen = i.IsOpened;
                }

                // Đếm số lần vượt 80%
                int over80 = 0;
                float prev = 0;
                foreach (var i in list)
                {
                    if (prev < 80 && i.FillLevel >= 80)
                        over80++;
                    prev = i.FillLevel;
                }

                // Trung bình mức đầy (lọc trùng nhẹ)
                var uniqueValues = new List<float>();
                float? last = null;
                foreach (var item in list)
                {
                    if (last == null || Math.Abs(item.FillLevel - last.Value) >= 0.5f)
                    {
                        uniqueValues.Add(item.FillLevel);
                        last = item.FillLevel;
                    }
                }

                float avg = uniqueValues.Any() ? (float)uniqueValues.Average() : 0;

                return new
                {
                    Label = g.Key,
                    AvgFill = MathF.Round(avg, 1),
                    OpenCount = openCount, // Sử dụng biến đã đếm chính xác
                    Over80Count = over80
                };
            })
            .OrderBy(x => x.Label)
            .ToList();

            return Ok(new
            {
                labels = result.Select(x => x.Label),
                avgFill = result.Select(x => x.AvgFill),
                openCount = result.Select(x => x.OpenCount),
                over80Count = result.Select(x => x.Over80Count)
            });
        }


    }

    // Model để nhận dữ liệu từ app gửi lên khi bật/tắt switch
    public class AutoModeRequest
    {
        public bool Enabled { get; set; }
    }
}