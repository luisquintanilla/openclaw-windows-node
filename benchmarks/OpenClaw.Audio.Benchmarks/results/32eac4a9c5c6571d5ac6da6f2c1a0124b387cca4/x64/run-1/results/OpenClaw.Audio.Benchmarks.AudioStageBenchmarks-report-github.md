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
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **31.918 μs** | **1.8503 μs** | **5.4556 μs** | **29.992 μs** |  **1.03** |    **0.24** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 18.971 μs | 0.5412 μs | 1.5702 μs | 18.583 μs |  0.61 |    0.11 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Clipped     | 11.866 μs | 0.7236 μs | 2.0878 μs | 11.124 μs |  0.38 |    0.09 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 13.296 μs | 0.3992 μs | 1.1261 μs | 13.162 μs |  0.43 |    0.08 |         - |          NA |
| &#39;copy + all stage&#39;          | Clipped     |  4.328 μs | 0.1688 μs | 0.4816 μs |  4.264 μs |  0.14 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **27.489 μs** | **0.7632 μs** | **2.1402 μs** | **27.070 μs** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 18.626 μs | 0.4753 μs | 1.3407 μs | 18.466 μs |  0.68 |    0.07 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback10  | 10.417 μs | 0.1850 μs | 0.2987 μs | 10.466 μs |  0.38 |    0.03 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 12.593 μs | 0.2787 μs | 0.8217 μs | 12.377 μs |  0.46 |    0.05 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback10  |  4.704 μs | 0.1170 μs | 0.3450 μs |  4.737 μs |  0.17 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **27.166 μs** | **0.6588 μs** | **1.9426 μs** | **27.332 μs** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 18.152 μs | 0.4288 μs | 1.2509 μs | 18.641 μs |  0.67 |    0.07 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback100 | 22.015 μs | 1.5841 μs | 4.5706 μs | 20.569 μs |  0.81 |    0.18 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 14.063 μs | 0.4676 μs | 1.3566 μs | 13.650 μs |  0.52 |    0.06 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback100 | 12.824 μs | 0.3023 μs | 0.8575 μs | 13.028 μs |  0.47 |    0.05 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **28.621 μs** | **1.0576 μs** | **3.0849 μs** | **28.029 μs** |  **1.01** |    **0.15** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 18.413 μs | 0.3912 μs | 1.1535 μs | 18.938 μs |  0.65 |    0.08 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback50  | 14.295 μs | 0.3082 μs | 0.9088 μs | 14.526 μs |  0.50 |    0.06 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 12.783 μs | 0.3782 μs | 1.0851 μs | 12.827 μs |  0.45 |    0.06 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback50  |  8.035 μs | 0.1659 μs | 0.4891 μs |  7.866 μs |  0.28 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **53.046 μs** | **1.8540 μs** | **5.2595 μs** | **51.382 μs** |  **1.01** |    **0.14** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 27.055 μs | 0.7393 μs | 2.0732 μs | 26.621 μs |  0.51 |    0.06 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fixed       | 26.585 μs | 0.5309 μs | 1.4799 μs | 26.436 μs |  0.51 |    0.05 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  8.113 μs | 0.2531 μs | 0.7383 μs |  7.808 μs |  0.15 |    0.02 |         - |          NA |
| &#39;copy + all stage&#39;          | Fixed       |  8.541 μs | 0.2091 μs | 0.6033 μs |  8.678 μs |  0.16 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **27.541 μs** | **0.6018 μs** | **1.7266 μs** | **27.628 μs** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 20.884 μs | 0.6910 μs | 1.9825 μs | 20.902 μs |  0.76 |    0.09 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Ordinary    | 10.159 μs | 0.2653 μs | 0.7821 μs | 10.342 μs |  0.37 |    0.04 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 12.486 μs | 0.3249 μs | 0.9578 μs | 12.315 μs |  0.46 |    0.04 |         - |          NA |
| &#39;copy + all stage&#39;          | Ordinary    |  3.939 μs | 0.1299 μs | 0.3726 μs |  3.905 μs |  0.14 |    0.02 |         - |          NA |
