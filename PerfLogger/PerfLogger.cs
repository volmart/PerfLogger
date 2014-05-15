using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PerfLogger
{
    internal class PerfLogger
    {
        #region Private Fields

        private readonly log4net.ILog m_log = log4net.LogManager.GetLogger("MainLog");

        private readonly int m_pid;
        private PerformanceCounter m_cpuCounter;
        private PerformanceCounter m_ramCounter;
        private PerformanceCounter m_cpuProcCounter;
        private Process m_monitoringProcess;
        private List<Process> m_childProcesses = new List<Process>();
        private List<PerformanceCounter> m_childCpuCounters = new List<PerformanceCounter>();

        #endregion

        /// <summary>
        /// The default Constructor
        /// </summary>
        public PerfLogger(int pid)
        {
            m_pid = pid;
            Init();
            Start();
        }

        private void Init()
        {
            m_cpuCounter = new PerformanceCounter();

            m_cpuCounter.CategoryName = "Processor";
            m_cpuCounter.CounterName = "% Processor Time";
            m_cpuCounter.InstanceName = "_Total";

            m_ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            m_monitoringProcess = Process.GetProcessById(m_pid);
            if (m_monitoringProcess != null)
            {
                m_cpuProcCounter = GetCpuCounter(m_monitoringProcess);

                m_childProcesses = GetChildProcesses(m_monitoringProcess);
                m_childCpuCounters = GetCpuCounters(m_childProcesses);

                m_childProcesses.ForEach(p => Log("Child process: " + p.Id + "\t" + p.ProcessName));
            }
        }

        private void Start()
        {
            if (m_monitoringProcess == null)
            {
                Log("Process is null");
                return;
            }

            uint interval = PerfLoggerSettings.Default.Interval;
            while (m_monitoringProcess != null && !m_monitoringProcess.HasExited)
            {
                try
                {
                    ProduceLogSample();
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());

                    // Exception means smth wrong with obtaining data from counters
                    // Some process died or smth like that, recreating counters in this case
                    Init();
                }

                System.Threading.Thread.Sleep((int)interval);
            }

            Log("Process exited: " + m_monitoringProcess.HasExited);
        }

        private void ProduceLogSample()
        {
            LogSample logSample = new LogSample();

            if (PerfLoggerSettings.Default.EnableSystemUsage)
            {
                logSample.CpuUsage = GetCurrentCpuUsage();
                logSample.FreeMemory = GetAvailableRam();
            }

            logSample.ProcessMemoryUsage = (int) m_monitoringProcess.PrivateMemorySize64 / (1024 * 1024);
            logSample.ProcessCpuUsage = m_cpuProcCounter.NextValue() / Environment.ProcessorCount;

            if (PerfLoggerSettings.Default.EnableChildServicesUsage)
            {
                logSample.ChildMemoryUsage = GetChildsRam();
                logSample.ChildCpuUsage = GetChildsCpuUsage();
            }

            logSample.Log();
        }

        private List<PerformanceCounter> GetCpuCounters(IEnumerable<Process> processes)
        {
            return processes.Select(GetCpuCounter).ToList();
        }

        private PerformanceCounter GetCpuCounter(Process proc)
        {
            return new PerformanceCounter()
            {
                CategoryName = "Process",
                CounterName = "% Processor Time",
                InstanceName = GetProcessInstanceName(proc.Id),
            };
        }

        private string GetProcessInstanceName(int pid)
        {
            var cat = new PerformanceCounterCategory("Process");

            string[] instances = cat.GetInstanceNames();
            foreach (string instance in instances)
            {
                try
                {
                    using (var cnt = new PerformanceCounter("Process", "ID Process", instance, true))
                    {
                        int val = (int)cnt.RawValue;
                        if (val == pid)
                        {
                            return instance;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                }
            }

            throw new Exception("Could not find performance counter instance name for current process. This is truly strange ...");
        }

        private List<Process> GetChildProcesses(Process process)
        {
            var results = new List<Process>();

            // Query the management system objects for any process that has the current
            // process listed as it's parentprocessid
            string queryText = string.Format("select processid from win32_process where parentprocessid = {0}", process.Id);
            using (var searcher = new System.Management.ManagementObjectSearcher(queryText))
            {
                foreach (var obj in searcher.Get())
                {
                    object data = obj.Properties["processid"].Value;
                    if (data != null)
                    {
                        // Process may be not alive
                        try
                        {
                            // Retrieve the process
                            var childId = Convert.ToInt32(data);
                            var childProcess = Process.GetProcessById(childId);

                            results.Add(childProcess);
                            results.AddRange(GetChildProcesses(childProcess));
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString());
                        }
                    }
                }
            }

            return results;
        }

        private int GetChildsRam()
        {
            long totalMemory = m_childProcesses.Sum(p => p.PrivateMemorySize64);

            return (int)totalMemory / (1024 * 1024);
        }

        private float GetChildsCpuUsage()
        {
            return m_childCpuCounters.Sum(c => c.NextValue()) / Environment.ProcessorCount;
        }

        private float GetCurrentCpuUsage()
        { 
            return m_cpuCounter.NextValue();
        } 

        private float GetAvailableRam()
        {
            return m_ramCounter.NextValue(); 
        }

        private void Log(string message)
        {
            m_log.Debug(message);
            Console.WriteLine(message);
        }
    }
}
