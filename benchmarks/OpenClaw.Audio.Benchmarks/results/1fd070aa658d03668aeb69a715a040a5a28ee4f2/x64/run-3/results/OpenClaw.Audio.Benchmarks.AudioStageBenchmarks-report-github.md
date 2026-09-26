```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9448/25H2/2025Update/HudsonValley2)
11th Gen Intel Core i9-11950H 2.60GHz (Max: 2.61GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]       : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  AudioDefault : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=AudioDefault  MaxRelativeError=0.02  PowerPlanMode=67b4a053-3646-4532-affd-0535c9ea82a7  
Runtime=.NET 10.0  

```
| Method                      | Scenario    | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------- |------------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **28.706 μs** | **0.5668 μs** | **0.6748 μs** |  **1.00** |    **0.03** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 19.176 μs | 0.3807 μs | 0.5082 μs |  0.67 |    0.02 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 12.618 μs | 0.2358 μs | 0.2807 μs |  0.44 |    0.01 |         - |          NA |
|                             |             |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **28.091 μs** | **0.5515 μs** | **0.9803 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 18.950 μs | 0.3712 μs | 0.7328 μs |  0.68 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 13.005 μs | 0.2449 μs | 0.4540 μs |  0.46 |    0.02 |         - |          NA |
|                             |             |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **28.222 μs** | **0.5433 μs** | **1.0468 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 19.338 μs | 0.3840 μs | 0.9201 μs |  0.69 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 12.878 μs | 0.2564 μs | 0.3916 μs |  0.46 |    0.02 |         - |          NA |
|                             |             |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **28.380 μs** | **0.5639 μs** | **1.1131 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 19.310 μs | 0.3862 μs | 0.7712 μs |  0.68 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 13.215 μs | 0.2469 μs | 0.5100 μs |  0.47 |    0.03 |         - |          NA |
|                             |             |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **55.725 μs** | **1.0624 μs** | **1.0434 μs** |  **1.00** |    **0.03** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 28.255 μs | 0.5631 μs | 1.1375 μs |  0.51 |    0.02 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  8.853 μs | 0.1723 μs | 0.1844 μs |  0.16 |    0.00 |         - |          NA |
|                             |             |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **28.328 μs** | **0.5574 μs** | **0.9763 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 19.503 μs | 0.3825 μs | 0.6993 μs |  0.69 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 12.901 μs | 0.2549 μs | 0.4397 μs |  0.46 |    0.02 |         - |          NA |
