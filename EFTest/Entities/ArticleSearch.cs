using System;
using System.Collections.Generic;
using System.Text;
using NpgsqlTypes;

namespace EFTest.Entities
{
    public class ArticleSearch
    {
        public Guid Id { get; set; }

        public Guid ArticleId { get; set; }

        public Article Article { get; set; }

        public NpgsqlTsVector SearchVector { get; set; }
    }
}
