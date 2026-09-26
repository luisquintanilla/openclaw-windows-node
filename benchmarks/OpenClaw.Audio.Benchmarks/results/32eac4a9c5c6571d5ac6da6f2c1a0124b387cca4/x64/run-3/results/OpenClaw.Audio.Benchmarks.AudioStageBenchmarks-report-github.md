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
| **&#39;copy + scalar stage&#39;**       | **Clipped**     | **39.789 μs** | **3.3496 μs** | **9.8763 μs** | **39.117 μs** |  **1.07** |    **0.39** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Clipped     | 20.450 μs | 0.8735 μs | 2.5618 μs | 20.145 μs |  0.55 |    0.16 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Clipped     | 14.134 μs | 0.7595 μs | 2.1422 μs | 13.722 μs |  0.38 |    0.11 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Clipped     | 14.459 μs | 0.5652 μs | 1.6578 μs | 14.355 μs |  0.39 |    0.11 |         - |          NA |
| &#39;copy + all stage&#39;          | Clipped     |  3.988 μs | 0.2095 μs | 0.5627 μs |  3.893 μs |  0.11 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback10**  | **36.833 μs** | **1.7784 μs** | **5.0450 μs** | **35.864 μs** |  **1.02** |    **0.19** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback10  | 23.631 μs | 1.4018 μs | 4.0669 μs | 23.522 μs |  0.65 |    0.14 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback10  | 12.720 μs | 0.8366 μs | 2.4403 μs | 12.361 μs |  0.35 |    0.08 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback10  | 14.136 μs | 0.5060 μs | 1.4919 μs | 13.851 μs |  0.39 |    0.07 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback10  |  4.637 μs | 0.0972 μs | 0.2851 μs |  4.658 μs |  0.13 |    0.02 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback100** | **29.152 μs** | **1.4082 μs** | **4.0629 μs** | **28.032 μs** |  **1.02** |    **0.19** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback100 | 20.813 μs | 0.8997 μs | 2.5670 μs | 21.215 μs |  0.73 |    0.13 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback100 | 19.920 μs | 0.4626 μs | 1.2740 μs | 20.072 μs |  0.70 |    0.10 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback100 | 12.263 μs | 0.2427 μs | 0.6135 μs | 12.191 μs |  0.43 |    0.06 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback100 | 13.293 μs | 0.3346 μs | 0.9601 μs | 13.280 μs |  0.46 |    0.07 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fallback50**  | **32.863 μs** | **2.6638 μs** | **7.8543 μs** | **28.998 μs** |  **1.05** |    **0.33** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fallback50  | 20.100 μs | 0.8023 μs | 2.3149 μs | 19.358 μs |  0.64 |    0.15 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fallback50  | 14.720 μs | 0.4459 μs | 1.2576 μs | 14.518 μs |  0.47 |    0.10 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fallback50  | 12.701 μs | 0.2737 μs | 0.7809 μs | 12.595 μs |  0.41 |    0.09 |         - |          NA |
| &#39;copy + all stage&#39;          | Fallback50  |  8.564 μs | 0.1688 μs | 0.3211 μs |  8.658 μs |  0.27 |    0.06 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Fixed**       | **55.337 μs** | **1.5256 μs** | **4.3278 μs** | **54.638 μs** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Fixed       | 28.605 μs | 0.5641 μs | 1.3624 μs | 28.643 μs |  0.52 |    0.05 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Fixed       | 33.361 μs | 2.1447 μs | 6.2900 μs | 31.494 μs |  0.61 |    0.12 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Fixed       |  9.914 μs | 0.4579 μs | 1.3502 μs | 10.158 μs |  0.18 |    0.03 |         - |          NA |
| &#39;copy + all stage&#39;          | Fixed       |  9.729 μs | 0.5422 μs | 1.5987 μs |  9.342 μs |  0.18 |    0.03 |         - |          NA |
|                             |             |           |           |           |           |       |         |           |             |
| **&#39;copy + scalar stage&#39;**       | **Ordinary**    | **33.355 μs** | **1.3891 μs** | **4.0958 μs** | **31.899 μs** |  **1.01** |    **0.17** |         **-** |          **NA** |
| &#39;copy + meter stage&#39;        | Ordinary    | 20.509 μs | 0.9600 μs | 2.8003 μs | 19.646 μs |  0.62 |    0.11 |         - |          NA |
| &#39;copy + meter + VAD stage&#39;  | Ordinary    | 12.630 μs | 0.7649 μs | 2.2433 μs | 12.195 μs |  0.38 |    0.08 |         - |          NA |
| &#39;copy + meter + gain stage&#39; | Ordinary    | 16.364 μs | 0.7541 μs | 2.2118 μs | 15.975 μs |  0.50 |    0.09 |         - |          NA |
| &#39;copy + all stage&#39;          | Ordinary    |  4.921 μs | 0.2747 μs | 0.8099 μs |  4.755 μs |  0.15 |    0.03 |         - |          NA |
