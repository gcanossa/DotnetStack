using GKit.MudBlazorExt;
using MudBlazor;

namespace Test.Repo.MudBlazorExt;

public class GriQueryDataExtensionsTest
{
    [Fact]
    public void GriQueryDataExtensions_GetSortDefinitions_ReturnsSortDefinitions()
    {
        var query = new List<TestObject>().AsQueryable();
        query = GridQueryDataExtensions.NullCheckingOrderBy(query, new SortDefinition<TestObject>[]
        {
            new SortDefinition<TestObject>("Obj1.Obj2.Obj1.Obj2.Value",  false, 0, o => o.Value ),
        });

        Console.WriteLine(query);
    }

    public class TestObject
    {
        public TestObject Obj1 { get; set; }
        public TestObject Obj2 { get; set; }
        public int Value { get; set; }
    }
}