using IOT_FE.Services.Api;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Firebase.Auth;
using Plugin.Firebase.Bundled.Shared;
using Plugin.Firebase.Crashlytics;
using Refit;
using IOT_FE.Helpers;
using Microcharts.Maui;
using IOT_FE.Views;
using System.Text.Json;







#if IOS
using Plugin.Firebase.Bundled.Platforms.iOS;
#else
using Plugin.Firebase.Bundled.Platforms.Android;
#endif


namespace IOT_FE
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
               .UseMicrocharts();

            //URL của Web API
            //const string BaseApiUrl = "http://192.168.1.179:5074";
            const string BaseApiUrl = "https://iotapi.nguyenlethanhphong.io.vn/";

            // Đăng ký Refit client
            builder.Services.AddRefitClient<IBinApi>(new RefitSettings
            {
                // ✅ Bổ sung đầy đủ serializer options để tránh null-silent và lỗi kiểu dữ liệu
                ContentSerializer = new SystemTextJsonContentSerializer(
           new JsonSerializerOptions
           {
               PropertyNameCaseInsensitive = true,  // bỏ qua hoa/thường key
               ReadCommentHandling = JsonCommentHandling.Skip,
               AllowTrailingCommas = true,
               NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
               DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
               WriteIndented = false
           }),

                // ✅ Bắt Refit throw lỗi khi Deserialize thất bại
                ExceptionFactory = async response =>
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[DEBUG] ❌ Deserialize failed — Raw: {content}");
                    if (!response.IsSuccessStatusCode)
                        return new Exception($"Refit HTTP Error: {(int)response.StatusCode} {response.ReasonPhrase}");
                    if (string.IsNullOrWhiteSpace(content))
                        return new Exception("Empty response body from server.");
                    return null;
                }
            })
   .ConfigureHttpClient(c =>
   {
       c.BaseAddress = new Uri(BaseApiUrl);
       c.Timeout = TimeSpan.FromSeconds(10);
   });


            builder.Services.AddSingleton<ISignalRService, SignalRService>();
            builder.Services.AddSingleton<IDeviceNotificationService, DeviceNotificationService>();


#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // ✅ Gắn service provider toàn cục để gọi từ MainActivity, ViewModel...
            ServiceHelper.Provider = app.Services;

            return app;
        }

        private static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
#if IOS
            events.AddiOS(iOS => iOS.FinishedLaunching((app, launchOptions) => {
                CrossFirebase.Initialize(CreateCrossFirebaseSettings());
                return false;
            }));
#else
                events.AddAndroid(android => android.OnCreate((activity, _) =>
                    CrossFirebase.Initialize(activity, CreateCrossFirebaseSettings())));
                CrossFirebaseCrashlytics.Current.SetCrashlyticsCollectionEnabled(true);
#endif
            });

            builder.Services.AddSingleton(_ => CrossFirebaseAuth.Current);
            return builder;
        }

        private static CrossFirebaseSettings CreateCrossFirebaseSettings()
        {
            return new CrossFirebaseSettings(isAuthEnabled: true,
            isCloudMessagingEnabled: true, isAnalyticsEnabled: true);
        }
    }
}
