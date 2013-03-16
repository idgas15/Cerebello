﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace CerebelloWebRole.Code
{
    public class DebugConfig
    {
        [XmlRoot("configuration", IsNullable = false)]
        public class ConfigurationItem
        {
            [XmlElement("debug")]
            public DebugItem Debug { get; set; }

            public class DebugItem
            {
                [XmlElement("settings")]
                public SettingsList Settings { get; set; }

                [XmlElement("datesAndTimes")]
                public DatesAndTimesList DatesAndTimes { get; set; }

                [XmlElement("emails")]
                public EmailsList Emails { get; set; }

                public override string ToString()
                {
                    return string.Format(@"<debug>");
                }

                public class SettingsList
                {
                    [XmlAttribute("use")]
                    public string Use { get; set; }

                    [XmlElement("setting")]
                    public List<SettingItem> Items { get; set; }

                    public override string ToString()
                    {
                        return string.Format(@"<settings use=""{0}"">", this.Use);
                    }
                }

                public class SettingItem
                {
                    [XmlAttribute("name")]
                    public string Name { get; set; }

                    [XmlElement("useLocalResources")]
                    public UseLocalResourcesItem UseLocalResources { get; set; }

                    [XmlElement("sendEmailsTo")]
                    public SendEmailsToItem SendEmailsTo { get; set; }

                    [XmlElement("customDateTime")]
                    public CustomDateTimeItem CustomDateTime { get; set; }

                    [XmlElement("restoreDataBase")]
                    public RestoreDataBaseItem RestoreDataBase { get; set; }

                    public override string ToString()
                    {
                        return string.Format(@"<setting name=""{0}"">", this.Name);
                    }

                    public class UseLocalResourcesItem
                    {
                        [XmlAttribute("enabled")]
                        public bool Enabled { get; set; }

                        public override string ToString()
                        {
                            return string.Format(
                                @"<useLocalResources enabled=""{0}"">",
                                this.Enabled ? "true" : "false");
                        }
                    }

                    public class SendEmailsToItem
                    {
                        [XmlAttribute("enabled")]
                        public bool Enabled { get; set; }

                        [XmlAttribute("use")]
                        public string Use { get; set; }

                        [XmlAttribute("allowed")]
                        public string Allowed { get; set; }

                        public override string ToString()
                        {
                            return string.Format(
                                @"<sendEmailsTo enabled=""{0}"" use=""{1}"" allowed=""{2}"">",
                                this.Enabled ? "true" : "false",
                                this.Use,
                                this.Allowed);
                        }
                    }

                    public class CustomDateTimeItem
                    {
                        [XmlAttribute("enabled")]
                        public bool Enabled { get; set; }

                        [XmlAttribute("use")]
                        public string Use { get; set; }

                        public override string ToString()
                        {
                            return string.Format(
                                @"<customDateTime enabled=""{0}"" use=""{1}"">",
                                this.Enabled ? "true" : "false",
                                this.Use);
                        }
                    }

                    public class RestoreDataBaseItem
                    {
                        [XmlAttribute("enabled")]
                        public bool Enabled { get; set; }

                        [XmlAttribute("name")]
                        public string Name { get; set; }

                        [XmlAttribute("hashed")]
                        public bool Hashed { get; set; }

                        public override string ToString()
                        {
                            return string.Format(
                                @"<restoreDataBase enabled=""{0}"" name=""{1}"" hashed=""{2}"">",
                                this.Enabled ? "true" : "false",
                                this.Name,
                                this.Hashed ? "true" : "false");
                        }
                    }
                }
            }

            public class DatesAndTimesList
            {
                [XmlElement("item")]
                public List<DateTimeItem> Items { get; set; }

                public class DateTimeItem
                {
                    [XmlAttribute("name")]
                    public string Name { get; set; }

                    [XmlAttribute("set")]
                    public string Set { get; set; }

                    [XmlAttribute("absolute")]
                    public string Absolute { get; set; }

                    [XmlAttribute("relative")]
                    public string Relative { get; set; }

                    public override string ToString()
                    {
                        return string.Format(
                            @"<item name=""{0}"" set=""{1}"" absolute=""{2}"" relative=""{3}"">",
                            this.Name,
                            this.Set,
                            this.Absolute,
                            this.Relative);
                    }
                }

                public override string ToString()
                {
                    return string.Format(@"datesAndTimes");
                }
            }

            public class EmailsList
            {
                [XmlElement("item")]
                public List<EmailItem> Items { get; set; }

                public class EmailItem
                {
                    [XmlAttribute("name")]
                    public string Name { get; set; }

                    [XmlAttribute("type")]
                    public string Type { get; set; }

                    [XmlAttribute("value")]
                    public string Value { get; set; }

                    public override string ToString()
                    {
                        return string.Format(@"<item name=""{0}"" type=""{1}"" value=""{2}"">", this.Name, this.Type, this.Value);
                    }
                }

                public override string ToString()
                {
                    return string.Format(@"emails");
                }
            }

            public override string ToString()
            {
                return "configuration";
            }
        }

        // this class is used only for debugging purposes... it may not be injectable, nor testable in any way
        private static readonly string cerebelloDebugPath;
        private static readonly string cerebelloDebugConfigPath;

        static DebugConfig()
        {
#if DEBUG
            // reading the debug.config file placed on the desktop
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            cerebelloDebugPath = Path.Combine(desktopPath, "Cerebello.Debug");
            cerebelloDebugConfigPath = Path.Combine(cerebelloDebugPath, "debug.config");

            if (Directory.Exists(desktopPath))
            {
                {
                    var watcher = new FileSystemWatcher(desktopPath, "debug.config");
                    watcher.Created += WatcherEvent;
                    watcher.Deleted += WatcherEvent;
                    watcher.Changed += WatcherEvent;
                    watcher.Renamed += WatcherEvent;
                    watcher.Error += WatcherError;
                    watcher.Disposed += WatcherDisposed;
                    watcher.IncludeSubdirectories = true;
                    watcher.EnableRaisingEvents = true;
                }

                {
                    var watcher = new FileSystemWatcher(desktopPath, "Cerebello.Debug");
                    watcher.Created += WatcherEvent;
                    watcher.Deleted += WatcherEvent;
                    watcher.Changed += WatcherEvent;
                    watcher.Renamed += WatcherEvent;
                    watcher.Error += WatcherError;
                    watcher.Disposed += WatcherDisposed;
                    watcher.EnableRaisingEvents = true;
                }

                inst = new DebugConfig();
            }
#endif
        }

        static void WatcherDisposed(object sender, EventArgs e)
        {
        }

        static void WatcherError(object sender, ErrorEventArgs e)
        {
        }

        static void WatcherEvent(object sender, FileSystemEventArgs e)
        {
            var re = e as RenamedEventArgs;
            var dirChanged = scii(e.FullPath, cerebelloDebugPath) || re != null && scii(re.OldFullPath, cerebelloDebugPath);
            var fileChanged = scii(e.FullPath, cerebelloDebugConfigPath) || re != null && scii(re.OldFullPath, cerebelloDebugConfigPath);
            if (fileChanged || dirChanged)
                lock (locker)
                {
                    inst = new DebugConfig();
                    HttpRuntime.UnloadAppDomain();
                }
        }

        private DebugConfig()
        {
#if DEBUG
            // loading the debug.config file, and reading it
            if (File.Exists(cerebelloDebugConfigPath))
            {
                var ser = new XmlSerializer(typeof(ConfigurationItem));
                using (var reader = XmlReader.Create(cerebelloDebugConfigPath))
                {
                    config = (ConfigurationItem)ser.Deserialize(reader);
                }
            }

            if (config != null)
            {
                var settingToUse = config.Debug.Settings.Use;
                setting = config.Debug.Settings.Items.SingleOrDefault(x => scii(x.Name, settingToUse));

                if (setting != null && setting.CustomDateTime != null && setting.CustomDateTime.Enabled)
                {
                    var dt = config.Debug.DatesAndTimes.Items.SingleOrDefault(x => scii(x.Name, setting.CustomDateTime.Use));
                    if (dt != null)
                    {
                        var now = DateTime.Now;

                        TimeSpan offset;
                        TimeSpan.TryParse(dt.Relative, out offset);

                        DateTime absolute;
                        if (!DateTime.TryParse(dt.Absolute, out absolute))
                            absolute = now;

                        var parts = (dt.Set ?? "").Split(';');
                        var newDate = MergeDates(now, absolute, parts) + offset;

                        currentTimeOffset = newDate - now;
                    }
                }
            }
#endif
        }

        private static DateTime MergeDates(DateTime dst, DateTime src, string[] parts)
        {
            if (dst == src || parts.Contains("datetime", iic))
                return src;

            var newDate = dst;
            if (parts.Contains("time", iic))
                newDate = new DateTime(newDate.Year, newDate.Month, newDate.Day, src.Hour, src.Minute, src.Second);
            else
            {
                if (parts.Contains("hour", iic))
                    newDate = new DateTime(newDate.Year, newDate.Month, newDate.Day, src.Hour, newDate.Minute, newDate.Second);

                if (parts.Contains("minute", iic))
                    newDate = new DateTime(newDate.Year, newDate.Month, newDate.Day, newDate.Hour, src.Minute, newDate.Second);

                if (parts.Contains("second", iic))
                    newDate = new DateTime(newDate.Year, newDate.Month, newDate.Day, newDate.Hour, newDate.Minute, src.Second);
            }

            if (parts.Contains("date", iic))
                newDate = new DateTime(src.Year, src.Month, src.Day, newDate.Hour, newDate.Minute, newDate.Second);
            else
            {
                if (parts.Contains("day", iic) || parts.Contains("dayofmonth", iic))
                    newDate = new DateTime(newDate.Year, newDate.Month, src.Day, newDate.Hour, newDate.Minute, newDate.Second);

                if (parts.Contains("dayofweek"))
                {
                    var day = TimeSpan.FromDays(1).Ticks;
                    var origin = -(int)new DateTime().DayOfWeek * day;
                    var newDatePart = Truncate(newDate.Ticks, origin, day * 7) + newDate.Ticks % day;
                    var dayOfWeekPart = (int)src.DayOfWeek * day;
                    newDate = new DateTime(newDatePart + dayOfWeekPart);
                }

                if (parts.Contains("week", iic))
                {
                    var week = TimeSpan.FromDays(7).Ticks;
                    var origin = -(int)new DateTime().DayOfWeek * week / 7;
                    var startOfNowWeek = Truncate(src.Ticks, origin, week);
                    var dayAndTimeInsideNewWeek = (newDate.Ticks - origin) % week;
                    newDate = new DateTime(startOfNowWeek + dayAndTimeInsideNewWeek);
                }
                else
                {
                    if (parts.Contains("month", iic))
                        newDate = new DateTime(newDate.Year, src.Month, newDate.Day, newDate.Hour, newDate.Minute, newDate.Second);

                    if (parts.Contains("year", iic))
                        newDate = new DateTime(src.Year, newDate.Month, newDate.Day, newDate.Hour, newDate.Minute, newDate.Second);
                }
            }

            return newDate;
        }

        static long Truncate(long value, long origin, long unit)
        {
            return (value - origin) / unit * unit + origin;
        }

        private static readonly object locker = new object();
        [CanBeNull]
        private static DebugConfig inst;

        private static readonly Func<string, string, bool> scii = (a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a, b) == 0;
        private static readonly StringComparer iic = StringComparer.InvariantCultureIgnoreCase;

        [CanBeNull]
        private readonly ConfigurationItem config;
        [CanBeNull]
        private readonly ConfigurationItem.DebugItem.SettingItem setting;

        /// <summary>
        /// UseLocalResourcesOnly indicates whether to use only local resources,
        /// not using the internet to get anything. This does not affect e-mails.
        /// </summary>
        public static bool UseLocalResourcesOnly
        {
            get
            {
#if DEBUG
                lock (locker)
                    if (inst != null)
                        return inst.setting != null
                            && inst.setting.UseLocalResources != null
                            && inst.setting.UseLocalResources.Enabled;
#endif
                return false;
            }
        }

        /// <summary>
        /// UseFileSystemEmailBox indicates whether any e-mail redirects goes to the file-system.
        /// </summary>
        public static bool UseFileSystemEmailBox
        {
            get
            {
#if DEBUG
                lock (locker)
                    if (PathListToSaveEmailsTo.Any())
                        return true;
#endif
                return false;
            }
        }

        private readonly TimeSpan currentTimeOffset;
        /// <summary>
        /// Determines a time offset, to simulate a different date/time.
        /// This is useful when working with the schedule component, and other
        /// time related stuff.
        /// </summary>
        public static TimeSpan CurrentTimeOffset
        {
            get
            {
#if DEBUG
                lock (locker)
                    if (inst != null)
                        return inst.currentTimeOffset;
#endif
                return TimeSpan.Zero;
            }
        }

        private HashSet<string> allAllowedEmails;
        /// <summary>
        /// Determines whether an e-mail can be sent to a destination address or not,
        /// using a real smtp server.
        /// </summary>
        /// <param name="address">E-mail address that you want to send a message to.</param>
        /// <returns>True if a real e-mail can be sent to the address; otherwise false.</returns>
        public static bool CanSendEmailToAddress(string address)
        {
#if DEBUG
            lock (locker)
            {
                if (inst != null
                    && inst.config != null
                    && inst.setting != null
                    && inst.setting.SendEmailsTo != null
                    && inst.setting.SendEmailsTo.Enabled)
                {
                    var allowed = inst.setting.SendEmailsTo.Allowed ?? "";
                    if (inst.allAllowedEmails == null)
                    {
                        var itemNames = allowed.Split(';');

                        var emails1 = inst.config.Debug.Emails.Items
                                          .Where(x => itemNames.Contains(x.Name, iic) && scii(x.Type, "smtp"))
                                          .Select(x => x.Value);

                        var emails2 = itemNames.Where(x => x.Contains('@'));

                        var emails3 = itemNames.Contains("*")
                                          ? inst.config.Debug.Emails.Items.Where(x => scii(x.Type, "smtp")).Select(x => x.Value)
                                          : new string[0];

                        inst.allAllowedEmails = new HashSet<string>(emails1.Concat(emails2).Concat(emails3));
                    }

                    if (inst.allAllowedEmails != null)
                        return inst.allAllowedEmails.Contains(address);
                }
                else
                {
                    // in DEBUG mode the default behavior is not to send any e-mails
                    // to anyone except these well-known e-mail addresses
                    if (!new[]
                        {
                            "cerebello@cerebello.com.br",
                            "masbicudo@gmail.com",
                            "masbicudo@hotmail.com",
                            "masbicudo@mail.com",
                            "masbicudo@yahoo.com.br",
                            "andrerpena@gmail.com",
                        }
                        .Contains(address))
                    {
                        return false;
                    }
                }
            }
#endif
            return true;
        }

        private string[] redirectEmailsToSmtp;
        /// <summary>
        /// Determines the addresses to which e-mail should also be sent to.
        /// Everything in the original message will not be changed, except the destination address.
        /// </summary>
        public static string[] EmailAddressesToCopyEmailsTo
        {
            get
            {
#if DEBUG
                lock (locker)
                {
                    if (inst != null && inst.redirectEmailsToSmtp == null)
                    {
                        if (inst.config != null
                            && inst.setting != null
                            && inst.setting.SendEmailsTo != null
                            && inst.setting.SendEmailsTo.Enabled)
                        {
                            var itemNames = inst.setting.SendEmailsTo.Use.Split(';');
                            inst.redirectEmailsToSmtp = inst.config.Debug.Emails.Items
                                                         .Where(x => itemNames.Contains(x.Name, iic) && scii(x.Type, "smtp"))
                                                         .Select(x => x.Value)
                                                         .ToArray();
                        }
                    }

                    if (inst != null && inst.redirectEmailsToSmtp != null)
                        return inst.redirectEmailsToSmtp;
                }
#endif
                return new string[0];
            }
        }

        private string[] redirectEmailsToFileSystem;
        /// <summary>
        /// 
        /// </summary>
        public static string[] PathListToSaveEmailsTo
        {
            get
            {
#if DEBUG
                lock (locker)
                {
                    if (inst != null && inst.redirectEmailsToFileSystem == null)
                    {
                        if (inst.config != null && inst.setting != null && inst.setting.SendEmailsTo != null && inst.setting.SendEmailsTo.Enabled)
                        {
                            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                            Func<ConfigurationItem.EmailsList.EmailItem, string> getPath = i =>
                                scii(i.Type, "desktop") ? Path.Combine(desktopPath, i.Value) :
                                scii(i.Type, "file") ? i.Value :
                                "";

                            var itemNames = inst.setting.SendEmailsTo.Use.Split(';');
                            inst.redirectEmailsToFileSystem = inst.config.Debug.Emails.Items
                                         .Where(x => itemNames.Contains(x.Name, iic)
                                                  && new[] { "desktop", "path" }.Contains(x.Type.ToLowerInvariant(), iic))
                                         .Select(getPath)
                                         .ToArray();
                        }
                    }

                    if (inst != null && inst.redirectEmailsToFileSystem != null)
                        return inst.redirectEmailsToFileSystem;
                }
#endif
                return new string[0];
            }
        }

        /// <summary>
        /// Useful to avoid C# and Resharper complaining about unreachable code,
        /// after #if DEBUG #endif statements, and about unused values before the #if DEBUG code.
        /// </summary>
        public static bool IsDebug
        {
            get
            {
#if DEBUG
                return true;
#endif
                // pragma: disable warning about unreachable code
#pragma warning disable 162
                // ReSharper disable HeuristicUnreachableCode
                return false;
                // ReSharper restore HeuristicUnreachableCode
#pragma warning restore 162
            }
        }

        /// <summary>
        /// Gets the current running host environment.
        /// </summary>
        public static HostEnv HostEnvironment
        {
            get
            {
                var exeName = Regex.Match(Environment.CommandLine, @"^(?:""(?<FN>[^""]*)""|(?<FN>[^\s]*))").Groups["FN"].Value;

                if (!string.IsNullOrWhiteSpace(exeName))
                {
                    if (exeName.EndsWith("iisexpress.exe", StringComparison.InvariantCultureIgnoreCase))
                        return HostEnv.IisExpress;

                    if (exeName.EndsWith("w3wp.exe", StringComparison.InvariantCultureIgnoreCase))
                        return HostEnv.Iis;

                    if (Regex.IsMatch(exeName, @"WebDev.WebServer\d*\.EXE$", RegexOptions.IgnoreCase))
                        return HostEnv.WebDevServer;

                    if (exeName.EndsWith("WaIISHost.exe", StringComparison.InvariantCultureIgnoreCase))
                        return HostEnv.WindowsAzureIisHost;
                }

                return HostEnv.Unknown;
            }
        }
    }
}
