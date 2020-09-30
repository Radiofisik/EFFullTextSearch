using System;
using System.Linq;
using System.Linq.Expressions;
using EFTest;
using EFTest.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UnitTests
{
    public class EFTests
    {
        [Fact]
        public void IncludeTest()
        {
            var context = new EfContext();
            var articles = context.Articles.Include(_ => _.Tags).ToList();
            Assert.NotNull(articles);
        }

        [Fact]
        public void IfChangesImmediatelyAccessible()
        {
            var context = new EfContext();
            var newSuperBlog = new Article() {};
            context.Add(newSuperBlog);

            //before saveChanges data is not accessible for query
            context.SaveChanges();
        }

        [Fact]
        public void FullTextSearch()
        {
            var context = new EfContext();
            var searchWord = "программирование";

            var result = context.Articles.Where(a => a.Search.SearchVector.Matches(EF.Functions.ToTsQuery("russian",searchWord)))
                .OrderByDescending(a => a.Search.SearchVector.Rank(EF.Functions.ToTsQuery("russian",searchWord))).ToList();

        }

    }
}