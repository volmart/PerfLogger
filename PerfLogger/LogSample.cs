
namespace PerfLogger
{
    using System;

    internal class LogSample
    {
        private static readonly log4net.ILog s_csvLog = log4net.LogManager.GetLogger("LogSample");

        static LogSample()
        {
            s_csvLog.Debug(LogSample.LogHeader);        
        }

        public static string LogHeader
        {
            get
            {
                var columns = new System.Collections.Generic.List<string>();
                columns.Add("Time");

                if (PerfLoggerSettings.Default.EnableSystemUsage)
                {
                    columns.Add("CPU%");
                    columns.Add("FreeMemMB");
                }

                columns.Add("ProcCPU%");
                columns.Add("ProcMemMB");

                if (PerfLoggerSettings.Default.EnableChildServicesUsage)
                {
                    columns.Add("ChildCPU%");
                    columns.Add("ChildMemMB");
                }

                return string.Join(",\t", columns);
            }
        }

        public float CpuUsage { get; set; }

        public float FreeMemory { get; set; }

        public float ProcessCpuUsage { get; set; }

        public int ProcessMemoryUsage { get; set; }

        public float ChildCpuUsage { get; set; }

        public int ChildMemoryUsage { get; set; }

        public bool IsOverThreshold
        {
            get
            {
                int maxCpuUsage = (int)CpuUsage;
                int maxMemUsage = ProcessMemoryUsage;

                maxCpuUsage = Math.Max(maxCpuUsage, (int)Math.Max(ProcessCpuUsage, ChildCpuUsage));
                maxMemUsage = Math.Max(maxMemUsage, ChildMemoryUsage);

                bool result = maxCpuUsage > PerfLoggerSettings.Default.CpuThreshold ||
                              maxMemUsage > PerfLoggerSettings.Default.MemoryThreshold;

                return result;
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
            var columns = new System.Collections.Generic.List<string>();
            columns.Add(DateTime.Now.ToString("HH:mm:ss"));

            if (PerfLoggerSettings.Default.EnableSystemUsage)
            {
                columns.Add(CpuUsage.ToString("0"));
                columns.Add(FreeMemory.ToString("0"));
            }

            columns.Add(ProcessCpuUsage.ToString("0"));
            columns.Add(ProcessMemoryUsage.ToString("0"));

            if (PerfLoggerSettings.Default.EnableChildServicesUsage)
            {
                columns.Add(ChildCpuUsage.ToString("0"));
                columns.Add(ChildMemoryUsage.ToString("0"));
            }

            string result = string.Join(",\t", columns);
            Console.WriteLine(result);

            return result;
        }
    }
}