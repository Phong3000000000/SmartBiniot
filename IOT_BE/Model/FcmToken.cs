﻿namespace IOT_BE.Model
{
    public class FcmToken
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public string Token { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}