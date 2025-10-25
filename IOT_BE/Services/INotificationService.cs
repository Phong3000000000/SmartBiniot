using IOT_BE.Models;

namespace IOT_BE.Services
{
    public interface INotificationService
    {
        // Gửi thông báo thùng rác đầy
        Task SendBinFullNotificationAsync(BinData bin);

    }
}