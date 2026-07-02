using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShowVirtualDesktopNumber
{
    internal static class Program
    {
        private static System.Threading.Mutex singleInstanceMutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            singleInstanceMutex = new System.Threading.Mutex(true, @"Global\ShowVirtualDesktopNumber.SingleInstance", out createdNew);
            if (!createdNew)
            {
                singleInstanceMutex.Dispose();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
            singleInstanceMutex.ReleaseMutex();
            singleInstanceMutex.Dispose();
        }
    }

    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly Timer refreshTimer;
        private readonly ToolStripLabel versionItem;
        private readonly ToolStripMenuItem languageItem;
        private readonly ToolStripMenuItem exitItem;

        private bool trayDisposed;
        private int lastDesktopNumber = -1;
        private AppLanguage currentLanguage;

        public TrayApplicationContext()
        {
            currentLanguage = LanguageSettings.Load();

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = SystemMenuFont.Create();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Padding = new Padding(1, 3, 1, 3);
            menu.BackColor = SystemMenuTheme.BackColor;
            menu.ForeColor = SystemMenuTheme.ForeColor;
            menu.RenderMode = ToolStripRenderMode.Professional;
            menu.Renderer = new ModernTrayMenuRenderer();

            versionItem = new ToolStripLabel();
            versionItem.AutoSize = false;
            versionItem.TextAlign = ContentAlignment.MiddleCenter;
            versionItem.ForeColor = SystemMenuTheme.SecondaryForeColor;
            versionItem.Padding = new Padding(0, 1, 0, 1);
            menu.Items.Add(versionItem);
            menu.Items.Add(new ToolStripSeparator());

            languageItem = new ToolStripMenuItem();
            languageItem.AutoSize = false;
            languageItem.TextAlign = ContentAlignment.MiddleCenter;
            languageItem.Padding = new Padding(0, 1, 0, 1);
            languageItem.Click += delegate { ToggleLanguage(); };
            menu.Items.Add(languageItem);

            exitItem = new ToolStripMenuItem();
            exitItem.AutoSize = false;
            exitItem.TextAlign = ContentAlignment.MiddleCenter;
            exitItem.Padding = new Padding(0, 1, 0, 1);
            exitItem.Click += delegate { ExitApplication(); };
            menu.Items.Add(exitItem);

            trayIcon = new NotifyIcon();
            trayIcon.Visible = true;
            ApplyLanguageText();
            trayIcon.Icon = TrayIconRenderer.CreateIcon("?");
            trayIcon.ContextMenuStrip = menu;

            refreshTimer = new Timer();
            refreshTimer.Interval = 150;
            refreshTimer.Tick += delegate { RefreshDesktopNumber(); };
            refreshTimer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            RefreshDesktopNumber();
        }

        private void RefreshDesktopNumber()
        {
            int number = VirtualDesktopReader.GetCurrentDesktopNumber();
            if (number <= 0)
            {
                trayIcon.Text = TextResources.UnknownDesktop(currentLanguage);
                UpdateTrayIcon("?");
                return;
            }

            if (number == lastDesktopNumber)
            {
                return;
            }

            lastDesktopNumber = number;
            trayIcon.Text = TextResources.CurrentDesktop(currentLanguage, number);
            UpdateTrayIcon(number.ToString());
        }

        private void ToggleLanguage()
        {
            currentLanguage = currentLanguage == AppLanguage.Chinese ? AppLanguage.English : AppLanguage.Chinese;
            LanguageSettings.Save(currentLanguage);
            ApplyLanguageText();
            RefreshDesktopNumber();
        }

        private void ApplyLanguageText()
        {
            versionItem.Text = BuildInfo.VersionText;
            languageItem.Text = TextResources.LanguageSwitch(currentLanguage);
            exitItem.Text = TextResources.Exit(currentLanguage);
            ResizeMenuItems();

            if (lastDesktopNumber > 0)
            {
                trayIcon.Text = TextResources.CurrentDesktop(currentLanguage, lastDesktopNumber);
            }
            else
            {
                trayIcon.Text = TextResources.AppName(currentLanguage);
            }
        }

        private void ResizeMenuItems()
        {
            int versionWidth = TextRenderer.MeasureText(versionItem.Text, versionItem.Font).Width;
            int languageWidth = TextRenderer.MeasureText(languageItem.Text, languageItem.Font).Width;
            int exitWidth = TextRenderer.MeasureText(exitItem.Text, exitItem.Font).Width;
            int width = Math.Max(versionWidth, Math.Max(languageWidth, exitWidth)) + 4;
            width = Math.Max(70, Math.Min(width, 122));

            versionItem.Size = new Size(width, 20);
            languageItem.Size = new Size(width, 23);
            exitItem.Size = new Size(width, 23);
        }

        private void UpdateTrayIcon(string text)
        {
            Icon oldIcon = trayIcon.Icon;
            trayIcon.Icon = TrayIconRenderer.CreateIcon(text);
            if (oldIcon != null)
            {
                oldIcon.Dispose();
            }
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.General)
            {
                UpdateTrayIcon(lastDesktopNumber > 0 ? lastDesktopNumber.ToString() : "?");
            }
        }

        private void ExitApplication()
        {
            refreshTimer.Stop();
            CleanupTrayIcon();
            ExitThread();
        }

        private void CleanupTrayIcon()
        {
            if (!trayDisposed && trayIcon != null)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                trayIcon.Visible = false;
                if (trayIcon.Icon != null)
                {
                    trayIcon.Icon.Dispose();
                }
                trayIcon.Dispose();
                trayDisposed = true;
            }
        }
    }

    internal enum AppLanguage
    {
        Chinese,
        English
    }

    internal static class TextResources
    {
        public static string AppName(AppLanguage language)
        {
            return language == AppLanguage.English ? "Virtual Desktop Number" : "虚拟桌面编号";
        }

        public static string CurrentDesktop(AppLanguage language, int number)
        {
            return language == AppLanguage.English ? "Current virtual desktop: " + number : "当前虚拟桌面：" + number;
        }

        public static string UnknownDesktop(AppLanguage language)
        {
            return language == AppLanguage.English ? "Virtual desktop number: unknown" : "虚拟桌面编号：未知";
        }

        public static string LanguageSwitch(AppLanguage language)
        {
            return language == AppLanguage.English ? "中文" : "English";
        }

        public static string Exit(AppLanguage language)
        {
            return language == AppLanguage.English ? "Exit" : "退出";
        }
    }

    internal static class LanguageSettings
    {
        private const string FileName = "config.toml";
        private const string LegacyFileName = "ShowVirtualDesktopNumber.toml";

        public static AppLanguage Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                AppLanguage legacyLanguage;
                if (TryLoadLegacy(out legacyLanguage))
                {
                    Save(legacyLanguage);
                    return legacyLanguage;
                }

                Save(AppLanguage.Chinese);
                return AppLanguage.Chinese;
            }

            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int equalsIndex = line.IndexOf('=');
                if (equalsIndex < 0)
                {
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim();
                string value = TrimTomlString(line.Substring(equalsIndex + 1).Trim());
                if (String.Equals(key, "language", StringComparison.OrdinalIgnoreCase))
                {
                    return String.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? AppLanguage.English : AppLanguage.Chinese;
                }
            }

            return AppLanguage.Chinese;
        }

        public static void Save(AppLanguage language)
        {
            string path = GetConfigPath();
            string languageValue = language == AppLanguage.English ? "en" : "zh";
            string[] lines = new string[]
            {
                "# ShowVirtualDesktopNumber configuration",
                "# language: zh or en",
                "language = \"" + languageValue + "\""
            };
            File.WriteAllLines(path, lines);
        }

        private static bool TryLoadLegacy(out AppLanguage language)
        {
            language = AppLanguage.Chinese;
            string legacyPath = Path.Combine(GetConfigDirectory(), LegacyFileName);
            if (!File.Exists(legacyPath))
            {
                return false;
            }

            string[] lines = File.ReadAllLines(legacyPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = StripComment(lines[i]).Trim();
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex < 0)
                {
                    continue;
                }

                string key = line.Substring(0, equalsIndex).Trim();
                string value = TrimTomlString(line.Substring(equalsIndex + 1).Trim());
                if (String.Equals(key, "language", StringComparison.OrdinalIgnoreCase))
                {
                    language = String.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? AppLanguage.English : AppLanguage.Chinese;
                    return true;
                }
            }

            return false;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(GetConfigDirectory(), FileName);
        }

        private static string GetConfigDirectory()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(exePath);
            if (String.IsNullOrEmpty(directory))
            {
                directory = AppDomain.CurrentDomain.BaseDirectory;
            }

            return directory;
        }

        private static string StripComment(string line)
        {
            int commentIndex = line.IndexOf('#');
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        private static string TrimTomlString(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }

    internal sealed class ModernTrayMenuRenderer : ToolStripProfessionalRenderer
    {
        public ModernTrayMenuRenderer()
            : base(new ModernTrayMenuColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (Pen pen = new Pen(SystemMenuTheme.BorderColor))
            {
                Rectangle bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(SystemMenuTheme.SeparatorColor))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is ToolStripMenuItem)
            {
                return;
            }

            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            Color backColor = e.Item.Selected ? SystemMenuTheme.HoverBackColor : SystemMenuTheme.BackColor;
            using (SolidBrush brush = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(brush, new Rectangle(0, 0, e.Item.Width, e.Item.Height));
            }

            ToolStripMenuItem menuItem = e.Item as ToolStripMenuItem;
            if (menuItem != null)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    menuItem.Text,
                    menuItem.Font,
                    new Rectangle(0, 0, e.Item.Width, e.Item.Height),
                    SystemMenuTheme.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
        }
    }

    internal sealed class ModernTrayMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return SystemMenuTheme.BackColor; } }
        public override Color ImageMarginGradientBegin { get { return SystemMenuTheme.BackColor; } }
        public override Color ImageMarginGradientMiddle { get { return SystemMenuTheme.BackColor; } }
        public override Color ImageMarginGradientEnd { get { return SystemMenuTheme.BackColor; } }
        public override Color MenuBorder { get { return SystemMenuTheme.BorderColor; } }
        public override Color MenuItemBorder { get { return SystemMenuTheme.HoverBorderColor; } }
        public override Color MenuItemSelected { get { return SystemMenuTheme.HoverBackColor; } }
        public override Color MenuItemSelectedGradientBegin { get { return SystemMenuTheme.HoverBackColor; } }
        public override Color MenuItemSelectedGradientEnd { get { return SystemMenuTheme.HoverBackColor; } }
    }

    internal static class SystemMenuFont
    {
        public static Font Create()
        {
            try
            {
                return new Font("Segoe UI Variable Text", SystemFonts.MenuFont.SizeInPoints, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                return new Font("Segoe UI", SystemFonts.MenuFont.SizeInPoints, FontStyle.Regular, GraphicsUnit.Point);
            }
        }
    }

    internal static class SystemMenuTheme
    {
        public static Color BackColor
        {
            get { return Color.White; }
        }

        public static Color ForeColor
        {
            get { return Color.FromArgb(32, 32, 32); }
        }

        public static Color SecondaryForeColor
        {
            get { return Color.FromArgb(96, 96, 96); }
        }

        public static Color BorderColor
        {
            get { return Color.FromArgb(225, 225, 225); }
        }

        public static Color SeparatorColor
        {
            get { return Color.FromArgb(235, 235, 235); }
        }

        public static Color HoverBackColor
        {
            get { return Color.FromArgb(245, 245, 245); }
        }

        public static Color HoverBorderColor
        {
            get { return Color.FromArgb(238, 238, 238); }
        }
    }

    internal static class TrayIconRenderer
    {
        public static Icon CreateIcon(string text)
        {
            Bitmap bitmap = new Bitmap(64, 64);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                graphics.Clear(Color.Transparent);

                bool lightTaskbar = ThemeReader.IsSystemLightTheme();
                Color textColor = lightTaskbar ? Color.FromArgb(32, 32, 32) : Color.White;
                Color shadowColor = lightTaskbar ? Color.FromArgb(120, Color.White) : Color.FromArgb(120, Color.Black);

                using (GraphicsPath textPath = CreateFittedTextPath(text))
                using (SolidBrush textBrush = new SolidBrush(textColor))
                using (SolidBrush shadowBrush = new SolidBrush(shadowColor))
                {
                    using (GraphicsPath shadowPath = (GraphicsPath)textPath.Clone())
                    using (Matrix shadowOffset = new Matrix())
                    {
                        shadowOffset.Translate(1.0f, 1.0f);
                        shadowPath.Transform(shadowOffset);
                        graphics.FillPath(shadowBrush, shadowPath);
                    }

                    graphics.FillPath(textBrush, textPath);
                }
            }

            IntPtr iconHandle = bitmap.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(iconHandle).Clone();
            DestroyIcon(iconHandle);
            bitmap.Dispose();
            return icon;
        }

        private static GraphicsPath CreateFittedTextPath(string text)
        {
            GraphicsPath path = new GraphicsPath();
            using (FontFamily family = CreateSystemFontFamily())
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Near;
                path.AddString(text, family, (int)FontStyle.Bold, 72f, Point.Empty, format);
            }

            RectangleF textBounds = path.GetBounds();
            float targetWidth = text.Length <= 1 ? 44f : (text.Length == 2 ? 52f : 57f);
            float targetHeight = text.Length <= 1 ? 52f : (text.Length == 2 ? 51f : 48f);
            float scale = Math.Min(targetWidth / textBounds.Width, targetHeight / textBounds.Height);
            float x = (64f - textBounds.Width * scale) / 2f - textBounds.X * scale;
            float y = (64f - textBounds.Height * scale) / 2f - textBounds.Y * scale - (text.Length <= 1 ? 1f : 0f);

            using (Matrix matrix = new Matrix())
            {
                matrix.Scale(scale, scale);
                matrix.Translate(x / scale, y / scale);
                path.Transform(matrix);
            }

            return path;
        }

        private static FontFamily CreateSystemFontFamily()
        {
            try
            {
                return new FontFamily("Segoe UI Variable Display");
            }
            catch
            {
                return new FontFamily("Segoe UI");
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }

    internal static class ThemeReader
    {
        public static bool IsSystemLightTheme()
        {
            object value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "SystemUsesLightTheme",
                0);

            if (value is int)
            {
                return ((int)value) != 0;
            }

            return false;
        }
    }

    internal static class VirtualDesktopReader
    {
        public static int GetCurrentDesktopNumber()
        {
            string sessionDesktopPath = GetCurrentSessionVirtualDesktopPath();
            byte[] desktopIds = ReadBinaryValue(sessionDesktopPath, "VirtualDesktopIDs");
            if (desktopIds == null || desktopIds.Length < 16)
            {
                desktopIds = ReadBinaryValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops", "VirtualDesktopIDs");
            }

            Guid currentDesktopId = ReadCurrentDesktopId();

            if (desktopIds == null || desktopIds.Length < 16 || currentDesktopId == Guid.Empty)
            {
                return 1;
            }

            int desktopCount = desktopIds.Length / 16;
            for (int index = 0; index < desktopCount; index++)
            {
                byte[] guidBytes = new byte[16];
                Buffer.BlockCopy(desktopIds, index * 16, guidBytes, 0, 16);
                Guid desktopId = new Guid(guidBytes);
                if (desktopId.Equals(currentDesktopId))
                {
                    return index + 1;
                }
            }

            return 1;
        }

        private static Guid ReadCurrentDesktopId()
        {
            Guid id = ReadGuidValue(GetCurrentSessionVirtualDesktopPath(), "CurrentVirtualDesktop");
            if (id != Guid.Empty)
            {
                return id;
            }

            id = ReadGuidValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops", "CurrentVirtualDesktop");
            if (id != Guid.Empty)
            {
                return id;
            }

            string sessionPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo";
            using (RegistryKey sessions = Registry.CurrentUser.OpenSubKey(sessionPath))
            {
                if (sessions == null)
                {
                    return Guid.Empty;
                }

                string[] names = sessions.GetSubKeyNames();
                for (int i = 0; i < names.Length; i++)
                {
                    string path = sessionPath + "\\" + names[i] + @"\VirtualDesktops";
                    id = ReadGuidValue(path, "CurrentVirtualDesktop");
                    if (id != Guid.Empty)
                    {
                        return id;
                    }
                }
            }

            return Guid.Empty;
        }

        private static string GetCurrentSessionVirtualDesktopPath()
        {
            uint sessionId;
            if (!ProcessIdToSessionId(GetCurrentProcessId(), out sessionId))
            {
                return null;
            }

            return @"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\" + sessionId + @"\VirtualDesktops";
        }

        private static Guid ReadGuidValue(string path, string valueName)
        {
            byte[] bytes = ReadBinaryValue(path, valueName);
            if (bytes != null && bytes.Length == 16)
            {
                return new Guid(bytes);
            }

            object value = ReadValue(path, valueName);
            if (value is string)
            {
                Guid parsed;
                if (Guid.TryParse((string)value, out parsed))
                {
                    return parsed;
                }
            }

            return Guid.Empty;
        }

        private static byte[] ReadBinaryValue(string path, string valueName)
        {
            object value = ReadValue(path, valueName);
            return value as byte[];
        }

        private static object ReadValue(string path, string valueName)
        {
            if (String.IsNullOrEmpty(path))
            {
                return null;
            }

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
            {
                if (key == null)
                {
                    return null;
                }

                return key.GetValue(valueName);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);
    }
}
