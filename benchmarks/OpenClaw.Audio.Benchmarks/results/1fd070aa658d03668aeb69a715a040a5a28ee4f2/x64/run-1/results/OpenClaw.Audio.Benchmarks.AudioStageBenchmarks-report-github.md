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
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **27.257 μs** | **0.5407 μs** | **1.2850 μs** | **26.881 μs** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 18.980 μs | 0.3780 μs | 1.0220 μs | 18.854 μs |  0.70 |    0.05 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 12.126 μs | 0.2419 μs | 0.5258 μs | 11.973 μs |  0.45 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **28.114 μs** | **0.9331 μs** | **2.5543 μs** | **27.397 μs** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 18.035 μs | 0.3516 μs | 0.5678 μs | 17.882 μs |  0.65 |    0.06 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 12.522 μs | 0.2498 μs | 0.6268 μs | 12.292 μs |  0.45 |    0.04 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **27.123 μs** | **0.5494 μs** | **1.5314 μs** | **26.808 μs** |  **1.00** |    **0.08** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 18.193 μs | 0.3623 μs | 0.8751 μs | 17.942 μs |  0.67 |    0.05 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 12.298 μs | 0.2432 μs | 0.6012 μs | 12.130 μs |  0.45 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **27.227 μs** | **0.5425 μs** | **1.4850 μs** | **27.150 μs** |  **1.00** |    **0.08** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 18.558 μs | 0.3695 μs | 1.0301 μs | 18.214 μs |  0.68 |    0.05 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 12.684 μs | 0.2528 μs | 0.7004 μs | 12.561 μs |  0.47 |    0.04 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **56.292 μs** | **1.1197 μs** | **1.8708 μs** | **56.083 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 27.749 μs | 0.5487 μs | 1.1332 μs | 27.507 μs |  0.49 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  8.602 μs | 0.1703 μs | 0.4457 μs |  8.540 μs |  0.15 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **29.626 μs** | **0.6993 μs** | **2.0618 μs** | **28.899 μs** |  **1.00** |    **0.10** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 21.779 μs | 0.6705 μs | 1.9238 μs | 22.455 μs |  0.74 |    0.08 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 15.539 μs | 0.2623 μs | 0.2454 μs | 15.523 μs |  0.53 |    0.04 |         - |          NA |
