# GC.Csv

The library provide an extension method to the type _IEnumerable\<T\>_ which produces a CSV string.

```cs
string csv = items.ToCsvString(b =>
    b.AddColumn("Number", p => p.Num)
    .AddColumn("Date", p => p.DateNow.ToString("dd/MM/yyyy HH:mm"))
    .AddColumn("Tit", p => p.Title));

```
