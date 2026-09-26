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
| **&#39;copy + scalar gain&#39;**     | **480**    | **Clipped** |   **371.96 ns** |  **11.074 ns** |  **31.594 ns** |   **367.18 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Clipped |    79.29 ns |   1.582 ns |   2.270 ns |    79.01 ns |  0.21 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Clipped |    18.02 ns |   0.411 ns |   0.489 ns |    18.07 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Low**     |   **411.62 ns** |   **8.191 ns** |  **17.278 ns** |   **408.03 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Low     |    78.36 ns |   1.694 ns |   4.996 ns |    77.75 ns |  0.19 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Low     |    20.28 ns |   0.460 ns |   0.930 ns |    20.19 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Speech**  |   **414.91 ns** |   **8.290 ns** |  **19.049 ns** |   **415.41 ns** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Speech  |    73.80 ns |   1.575 ns |   4.644 ns |    72.96 ns |  0.18 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Speech  |    17.99 ns |   0.414 ns |   0.845 ns |    17.79 ns |  0.04 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Clipped** |   **393.90 ns** |   **7.875 ns** |  **13.583 ns** |   **394.34 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Clipped |    73.51 ns |   1.512 ns |   3.738 ns |    72.54 ns |  0.19 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Clipped |    19.82 ns |   0.397 ns |   0.652 ns |    19.67 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Low**     |   **435.82 ns** |   **8.740 ns** |  **20.431 ns** |   **435.32 ns** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Low     |    81.95 ns |   1.745 ns |   5.145 ns |    80.95 ns |  0.19 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Low     |    20.45 ns |   0.460 ns |   0.794 ns |    20.45 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Speech**  |   **432.60 ns** |   **8.707 ns** |  **17.786 ns** |   **432.36 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Speech  |    72.67 ns |   1.878 ns |   5.536 ns |    71.75 ns |  0.17 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Speech  |    19.58 ns |   0.373 ns |   0.349 ns |    19.44 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Clipped** |   **884.05 ns** |  **17.605 ns** |  **27.924 ns** |   **889.23 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Clipped |   148.95 ns |   3.026 ns |   5.684 ns |   147.97 ns |  0.17 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Clipped |    44.70 ns |   0.938 ns |   1.220 ns |    44.66 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Low**     |   **713.75 ns** |  **13.712 ns** |  **27.384 ns** |   **718.08 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Low     |   153.04 ns |   3.008 ns |   8.080 ns |   151.57 ns |  0.21 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Low     |    43.25 ns |   0.906 ns |   1.514 ns |    43.45 ns |  0.06 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Speech**  |   **719.93 ns** |  **14.276 ns** |  **29.161 ns** |   **728.22 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Speech  |   154.61 ns |   3.444 ns |  10.155 ns |   158.74 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Speech  |    45.58 ns |   0.957 ns |   1.911 ns |    45.82 ns |  0.06 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Clipped** | **1,436.08 ns** |  **28.458 ns** |  **56.173 ns** | **1,454.84 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Clipped |   247.06 ns |   5.005 ns |  14.198 ns |   254.04 ns |  0.17 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Clipped |    63.77 ns |   1.309 ns |   2.393 ns |    64.48 ns |  0.04 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Low**     | **1,202.28 ns** |  **24.078 ns** |  **55.324 ns** | **1,223.58 ns** |  **1.00** |    **0.07** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Low     |   258.33 ns |   5.268 ns |  15.534 ns |   265.29 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Low     |    64.37 ns |   1.320 ns |   1.716 ns |    64.78 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Speech**  | **1,192.59 ns** |  **23.835 ns** |  **53.800 ns** | **1,215.65 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Speech  |   261.37 ns |   5.314 ns |  15.668 ns |   268.18 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Speech  |    64.12 ns |   1.328 ns |   3.051 ns |    65.55 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Clipped** | **3,774.22 ns** |  **74.672 ns** | **130.781 ns** | **3,821.44 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Clipped |   640.23 ns |  12.819 ns |  28.935 ns |   644.77 ns |  0.17 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Clipped |   526.94 ns |   9.938 ns |  12.204 ns |   523.86 ns |  0.14 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Low**     | **3,092.52 ns** |  **61.533 ns** |  **95.799 ns** | **3,107.69 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Low     |   674.49 ns |  15.130 ns |  44.375 ns |   684.79 ns |  0.22 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Low     |   258.44 ns |   5.146 ns |   9.915 ns |   259.43 ns |  0.08 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Speech**  | **3,659.01 ns** | **145.392 ns** | **428.693 ns** | **3,626.88 ns** |  **1.01** |    **0.17** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Speech  | 1,113.02 ns |  21.996 ns |  40.771 ns | 1,119.21 ns |  0.31 |    0.04 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Speech  |   245.29 ns |   6.863 ns |  19.801 ns |   239.43 ns |  0.07 |    0.01 |         - |          NA |
