using System.Text.RegularExpressions;

namespace GKit.Authentication.ActiveDirectory;

public partial record ADUser(
  Guid Id,
  string AccountName,
  string DisplayName,
  string Mail,
  IEnumerable<string> Groups
)
{
  public IEnumerable<string> GroupsNames =>
    Groups.Select(p => BaseGroupNameRegex().Match(p).Groups[1].ToString());

  [GeneratedRegex("^cn=([^,]+),")]
  private static partial Regex BaseGroupNameRegex();
}