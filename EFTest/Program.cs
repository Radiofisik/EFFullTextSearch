using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using EFTest.Entities;
using Microsoft.EntityFrameworkCore;

namespace EFTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //seed data
            await AddData();
        }

        private static async Task AddData()
        {
            var article = new Article()
            {
                Title = "Первая статья", Tags = new List<Tag>()
                {
                    new Tag() {Name = "поиск"},
                    new Tag() {Name = "программирование"}
                }
            };

            var context = new EfContext();
            await context.Database.MigrateAsync();

            //context.Articles.Add(article);
            context.SaveChanges();
        }
    }
}
