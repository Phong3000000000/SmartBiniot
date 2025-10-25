using FirebaseAdmin.Messaging;
using IOT_BE.Models;
using System.Net;

namespace IOT_BE.Services
{
    public class FirebaseMessagingService : IFirebaseMessagingService
    {
        public async Task SendNotificationAsync(string title, BinData bin, List<string> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                Console.WriteLine(" Không có token nào.");
                return;
            }

            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new Notification
                {
                    Title = title,
                    Body = $"Thùng rác đã đầy {bin.FillLevel}%.",
                    ImageUrl = "https://image.vietstock.vn/2025/07/16/HNR-ava_930363.png"
                },
                Data = new Dictionary<string, string>
                {
                    { "fillLevel", bin.FillLevel.ToString() },
                },
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        ClickAction = "FLUTTER_NOTIFICATION_CLICK",
                        ChannelId = "com.company.AppVietStock.general"

                    }
                }
            };

            try
            {
                var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);


                Console.WriteLine($"Gửi thành công {response.SuccessCount}/{tokens.Count}");
                foreach (var result in response.Responses)
                {
                    Console.WriteLine($" Result: Success = {result.IsSuccess}, MessageId = {result.MessageId}");
                    if (!result.IsSuccess)
                        Console.WriteLine($" Error: {result.Exception}");
                }
            }
            catch (FirebaseMessagingException fcmEx)
            {
                Console.WriteLine("FirebaseMessagingException: " + fcmEx.Message);
                Console.WriteLine("StatusCode: " + fcmEx.HttpResponse?.StatusCode);


                if (fcmEx.InnerException != null)
                    Console.WriteLine("Inner: " + fcmEx.InnerException.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Lỗi chung: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner: " + ex.InnerException.Message);
                    if (ex.InnerException.InnerException != null)
                        Console.WriteLine("Inner sâu hơn: " + ex.InnerException.InnerException.Message);
                }
            }
        }
    }
}
