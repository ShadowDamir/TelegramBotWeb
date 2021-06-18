using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TelegramBotWeb.Models
{
    public class UsersDBContext : DbContext
    {
        public UsersDBContext(DbContextOptions<UsersDBContext> options)
            : base(options)
        {
        }

        public DbSet<Users> Users { get; set; }
    }
}
