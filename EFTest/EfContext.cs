using System;
using System.Collections.Generic;
using System.Text;
using EFTest.Entities;
using Microsoft.EntityFrameworkCore;

namespace EFTest
{
    public class EfContext : DbContext
    {
        public DbSet<Article> Articles { get; set; }

        public DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Article>()
                .HasMany(a => a.Tags)
                .WithOne(t => t.Article)
                .HasForeignKey(_ => _.ArticleId);

            modelBuilder.Entity<Article>()
                .HasOne(a => a.Search)
                .WithOne(t => t.Article)
                .HasForeignKey<ArticleSearch>(_ => _.ArticleId);

            modelBuilder.Entity<ArticleSearch>()
                .HasIndex(p => p.SearchVector)
                .HasMethod("GIN");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=testdb;Username=postgres;Password=postgres");
    }
}
