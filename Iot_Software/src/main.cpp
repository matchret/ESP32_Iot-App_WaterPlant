#include <Arduino.h>
#include <ArduinoJson.h>
#include <WiFi.h>
#include <WiFiClientSecure.h>
#include <PubSubClient.h>
#include "Secrets.h"

// ===== CLIENTS =====
WiFiClientSecure net;
PubSubClient client(net);

// Forward declaration
void messageHandler(char* topic, byte* payload, unsigned int length);


// ===== CONTROL =====
#define CONTROL1_PIN 16
#define CONTROL2_PIN 19
#define CONTROL3_PIN 21
#define CONTROL4_PIN 22


// ===== SENSOR HUMIDITY =====
#define SENSOR1_PIN 34
#define SENSOR2_PIN 35
#define SENSOR3_PIN 32
#define SENSOR4_PIN 33

// ===== WATER LEVEL =====
#define WATER_POWER_PIN 25

#define WATER_LEVEL_25_PIN 26
#define WATER_LEVEL_50_PIN 27
#define WATER_LEVEL_75_PIN 14
#define WATER_LEVEL_100_PIN 13

const int AirValue = 3450;
int WaterValue = 1700;

// ===== TIME SETTINGS =====
const int publishIntervalSeconds = 900; // 15 minutes
time_t lastPublishTime = 0;

// Settings par plante
int minHumidity[4] = {10, 10, 10, 10};        // % minimum
int targetHumidity[4] = {80, 80, 80, 80};     // % pour affichage
int pumpDuration[4] = {1000, 1000, 1000, 1000}; // ms d'activation
bool plantEnabled[4] = {false,false,false,false}; // si la plante est activée pour l'arrosage automatique


int sensorPins[4] = {SENSOR1_PIN, SENSOR2_PIN, SENSOR3_PIN, SENSOR4_PIN};
int controlPins[4] = {CONTROL1_PIN, CONTROL2_PIN, CONTROL3_PIN, CONTROL4_PIN};

void waterPlant(int plant) {
  digitalWrite(controlPins[plant], LOW);
  delay(pumpDuration[plant]);
  digitalWrite(controlPins[plant], HIGH);
}

// ===== WIFI CONNECT =====
void connectWiFi() {
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected");
}

void syncTime() {
  configTime(0, 0, "pool.ntp.org", "time.nist.gov");

  Serial.print("Waiting for NTP time");
  time_t now = time(nullptr);

  while (now < 1700000000) {
    delay(500);
    Serial.print(".");
    now = time(nullptr);
  }

  Serial.println("\nTime synced");
}

// ===== AWS CONNECT =====
void connectAWS() {
  net.setCACert(root_ca);
  net.setCertificate(device_cert);
  net.setPrivateKey(private_key);

  client.setServer(aws_endpoint, aws_port);
  client.setBufferSize(2048);
  client.setKeepAlive(60);
  client.setCallback(messageHandler);

  const char* clientId = "iot_frontier_waterPlant";

  Serial.print("Connecting to AWS IoT as ");
  Serial.println(clientId);

  if (client.connect(clientId)) {
    Serial.println("AWS IoT connected!");

    Serial.print("Subscribing to: ");
    Serial.println(shadowDeltaTopic);

    bool ok = client.subscribe(shadowDeltaTopic);

    if (ok) {
      Serial.println("Subscribed to shadow delta topic!");
    } else {
      Serial.println("FAILED to subscribe to shadow delta topic!");
    }
  } else {
    Serial.print("Failed, rc=");
    Serial.println(client.state());
  }
}

// ===== IoT =====

void messageHandler(char* topic, byte* payload, unsigned int length) {
  Serial.println("===== MESSAGE RECEIVED =====");
  Serial.print("Topic: ");
  Serial.println(topic);
  Serial.print("Payload: ");
  Serial.write(payload, length);
  Serial.println();

  JsonDocument doc;

  DeserializationError error = deserializeJson(doc, payload, length);

  if (error) {
    Serial.println("JSON parse failed");
    return;
  }

  JsonObject state = doc["state"];

 if (state["waterNow"].is<int>())
{
    int plant = state["waterNow"];

    Serial.printf("Manual watering plant %d\n", plant + 1);

    if (plant >= 0 && plant < 4 && plantEnabled[plant] && getWaterLevel() > 0)
    {
        waterPlant(plant);

        String report = "{";
        report += "\"state\":{";
        report += "\"reported\":{";
        report += "\"waterNow\":" + String(plant);
        report += "},";
        report += "\"desired\":{";
        report += "\"waterNow\":null";
        report += "}";
        report += "}}";

        client.publish(shadowUpdateTopic, report.c_str());
    }
}

  if (state["minHumidity"]) {
    for (int i = 0; i < 4; i++) {
      minHumidity[i] = constrain(state["minHumidity"][i].as<int>(), 0, 100);
    }
  }

  if (state["targetHumidity"]) {
  for (int i = 0; i < 4; i++) {
        targetHumidity[i] = constrain(state["targetHumidity"][i].as<int>(), 0, 100);

    if (targetHumidity[i] <= minHumidity[i]) {
      targetHumidity[i] = minHumidity[i] + 5;
    }
    targetHumidity[i] = constrain(targetHumidity[i], 0, 100);
  }
}

  if (state["pumpDuration"]) {
    for (int i = 0; i < 4; i++) {
      pumpDuration[i] = constrain(state["pumpDuration"][i].as<int>(), 500, 10000);
    }
  }

  if (state["plantEnabled"]) {
  for (int i = 0; i < 4; i++) {
    plantEnabled[i] = state["plantEnabled"][i].as<bool>();
  }
}

  Serial.println("Shadow settings updated");

  int humidity[4];
  for (int i = 0; i < 4; i++)
    humidity[i] = readHumidity(sensorPins[i]);

publishShadowState(humidity, getWaterLevel());
}

