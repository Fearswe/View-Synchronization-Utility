namespace View_Synchronization_Utility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Reflection;

    public class TrayIcon
    {
        private AppConfig AppConfig { get; }
        private NotifyIcon NotifyIcon { get; }

        private ContextMenuStrip? ContextMenuStrip { get; set; }

        private Dictionary<String, Image> Images { get; }

        private Icon RunningIcon { get; }
        private Icon PausedIcon { get; }

        public TrayIcon(AppConfig appConfig)
        {

            this.AppConfig = appConfig;

            this.RunningIcon = Utils.GetEmbeddedIcon("VSU-On.ico");
            this.PausedIcon = Utils.GetEmbeddedIcon("VSU-Off.ico");

            this.ContextMenuStrip = new ContextMenuStrip();
            this.NotifyIcon = new NotifyIcon()
            {
                Icon = this.RunningIcon,
                Text = $"[Running] {Assembly.GetExecutingAssembly().GetName().Name}",
                Visible = true,
                ContextMenuStrip = this.ContextMenuStrip
            };

            this.Images = this.LoadImages();

            var togglePausedButton = new ToolStripButton("&Pause", this.Images["pause"])
            {
                Checked = false,
                CheckOnClick = true
            };
            togglePausedButton.CheckedChanged += this.TogglePaused;
            this.ContextMenuStrip.Items.Add(togglePausedButton);
            
            this.CreateSettingsMenuItems();

            this.NotifyIcon.ContextMenuStrip.Items.Add(new ToolStripButton("E&xit", this.Images["close-circle"], this.OnClose));

        }

        private Dictionary<String, Image> LoadImages()
        {
            return new Dictionary<String, Image>
            {
                { ButtonIcons.FolderDown.ToString(), Utils.GetEmbeddedImage(ButtonIcons.FolderDown.ToString()) },
                { ButtonIcons.FolderRight.ToString(), Utils.GetEmbeddedImage(ButtonIcons.FolderRight.ToString()) },
                { ButtonIcons.FolderLeft.ToString(), Utils.GetEmbeddedImage(ButtonIcons.FolderLeft.ToString()) },
                { ButtonIcons.FolderUp.ToString(), Utils.GetEmbeddedImage(ButtonIcons.FolderUp.ToString()) },

                { $"{ButtonIcons.Checkbox}-checked", Utils.GetEmbeddedImage($"{ButtonIcons.Checkbox}-checked") },
                { $"{ButtonIcons.Checkbox}-unchecked", Utils.GetEmbeddedImage($"{ButtonIcons.Checkbox}-unchecked") },

                { "close-circle", Utils.GetEmbeddedImage("close-circle") },
                { "pause", Utils.GetEmbeddedImage("pause.png") },
                { "resume", Utils.GetEmbeddedImage("resume.png") },
                { "menu-open", Utils.GetEmbeddedImage("menu-open.png") }
            };
        }

        private void CreateSettingsMenuItems()
        {

            var dropDown = new ToolStripDropDownButton("App &config")
            {
                DropDownDirection = ToolStripDropDownDirection.Left,
                Image = this.Images["menu-open"]
            };
            foreach (PropertyInfo prop in this.AppConfig.GetType().GetProperties())
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var trayButtonInfoAttribute = prop.GetCustomAttribute<TrayButtonInfoAttribute>();
                if (trayButtonInfoAttribute != null)
                {
                    if (type == typeof(String))
                    {
                        var folderButton = new ToolStripButton($"{trayButtonInfoAttribute.DisplayName}: {prop.GetValue(this.AppConfig)}")
                        {
                            Name = prop.Name,
                            AutoSize = true,
                            Image = this.GetFolderImage(trayButtonInfoAttribute.Icon)
                        };
                        folderButton.Width = TextRenderer.MeasureText(folderButton.Text, folderButton.Font).Width;
                        folderButton.Click += this.OnFolderButtonClicked;
                        dropDown.DropDownItems.Add(folderButton);
                    }
                    else if (type == typeof(Boolean))
                    {
                        var checkValue = (Boolean)prop.GetValue(this.AppConfig);
                        var checkboxButton = new ToolStripButton(trayButtonInfoAttribute.DisplayName)
                        {
                            Image = this.GetCheckboxImage(checkValue),
                            Name = prop.Name,
                            Checked = checkValue,
                            AutoSize = true,
                            CheckOnClick = true
                        };
                        checkboxButton.CheckedChanged += this.OnCheckboxToggled;
                        checkboxButton.Width = TextRenderer.MeasureText(checkboxButton.Text, checkboxButton.Font).Width;
                        dropDown.DropDownItems.Add(checkboxButton);
                    }
                }
            }
            NotifyIcon.ContextMenuStrip.Items.Add(dropDown);
        }

        private void OnCheckboxToggled(Object? sender, EventArgs e)
        {
            if(sender is ToolStripButton item)
            {
                this.AppConfig.GetType().GetProperty(item.Name)?.SetValue(this.AppConfig, item.Checked);
                item.Image = this.GetCheckboxImage(item.Checked);
            }
        }

        private void OnFolderButtonClicked(Object? sender, EventArgs e)
        {
            if (sender is ToolStripButton item)
            {
                var property = this.AppConfig.GetType().GetProperty(item.Name);
                if (property != null)
                {
                    var thread = new Thread(() =>
                    {
                        var folderDialog = new FolderBrowserDialog();
                        folderDialog.InitialDirectory = property?.GetValue(this.AppConfig) as String ?? "C:\\";
                        folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                        var result = folderDialog.ShowDialog();
                        if (result == DialogResult.OK)
                        {
                            property?.SetValue(this.AppConfig, folderDialog.SelectedPath);
                            
                            
                        }
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    var trayButtonInfoAttribute = property?.GetCustomAttribute<TrayButtonInfoAttribute>();
                    item.Text = $"{trayButtonInfoAttribute.DisplayName}: {property.GetValue(this.AppConfig)}";

                }
            }
        }

        private void OnClose(object sender, EventArgs e)
        {
            this.NotifyIcon.Dispose(); // Just to remove the tray icon when you close. Otherwise it sticks around until you hover over it.
            Application.Exit();
        }

        private void TogglePaused(object sender, EventArgs e)
        {
            if (sender is ToolStripButton item)
            {
                this.AppConfig.Paused = item.Checked;
                this.NotifyIcon.Text = $"[{(item.Checked ? "Paused" : "Running")}] {Assembly.GetExecutingAssembly().GetName().Name}";
                this.NotifyIcon.Icon = item.Checked ? this.PausedIcon : this.RunningIcon;
                item.Text = item.Checked ? "&Resume" : "&Pause";
                item.Image = item.Checked ? this.Images["resume"] : this.Images["pause"];
            }
        }

        public void SendNotification(String title, String text, WatcherChangeTypes type)
        {
            if (type == WatcherChangeTypes.All
            || this.AppConfig.Notify
            && ((type == WatcherChangeTypes.Created && this.AppConfig.NotifyCreated)
            || (type == WatcherChangeTypes.Deleted && this.AppConfig.NotifyRemoved)
            || (type == WatcherChangeTypes.Renamed && this.AppConfig.NotifyRenamed)
            || (type == WatcherChangeTypes.Changed && this.AppConfig.NotifyChanged)))
            {
                NotifyIcon.BalloonTipText = title;
                NotifyIcon.BalloonTipTitle = text;
                NotifyIcon.BalloonTipIcon = type == WatcherChangeTypes.All ? ToolTipIcon.Error : ToolTipIcon.Info;
                NotifyIcon.ShowBalloonTip(60);
            }
        }

        [AttributeUsage(AttributeTargets.Property)]
        public class TrayButtonInfoAttribute : Attribute
        {
            public String DisplayName { get; }
            public ButtonIcons Icon { get; }
            public TrayButtonInfoAttribute(String displayName, ButtonIcons icon)
            {
                this.DisplayName = displayName;
                this.Icon = icon;
            }

        }

        private Image GetCheckboxImage(Boolean isChecked)
        {
            return this.Images[$"{ButtonIcons.Checkbox}-{(isChecked ? "checked" : "unchecked")}"];
        }

        private Image GetFolderImage(ButtonIcons type)
        {
            return this.Images[type.ToString()];
        }

        public enum ButtonIcons
        {
            FolderDown,
            FolderRight,
            FolderLeft,
            FolderUp,
            Checkbox
        }



    }
}
