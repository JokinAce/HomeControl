using System.Globalization;

namespace HomeController.ESP32 {

    public class Boiler {
        private HttpClient HttpClient { get; set; }
        public BoilerStatus? Status { get; set; }

        public Boiler(HttpClient httpClient) {
            HttpClient = httpClient;
        }

        public async Task Get() {
            try {
                string boilerRead = await HttpClient.GetStringAsync("http://192.168.178.68:8443/status"); //  Old Version used https
                string[] boilerReadParse = boilerRead.Split(',');

                Status = new BoilerStatus(boilerReadParse[0] == "1", float.Parse(boilerReadParse[1], CultureInfo.InvariantCulture.NumberFormat), boilerReadParse[2] == "1");
            }
            catch (Exception) {
                Status = new BoilerStatus(false, 1337, false);
            }
        }

        public async Task Set(bool manualMode, bool isRelayOn) {
            Status = new BoilerStatus(isRelayOn, Status?.CurrentTemp, manualMode);
            await HttpClient.GetAsync($"http://192.168.178.68:8443/setMode?manualmode={manualMode}&is_relay_on={isRelayOn}");
        }

        public record BoilerStatus(bool IsRelayOn, float? CurrentTemp, bool ManualMode);
    }
}
