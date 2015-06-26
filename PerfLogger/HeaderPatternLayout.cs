using System;
using log4net.Layout;

namespace PerfLogger
{
    public class HeaderPatternLayout : PatternLayout
    {
        public override string Header
        {
            get
            {
                return LogSample.LogHeader + Environment.NewLine;
            }
        }
    }
}