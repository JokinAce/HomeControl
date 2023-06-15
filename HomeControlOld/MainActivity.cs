using Android.App;
using Android.Net;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.BottomNavigation;
using System.Net;
using System;
using System.Threading;
using Android.Content.PM;
using System.Globalization;
using Android.Content;
using AndroidX.Core.App;
using HomeController;

namespace HomeControlOld {
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, BottomNavigationView.IOnItemSelectedListener {
        private static readonly WebClient webClient = new WebClient() {
            Proxy = null,
            Headers = new WebHeaderCollection { "apiKey: PLACEHOLDER" }
        };

        static readonly string webAddress = "PLACEHOLDER";

        static readonly string notifyChannelId = "home_control";
        static readonly string notifyChannelName = "HomeControl";
        static readonly int notifyNotificationId = 1000;

        static readonly int AlarmIntervalInMinutes = 60;

        [BroadcastReceiver(Enabled = true, DirectBootAware = true, Exported = true)]
        [IntentFilter(new[] { Intent.ActionBootCompleted}, Priority = (int)IntentFilterPriority.HighPriority)]
        public class BootBroadcastReceiver : BroadcastReceiver {
            public override void OnReceive(Context context, Intent intent) {
                Intent i = new Intent(context, typeof(MainActivity));
                i.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(i);
            }
        }

        void CreateNotificationChannel() {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) {
                // Notification channels are new in API 26 (and not a part of the
                // support library). There is no need to create a notification
                // channel on older versions of Android.
                return;
            }

            var channel = new NotificationChannel(notifyChannelId, notifyChannelName, NotificationImportance.Default) {
                Description = "Outage Notifications"
            };

            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.CreateNotificationChannel(channel);
        }

