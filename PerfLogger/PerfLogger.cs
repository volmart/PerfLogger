using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PerfLogger
{
    internal class PerfLogger
    {
        #region Private Fields

        private readonly log4net.ILog m_log = log4net.LogManager.GetLogger("MainLog");

        private readonly int m_pid;
        private PerformanceCounter m_cpuCounter;
        private PerformanceCounter m_memCounter;
        private PerformanceCounter m_cpuProcCounter;
        private PerformanceCounter m_memProcCounter;
        private Process m_monitoringProcess;
        private List<Process> m_childProcesses = new List<Process>();
        private List<PerformanceCounter> m_childCpuCounters = new List<PerformanceCounter>();
        private List<PerformanceCounter> m_childMemCounters = new List<PerformanceCounter>();

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
            InitSystemCounters();
            InitProcessCounters();
            InitChildCounters();
        }

        private void InitSystemCounters()
        {
            if (PerfLoggerSettings.Default.EnableSystemUsage)
            {
                m_cpuCounter = new PerformanceCounter
                {
                    CategoryName = "Processor",
                    CounterName = "% Processor Time",
                    InstanceName = "_Total"
                };

                m_memCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
        }

        private void InitProcessCounters()
        {
            m_monitoringProcess = Process.GetProcessById(m_pid);
            if (m_monitoringProcess != null)
            {
                m_cpuProcCounter = GetCpuCounters(new[] { m_monitoringProcess }).FirstOrDefault();
                m_memProcCounter = GetMemCounters(new[] { m_monitoringProcess }).FirstOrDefault();
            }
        }
        
        private void InitChildCounters()
        {
            if (PerfLoggerSettings.Default.EnableChildServicesUsage && m_monitoringProcess != null)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                m_childProcesses = GetChildProcesses(m_monitoringProcess);
                m_childCpuCounters = GetCpuCounters(m_childProcesses);
                m_childMemCounters = GetMemCounters(m_childProcesses);

                Log("Recreated child counters in " + sw.ElapsedMilliseconds + "ms, " + m_childProcesses.Count);
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
                    System.Threading.Thread.Sleep((int)interval);
                }
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

            logSample.ProcessMemoryUsage = (int) m_memProcCounter.NextValue() / (1024 * 1024);
            logSample.ProcessCpuUsage = m_cpuProcCounter.NextValue() / Environment.ProcessorCount;

            if (PerfLoggerSettings.Default.EnableChildServicesUsage)
            {
                logSample.ChildMemoryUsage = GetChildsMemeUsage();
                logSample.ChildCpuUsage = GetChildsCpuUsage();
            }

            logSample.Log();
        }

        private List<PerformanceCounter> GetProcessCounters(IEnumerable<Process> processes, string counter)
        {
            var counters = GetProcessInstanceNames(processes).Select(name => 
                new PerformanceCounter()
                {
                    CategoryName = "Process",
                    CounterName = counter,
                    InstanceName = name,
                });

            return counters.ToList();
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

        private int GetChildsMemeUsage()
        {
            float totalMemory = m_childMemCounters.Sum(c => c.NextValue());

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
            return m_memCounter.NextValue(); 
        }

        private void Log(string message)
        {
            m_log.Debug(message);
            Console.WriteLine(message);
        }
    }
}
