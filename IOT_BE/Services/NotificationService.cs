using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using IOT_BE.Services;
using IOT_BE.Hubs;
using IOT_BE.Models;

namespace IOT_BE
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<PublicNotificationHub> _hubContext;
        private readonly IFirebaseMessagingService _firebaseService;
        private readonly IDeviceStatusService _deviceStatusService;
        private readonly IOT_BEDbContext _context;



        public NotificationService(
               IHubContext<PublicNotificationHub> hubContext,
               IFirebaseMessagingService firebaseService,
               IDeviceStatusService deviceStatusService,
               IOT_BEDbContext context)
        {
            _hubContext = hubContext;
            _firebaseService = firebaseService;
            _deviceStatusService = deviceStatusService;
            _context = context;
        }




        // Method chính cho việc tạo thông báo thùng rác đầy
        public async Task SendBinFullNotificationAsync(BinData bin)
        {
            Console.WriteLine($"🚮 Sending bin full notification: {bin.FillLevel}%");

            var notificationData = new
            {
                Title = "Cảnh báo thùng rác",
                Body = $"Thùng rác đã đầy {bin.FillLevel}%",
                Type = "bin_full",
                FillLevel = bin.FillLevel,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // 1️⃣ Lấy danh sách devices
                var openDevices = await _deviceStatusService.GetOpenDevicesAsync();
                var closedDevices = await _deviceStatusService.GetClosedDevicesAsync();

                Console.WriteLine($"📊 Open devices: {openDevices.Count}, Closed devices: {closedDevices.Count}");

                // 2️⃣ Gửi SignalR cho thiết bị đang mở
                if (openDevices.Any())
                {
                    Console.WriteLine("📡 Sending SignalR bin alert...");
                    await _hubContext.Clients.All.SendAsync("ReceiveBinAlert", notificationData);
                }

                // 3️⃣ Gửi Firebase cho thiết bị đã đóng
                if (closedDevices.Any())
                {
                    Console.WriteLine("🔥 Sending FCM bin alert...");
                    var closedTokens = await _context.FcmTokens
                        .Where(t => closedDevices.Contains(t.UserId) && !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (closedTokens.Any())
                    {
                        await _firebaseService.SendNotificationAsync(notificationData.Title, bin, closedTokens);
                    }
                }

                // 4️⃣ Nếu không có thiết bị nào → broadcast toàn bộ
                if (!openDevices.Any() && !closedDevices.Any())
                {
                    Console.WriteLine("⚡ No tracked devices, broadcasting to ALL");

                    await _hubContext.Clients.All.SendAsync("ReceiveBinAlert", notificationData);

                    var allTokens = await _context.FcmTokens
                        .Where(t => !string.IsNullOrEmpty(t.Token))
                        .Select(t => t.Token)
                        .ToListAsync();

                    if (allTokens.Any())
                    {
                        await _firebaseService.SendNotificationAsync(notificationData.Title, bin, allTokens);
                    }
                }

                Console.WriteLine("✅ Bin full notification sent!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending bin notification: {ex.Message}");
            }
        }




    }
}