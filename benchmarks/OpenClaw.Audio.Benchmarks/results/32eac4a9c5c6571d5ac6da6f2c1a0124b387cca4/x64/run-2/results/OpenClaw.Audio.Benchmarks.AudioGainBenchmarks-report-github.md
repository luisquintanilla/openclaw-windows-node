```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9448/25H2/2025Update/HudsonValley2)
11th Gen Intel Core i9-11950H 2.60GHz (Max: 2.61GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]       : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  AudioDefault : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=AudioDefault  MaxRelativeError=0.02  PowerPlanMode=67b4a053-3646-4532-affd-0535c9ea82a7  
Runtime=.NET 10.0  

```
| Method                   | Length | Profile | Mean        | Error     | StdDev     | Median      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------- |-------- |------------:|----------:|-----------:|------------:|------:|--------:|----------:|------------:|
| **&#39;copy + scalar gain&#39;**     | **480**    | **Clipped** |   **363.69 ns** |  **9.710 ns** |  **28.631 ns** |   **372.79 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Clipped |    70.12 ns |  2.146 ns |   6.259 ns |    70.60 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Clipped |    17.98 ns |  0.461 ns |   1.358 ns |    18.12 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Low**     |   **393.71 ns** | **11.488 ns** |  **33.873 ns** |   **388.78 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Low     |    72.90 ns |  1.980 ns |   5.839 ns |    75.02 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Low     |    17.83 ns |  0.506 ns |   1.493 ns |    18.37 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Speech**  |   **386.35 ns** | **10.160 ns** |  **29.958 ns** |   **399.01 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Speech  |    72.84 ns |  2.078 ns |   6.128 ns |    75.27 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Speech  |    18.86 ns |  0.573 ns |   1.691 ns |    19.64 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Clipped** |   **372.15 ns** | **11.580 ns** |  **34.144 ns** |   **382.62 ns** |  **1.01** |    **0.13** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Clipped |    72.41 ns |  2.099 ns |   6.189 ns |    75.24 ns |  0.20 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Clipped |    18.44 ns |  0.509 ns |   1.499 ns |    18.42 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Low**     |   **408.29 ns** | **10.862 ns** |  **32.026 ns** |   **415.11 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Low     |    75.20 ns |  2.266 ns |   6.683 ns |    76.86 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Low     |    19.34 ns |  0.545 ns |   1.608 ns |    20.14 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Speech**  |   **410.49 ns** | **11.700 ns** |  **34.498 ns** |   **425.84 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Speech  |    74.72 ns |  2.309 ns |   6.772 ns |    70.29 ns |  0.18 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Speech  |    19.56 ns |  0.533 ns |   1.571 ns |    19.48 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Clipped** |   **847.57 ns** | **21.027 ns** |  **62.000 ns** |   **862.48 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Clipped |   142.70 ns |  3.889 ns |  11.467 ns |   147.07 ns |  0.17 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Clipped |    39.41 ns |  1.092 ns |   3.218 ns |    40.31 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Low**     |   **670.56 ns** | **16.197 ns** |  **47.756 ns** |   **691.90 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Low     |   149.98 ns |  3.901 ns |  11.504 ns |   155.54 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Low     |    37.88 ns |  1.047 ns |   3.088 ns |    38.98 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Speech**  |   **667.88 ns** | **16.405 ns** |  **48.113 ns** |   **689.57 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Speech  |   150.74 ns |  3.869 ns |  11.409 ns |   155.77 ns |  0.23 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Speech  |    39.71 ns |  1.104 ns |   3.256 ns |    41.00 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Clipped** | **1,306.20 ns** | **34.268 ns** | **101.039 ns** | **1,317.38 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Clipped |   238.05 ns |  7.169 ns |  21.138 ns |   240.80 ns |  0.18 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Clipped |    59.94 ns |  1.519 ns |   4.477 ns |    61.51 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Low**     | **1,098.95 ns** | **30.089 ns** |  **88.717 ns** | **1,061.97 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Low     |   244.14 ns |  6.586 ns |  19.420 ns |   245.48 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Low     |    57.54 ns |  1.767 ns |   5.209 ns |    54.99 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Speech**  | **1,107.47 ns** | **28.034 ns** |  **82.659 ns** | **1,135.76 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Speech  |   238.89 ns |  6.305 ns |  18.592 ns |   227.19 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Speech  |    60.55 ns |  1.661 ns |   4.898 ns |    62.52 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Clipped** | **3,465.33 ns** | **84.173 ns** | **248.186 ns** | **3,571.86 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Clipped |   599.07 ns | 15.476 ns |  45.632 ns |   580.52 ns |  0.17 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Clipped |   209.27 ns |  4.230 ns |  11.792 ns |   212.43 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Low**     | **2,840.86 ns** | **68.828 ns** | **202.940 ns** | **2,917.66 ns** |  **1.01** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Low     |   583.70 ns | 10.565 ns |   8.822 ns |   580.73 ns |  0.21 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Low     |   214.67 ns |  6.988 ns |  20.275 ns |   211.19 ns |  0.08 |    0.01 |         - |          NA |
|                          |        |         |             |           |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Speech**  | **2,850.78 ns** | **79.829 ns** | **235.376 ns** | **2,796.99 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Speech  |   632.20 ns | 16.091 ns |  45.121 ns |   620.16 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Speech  |   210.60 ns |  4.688 ns |  13.601 ns |   206.25 ns |  0.07 |    0.01 |         - |          NA |
