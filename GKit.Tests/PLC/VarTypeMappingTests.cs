using GKit.PLC;
using S7.Net;

namespace GKit.Tests.PLC;

/// <summary>
/// The CLR-to-S7 mapping is the single highest-risk piece of logic in the suite: getting
/// signedness or width wrong does not throw, it returns plausible-looking numbers. A DInt
/// holding -1 read as a DWord comes back as 4294967295.
/// </summary>
public class VarTypeMappingTests
{
  public static TheoryData<Type, VarType> FaithfulMappings => new()
  {
    { typeof(bool), VarType.Bit },
    { typeof(byte), VarType.Byte },
    { typeof(short), VarType.Int },
    { typeof(ushort), VarType.Word },
    { typeof(int), VarType.DInt },
    { typeof(uint), VarType.DWord },
    { typeof(float), VarType.Real },
    { typeof(double), VarType.LReal },
    { typeof(string), VarType.String },
    { typeof(DateTime), VarType.DateTimeLong },
  };

  [Theory]
  [MemberData(nameof(FaithfulMappings))]
  public void Clr_types_map_to_the_S7_type_with_the_same_width_and_signedness(Type clrType, VarType expected)
  {
    Assert.Equal(expected, EntityTypeBuilder<object>.FromClrType(clrType));
  }

  [Theory]
  [MemberData(nameof(FaithfulMappings))]
  public void Arrays_map_to_their_element_type(Type clrType, VarType expected)
  {
    if (clrType == typeof(string)) return;   // string is itself IEnumerable<char>

    Assert.Equal(expected, EntityTypeBuilder<object>.FromClrType(clrType.MakeArrayType()));
  }

  [Fact]
  public void Signed_and_unsigned_16_bit_do_not_collide()
  {
    Assert.NotEqual(
      EntityTypeBuilder<object>.FromClrType(typeof(short)),
      EntityTypeBuilder<object>.FromClrType(typeof(ushort)));
  }

  [Fact]
  public void Signed_and_unsigned_32_bit_do_not_collide()
  {
    Assert.NotEqual(
      EntityTypeBuilder<object>.FromClrType(typeof(int)),
      EntityTypeBuilder<object>.FromClrType(typeof(uint)));
  }

  private enum ByteBacked : byte { A }
  private enum IntBacked { A }

  [Fact]
  public void Enums_map_through_their_underlying_type()
  {
    Assert.Equal(VarType.Byte, EntityTypeBuilder<object>.FromClrType(typeof(ByteBacked)));
    Assert.Equal(VarType.DInt, EntityTypeBuilder<object>.FromClrType(typeof(IntBacked)));
  }

  [Theory]
  [InlineData(typeof(long))]     // S7 LInt; needs an explicit VarType
  [InlineData(typeof(decimal))]  // no S7 equivalent
  [InlineData(typeof(object))]   // only meaningful behind a ValueConverter
  public void Types_without_a_faithful_mapping_are_not_guessed(Type clrType)
  {
    // Previously these silently became VarType.DWord, reading four bytes from wherever the
    // address happened to point.
    Assert.Null(EntityTypeBuilder<object>.FromClrType(clrType));
  }
}
