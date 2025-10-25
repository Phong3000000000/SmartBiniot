using IOT_BE.Model;
using IOT_BE.Models;
using Microsoft.EntityFrameworkCore;

namespace IOT_BE.Services
{
    public class IOT_BEDbContext:DbContext
    {
        public IOT_BEDbContext(DbContextOptions<IOT_BEDbContext> options) : base(options) { }


        public DbSet<DeviceStatus> DeviceStatuses { get; set; }
        public DbSet<FcmToken> FcmTokens { get; set; }

        public DbSet<BinData> BinData { get; set; }

    }
}
