using AndroidX.ConstraintLayout.Core.Parser;
using IOT_FE.Model;
using Refit;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IOT_FE.Services.Api
{
    public interface IBinApi
    {
        [Get("/api/SmartBin/current")]
        Task<BinStatusResponse> GetCurrentFillLevel();

        [Get("/api/SmartBin/auto-mode")]
        Task<AutoModeResponse> GetAutoModeStatus();

        [Post("/api/SmartBin/auto-mode")]
        Task<ApiResponse> SetAutoMode([Body] AutoModeRequest request);

        [Get("/api/SmartBin/manual-open")]
        Task<ManualOpenResponse> GetManualOpenStatus();

        [Post("/api/SmartBin/manual-open")]
        Task<ApiResponse> SetManualOpen([Body] ManualOpenRequest request);

        [Get("/api/SmartBin/statistics")]
        Task<BinStatisticsResponse> GetStatisticsAsync();



        [Get("/api/SmartBin/chart-summary")]
        Task<ChartSummaryResponse> GetChartSummaryAsync([Query] string range);





        //  Device Status APIs
        [Get("/api/DeviceStatus/all")]
        Task<List<DeviceStatusModel>> GetAllDevicesAsync();

        [Get("/api/DeviceStatus/open")]
        Task<List<DeviceStatusModel>> GetOpenDevicesAsync();

        [Get("/api/DeviceStatus/closed")]
        Task<List<DeviceStatusModel>> GetClosedDevicesAsync();

        [Get("/api/DeviceStatus/check/{deviceId}")]
        Task<DeviceStatusModel> CheckDeviceStatusAsync(string deviceId);

        [Post("/api/DeviceStatus/update")]
        Task<ApiResponse> UpdateDeviceStatusAsync([Body] DeviceStatusUpdateRequest request);



        [Post("/api/FcmToken/save")]
        Task<ApiResponse<string>> SaveTokenAsync([Body] FcmToken tokenModel);

    }

    public class BinStatusResponse
    {
        public double FillLevel { get; set; }
    }

    public class AutoModeResponse
    {
        public bool Enabled { get; set; }
    }

    public class AutoModeRequest
    {
        public bool Enabled { get; set; }
        public bool IsAuto { get; internal set; }
    }

    public class ApiResponse
    {
        public string Message { get; set; }
    }
    public class ManualOpenRequest
    {
        public bool Open { get; set; }
    }

    public class ManualOpenResponse
    {
        public bool Open { get; set; }
    }

    public class BinStatisticsResponse
    {
        public float AverageFillLevel { get; set; }
        public int OpenCountToday { get; set; }
        public int Over80Count { get; set; } 

    }

    public class ChartSummaryResponse
    {
        [JsonPropertyName("labels")]
        public List<string>? Labels { get; set; }

        [JsonPropertyName("avgFill")]
        public List<float>? AvgFill { get; set; }

        [JsonPropertyName("openCount")]
        public List<int>? OpenCount { get; set; }

        [JsonPropertyName("over80Count")]
        public List<int>? Over80Count { get; set; }
    }

}
