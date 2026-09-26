```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9448/25H2/2025Update/HudsonValley2)
11th Gen Intel Core i9-11950H 2.60GHz (Max: 2.61GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]       : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  AudioDefault : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=AudioDefault  MaxRelativeError=0.02  PowerPlanMode=67b4a053-3646-4532-affd-0535c9ea82a7  
Runtime=.NET 10.0  

```
| Method                   | Length | Profile | Mean        | Error      | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------- |-------- |------------:|-----------:|-----------:|------------:|------:|--------:|----------:|------------:|
| **&#39;copy + scalar gain&#39;**     | **480**    | **Clipped** |   **352.99 ns** |   **8.300 ns** |  **24.474 ns** |   **361.20 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Clipped |    66.78 ns |   1.691 ns |   4.987 ns |    66.96 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Clipped |    18.71 ns |   0.415 ns |   1.064 ns |    18.82 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Low**     |   **387.26 ns** |   **8.188 ns** |  **24.144 ns** |   **394.78 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Low     |    72.19 ns |   1.625 ns |   4.791 ns |    73.54 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Low     |    19.10 ns |   0.472 ns |   1.391 ns |    19.39 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Speech**  |   **390.70 ns** |   **8.546 ns** |  **25.199 ns** |   **393.86 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Speech  |    75.20 ns |   1.892 ns |   5.579 ns |    77.15 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Speech  |    18.76 ns |   0.423 ns |   1.219 ns |    19.24 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Clipped** |   **369.36 ns** |   **7.599 ns** |  **22.288 ns** |   **376.71 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Clipped |    74.28 ns |   1.669 ns |   4.921 ns |    75.79 ns |  0.20 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Clipped |    19.60 ns |   0.506 ns |   1.460 ns |    19.42 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Low**     |   **415.22 ns** |   **9.054 ns** |  **26.696 ns** |   **419.82 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Low     |    79.12 ns |   2.205 ns |   6.500 ns |    81.01 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Low     |    19.15 ns |   0.502 ns |   1.479 ns |    19.68 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Speech**  |   **412.10 ns** |  **11.001 ns** |  **29.554 ns** |   **421.01 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Speech  |    77.87 ns |   2.216 ns |   6.535 ns |    77.75 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Speech  |    18.71 ns |   0.502 ns |   1.481 ns |    17.89 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Clipped** |   **894.51 ns** |  **23.057 ns** |  **64.275 ns** |   **900.30 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Clipped |   159.84 ns |   8.193 ns |  24.029 ns |   154.75 ns |  0.18 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Clipped |    38.71 ns |   0.977 ns |   2.773 ns |    37.70 ns |  0.04 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Low**     |   **699.45 ns** |  **20.952 ns** |  **57.707 ns** |   **719.47 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Low     |   149.08 ns |   4.308 ns |  12.703 ns |   143.53 ns |  0.21 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Low     |    40.39 ns |   1.030 ns |   3.036 ns |    38.89 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Speech**  |   **670.69 ns** |  **17.369 ns** |  **50.940 ns** |   **683.16 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Speech  |   168.85 ns |   8.371 ns |  24.681 ns |   163.88 ns |  0.25 |    0.04 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Speech  |    40.28 ns |   1.381 ns |   3.872 ns |    39.68 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Clipped** | **1,427.88 ns** |  **66.526 ns** | **196.154 ns** | **1,380.04 ns** |  **1.02** |    **0.19** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Clipped |   219.74 ns |   4.251 ns |   3.550 ns |   219.63 ns |  0.16 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Clipped |    66.78 ns |   4.148 ns |  12.229 ns |    63.69 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Low**     | **1,292.98 ns** |  **52.072 ns** | **151.071 ns** | **1,252.21 ns** |  **1.01** |    **0.16** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Low     |   277.58 ns |   8.343 ns |  23.532 ns |   276.55 ns |  0.22 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Low     |    68.60 ns |   2.330 ns |   6.685 ns |    66.50 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Speech**  | **1,469.12 ns** |  **88.032 ns** | **255.396 ns** | **1,405.41 ns** |  **1.03** |    **0.24** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Speech  |   264.38 ns |   6.942 ns |  20.140 ns |   263.96 ns |  0.19 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Speech  |    73.42 ns |   3.507 ns |  10.006 ns |    71.78 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Clipped** | **4,542.04 ns** | **315.856 ns** | **916.357 ns** | **4,397.37 ns** |  **1.04** |    **0.29** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Clipped |   688.57 ns |  32.703 ns |  95.395 ns |   667.57 ns |  0.16 |    0.04 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Clipped |   241.73 ns |   8.836 ns |  26.053 ns |   233.78 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Low**     | **2,867.62 ns** |  **81.605 ns** | **238.045 ns** | **2,780.97 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Low     |   579.77 ns |  11.560 ns |  10.247 ns |   578.14 ns |  0.20 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Low     |   203.41 ns |   4.478 ns |  13.135 ns |   198.43 ns |  0.07 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Speech**  | **3,023.20 ns** |  **60.000 ns** | **160.153 ns** | **3,088.77 ns** |  **1.00** |    **0.08** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Speech  |   673.10 ns |  20.515 ns |  60.489 ns |   684.31 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Speech  |   242.06 ns |  10.290 ns |  30.341 ns |   232.14 ns |  0.08 |    0.01 |         - |          NA |
