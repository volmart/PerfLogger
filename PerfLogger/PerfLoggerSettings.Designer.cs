﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.19448
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PerfLogger {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "11.0.0.0")]
    internal sealed partial class PerfLoggerSettings : global::System.Configuration.ApplicationSettingsBase {
        
        private static PerfLoggerSettings defaultInstance = ((PerfLoggerSettings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new PerfLoggerSettings())));
        
        public static PerfLoggerSettings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("1000")]
        public uint Interval {
            get {
                return ((uint)(this["Interval"]));
            }
            set {
                this["Interval"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public ushort CpuThreshold {
            get {
                return ((ushort)(this["CpuThreshold"]));
            }
            set {
                this["CpuThreshold"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("100")]
        public uint MemoryThreshold {
            get {
                return ((uint)(this["MemoryThreshold"]));
            }
            set {
                this["MemoryThreshold"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool EnableSystemUsage {
            get {
                return ((bool)(this["EnableSystemUsage"]));
            }
            set {
                this["EnableSystemUsage"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool EnableChildServicesUsage {
            get {
                return ((bool)(this["EnableChildServicesUsage"]));
            }
            set {
                this["EnableChildServicesUsage"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("500")]
        public uint RecreateChildCountersIntervals {
            get {
                return ((uint)(this["RecreateChildCountersIntervals"]));
            }
            set {
                this["RecreateChildCountersIntervals"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("20000")]
        public int MonitoringDelay {
            get {
                return ((int)(this["MonitoringDelay"]));
            }
            set {
                this["MonitoringDelay"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5000")]
        public int DelayOnException {
            get {
                return ((int)(this["DelayOnException"]));
            }
            set {
                this["DelayOnException"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool Enabled {
            get {
                return ((bool)(this["Enabled"]));
            }
            set {
                this["Enabled"] = value;
            }
        }
    }
}
