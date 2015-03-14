
using System.Collections.Generic;
using System.Linq;

namespace PerfLogger
{
    using System;

    internal class LogSample
    {
        private static readonly log4net.ILog s_csvLog = log4net.LogManager.GetLogger("LogSample");
        private static readonly List<string> s_childProceList = new List<string>();

        private readonly Dictionary<string, float> m_childCpuUsage = new Dictionary<string, float>();
        private readonly Dictionary<string, long> m_childMemoryUsage = new Dictionary<string, long>();
        private readonly Dictionary<string, int> m_wmiIntegerCountersValues = new Dictionary<string, int>();

        public LogSample()
        {
            foreach (string process in s_childProceList)
            {
                m_childCpuUsage[process] = -1;
                m_childMemoryUsage[process] = -1;
            }
        }

        public static string LogHeader
        {
            get
            {
                var columns = new List<string>();
                columns.Add("Time");

                if (PerfLoggerSettings.Default.EnableSystemUsage)
                {
                    columns.Add("CPU%");
                    columns.Add("FreeMemMB");
                }

                columns.Add("ProcCPU%");
                columns.Add("ProcMemMB");

                if (PerfLoggerSettings.Default.EnableWmiCounters)
                {
                    columns.AddRange(PerfLoggerSettings.Default.WmiCounters.Cast<string>());
                }

                if (PerfLoggerSettings.Default.EnableChildServicesUsage)
                {
                    foreach (string process in s_childProceList)
                    {
                        columns.Add(string.Format("{0} CPU%", process));
                        columns.Add(string.Format("{0} MemMB", process));
                    }
                }

                return string.Join(",\t", columns);
            }
        }

        public float CpuUsage { get; set; }

        public float FreeMemory { get; set; }

        public float ProcessCpuUsage { get; set; }

        public long ProcessMemoryUsage { get; set; }

        public Dictionary<string, int> WmiIntegerCountersValues
        {
            get { return m_wmiIntegerCountersValues; }
        }

        public bool IsOverThreshold
        {
            get
            {
                int maxCpuUsage = (int)CpuUsage;
                long maxMemUsage = ProcessMemoryUsage;

                if (PerfLoggerSettings.Default.EnableChildServicesUsage && 
                    m_childCpuUsage.Count > 0 && 
                    m_childMemoryUsage.Count > 0)
                {
                    maxCpuUsage = Math.Max(maxCpuUsage, (int)m_childCpuUsage.Max(c => c.Value));
                    maxMemUsage = Math.Max(maxMemUsage, m_childMemoryUsage.Max(c => c.Value));                    
                }

                bool result = maxCpuUsage > PerfLoggerSettings.Default.CpuThreshold ||
                              maxMemUsage > PerfLoggerSettings.Default.MemoryThreshold;

                return result;
            }
        }

        public static void SetHeader(List<string> childProcessesList)
        {
            bool updated = false;

            foreach (string process in childProcessesList)
            {
                if (!s_childProceList.Contains(process))
                {
                    // Only append processes to not break csv
                    s_childProceList.Add(process);
                    updated = true;
                }
            }

            if (updated || childProcessesList.Count == 0)
            {
                // Update header only when process list changed
                s_csvLog.Debug(LogHeader);
            }
        }

        public void SetChildCpuUsage(Dictionary<string, float> values)
        {
            foreach (var childCpu in values)
            {
                m_childCpuUsage[childCpu.Key] = childCpu.Value;
            }
        }

        public void SetChildMemoryUsage(Dictionary<string, long> values)
        {
            foreach (var childMem in values)
            {
                m_childMemoryUsage[childMem.Key] = childMem.Value;
            }
        }

        public void Log()
        {
            if (IsOverThreshold)
            {
                s_csvLog.Debug(ToString());
            }
        }

        public override string ToString()
        {
            var columns = new List<string>();
            columns.Add(DateTime.Now.ToString("HH:mm:ss"));

            if (PerfLoggerSettings.Default.EnableSystemUsage)
            {
                columns.Add(string.Format("{0,4}", (int)Math.Ceiling(CpuUsage)));
                columns.Add(string.Format("{0,6}", (int)FreeMemory));
            }

            columns.Add(string.Format("{0,4}", (int)Math.Ceiling(ProcessCpuUsage)));
            columns.Add(string.Format("{0,6}", ProcessMemoryUsage));

            if (PerfLoggerSettings.Default.EnableWmiCounters)
            {
                columns.AddRange(WmiIntegerCountersValues.Values.Select(v => string.Format("{0,5}", v)));
            }

            if (PerfLoggerSettings.Default.EnableChildServicesUsage)
            {
                foreach (string process in s_childProceList)
                {
                    columns.Add(string.Format("{0,4}", (int)Math.Ceiling(m_childCpuUsage[process])));
                    columns.Add(string.Format("{0,6}", m_childMemoryUsage[process]));
                }
            }

            string result = string.Join(",", columns);
            Console.WriteLine(result);

            return result;
        }
    }
}