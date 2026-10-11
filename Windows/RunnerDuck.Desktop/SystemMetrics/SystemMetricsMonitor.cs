/*
 SystemMetricsMonitor.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

using RunnerDuck.Core;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunnerDuck.App.SystemMetrics;

internal sealed class SystemMetricsMonitor : IDisposable
{
    private readonly CPUMonitor cpuMonitor = new();
    private readonly SystemMetricsSampler sampler;
    private readonly FormsTimer timer = new();
    private bool isSampling;
    private int generation;

    public SystemMetricsMonitor()
    {
        sampler = new SystemMetricsSampler(WindowsSystemMetrics.Environment(cpuMonitor));
        timer.Tick += async (_, _) => await SampleAsync();
    }

    public event Action<Metrics>? Updated;

    public Metrics Metrics { get; } = new();

    public SystemMetricsConfiguration Configuration { get; private set; } = new();

    public void Configure(SystemMetricsConfiguration configuration)
    {
        generation++;
        if (Configuration.MonitorsNetwork != configuration.MonitorsNetwork)
        {
            sampler.Reset();
        }
        Configuration = new SystemMetricsConfiguration
        {
            MonitorsMemory = configuration.MonitorsMemory,
            MonitorsStorage = configuration.MonitorsStorage,
            MonitorsBattery = configuration.MonitorsBattery,
            MonitorsNetwork = configuration.MonitorsNetwork,
        };
        Metrics.ApplyConfiguration(Configuration);
        Updated?.Invoke(Metrics);
    }

    public void Start(UpdateInterval interval)
    {
        generation++;
        sampler.Reset();
        cpuMonitor.Sample();
        timer.Interval = (int)interval * 1000;
        timer.Start();
    }

    public void Stop()
    {
        generation++;
        timer.Stop();
        sampler.Reset();
    }

    public void Dispose() => timer.Dispose();

    private async Task SampleAsync()
    {
        if (isSampling)
        {
            return;
        }
        isSampling = true;
        try
        {
            var configuration = Configuration;
            var sampleGeneration = generation;
            var bundle = await Task.Run(() => sampler.Sample(configuration));
            if (sampleGeneration != generation)
            {
                return;
            }
            Metrics.Update(bundle);
            Updated?.Invoke(Metrics);
        }
        catch (Exception exception)
        {
            Log.Write($"Sampling system metrics failed: {exception.Message}");
        }
        finally
        {
            isSampling = false;
        }
    }
}
