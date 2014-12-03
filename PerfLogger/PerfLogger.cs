using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;

namespace PerfLogger
{
    internal class PerfLogger
    {
        #region Private Fields

        private readonly log4net.ILog m_log = log4net.LogManager.GetLogger("MainLog");
        private readonly List<string> m_skipProcesses = new List<string>();
 
        private readonly int m_pid;
        private PerformanceCounter m_cpuCounter;
        private PerformanceCounter m_memCounter;
        private PerformanceCounter m_cpuProcCounter;
        private PerformanceCounter m_memProcCounter;
        private Process m_monitoringProcess;
        private List<Process> m_childProcesses = new List<Process>();
        private List<PerformanceCounter> m_childCpuCounters = new List<PerformanceCounter>();
        private List<PerformanceCounter> m_childMemCounters = new List<PerformanceCounter>();
        private List<PerformanceCounter> m_resourceManagerCounters = new List<PerformanceCounter>();

        #endregion

        /// <summary>
        /// The default Constructor
        /// </summary>
        public PerfLogger(int pid)
        {
            Log("Monitoring process id " + pid);
            m_pid = pid;

            Wait(PerfLoggerSettings.Default.MonitoringDelay);
            Init();
            Start();
        }

        private void Wait(int delay)
        {
            Log("Start monitoring after " + delay + "msec");
            Thread.Sleep(delay);
        }

        private void Init()
        {
            m_skipProcesses.Add("conhost");
            m_skipProcesses.Add("PerfLogger");

            PerformanceCounter.CloseSharedResources();

            InitSystemCounters();
            InitProcessCounters();
            InitChildCounters();
            InitResourceManagerCounters();
        }

