using System.Linq;

namespace PerfLogger
{
    public class Program
    {
        static log4net.ILog s_log = log4net.LogManager.GetLogger("MainLog");

        public static void Main(string[] args)
        {
            int pid = 0;
            if (args.Length > 0)
            {
                int.TryParse(args[0], out pid);
                if (System.Diagnostics.Process.GetProcessById(pid) == null)
                {
                    pid = 0;
                }
            }
            else
            {
                var proc = System.Diagnostics.Process.GetProcessesByName("PinkPantherClient").FirstOrDefault();
                if (proc != null)
                {
                    pid = proc.Id;
                }
            }

            ConfigureLog4Net("_" + pid);

            if (pid != 0)
            {
                PerfLogger logger = new PerfLogger(pid);
            }
            else
            {
                s_log.Debug("Non existing process id");
            }
        }

        private static void ConfigureLog4Net(string fileSuffix)
        {
            log4net.GlobalContext.Properties["LogName"] = fileSuffix;
            string logConfigFile = "log4net.config";
            if (System.IO.File.Exists(logConfigFile))
            {
                log4net.Config.XmlConfigurator.ConfigureAndWatch(new System.IO.FileInfo(logConfigFile));
            }
            
            s_log.Debug("test");
        }
    }
}
