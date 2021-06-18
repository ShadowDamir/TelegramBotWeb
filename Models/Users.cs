using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;

namespace TelegramBotWeb.Models
{
    public class Users
    {
        [Key] public int ChatId { get; set; }
        public string Username_real { get; set; }
        public string Username { get; set; }
        public string LastBotMessage { get; set; }
        public DateTime FirstMessageTime { get; set; }
    }
}
