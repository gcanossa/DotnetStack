using Microsoft.EntityFrameworkCore;

namespace GKit.MudBlazorExt;

public interface IEditEntityForm<T> where T : class
{
  public T Model { get; set; }
  public DbContext Context { get; set; }
}