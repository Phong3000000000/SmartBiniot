using System.ComponentModel.DataAnnotations;

namespace IOT_BE.Models
{
    public class BinData
    {
        [Key]
        public int Id { get; set; }

        // 📈 Mức đầy của thùng (0–100%)
        public float FillLevel { get; set; }

        // 🚪 Có mở nắp không (ESP32 gửi true nếu có người)
        public bool IsOpened { get; set; }

        // ⏰ Thời điểm gửi dữ liệu
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}

