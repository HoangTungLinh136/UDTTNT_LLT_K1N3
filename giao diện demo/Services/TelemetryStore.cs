using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace giao_dien_demo.Services
{
    public class ServerMetricPoint
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsagePercent { get; set; }
        public double RamUsageMb { get; set; }
        public double DiskUsagePercent { get; set; }
        public double NetworkInKbps { get; set; }
        public double NetworkOutKbps { get; set; }
    }

    public class ApiEndpointMetric
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public long TotalRequests { get; set; }
        public long TotalErrors { get; set; }
        public double AverageLatencyMs { get; set; }
        
        // P95 calculation
        private List<double> _latencies = new List<double>();
        private readonly object _lock = new object();

        public void AddLatency(double latencyMs)
        {
            lock (_lock)
            {
                _latencies.Add(latencyMs);
                if (_latencies.Count > 1000) // Keep last 1000 for P95/Avg
                {
                    _latencies.RemoveAt(0);
                }
                AverageLatencyMs = _latencies.Average();
            }
        }

        public double GetP95()
        {
            lock (_lock)
            {
                if (!_latencies.Any()) return 0;
                var sorted = _latencies.OrderBy(x => x).ToList();
                int idx = (int)Math.Ceiling(95.0 / 100.0 * sorted.Count) - 1;
                return sorted[idx];
            }
        }
    }

    public interface ITelemetryStore
    {
        void AddServerMetric(ServerMetricPoint point);
        List<ServerMetricPoint> GetServerMetrics();
        void RecordApiRequest(string method, string path, double latencyMs, bool isError);
        List<ApiEndpointMetric> GetApiMetrics();
    }

    public class TelemetryStore : ITelemetryStore
    {
        private readonly ConcurrentQueue<ServerMetricPoint> _serverMetrics = new ConcurrentQueue<ServerMetricPoint>();
        private readonly ConcurrentDictionary<string, ApiEndpointMetric> _apiMetrics = new ConcurrentDictionary<string, ApiEndpointMetric>();
        private const int MaxServerPoints = 120; // 10s per point -> 20 mins

        public void AddServerMetric(ServerMetricPoint point)
        {
            _serverMetrics.Enqueue(point);
            while (_serverMetrics.Count > MaxServerPoints)
            {
                _serverMetrics.TryDequeue(out _);
            }
        }

        public List<ServerMetricPoint> GetServerMetrics()
        {
            return _serverMetrics.ToList();
        }

        public void RecordApiRequest(string method, string path, double latencyMs, bool isError)
        {
            var key = $"{method} {path}";
            var metric = _apiMetrics.GetOrAdd(key, k => new ApiEndpointMetric { Method = method, Path = path });
            
            metric.TotalRequests++;
            if (isError) metric.TotalErrors++;
            metric.AddLatency(latencyMs);
        }

        public List<ApiEndpointMetric> GetApiMetrics()
        {
            return _apiMetrics.Values.ToList();
        }
    }
}
