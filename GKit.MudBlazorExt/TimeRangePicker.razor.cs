using Microsoft.AspNetCore.Components;

namespace GKit.MudBlazorExt;

public partial class TimeRangePicker : ComponentBase
{
    public class TimeRange
    {
        public TimeSpan? Start { get; set; }
        public TimeSpan? End { get; set; }
    }
}