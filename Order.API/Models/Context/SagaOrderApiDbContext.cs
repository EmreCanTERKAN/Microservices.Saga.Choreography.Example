﻿using Microsoft.EntityFrameworkCore;

namespace Order.API.Models.Context
{
    public class SagaOrderApiDbContext : DbContext
    {
        public SagaOrderApiDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
    }
}
