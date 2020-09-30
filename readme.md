---
title: Fulltext Search
description: поиск с весами на postgresql с EFCore
---

В большинстве руководств по реализации полнотекстового поиска рассматривается простой случай когда все поля для поиска находятся в одной таблице, тогда нет проблем с созданием индекса... Однако жизнь оказывается несколько сложней и в реальной системе как правило данные, по которым нужно искат раскиданы по разным таблицам. Рассмотрим такой случай. Для начала сделаем сущности и контекст, добавим миграцию.

```c#
 public class Article
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public List<Tag> Tags { get; set; }
    }

 public class Tag
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid ArticleId { get; set; }

        public Article Article { get; set; }
    }

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
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=localhost;Database=testdb;Username=postgres;Password=postgres");
    }
```

В простейшем виде задачу полнотекстового поиска с учетом весов можно решить запросом

```sql
select *
from "Articles"
         left join (
    select "ArticleId", string_agg("Name", ',') "Tags"
    from "Tags"
    group by "ArticleId") t on "Id" = t."ArticleId"
where setweight(to_tsvector('russian', "Articles"."Title"), 'A') ||
      setweight(to_tsvector('russian', "Articles"."Content"), 'B') ||
      setweight(to_tsvector('russian', coalesce("t"."Tags", '')), 'C')
          @@ to_tsquery('russian', 'программирование');

```

Для упрощения запроса можно ввести функцию

```sql
create or replace function make_vector(title text, content text, tags text)
    returns tsvector as
$$
begin
    return (
                setweight(to_tsvector('russian', coalesce(title, '')), 'A') ||
                setweight(to_tsvector('russian', coalesce(content, '')), 'B') ||
                setweight(to_tsvector('russian', coalesce(tags, '')), 'C')
        );
end;
$$ language 'plpgsql' immutable;
```

Запрос упростится

```sql
select *, make_vector("Articles"."Title", "Articles"."Content", "t"."Tags" )
from "Articles"
         left join (
    select "ArticleId", string_agg("Name", ',') "Tags"
    from "Tags"
    group by "ArticleId") t on "Id" = t."ArticleId"
where make_vector("Articles"."Title", "Articles"."Content", "t"."Tags" )
          @@ to_tsquery('russian', 'программирование');
```

по хорошему результат поиска надо упорядочить по релевантности

```sql
select *, ts_rank(make_vector("Articles"."Title", "Articles"."Content", "t"."Tags" ), to_tsquery('russian', 'программирование')) as rank
from "Articles"
         left join (
    select "ArticleId", string_agg("Name", ',') "Tags"
    from "Tags"
    group by "ArticleId") t on "Id" = t."ArticleId"
where make_vector("Articles"."Title", "Articles"."Content", "t"."Tags" )
          @@ to_tsquery('russian', 'программирование')
order by rank desc;
```

еще упростить запрос можно введя 

```sql
create view articleTags as
select "ArticleId", string_agg("Name", ',') "Tags"
from "Tags"
group by "ArticleId";

create view searchView as select "Articles"."Id",
       make_vector("Articles"."Title", "Articles"."Content", "t"."Tags") as "search"
from "Articles"
         left join articleTags t on "Id" = t."ArticleId";
```

```sql
select *, ts_rank(s.search, to_tsquery('russian', 'программирование')) as rank
from "Articles"
         left join searchView s on s."Id"="Articles"."Id"
where s."search" @@ to_tsquery('russian', 'программирование')
order by rank desc;
```

Теперь допустим что данные меняются крайне редко, а поиск должен работать быстро. тогда можно применить materialized view с индексом

```sql
create  materialized view searchView2 as select "Articles"."Id",
       make_vector("Articles"."Title", "Articles"."Content", "t"."Tags") as "search"
from "Articles"
         left join articleTags t on "Id" = t."ArticleId";

CREATE INDEX ON searchView2 USING GIN (search);
```

Обновлять можно периодически или по триггерам

```sql
create or replace function refresh_search()
    returns trigger
    language plpgsql
as
$$
begin
    refresh materialized view searchView2;
    return null;
end;
$$;

create trigger refresh_search
    after insert or update or delete or truncate
    on "Articles"
    for statement
    execute procedure refresh_search();

create trigger refresh_search
    after insert or update or delete or truncate
    on "Tags"
    for statement
    execute procedure refresh_search();
```

Поиск в таком случае осуществляется очень быстро по индексу, но операция обновления materialized view дорогая, и делать ее внутри триггера крайне не желательно, особенно если операции изменения в таблицах не редки. Нужен более ресурсосберегающий подход, примерно с той же идеалогией, будем использовать вместо materialized view таблицу.

Для поиска добавим отдельную таблицу

```c#
public class ArticleSearch
{
    public Guid Id { get; set; }
    public Guid ArticleId { get; set; }
    public Article Article { get; set; }
    public NpgsqlTsVector SearchVector { get; set; }
}

 modelBuilder.Entity<Article>()
     .HasOne(a => a.Search)
     .WithOne(t => t.Article)
     .HasForeignKey<ArticleSearch>(_ => _.ArticleId);

 modelBuilder.Entity<ArticleSearch>()
     .HasIndex(p => p.SearchVector)
     .HasMethod("GIN");
```

Осталось сделать самое сложное - добавить в поисковый вектор данные, причем это надо делать при всех изменениях в обоих таблицах - тегов и статей

Первоначально заполним таблицу

```sql
create extension if not exists "uuid-ossp";

insert into "ArticleSearch"("Id", "ArticleId", "SearchVector")
select uuid_generate_v4(), "Id", "search"
from searchView;
```

Создадим функцию обновления 

```sql
create or replace function update_search_for_article(id uuid)
    returns void
    language plpgsql
as
$$
begin
    delete from "ArticleSearch" where "ArticleId"="Id";
    insert into "ArticleSearch"("Id", "ArticleId", "SearchVector")
    select uuid_generate_v4(), "Id", "search"
    from searchView
    where searchView."Id" = id;
end;
$$;
```

Используем ее в триггерах

```sql
create or replace function refresh_search_by_articles()
    returns trigger
    language plpgsql
as
$$
begin
    perform update_search_for_article(new."Id");
    return null;
end;
$$;

create trigger refresh_search
    after insert or update or delete
    on "Articles"
    for each row
    execute procedure refresh_search_by_articles();


create or replace function refresh_search_by_tags()
    returns trigger
    language plpgsql
as
$$
begin
    perform update_search_for_article(new."ArticleId");
    return null;
end;
$$;

create trigger refresh_search
    after insert or update or delete
    on "Tags"
    for statement
    execute procedure refresh_search_by_tags();
    
 create index on "ArticleSearch" using gin ("SearchVector");
```

теперь наш запрос снова работает и если посмотреть explain использует индекс

```sql
select *, ts_rank(s."SearchVector", to_tsquery('russian', 'программирование')) as rank
from "Articles"
         left join "ArticleSearch" s on s."ArticleId"="Articles"."Id"
where s."SearchVector" @@ to_tsquery('russian', 'программирование')
order by rank desc;
```

тот же запрос с использованием EF будет иметь вид

```c#
 var context = new EfContext();
 var searchWord = "программирование";
 var result = context.Articles.Where(a => a.Search.SearchVector.Matches(EF.Functions.ToTsQuery("russian", searchWord)))
             .OrderByDescending(a => a.Search.SearchVector.Rank(EF.Functions.ToTsQuery("russian", searchWord))).ToList();
```

