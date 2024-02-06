package main

import (
	"crypto/tls"
	"fmt"
	"io"
	"log"
	"net/http"
	"strconv"
	"strings"
	"time"
)

const APIKEY_BOILER = ""

type Boiler struct {
	IsRelayOn   bool
	CurrentTemp float64
	ManualMode  bool
}

func formatBool(b bool) string {
	if b {
		return "True"
	}
	return "False"
}

func updateValues(boiler *Boiler) {
	transport := &http.Transport{
		TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
	}
	client := &http.Client{Transport: transport}

	for {
		request, err := http.NewRequest("GET", "https://192.168.178.68:8443/status", nil)
		if err != nil {
			fmt.Println("Error making HTTP GET request:", err)
			continue
		}

		request.Header.Add("apiKey", APIKEY_BOILER)

		response, err := client.Do(request)
		if err != nil {
			fmt.Println("Error making HTTP GET response:", err)
			continue
		}

		body, err := io.ReadAll(response.Body)
		response.Body.Close()

		if err != nil {
			fmt.Println("Error reading response body:", err)
			continue
		}

		content := strings.Split(string(body), ",")

		if len(content) < 3 {
			fmt.Println("Invalid response content length")
			continue
		}

		boiler.IsRelayOn = content[0] == "1"

		f, err := strconv.ParseFloat(content[1], 64)
		if err != nil {
			fmt.Println("Error parsing temperature:", err)
			continue
		}
		boiler.CurrentTemp = f

		boiler.ManualMode = content[2] == "1"

		time.Sleep(5 * time.Minute)
	}
}

func setBoiler(boiler *Boiler) {
	transport := &http.Transport{
		TLSClientConfig: &tls.Config{InsecureSkipVerify: true},
	}
	client := &http.Client{Transport: transport}

	request, err := http.NewRequest("GET", "https://192.168.178.68:8443/setMode", nil)
	if err != nil {
		fmt.Println("Error making HTTP GET request:", err)
		return
	}

	requestquery := request.URL.Query()
	requestquery.Add("manualmode", formatBool(boiler.ManualMode))
	requestquery.Add("is_relay_on", formatBool(boiler.IsRelayOn))
	request.URL.RawQuery = requestquery.Encode()

	request.Header.Add("apiKey", APIKEY_BOILER)

	_, err = client.Do(request)
	if err != nil {
		fmt.Println("Error sending request:", err)
	}
}

func main() {
	fmt.Println("Starting")
	_apiKey := ""

	boiler := &Boiler{
		IsRelayOn:   false,
		CurrentTemp: 1337,
		ManualMode:  false,
	}

	go updateValues(boiler)

	http.HandleFunc("/boiler/get", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("apiKey") != _apiKey {
			w.WriteHeader(401)
			return
		}

		response := fmt.Sprintf("%v,%v,%v", formatBool(boiler.IsRelayOn), boiler.CurrentTemp, formatBool(boiler.ManualMode))
		response = Encrypt(response)

		w.Header().Set("Content-Type", "text/plain")
		w.Write([]byte(response))
	})

	http.HandleFunc("/boiler/set", func(w http.ResponseWriter, r *http.Request) {
		if r.Header.Get("apiKey") != _apiKey || !r.URL.Query().Has("obfuscated") {
			w.WriteHeader(401)
			return
		}

		deobfuscated := Decrypt(r.URL.Query().Get("obfuscated"))

		if deobfuscated.IsReplay() {
			w.WriteHeader(401)
			return
		}

		content := strings.Split(deobfuscated.ContentMessage, ",")

		boiler.ManualMode = content[0] == "True"
		boiler.IsRelayOn = content[1] == "True"

		go setBoiler(boiler)

		w.WriteHeader(201)
	})

	log.Fatal(http.ListenAndServe(":5000", nil))
}

// HttpClient httpClient = new();
// httpClient.DefaultRequestHeaders.Add("apiKey", "");

// // Prepare Resources
// Boiler boiler = new(httpClient);

// // Do timed Readings
// _ = new Timer(async (state) => {
//     await boiler.Get();
// }, null, 0, 300000);

// // API Endpoints
// string _apiKey = "";

// app.MapGet("/boiler/get", ([FromHeader] string? apiKey) => {
//     if (_apiKey != apiKey) return Results.Unauthorized();

//     string response = $"{boiler?.Status?.IsRelayOn},{boiler?.Status?.CurrentTemp},{boiler?.Status?.ManualMode}";
//     response = Obfuscator.Encrypt(response);

//     return Results.Text(response, "text/plain");
// });

// app.MapGet("/boiler/set", ([FromHeader] string? apiKey, [FromQuery] string obfuscated) => {
//     if (_apiKey != apiKey) return Results.Unauthorized();

//     Obfuscator.Content deobfuscated = Obfuscator.Decrypt(obfuscated);
//     if (deobfuscated.IsReplay())
//         return Results.Unauthorized();

//     _ = Task.Run(async () => {
//         string[] settings = deobfuscated.ContentMessage.Split(',');
//         await boiler.Set(settings[0] == "True", settings[1] == "True");
//     });

//     return Results.Ok();
// });

// app.Run();
