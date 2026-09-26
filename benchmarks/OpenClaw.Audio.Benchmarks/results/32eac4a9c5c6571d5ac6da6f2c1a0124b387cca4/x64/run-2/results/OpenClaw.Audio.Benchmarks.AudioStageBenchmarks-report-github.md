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
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **27.293 μs** | **0.5440 μs** | **1.4613 μs** | **26.978 μs** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 18.531 μs | 0.3688 μs | 1.0522 μs | 18.120 μs |  0.68 |    0.05 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Clipped     |  9.815 μs | 0.2312 μs | 0.6816 μs | 10.065 μs |  0.36 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 12.421 μs | 0.2413 μs | 0.3965 μs | 12.571 μs |  0.46 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Clipped     |  3.825 μs | 0.1154 μs | 0.3312 μs |  3.872 μs |  0.14 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **27.083 μs** | **0.5404 μs** | **1.5850 μs** | **27.678 μs** |  **1.00** |    **0.08** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 18.150 μs | 0.3991 μs | 1.1766 μs | 18.523 μs |  0.67 |    0.06 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback10  | 10.849 μs | 0.2140 μs | 0.5368 μs | 10.974 μs |  0.40 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 12.390 μs | 0.2453 μs | 0.5782 μs | 12.541 μs |  0.46 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback10  |  4.506 μs | 0.0899 μs | 0.2650 μs |  4.610 μs |  0.17 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **26.144 μs** | **0.5179 μs** | **1.3088 μs** | **26.340 μs** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 17.863 μs | 0.3570 μs | 0.9713 μs | 17.962 μs |  0.68 |    0.05 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback100 | 18.122 μs | 0.3606 μs | 0.9872 μs | 18.461 μs |  0.69 |    0.05 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 12.082 μs | 0.2385 μs | 0.5760 μs | 12.161 μs |  0.46 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback100 | 12.447 μs | 0.2482 μs | 0.7200 μs | 12.419 μs |  0.48 |    0.04 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **26.669 μs** | **0.5299 μs** | **0.9954 μs** | **26.934 μs** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 17.965 μs | 0.3588 μs | 0.9942 μs | 18.326 μs |  0.67 |    0.05 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback50  | 13.667 μs | 0.2728 μs | 0.7827 μs | 13.599 μs |  0.51 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 12.187 μs | 0.2436 μs | 0.6375 μs | 12.243 μs |  0.46 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback50  |  8.152 μs | 0.1623 μs | 0.3825 μs |  8.280 μs |  0.31 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **50.569 μs** | **1.0186 μs** | **3.0034 μs** | **50.120 μs** |  **1.00** |    **0.08** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 26.114 μs | 0.5180 μs | 1.5191 μs | 26.328 μs |  0.52 |    0.04 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fixed       | 25.954 μs | 0.5191 μs | 1.4809 μs | 25.980 μs |  0.51 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  7.984 μs | 0.1905 μs | 0.5618 μs |  8.125 μs |  0.16 |    0.01 |         - |          NA |
| &#39;copy + all stage&#39;          | Fixed       |  8.009 μs | 0.1875 μs | 0.5528 μs |  8.028 μs |  0.16 |    0.01 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **26.594 μs** | **0.5269 μs** | **1.3220 μs** | **27.002 μs** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 18.093 μs | 0.3605 μs | 1.0109 μs | 18.438 μs |  0.68 |    0.05 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Ordinary    |  9.559 μs | 0.2104 μs | 0.6205 μs |  9.735 μs |  0.36 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 12.420 μs | 0.2480 μs | 0.6222 μs | 12.570 μs |  0.47 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Ordinary    |  3.784 μs | 0.0869 μs | 0.2561 μs |  3.807 μs |  0.14 |    0.01 |         - |          NA |
