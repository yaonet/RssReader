using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RssReader.Models;

namespace RssReader.Data
{
    public class RssReaderContext : DbContext
    {
        public RssReaderContext (DbContextOptions<RssReaderContext> options)
            : base(options)
        {
        }

        public DbSet<RssReader.Models.Feed> Feed { get; set; } = default!;
        public DbSet<RssReader.Models.Category> Category { get; set; } = default!;
        public DbSet<RssReader.Models.AppSetting> AppSetting { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Category>()
                .HasMany(c => c.Feeds)
                .WithOne(f => f.Category)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Feed>()
                .HasMany(f => f.Articles)
                .WithOne(a => a.Feed)
                .HasForeignKey(a => a.FeedId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AppSetting>()
                .HasIndex(s => s.Key)
                .IsUnique();
        }
    }
}
