using System.Linq.Expressions;
using MudBlazor;
using NPOI.SS.Formula.Functions;

namespace GKit.MudBlazorExt;

public static class GridQueryDataExtensions
{
    public static IQueryable<TData> OrderBy<TData>(
        this IQueryable<TData> source,
        IEnumerable<SortDefinition<TData>> sortDefinitions)
    {
        return QuerySortExtensions.OrderBy(source, sortDefinitions,
            (parameter, sortDefinition) =>
            {
                var sortBys = sortDefinition.SortBy.Split('.');
                var property = typeof(TData).GetProperty(sortBys[0])!;
                var result = Expression.Property(parameter, typeof(TData), sortBys[0]);

                for (var i = 1; i < sortBys.Length; i++)
                {
                    result = Expression.Property(result, property!.PropertyType, sortBys[i]);
                    property = property.PropertyType.GetProperty(sortBys[i])!;
                }

                return result;
            });
    }

    public static IQueryable<TData> NullCheckingOrderBy<TData>(
        this IQueryable<TData> source,
        IEnumerable<SortDefinition<TData>> sortDefinitions)
    {
        return QuerySortExtensions.OrderBy(source, sortDefinitions,
            (parameter, sortDefinition) =>
            {
                var sortBys = sortDefinition.SortBy.Split('.');
                var property = typeof(TData).GetProperty(sortBys[0])!;
                var memberExpressions = new List<MemberExpression>()
                    { Expression.Property(parameter, typeof(TData), sortBys[0]) };

                for (var i = 1; i < sortBys.Length; i++)
                {
                    memberExpressions.Add(Expression.Property(memberExpressions.Last(), property!.PropertyType,
                        sortBys[i]));
                    property = property.PropertyType.GetProperty(sortBys[i])!;
                }

                if (memberExpressions.Count == 1)
                    return memberExpressions.First();
                else
                {
                    Expression result = memberExpressions.Last();
                    for (var i = 2; i <= memberExpressions.Count; i++)
                    {
                        result = Expression.Condition(
                            Expression.Equal(memberExpressions[^i], Expression.Constant(null)),
                            Expression.Default(property.PropertyType), result);
                    }

                    return result;
                }
            });
    }
}