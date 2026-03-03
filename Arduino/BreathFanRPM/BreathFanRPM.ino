// Breath Controller — DC Motor Generator Reader
// Motor positive → A0, motor negative → GND
// Breath spins propeller → motor generates voltage → Arduino reads it
// Sends "RPM:<value>" over serial for FanBreathInput.cs in Unity

const int ANALOG_PIN = A0;
const int SAMPLE_COUNT = 5;
const float EMA_ALPHA = 0.3;
const float DEAD_ZONE = 3.0;

float smoothedValue = 0;
unsigned long lastTime = 0;

void setup() {
  Serial.begin(9600);
  analogReadResolution(10);
  lastTime = millis();
}

void loop() {
  unsigned long now = millis();
  if (now - lastTime >= 100) {
    float total = 0;
    for (int i = 0; i < SAMPLE_COUNT; i++) {
      total += analogRead(ANALOG_PIN);
      delayMicroseconds(200);
    }
    float raw = total / SAMPLE_COUNT;

    smoothedValue = (EMA_ALPHA * raw) + ((1.0 - EMA_ALPHA) * smoothedValue);

    float output = smoothedValue;
    if (output < DEAD_ZONE) output = 0;

    Serial.print("RPM:");
    Serial.println((int)output);

    lastTime = now;
  }
}
