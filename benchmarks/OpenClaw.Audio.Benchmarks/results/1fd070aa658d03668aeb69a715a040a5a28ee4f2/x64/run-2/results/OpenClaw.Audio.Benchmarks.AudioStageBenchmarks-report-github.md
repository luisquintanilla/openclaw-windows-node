```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9448/25H2/2025Update/HudsonValley2)
11th Gen Intel Core i9-11950H 2.60GHz (Max: 2.61GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]       : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  AudioDefault : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=AudioDefault  MaxRelativeError=0.02  PowerPlanMode=67b4a053-3646-4532-affd-0535c9ea82a7  
Runtime=.NET 10.0  

```
| Method                      | Scenario    | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------- |------------ |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **29.339 μs** | **0.4818 μs** | **0.4948 μs** | **29.383 μs** |  **1.00** |    **0.02** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 19.454 μs | 0.3876 μs | 0.7374 μs | 19.731 μs |  0.66 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 12.822 μs | 0.2554 μs | 0.3744 μs | 12.766 μs |  0.44 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **28.269 μs** | **0.5577 μs** | **0.5967 μs** | **28.414 μs** |  **1.00** |    **0.03** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 19.373 μs | 0.3865 μs | 0.6017 μs | 19.599 μs |  0.69 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 13.048 μs | 0.2585 μs | 0.3539 μs | 13.070 μs |  0.46 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **28.283 μs** | **0.4576 μs** | **1.0421 μs** | **28.212 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 19.125 μs | 0.3756 μs | 0.5736 μs | 19.191 μs |  0.68 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 12.976 μs | 0.2550 μs | 0.3657 μs | 13.094 μs |  0.46 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **28.096 μs** | **0.5607 μs** | **0.4682 μs** | **28.121 μs** |  **1.00** |    **0.02** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 19.186 μs | 0.3743 μs | 0.6939 μs | 19.443 μs |  0.68 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 13.060 μs | 0.2573 μs | 0.3772 μs | 13.200 μs |  0.46 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **55.694 μs** | **1.1003 μs** | **1.0806 μs** | **55.498 μs** |  **1.00** |    **0.03** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 28.019 μs | 0.5583 μs | 1.1150 μs | 28.343 μs |  0.50 |    0.02 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  8.622 μs | 0.1720 μs | 0.4591 μs |  8.760 μs |  0.15 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **28.243 μs** | **0.5521 μs** | **0.8595 μs** | **28.464 μs** |  **1.00** |    **0.04** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 19.421 μs | 0.3786 μs | 0.6326 μs | 19.665 μs |  0.69 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 13.109 μs | 0.2607 μs | 0.4566 μs | 13.190 μs |  0.46 |    0.02 |         - |          NA |