// ===== SETUP =====
void setupPins() {
  for (int i = 0; i < 4; i++) {
    pinMode(controlPins[i], OUTPUT);
    digitalWrite(controlPins[i], HIGH); // pompe OFF
  }

  pinMode(WATER_POWER_PIN, OUTPUT);
  digitalWrite(WATER_POWER_PIN, LOW);

  pinMode(WATER_LEVEL_25_PIN, INPUT_PULLDOWN);
  pinMode(WATER_LEVEL_50_PIN, INPUT_PULLDOWN);
  pinMode(WATER_LEVEL_75_PIN, INPUT_PULLDOWN);
  pinMode(WATER_LEVEL_100_PIN, INPUT_PULLDOWN);
}

void setup() {
  Serial.begin(115200);

  setupPins();

  connectWiFi();
  syncTime();
  connectAWS();
}

// ===== LOOP =====
int readHumidity(int pin) {
  int raw = analogRead(pin);
  int humidity = map(raw, AirValue, WaterValue, 0, 100);
  return constrain(humidity, 0, 100);
}


int getWaterLevel()
{
  digitalWrite(WATER_POWER_PIN, HIGH);
  delay(10);

  bool s25  = digitalRead(WATER_LEVEL_25_PIN);
  bool s50  = digitalRead(WATER_LEVEL_50_PIN);
  bool s75  = digitalRead(WATER_LEVEL_75_PIN);
  bool s100 = digitalRead(WATER_LEVEL_100_PIN);

  digitalWrite(WATER_POWER_PIN, LOW);

  if (s25 && s50 && s75 && s100) return 100;
  if (s25 && s50 && s75 && !s100) return 75;
  if (!s25 && s50 && s75 && !s100) return 50;
  if (!s25 && !s50 && s75 && !s100) return 25;
  if (!s25 && !s50 && !s75 && !s100) return 0;

  return -1; // sensor error / impossible pattern
}

void publishShadowState(int humidity[], int waterLevel)
{

    String payload = "{";
    payload += "\"state\":{\"reported\":{";

    payload += "\"humidity\":[";
    for (int i = 0; i < 4; i++) {
        payload += String(humidity[i]);
        if (i < 3) payload += ",";
    }
    payload += "],";

    payload += "\"minHumidity\":[";
    for (int i = 0; i < 4; i++) {
        payload += String(minHumidity[i]);
        if (i < 3) payload += ",";
    }
    payload += "],";

    payload += "\"targetHumidity\":[";
    for (int i = 0; i < 4; i++) {
        payload += String(targetHumidity[i]);
        if (i < 3) payload += ",";
    }
    payload += "],";

    payload += "\"pumpDuration\":[";
    for (int i = 0; i < 4; i++) {
        payload += String(pumpDuration[i]);
        if (i < 3) payload += ",";
    }
    payload += "],";

    payload += "\"plantEnabled\":[";
    for (int i = 0; i < 4; i++) {
        payload += plantEnabled[i] ? "true" : "false";
        if (i < 3) payload += ",";
    }
    payload += "],";

    payload += "\"waterLevel\":";
    payload += String(waterLevel);

    payload += "}}}";

    client.publish(shadowUpdateTopic, payload.c_str());
    client.publish(mqtt_topic, payload.c_str());

    Serial.println("Shadow updated.");
}


void loop() {
  if (!client.connected()) {
    connectAWS();
    delay(1000);
    return;
  }

  int waterLevel = getWaterLevel();

bool waterSafe = waterLevel > 0;

if (!waterSafe)
{
  Serial.println("Tank empty or water level sensor error - watering disabled");
}

  client.loop(); // must run often for AWS messages

  time_t now = time(nullptr);

  if (now - lastPublishTime < publishIntervalSeconds) {
    return;
  }

  lastPublishTime = now;

  int humidity[4];

  for (int i = 0; i < 4; i++) {
    humidity[i] = readHumidity(sensorPins[i]);

    if (waterSafe && plantEnabled[i] && humidity[i] < minHumidity[i])  {
      int tries = 0;

      while (humidity[i] < targetHumidity[i] && tries < 3) {
        waterPlant(i);

        unsigned long waitStart = millis();
        while (millis() - waitStart < 60000) {
          client.loop();
          delay(100);
        }

        humidity[i] = readHumidity(sensorPins[i]);
        tries++;
      }
    }
  }

  publishShadowState(humidity, waterLevel);
}