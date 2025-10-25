using IOT_BE.Models;

namespace IOT_BE.Services
{
    public interface IFirebaseMessagingService
    {
        Task SendNotificationAsync(string title, BinData bin, List<string> tokens);
    }
}
