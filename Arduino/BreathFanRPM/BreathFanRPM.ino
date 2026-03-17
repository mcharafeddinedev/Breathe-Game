// Breath Controller — DC Motor Generator Reader
// Motor positive → A0, motor negative → GND
// Breath spins propeller → motor generates voltage → Arduino reads it
// Sends "RPM:<smoothed analog>" over serial for FanBreathInput.cs in Unity
// Unity handles baseline calibration and intensity mapping.
//
// Uses asymmetric EMA: fast attack (responds instantly to harder blowing),
// slow decay (holds intensity while fan blades spin down).

const int ANALOG_PIN = A0;
const int SAMPLE_COUNT = 5;
const float ATTACK_ALPHA = 0.5;
const float DECAY_ALPHA  = 0.15;

float smoothedValue = 0;
float baseline = 0;
bool baselined = false;
int calCount = 0;
float calSum = 0;
unsigned long lastTime = 0;

const int CAL_READINGS = 20;

void setup() {
  Serial.begin(9600);
  analogReadResolution(10);
  smoothedValue = analogRead(ANALOG_PIN);
  lastTime = millis();
}

void loop() {
  unsigned long now = millis();
  if (now - lastTime >= 50) {
    lastTime = now;

    float total = 0;
    for (int i = 0; i < SAMPLE_COUNT; i++) {
      total += analogRead(ANALOG_PIN);
      delayMicroseconds(200);
    }
    float raw = total / SAMPLE_COUNT;

    if (!baselined) {
      calSum += raw;
      calCount++;
      smoothedValue = raw;
      if (calCount >= CAL_READINGS) {
        baseline = calSum / calCount;
        baselined = true;
      }
      Serial.print("RPM:");
      Serial.println((int)raw);
      return;
    }

    float rawDev = abs(raw - baseline);
    float curDev = abs(smoothedValue - baseline);
    float alpha = (rawDev > curDev) ? ATTACK_ALPHA : DECAY_ALPHA;

    smoothedValue = (alpha * raw) + ((1.0 - alpha) * smoothedValue);

    Serial.print("RPM:");
    Serial.println((int)smoothedValue);
  }
}
