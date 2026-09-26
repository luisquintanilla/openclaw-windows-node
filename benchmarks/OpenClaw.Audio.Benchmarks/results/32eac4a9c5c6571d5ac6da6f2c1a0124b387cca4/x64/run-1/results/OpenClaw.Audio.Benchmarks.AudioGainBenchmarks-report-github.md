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
| **&#39;copy + scalar gain&#39;**     | **480**    | **Clipped** |   **378.28 ns** |   **9.835 ns** |  **27.899 ns** |   **383.60 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Clipped |    72.21 ns |   2.014 ns |   5.938 ns |    73.70 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Clipped |    17.32 ns |   0.410 ns |   0.974 ns |    17.61 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Low**     |   **415.22 ns** |   **8.260 ns** |  **18.131 ns** |   **421.68 ns** |  **1.00** |    **0.06** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Low     |    75.65 ns |   1.743 ns |   5.140 ns |    77.74 ns |  0.18 |    0.01 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Low     |    19.80 ns |   0.442 ns |   1.093 ns |    20.18 ns |  0.05 |    0.00 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **480**    | **Speech**  |   **382.47 ns** |   **9.370 ns** |  **27.481 ns** |   **368.85 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 480    | Speech  |    75.80 ns |   1.993 ns |   5.876 ns |    77.96 ns |  0.20 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 480    | Speech  |    20.41 ns |   1.014 ns |   2.910 ns |    19.47 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Clipped** |   **377.46 ns** |  **10.160 ns** |  **29.151 ns** |   **369.41 ns** |  **1.01** |    **0.11** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Clipped |    72.10 ns |   1.937 ns |   5.621 ns |    71.29 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Clipped |    19.86 ns |   0.494 ns |   1.326 ns |    20.02 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Low**     |   **450.55 ns** |  **10.576 ns** |  **29.128 ns** |   **454.38 ns** |  **1.00** |    **0.09** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Low     |    85.81 ns |   2.590 ns |   7.554 ns |    87.58 ns |  0.19 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Low     |    20.23 ns |   0.751 ns |   2.093 ns |    20.21 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **512**    | **Speech**  |   **614.45 ns** |  **62.722 ns** | **184.938 ns** |   **621.41 ns** |  **1.09** |    **0.47** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 512    | Speech  |   147.29 ns |  10.296 ns |  30.359 ns |   137.95 ns |  0.26 |    0.10 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 512    | Speech  |    22.33 ns |   1.814 ns |   5.349 ns |    19.26 ns |  0.04 |    0.02 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Clipped** |   **852.57 ns** |  **26.756 ns** |  **76.338 ns** |   **834.33 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Clipped |   151.39 ns |   5.798 ns |  16.543 ns |   149.53 ns |  0.18 |    0.02 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Clipped |    54.97 ns |   5.207 ns |  15.352 ns |    49.97 ns |  0.06 |    0.02 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Low**     |   **876.17 ns** |  **61.975 ns** | **180.784 ns** |   **820.84 ns** |  **1.04** |    **0.29** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Low     |   163.53 ns |   6.097 ns |  17.197 ns |   159.04 ns |  0.19 |    0.04 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Low     |    42.48 ns |   1.233 ns |   3.376 ns |    41.81 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **960**    | **Speech**  |   **743.33 ns** |  **33.942 ns** |  **96.838 ns** |   **726.98 ns** |  **1.02** |    **0.18** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 960    | Speech  |   182.30 ns |   9.943 ns |  29.317 ns |   179.63 ns |  0.25 |    0.05 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 960    | Speech  |    47.15 ns |   2.978 ns |   8.781 ns |    45.23 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Clipped** | **1,391.96 ns** |  **43.197 ns** | **126.688 ns** | **1,365.02 ns** |  **1.01** |    **0.13** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Clipped |   278.29 ns |  13.633 ns |  40.197 ns |   273.21 ns |  0.20 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Clipped |    65.45 ns |   2.243 ns |   6.364 ns |    65.02 ns |  0.05 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Low**     | **1,174.45 ns** |  **27.501 ns** |  **81.088 ns** | **1,205.49 ns** |  **1.00** |    **0.10** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Low     |   248.55 ns |   8.153 ns |  24.038 ns |   238.70 ns |  0.21 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Low     |    73.12 ns |   5.203 ns |  15.013 ns |    67.99 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **1600**   | **Speech**  | **1,251.23 ns** |  **66.635 ns** | **186.851 ns** | **1,229.94 ns** |  **1.02** |    **0.20** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 1600   | Speech  |   356.20 ns |  27.626 ns |  81.456 ns |   343.62 ns |  0.29 |    0.08 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 1600   | Speech  |    85.39 ns |   7.406 ns |  21.836 ns |    77.41 ns |  0.07 |    0.02 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Clipped** | **4,165.83 ns** | **209.896 ns** | **592.016 ns** | **3,996.13 ns** |  **1.02** |    **0.20** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Clipped |   705.23 ns |  33.391 ns |  98.454 ns |   673.90 ns |  0.17 |    0.03 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Clipped |   240.26 ns |   9.129 ns |  26.628 ns |   240.25 ns |  0.06 |    0.01 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Low**     | **3,714.29 ns** | **259.566 ns** | **753.048 ns** | **3,490.57 ns** |  **1.04** |    **0.29** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Low     |   833.12 ns |  52.513 ns | 154.837 ns |   800.52 ns |  0.23 |    0.06 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Low     |   292.16 ns |   9.429 ns |  27.801 ns |   294.31 ns |  0.08 |    0.02 |         - |          NA |
|                          |        |         |             |            |            |             |       |         |           |             |
| **&#39;copy + scalar gain&#39;**     | **4096**   | **Speech**  | **3,809.88 ns** | **180.207 ns** | **519.939 ns** | **3,704.03 ns** |  **1.02** |    **0.19** |         **-** |          **NA** |
| &#39;copy + tensor gain&#39;     | 4096   | Speech  |   720.10 ns |  31.839 ns |  90.840 ns |   694.13 ns |  0.19 |    0.04 |         - |          NA |
| &#39;copy only (diagnostic)&#39; | 4096   | Speech  |   268.65 ns |  12.280 ns |  36.208 ns |   267.91 ns |  0.07 |    0.01 |         - |          NA |