        protected override void OnCreate(Bundle savedInstanceState) {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            this.Window.SetNavigationBarColor(Android.Graphics.Color.Black);

            CreateNotificationChannel();

            var alarmManager = (AlarmManager)GetSystemService(AlarmService);
            var pendingIntent = PendingIntent.GetBroadcast(this, 0, new Intent(this, typeof(BoilerBroadcastReceiver)), 0);
            alarmManager.SetRepeating(AlarmType.ElapsedRealtime, SystemClock.ElapsedRealtime(), AlarmIntervalInMinutes * 60 * 1000, pendingIntent);


            TürTab();
            FindViewById<BottomNavigationView>(Resource.Id.navigation).SetOnItemSelectedListener(this);
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults) {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public bool OnNavigationItemSelected(IMenuItem item) {
            switch (item.ItemId) {
                case Resource.Id.navigation_tür:
                    TürTab();

                    FindViewById<BottomNavigationView>(Resource.Id.navigation).SetOnItemSelectedListener(this);
                    return true;
                case Resource.Id.navigation_boiler:
                    BoilerTab();

                    FindViewById<BottomNavigationView>(Resource.Id.navigation).SetOnItemSelectedListener(this);
                    return true;
            }
            return false;
        }

        private void TürTab() {
            SetContentView(Resource.Layout.activity_main);

            Button door_open = FindViewById<Button>(Resource.Id.tür_button_öffnen);

            door_open.Click += (object sender, EventArgs e) => {
                webClient.DownloadStringAsync(new System.Uri("PLACEHOLDER"));
            };
        }

        private void BoilerTab() {
            SetContentView(Resource.Layout.boiler_tab);

            TextView boiler_temp = FindViewById<TextView>(Resource.Id.boiler_temp);
            TextView boiler_status = FindViewById<TextView>(Resource.Id.boiler_status);
            Button boiler_button = FindViewById<Button>(Resource.Id.boiler_button);
            CheckBox boiler_checkbox = FindViewById<CheckBox>(Resource.Id.boiler_checkbox);

            try {
                Obfuscator.Content deobfucatedData = Obfuscator.Decrypt(webClient.DownloadString(webAddress  + "/boiler/get"));
                if (deobfucatedData.IsReplay())
                    throw new Exception("Replay detected");

                string[] status = deobfucatedData.ContentMessage.Split(',');

                bool isRelayOn = status[0] == "True";
                float currentTemp = float.Parse(status[1], CultureInfo.InvariantCulture.NumberFormat);
                bool manualMode = status[2] == "True";

                SyncValues();

                boiler_checkbox.CheckedChange += (object sender, CompoundButton.CheckedChangeEventArgs e) => {
                    manualMode = !e.IsChecked;
                    boiler_button.Enabled = !e.IsChecked;

                    string obfuscated = Obfuscator.Encrypt($"{manualMode},{isRelayOn}");

                    webClient.DownloadString(new System.Uri(webAddress + $"/boiler/set?obfuscated={obfuscated}"));
                };

                boiler_button.Click += (object sender, EventArgs e) => {
                    isRelayOn = !isRelayOn;
                    boiler_status.Text = isRelayOn ? "Angeschaltet" : "Ausgeschaltet";
                    boiler_button.Text = isRelayOn ? "Ausschalten" : "Einschalten";

                    string obfuscated = Obfuscator.Encrypt($"{manualMode},{isRelayOn}");

                    webClient.DownloadString(new System.Uri(webAddress + $"/boiler/set?obfuscated={obfuscated}"));
                };

                new Timer(UpdateValues, null, 30000, 30000);

                void SyncValues() {
                    boiler_temp.Text = $"{string.Format("{0:0.0}", currentTemp).Replace(',', '.')}°C";
                    boiler_status.Text = isRelayOn ? "Angeschaltet" : "Ausgeschaltet";
                    boiler_button.Text = isRelayOn ? "Ausschalten" : "Einschalten";
                    boiler_button.Enabled = manualMode;
                    boiler_checkbox.Checked = !manualMode;
                }

                void UpdateValues(object state) {
                    RunOnUiThread(() => {
                        deobfucatedData = Obfuscator.Decrypt(webClient.DownloadString(webAddress + "/boiler/get"));
                        if (deobfucatedData.IsReplay())
                            throw new Exception("Replay detected");

                        status = deobfucatedData.ContentMessage.Split(',');

                        bool isRelayOn = status[0] == "True";
                        float currentTemp = float.Parse(status[1], CultureInfo.InvariantCulture.NumberFormat);
                        bool manualMode = status[2] == "True";

                        SyncValues();
                    });
                };
            }
            catch (Exception e) {
                if (e.Message == "Replay detected") {
                    boiler_temp.Text = "EINDRINGLING ENTDECKT";
                } else {
                    boiler_temp.Text = "Error";
                }

                boiler_status.Text = "Unerreichbar";
                boiler_button.Text = "Unerreichbar";
                boiler_button.Enabled = false;
                boiler_checkbox.Checked = false;
            }
        }

        [BroadcastReceiver(Enabled = true, Exported = true)]
        public class BoilerBroadcastReceiver : BroadcastReceiver {
            public override void OnReceive(Android.Content.Context context, Intent intent) {

                var connectivityManager = (ConnectivityManager)context.GetSystemService(ConnectivityService);
                var networkCapabilities = connectivityManager.GetNetworkCapabilities(connectivityManager.ActiveNetwork);

                if (!networkCapabilities.HasTransport(Android.Net.TransportType.Wifi) && !networkCapabilities.HasTransport(Android.Net.TransportType.Cellular)) {
                    return;
                }

                try {
                    Obfuscator.Content deobfucatedData = Obfuscator.Decrypt(webClient.DownloadString(webAddress + "/boiler/get"));
                    if (deobfucatedData.IsReplay())
                        throw new Exception("Replay detected");

                    string[] status = deobfucatedData.ContentMessage.Split(',');

                    float currentTemp = float.Parse(status[1], CultureInfo.InvariantCulture.NumberFormat);

                    if (currentTemp == 1337) {
                        SendAlert("Boiler nicht erreichbar");
                    }
                    else if (currentTemp < 20) {
                        SendAlert("Boiler ist kalt");
                    }
                }
                catch (Exception e) {
                    if (e.Message == "Replay detected") {
                        SendAlert("Unsicherer Netzwerk");
                    }
                    else {
                        SendAlert("HomeController nicht erreichbar");
                    }
                }

                void SendAlert(string message) {
                    var builder = new NotificationCompat.Builder(context, notifyChannelId)
                        .SetSmallIcon(Resource.Drawable.ic_notifications_black_24dp)
                        .SetContentTitle("Boiler")
                        .SetContentText(message)
                        .SetAutoCancel(false)
                        .SetPriority(NotificationCompat.PriorityHigh);

                    NotificationManagerCompat.From(context).Notify(notifyNotificationId, builder.Build());
                }
            }
        }
    }
}

