using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using IOT_BE;
using IOT_BE.Hubs;
using IOT_BE.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Tùy chọn) Database context nếu bạn có database
builder.Services.AddDbContext<IOT_BEDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("IOTDb")));

//Thêm Controller + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//  SignalR
builder.Services.AddSignalR();

//CORS để MAUI gọi được API từ ngoài
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


//  Firebase config
if (FirebaseApp.DefaultInstance == null)
{
    var jsonPath = Path.Combine(AppContext.BaseDirectory, "plexiform-muse-461800-t5-firebase-adminsdk-fbsvc-3a1bd94d1f.json");
    var credential = GoogleCredential.FromFile(jsonPath);
    var json = File.ReadAllText(jsonPath);
    var projectId = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("project_id").GetString();

    Console.WriteLine(" Project ID từ JSON: " + projectId);

    FirebaseApp.Create(new AppOptions
    {
        Credential = credential,
        ProjectId = "plexiform-muse-461800-t5"
    });
}




//  Services cần thiết
builder.Services.AddScoped<IFirebaseMessagingService, FirebaseMessagingService>();
builder.Services.AddScoped<IDeviceStatusService, DeviceStatusService>();
builder.Services.AddScoped<INotificationService, NotificationService>();




var app = builder.Build();

// Bật CORS
app.UseCors("AllowAll");

//Bật Swagger UI khi chạy Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    //Mở trình duyệt tự động
    //var swaggerUrl = "https://iotapi.nguyenlethanhphong.io.vn/swagger";
    var swaggerUrl = "http://localhost:5074/swagger";
    _ = Task.Run(() => Process.Start(new ProcessStartInfo
    {
        FileName = swaggerUrl,
        UseShellExecute = true
    }));
}

// Không bắt buộc HTTPS với ESP32 hoặc mobile nội mạng
// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


//  Chỉ cần 1 Hub đơn giản
app.MapHub<PublicNotificationHub>("/publicnotificationhub").RequireCors("AllowAll");

app.Run();
