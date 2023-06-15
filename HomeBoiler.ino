//HTTP
#include <WebServer.h>

// Custom Lib with credentials
#include <Home_WiFi_Helper.h>

// Burnout
#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// TemperatureSensor
#include <OneWire.h>
#include <DallasTemperature.h>

//#include <ESP32Time.h>

// OTA
#include <Update.h>

// 4 Data
#define SENSOR 4
#define RELAY 12

#define NAME "ESP32-Boiler"
#define APIKEY "PLACEHOLDER"

OneWire oneWire(SENSOR);
DallasTemperature sensors(&oneWire);

WebServer server(8443);
static const char* contentType PROGMEM = "text/plain";

#define IS_APIKEY_VALID() (server.hasHeader("apiKey") && server.header("apiKey") == APIKEY)

static bool manualmode = false;
static float current_temperature = 1337;
static bool is_relay_on = false;

static void relay_switch(bool state) {
    is_relay_on = state;
    digitalWrite(RELAY, state);
}

void setup_ota_updater(void) {
    server.on(F("/"), HTTP_GET, []() {
        if (!IS_APIKEY_VALID()) {
            server.send(403);
            return;
        }

        const char serverIndex[] PROGMEM =
            "<script src='https://ajax.googleapis.com/ajax/libs/jquery/3.2.1/jquery.min.js'></script>"
            "<form method='POST' action='#' enctype='multipart/form-data' id='upload_form'>"
            "<input type='file' NAME='update'>"
            "<input type='submit' value='Update'>"
            "</form>"
            "<div id='prg'>progress: 0%</div>"
            "<script>"
            "$('form').submit(function(e){"
            "e.preventDefault();"
            "var form = $('#upload_form')[0];"
            "var data = new FormData(form);"
            " $.ajax({"
            "url: '/update',"
            "type: 'POST',"
            "data: data,"
            "contentType: false,"
            "processData:false,"
            "xhr: function() {"
            "var xhr = new window.XMLHttpRequest();"
            "xhr.upload.addEventListener('progress', function(evt) {"
            "if (evt.lengthComputable) {"
            "var per = evt.loaded / evt.total;"
            "$('#prg').html('progress: ' + Math.round(per*100) + '%');"
            "}"
            "}, false);"
            "return xhr;"
            "},"
            "success:function(d, s) {"
            "console.log('success!')"
            "},"
            "error: function (a, b, c) {"
            "}"
            "});"
            "});"
            "</script>";

        server.sendHeader(F("Connection"), F("close"));
        server.send(200, F("text/html"), serverIndex);
    });

    /*handling uploading firmware file */
    server.on(
        F("/update"), HTTP_POST, []() {
            server.sendHeader(F("Connection"), F("close"));
            server.send(200, contentType, (Update.hasError()) ? F("FAIL") : F("OK"));
            ESP.restart();
        },
        []() {
            HTTPUpload& upload = server.upload();
            if (upload.status == UPLOAD_FILE_START) {
                Update.begin(UPDATE_SIZE_UNKNOWN);
            } else if (upload.status == UPLOAD_FILE_WRITE) {
                /* flashing firmware to ESP*/
                Update.write(upload.buf, upload.currentSize);
            } else if (upload.status == UPLOAD_FILE_END) {
                Update.end(true);
            }
        });
}

void setup_webserver(void) {
    // Home_Cert.h
    //server.setServerKeyAndCert(cert_key, cert_key_len, cert, cert_len);

    server.on(F("/status"), []() {
        if (!IS_APIKEY_VALID()) {
            server.send(403);
            return;
        }

        const char seperator PROGMEM = ',';
        server.send(200, contentType, String(is_relay_on) + seperator + String(current_temperature) + seperator + String(manualmode));
    });

    server.on(F("/setMode"), []() {
        if (!IS_APIKEY_VALID()) {
            server.send(403);
            return;
        }

        const char true_csharp[] PROGMEM = "True"; // C# API returns a true bool uppercase

        if (server.arg(F("manualmode")) == true_csharp) {
            manualmode = true;

            if (server.arg(F("is_relay_on")) == true_csharp)
                relay_switch(true);
            else
                relay_switch(false);
        } else {
            manualmode = false;
        }

        server.send(200);
    });

    server.begin();
}

void setup(void) {
    pinMode(RELAY, OUTPUT);
    WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector

    home_setup_wifi(NAME);
    setup_ota_updater();
    setup_webserver();

    sensors.begin();
}

void autopilot(int temperature) {
    static const unsigned int boiler_offset = 2;
    if (temperature <= 42) {
        relay_switch(true);
    } else if (temperature >= 48 - boiler_offset) {
        relay_switch(false);
    }
}

void loop(void) {
    static unsigned int previous_millis = millis();
    unsigned int current_millis = millis();

    if (previous_millis - current_millis >= 60000) {
        sensors.requestTemperatures();
        current_temperature = sensors.getTempCByIndex(0);

        if (current_temperature >= 50) {
            relay_switch(false);
            manualmode = false;
        }

        if (!manualmode)
            autopilot(current_temperature);

        previous_millis = current_millis;
    }

    if (WiFi.status() == WL_CONNECTED)
        server.handleClient();

    delay(1000);
}
