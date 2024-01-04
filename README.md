[![nuget](https://img.shields.io/nuget/vpre/Streamx.Linq.ExTree?label=ExTree)](https://www.nuget.org/packages/Streamx.Linq.ExTree)
[![nuget](https://img.shields.io/nuget/vpre/Streamx.Linq.SQL.EFCore?label=ELINQ%20EF%20Core)](https://www.nuget.org/packages/Streamx.Linq.SQL.EFCore)

# XLinq
- ExTree converts MSIL to LINQ expressions, allowing any method to be converted to [Expression Tree](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions) **dynamically in runtime**. The library exposes `ExpressionTree.Parse(...)` with several overloads, accepting a `Func<>`, `Delegate` and `MethodInfo`. Nuget: <sub>[![nuget](https://img.shields.io/nuget/vpre/Streamx.Linq.ExTree?label=ExTree)](https://www.nuget.org/packages/Streamx.Linq.ExTree)</sub>
- SQL.Net.EFCore implements Linq to SQL for EF Core. It allows you to use C# (or your .NET language of choice) to write strongly typed SQL queries. Interactive tutorial available at https://try.entitylinq.com/
