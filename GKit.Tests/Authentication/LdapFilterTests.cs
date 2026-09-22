using GKit.Authentication.ActiveDirectory;

namespace GKit.Tests.Authentication;

/// <summary>
/// Filter escaping and DN parsing are pure string handling. Neither needs a directory server,
/// which is the whole reason they were sitting untested behind one.
/// </summary>
public class LdapFilterTests
{
  [Theory]
  [InlineData("mario.rossi", "mario.rossi")]
  [InlineData("", "")]
  [InlineData(null, "")]
  public void Ordinary_usernames_pass_through(string? input, string expected)
  {
    Assert.Equal(expected, LdapFilter.Escape(input));
  }

  [Theory]
  [InlineData("*", "\\2a")]
  [InlineData("(", "\\28")]
  [InlineData(")", "\\29")]
  [InlineData("\\", "\\5c")]
  [InlineData("/", "\\2f")]
  public void RFC_4515_metacharacters_are_escaped(string input, string expected)
  {
    Assert.Equal(expected, LdapFilter.Escape(input));
  }

  [Fact]
  public void A_filter_injection_attempt_is_neutralised()
  {
    // Interpolated raw, this changed the meaning of the search filter.
    var escaped = LdapFilter.Escape("*)(objectClass=*");

    Assert.DoesNotContain('*', escaped);
    Assert.DoesNotContain('(', escaped);
    Assert.DoesNotContain(')', escaped);
    Assert.Equal("\\2a\\29\\28objectClass=\\2a", escaped);
  }

  [Fact]
  public void A_null_byte_is_escaped()
  {
    Assert.Equal("a\\00b", LdapFilter.Escape("a\0b"));
  }
}

public class ADUserTests
{
  private static ADUser WithGroups(params string[] groups) =>
    new(Guid.NewGuid(), "mrossi", "Mario Rossi", "m@example.com", groups);

  [Fact]
  public void The_common_name_is_extracted_from_each_group_dn()
  {
    var user = WithGroups(
      "cn=operators,ou=groups,dc=example,dc=com",
      "cn=admins,ou=groups,dc=example,dc=com");

    Assert.Equal(["operators", "admins"], user.GroupsNames);
  }

  [Fact]
  public void A_dn_that_does_not_start_with_cn_yields_an_empty_name()
  {
    // Documents the current behaviour rather than asserting it is ideal: the regex is anchored,
    // so anything else collapses to "" instead of throwing.
    var user = WithGroups("ou=groups,dc=example,dc=com");

    Assert.Equal([""], user.GroupsNames);
  }

  [Fact]
  public void No_groups_yields_no_names()
  {
    Assert.Empty(WithGroups().GroupsNames);
  }
}
