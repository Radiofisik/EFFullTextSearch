using System;
using System.Collections.Generic;
using System.Text;

namespace EFTest.Entities
{
    public class Article
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public List<Tag> Tags { get; set; }
        public ArticleSearch Search { get; set; }
    }
}