        private void InitSystemCounters()
        {
            try
            {
                if (PerfLoggerSettings.Default.EnableSystemUsage)
                {
                    m_cpuCounter = GetCounter("Processor", "% Processor Time", "_Total");
                }

                m_memCounter = GetCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private void InitProcessCounters()
        {
            try
            {
                m_monitoringProcess = Process.GetProcessById(m_pid);
                if (m_monitoringProcess != null)
                {
                    m_cpuProcCounter = GetCpuCounters(new[] { m_monitoringProcess }).FirstOrDefault();
                    m_memProcCounter = GetMemCounters(new[] { m_monitoringProcess }).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }
        }

        private void InitChildCounters()
        {
            try
            {
                if (PerfLoggerSettings.Default.EnableChildServicesUsage && m_monitoringProcess != null)
                {
                    var sw = new Stopwatch();
                    sw.Start();

                    m_childProcesses = GetChildProcesses(m_monitoringProcess);
                    m_childCpuCounters = GetCpuCounters(m_childProcesses);
                    m_childMemCounters = GetMemCounters(m_childProcesses);

                    LogSample.SetHeader(m_childCpuCounters.Select(c => c.InstanceName).ToList());

                    Log("Recreated child counters in " + sw.ElapsedMilliseconds + "ms, " + m_childProcesses.Count);
                    m_childProcesses.ForEach(p => Log("Child process: " + p.Id + "\t" + p.ProcessName));
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }             
        }

        private void InitResourceManagerCounters()
        {
            try
            {
                if (PerfLoggerSettings.Default.EnableResourceManagerCounters)
                {
                    string categoryName = "ResourceManager";

                    m_resourceManagerCounters.Add(GetCounter(categoryName, "messages / sec"));                    
                    m_resourceManagerCounters.Add(GetCounter(categoryName, "queued messages"));
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            } 
        }

        private void Start()
        {
            if (m_monitoringProcess == null)
            {
                Log("Process is null");
                return;
            }

            uint iteration = 0;
            uint interval = PerfLoggerSettings.Default.Interval;
            uint recreateCountersIntervals = PerfLoggerSettings.Default.RecreateChildCountersIntervals;
            while (m_monitoringProcess != null && !m_monitoringProcess.HasExited)
            {
                try
                {
                    ProduceLogSample();
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());

                    Wait(PerfLoggerSettings.Default.DelayOnException);

                    // Exception means smth wrong with obtaining data from counters
                    // Some process died or smth like that, recreating counters in this case
                    Init();
                }

                // Requery child processes from time to time
                if (recreateCountersIntervals > 0 && ++iteration % recreateCountersIntervals == 0)
                {
                    InitChildCounters();
                }
                else
                {
                    Thread.Sleep((int)interval);
                }
            }

            Log("Process exited: " + m_monitoringProcess.HasExited);
        }

        private void ProduceLogSample()
        {
            var logSample = new LogSample();

            if (PerfLoggerSettings.Default.EnableSystemUsage)
            {
                logSample.CpuUsage = GetCurrentCpuUsage();
                logSample.FreeMemory = GetAvailableRam();
            }

            logSample.ProcessMemoryUsage = GetProcessMemoryUsage();
            logSample.ProcessCpuUsage = GetProcessCpuUsage();

            if (PerfLoggerSettings.Default.EnableChildServicesUsage)
            {
                logSample.SetChildMemoryUsage(GetChildsMemUsage());
                logSample.SetChildCpuUsage(GetChildsCpuUsage());
            }

            if (PerfLoggerSettings.Default.EnableResourceManagerCounters)
            {
                logSample.MessagesPerSecond = GetMessagesPerSecond();
                logSample.MessagesQueued = GetMessagesQueued();
            }

            logSample.Log();
        }

        private List<PerformanceCounter> GetProcessCounters(IEnumerable<Process> processes, string counter)
        {
            var counters = new List<PerformanceCounter>();
            try
            {
                counters = GetProcessInstanceNames(processes).Select(name => GetCounter("Process", counter, name)).Where(c => c != null).ToList();
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
            }

            return counters;
        }

        private List<PerformanceCounter> GetCpuCounters(IEnumerable<Process> processes)
        {
            return GetProcessCounters(processes, "% Processor Time");
        }

        private List<PerformanceCounter> GetMemCounters(IEnumerable<Process> processes)
        {
            return GetProcessCounters(processes, "Working set");
        }

        /// <summary>
        /// Counter names are not same as Process name when there are several of them, like Nss
        /// It will be Nss#1, Nss#2 etc
        /// </summary>
        private IEnumerable<string> GetProcessInstanceNames(IEnumerable<Process> processes)
        {
            var names = new List<string>();
            var pids = processes.Select(p => p.Id).ToList();

            var cat = new PerformanceCounterCategory("Process");

            string[] instances = cat.GetInstanceNames();
            foreach (string instance in instances)
            {
                try
                {
                    using (var cnt = new PerformanceCounter("Process", "ID Process", instance, true))
                    {
                        int val = (int)cnt.RawValue;
                        if (pids.Contains(val))
                        {
                            names.Add(instance);
                            if (names.Count == pids.Count)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                }
            }

            return names;
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

                            if (!m_skipProcesses.Contains(childProcess.ProcessName))
                            {
                                results.Add(childProcess);
                            }

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

        private PerformanceCounter GetCounter(string category, string counter, string instance = null)
        {
            PerformanceCounter result = null;
            try
            {
                result = new PerformanceCounter
                {
                    CategoryName = category,
                    CounterName = counter
                };

                if (!string.IsNullOrEmpty(instance))
                {
                    result.InstanceName = instance;
                }

                var test = result.NextValue();
            }
            catch (Exception ex)
            {
                result = null;
                Log(ex.ToString());
                Log("Error creating counter " + category + "-" + counter + "-" + instance ?? string.Empty);
            }

            return result;
        }

        private float GetProcessCpuUsage()
        {
            return m_cpuProcCounter != null ? m_cpuProcCounter.NextValue() / Environment.ProcessorCount : -1f;
        }

        private long ByteToMByte(long bytes)
        {
            const long divider = 1024 * 1024;
            return bytes / divider;
        }

        private long GetProcessMemoryUsage()
        {
            return m_memProcCounter != null ? ByteToMByte((long)m_memProcCounter.NextValue()) : -1;
        }

        private Dictionary<string, long> GetChildsMemUsage()
        {
            Dictionary<string, long> values = m_childMemCounters.ToDictionary(c => c.InstanceName, c => ByteToMByte((long)c.NextValue()));

            return values;
        }

        private Dictionary<string, float> GetChildsCpuUsage()
        {
            Dictionary<string, float> values = m_childCpuCounters.ToDictionary(c => c.InstanceName, c => c.NextValue() / (float)Environment.ProcessorCount);

            return values;
        }

        private float GetCurrentCpuUsage()
        {
            return m_cpuCounter != null ? m_cpuCounter.NextValue() : -1f;
        } 

        private float GetAvailableRam()
        {
            return m_memCounter != null ? m_memCounter.NextValue() : -1f; 
        }

        private int GetMessagesPerSecond()
        {
            return m_resourceManagerCounters[0] != null ? (int)m_resourceManagerCounters[0].NextValue() : -1;
        }

        private int GetMessagesQueued()
        {
            return m_resourceManagerCounters[1] != null ? (int)m_resourceManagerCounters[1].NextValue() : -1;
        }

        private void Log(string message)
        {
            m_log.Debug(message);
            Console.WriteLine(message);
        }
    }
}
